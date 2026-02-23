using FluentAssertions;
using LiveSQL.Core.Analysis;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Tests.Analysis;

public class BottleneckDetectorTests
{
    private readonly BottleneckDetector _detector = new();

    private static PlanNode CreateNode(
        NodeType nodeType,
        string physicalOp,
        double estimatedRows = 1,
        double actualRows = 1,
        double subtreeCost = 0.01,
        double costPercentage = 10,
        string? tableName = null,
        long tableRowCount = 0)
    {
        var node = new PlanNode
        {
            Id = 0,
            PhysicalOperator = physicalOp,
            LogicalOperator = physicalOp,
            NodeType = nodeType,
            Cost = new OperationCost
            {
                SubtreeCost = subtreeCost,
                TotalCost = subtreeCost,
                EstimatedRows = estimatedRows,
                ActualRows = actualRows,
                CostPercentage = costPercentage
            }
        };

        if (tableName != null)
        {
            node.Table = new TableReference
            {
                Schema = "dbo",
                TableName = tableName,
                EstimatedRowCount = tableRowCount
            };
        }

        return node;
    }

    [Fact]
    public void Detect_TableScanOnLargeTable_ShouldFlagBottleneck()
    {
        var node = CreateNode(
            NodeType.TableScan,
            "Table Scan",
            estimatedRows: 5000,
            actualRows: 5000,
            costPercentage: 80,
            tableName: "Orders",
            tableRowCount: 5000);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().Contain(b =>
            b.Title.Contains("Table Scan", StringComparison.OrdinalIgnoreCase) ||
            b.Title.Contains("Scan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_SmallTableScan_ShouldNotFlag()
    {
        var node = CreateNode(
            NodeType.TableScan,
            "Table Scan",
            estimatedRows: 10,
            actualRows: 10,
            costPercentage: 5,
            tableName: "Settings",
            tableRowCount: 10);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Where(b =>
            b.Title.Contains("Table Scan", StringComparison.OrdinalIgnoreCase) &&
            b.Severity >= Severity.High)
            .Should().BeEmpty();
    }

    [Fact]
    public void Detect_ExpensiveKeyLookup_ShouldFlagBottleneck()
    {
        var node = CreateNode(
            NodeType.KeyLookup,
            "Key Lookup",
            estimatedRows: 500,
            actualRows: 500,
            costPercentage: 40,
            tableName: "Orders",
            tableRowCount: 10000);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().Contain(b =>
            b.Title.Contains("Lookup", StringComparison.OrdinalIgnoreCase) ||
            b.Title.Contains("Key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_InaccurateEstimate_ShouldFlagBottleneck()
    {
        // Estimated 10 rows but got 50000 => ratio of 5000x
        var node = CreateNode(
            NodeType.SeqScan,
            "Seq Scan",
            estimatedRows: 10,
            actualRows: 50000,
            costPercentage: 60,
            tableName: "Logs",
            tableRowCount: 100000);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().Contain(b =>
            b.Title.Contains("Estimate", StringComparison.OrdinalIgnoreCase) ||
            b.Title.Contains("Cardinality", StringComparison.OrdinalIgnoreCase) ||
            b.Title.Contains("Row", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_ExpensiveSort_ShouldFlagBottleneck()
    {
        var node = CreateNode(
            NodeType.Sort,
            "Sort",
            estimatedRows: 50000,
            actualRows: 50000,
            costPercentage: 45,
            tableName: "Events",
            tableRowCount: 50000);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().Contain(b =>
            b.Title.Contains("Sort", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_ExpensiveHashJoin_ShouldFlagBottleneck()
    {
        var hashJoin = CreateNode(
            NodeType.HashJoin,
            "Hash Match",
            estimatedRows: 10000,
            actualRows: 10000,
            costPercentage: 40);

        hashJoin.Children.Add(CreateNode(
            NodeType.TableScan,
            "Table Scan",
            estimatedRows: 5000,
            actualRows: 5000,
            costPercentage: 30,
            tableName: "Orders",
            tableRowCount: 5000));

        hashJoin.Children.Add(CreateNode(
            NodeType.IndexScan,
            "Index Scan",
            estimatedRows: 5000,
            actualRows: 5000,
            costPercentage: 30,
            tableName: "OrderDetails",
            tableRowCount: 10000));

        var plan = new ExecutionPlan { RootNode = hashJoin };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().Contain(b =>
            b.Title.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
            b.Title.Contains("Join", StringComparison.OrdinalIgnoreCase) ||
            b.Title.Contains("Scan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detect_DominantOperation_ShouldFlagBottleneck()
    {
        var root = CreateNode(
            NodeType.NestedLoopJoin,
            "Nested Loops",
            estimatedRows: 100,
            actualRows: 100,
            costPercentage: 5);

        root.Children.Add(CreateNode(
            NodeType.ClusteredIndexSeek,
            "Clustered Index Seek",
            estimatedRows: 1,
            actualRows: 1,
            costPercentage: 2));

        root.Children.Add(CreateNode(
            NodeType.TableScan,
            "Table Scan",
            estimatedRows: 100000,
            actualRows: 100000,
            costPercentage: 93,
            tableName: "AuditLog",
            tableRowCount: 100000));

        var plan = new ExecutionPlan { RootNode = root };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().NotBeEmpty();
        bottlenecks.Should().Contain(b => b.ImpactPercentage >= 50 || b.Severity >= Severity.High);
    }

    [Fact]
    public void Detect_CleanPlan_ShouldReturnNoHighSeverityBottlenecks()
    {
        // A multi-node plan where no single node dominates avoids triggering "Dominant Operation"
        var root = CreateNode(
            NodeType.NestedLoopJoin,
            "Nested Loops",
            estimatedRows: 1,
            actualRows: 1,
            costPercentage: 40,
            subtreeCost: 0.003,
            tableName: null,
            tableRowCount: 0);

        root.Children.Add(CreateNode(
            NodeType.ClusteredIndexSeek,
            "Clustered Index Seek",
            estimatedRows: 1,
            actualRows: 1,
            costPercentage: 30,
            subtreeCost: 0.002,
            tableName: "Students",
            tableRowCount: 100));

        root.Children.Add(CreateNode(
            NodeType.ClusteredIndexSeek,
            "Clustered Index Seek",
            estimatedRows: 1,
            actualRows: 1,
            costPercentage: 30,
            subtreeCost: 0.001,
            tableName: "Courses",
            tableRowCount: 50));

        var plan = new ExecutionPlan { RootNode = root };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Where(b => b.Severity >= Severity.High).Should().BeEmpty();
    }

    [Fact]
    public void Detect_ShouldSetRelatedNode()
    {
        var node = CreateNode(
            NodeType.TableScan,
            "Table Scan",
            estimatedRows: 10000,
            actualRows: 10000,
            costPercentage: 90,
            tableName: "BigTable",
            tableRowCount: 100000);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Where(b => b.RelatedNode != null).Should().NotBeEmpty();
    }

    [Fact]
    public void Detect_ShouldIncludeRecommendation()
    {
        var node = CreateNode(
            NodeType.TableScan,
            "Table Scan",
            estimatedRows: 5000,
            actualRows: 5000,
            costPercentage: 80,
            tableName: "Orders",
            tableRowCount: 5000);

        var plan = new ExecutionPlan { RootNode = node };

        var bottlenecks = _detector.Detect(plan);

        bottlenecks.Should().AllSatisfy(b =>
            b.Recommendation.Should().NotBeNullOrEmpty());
    }
}
