using FluentAssertions;
using LiveSQL.Core.Analysis;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Tests.Analysis;

public class PlanComparatorTests
{
    private readonly PlanComparator _comparator = new();

    private static ExecutionPlan CreatePlan(
        double totalCost,
        int nodeCount = 1,
        string physicalOp = "Clustered Index Seek",
        NodeType nodeType = NodeType.ClusteredIndexSeek,
        double actualRows = 100)
    {
        var root = new PlanNode
        {
            Id = 0,
            PhysicalOperator = physicalOp,
            LogicalOperator = physicalOp,
            Label = physicalOp,
            NodeType = nodeType,
            Cost = new OperationCost
            {
                SubtreeCost = totalCost,
                TotalCost = totalCost,
                EstimatedRows = actualRows,
                ActualRows = actualRows,
                CostPercentage = 100
            },
            Table = new TableReference { Schema = "dbo", TableName = "Orders" }
        };

        for (int i = 1; i < nodeCount; i++)
        {
            root.Children.Add(new PlanNode
            {
                Id = i,
                PhysicalOperator = "Index Scan",
                LogicalOperator = "Index Scan",
                Label = "Index Scan",
                NodeType = NodeType.IndexScan,
                Depth = 1,
                Cost = new OperationCost
                {
                    SubtreeCost = totalCost / nodeCount,
                    TotalCost = totalCost / nodeCount,
                    EstimatedRows = 50,
                    ActualRows = 50,
                    CostPercentage = 100.0 / nodeCount
                }
            });
        }

        return new ExecutionPlan
        {
            QueryText = "SELECT * FROM Orders",
            DatabaseEngine = "SQL Server",
            RootNode = root,
            Metrics = new QueryMetrics { TotalCost = totalCost }
        };
    }

    [Fact]
    public void Compare_IdenticalPlans_ShouldShowNoChange()
    {
        var plan1 = CreatePlan(1.0);
        var plan2 = CreatePlan(1.0);

        var result = _comparator.Compare(plan1, plan2);

        result.Should().NotBeNull();
        result.CostReduction.Should().BeApproximately(0, 0.001);
        result.Verdict.Should().Be(ComparisonVerdict.NoChange);
    }

    [Fact]
    public void Compare_ImprovedPlan_ShouldShowPositiveCostReduction()
    {
        var before = CreatePlan(10.0);
        var after = CreatePlan(2.0);

        var result = _comparator.Compare(before, after);

        result.Should().NotBeNull();
        result.CostReduction.Should().BeGreaterThan(0);
        result.Verdict.Should().BeOneOf(
            ComparisonVerdict.Improved,
            ComparisonVerdict.SignificantImprovement);
    }

    [Fact]
    public void Compare_DegradedPlan_ShouldShowNegativeCostReduction()
    {
        var before = CreatePlan(1.0);
        var after = CreatePlan(5.0);

        var result = _comparator.Compare(before, after);

        result.Should().NotBeNull();
        result.CostReduction.Should().BeLessThan(0);
        result.Verdict.Should().Be(ComparisonVerdict.Regressed);
    }

    [Fact]
    public void Compare_DifferentStructures_ShouldTrackNodeCountChange()
    {
        var before = CreatePlan(5.0, nodeCount: 1, physicalOp: "Table Scan", nodeType: NodeType.TableScan);
        var after = CreatePlan(1.0, nodeCount: 3, physicalOp: "Nested Loops", nodeType: NodeType.NestedLoopJoin);

        var result = _comparator.Compare(before, after);

        result.Should().NotBeNull();
        result.OperatorCountChange.Should().Be(2); // 3 - 1 = 2
    }

    [Fact]
    public void Compare_ShouldPopulateImprovementsAndRegressions()
    {
        var before = CreatePlan(10.0, nodeCount: 1, physicalOp: "Table Scan", nodeType: NodeType.TableScan, actualRows: 50000);
        before.Bottlenecks.Add(new BottleneckInfo
        {
            Title = "Table Scan on Orders",
            Severity = Severity.High,
            ImpactPercentage = 100
        });

        var after = CreatePlan(1.0, nodeCount: 1, physicalOp: "Index Seek", nodeType: NodeType.IndexSeek, actualRows: 10);

        var result = _comparator.Compare(before, after);

        result.Improvements.Should().NotBeEmpty();
    }

    [Fact]
    public void Compare_ShouldCalculateCostReductionPercentage()
    {
        var before = CreatePlan(10.0);
        var after = CreatePlan(5.0);

        var result = _comparator.Compare(before, after);

        result.CostReduction.Should().BeApproximately(50.0, 1.0);
    }

    [Fact]
    public void Compare_ShouldDetectScanToSeekImprovement()
    {
        var before = CreatePlan(5.0, nodeCount: 1, physicalOp: "Table Scan", nodeType: NodeType.TableScan);
        var after = CreatePlan(0.5, nodeCount: 1, physicalOp: "Index Seek", nodeType: NodeType.IndexSeek);

        var result = _comparator.Compare(before, after);

        result.Improvements.Should().Contain(s => s.Contains("scan", StringComparison.OrdinalIgnoreCase)
                                                  || s.Contains("seek", StringComparison.OrdinalIgnoreCase)
                                                  || s.Contains("cost", StringComparison.OrdinalIgnoreCase));
    }
}
