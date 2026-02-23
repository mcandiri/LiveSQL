using FluentAssertions;
using LiveSQL.Core.Analysis;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Tests.Analysis;

public class QueryAnalyzerTests
{
    private readonly CostAnalyzer _costAnalyzer = new();
    private readonly BottleneckDetector _bottleneckDetector = new();
    private readonly IndexAdvisor _indexAdvisor = new();

    private QueryAnalyzer CreateAnalyzer() =>
        new(_costAnalyzer, _bottleneckDetector, _indexAdvisor);

    private static ExecutionPlan CreateOptimalPlan()
    {
        var root = new PlanNode
        {
            Id = 0,
            Label = "SELECT",
            PhysicalOperator = "SELECT",
            LogicalOperator = "SELECT",
            NodeType = NodeType.Compute,
            Cost = new OperationCost
            {
                SubtreeCost = 0.003,
                TotalCost = 0.001,
                EstimatedRows = 1,
                ActualRows = 1,
                CostPercentage = 30
            }
        };

        root.Children.Add(new PlanNode
        {
            Id = 1,
            Label = "Clustered Index Seek",
            PhysicalOperator = "Clustered Index Seek",
            LogicalOperator = "Clustered Index Seek",
            NodeType = NodeType.ClusteredIndexSeek,
            Cost = new OperationCost
            {
                SubtreeCost = 0.002,
                TotalCost = 0.002,
                EstimatedRows = 1,
                ActualRows = 1,
                CostPercentage = 45
            },
            Table = new TableReference { Schema = "dbo", TableName = "Students" },
            Index = new IndexReference { IndexName = "PK_Students", IsClustered = true }
        });

        return new ExecutionPlan
        {
            QueryText = "SELECT * FROM Students WHERE Id = 1",
            DatabaseEngine = "SQL Server",
            RootNode = root,
            Metrics = new QueryMetrics { TotalCost = 0.003 }
        };
    }

    private static ExecutionPlan CreateTableScanPlan()
    {
        return new ExecutionPlan
        {
            QueryText = "SELECT * FROM Orders WHERE CustomerId = 5",
            DatabaseEngine = "SQL Server",
            RootNode = new PlanNode
            {
                Id = 0,
                PhysicalOperator = "Table Scan",
                LogicalOperator = "Table Scan",
                NodeType = NodeType.TableScan,
                Cost = new OperationCost
                {
                    SubtreeCost = 5.0,
                    TotalCost = 5.0,
                    EstimatedRows = 50000,
                    ActualRows = 50000,
                    CostPercentage = 100
                },
                Table = new TableReference
                {
                    Schema = "dbo",
                    TableName = "Orders",
                    EstimatedRowCount = 50000
                },
                Predicate = "[Orders].[CustomerId] = @1"
            },
            Metrics = new QueryMetrics { TotalCost = 5.0 }
        };
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnExecutionPlan()
    {
        var analyzer = CreateAnalyzer();
        var plan = CreateOptimalPlan();

        var result = await analyzer.AnalyzeAsync(plan, CancellationToken.None);

        result.Should().NotBeNull();
        result.RootNode.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_TableScan_ShouldDetectBottlenecks()
    {
        var analyzer = CreateAnalyzer();
        var plan = CreateTableScanPlan();

        var result = await analyzer.AnalyzeAsync(plan, CancellationToken.None);

        result.Bottlenecks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_TableScan_ShouldGenerateIndexSuggestions()
    {
        var analyzer = CreateAnalyzer();
        var plan = CreateTableScanPlan();

        var result = await analyzer.AnalyzeAsync(plan, CancellationToken.None);

        result.IndexSuggestions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_OptimalPlan_ShouldHaveNoHighSeverityBottlenecks()
    {
        var analyzer = CreateAnalyzer();
        var plan = CreateOptimalPlan();

        var result = await analyzer.AnalyzeAsync(plan, CancellationToken.None);

        result.Bottlenecks.Where(b => b.Severity >= Severity.High).Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldMarkWarningNodes()
    {
        var analyzer = CreateAnalyzer();
        var plan = CreateTableScanPlan();

        var result = await analyzer.AnalyzeAsync(plan, CancellationToken.None);

        // The table scan node should be marked as warning
        result.RootNode.IsWarning.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldComputeMetrics()
    {
        var analyzer = CreateAnalyzer();
        var plan = CreateTableScanPlan();

        var result = await analyzer.AnalyzeAsync(plan, CancellationToken.None);

        result.Metrics.TotalOperators.Should().BeGreaterThan(0);
    }
}
