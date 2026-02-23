using FluentAssertions;
using LiveSQL.Core.Analysis;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Tests.Analysis;

public class IndexAdvisorTests
{
    private readonly IndexAdvisor _advisor = new();

    private static ExecutionPlan CreateTableScanPlan(
        string tableName = "Orders",
        string schema = "dbo",
        double actualRows = 5000,
        long tableRowCount = 50000)
    {
        return new ExecutionPlan
        {
            QueryText = $"SELECT * FROM {tableName} WHERE CustomerId = 5",
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
                    EstimatedRows = actualRows,
                    ActualRows = actualRows,
                    CostPercentage = 90
                },
                Table = new TableReference
                {
                    Schema = schema,
                    TableName = tableName,
                    EstimatedRowCount = tableRowCount
                },
                Predicate = $"[{tableName}].[CustomerId] = @1",
                OutputColumns = "OrderId, CustomerId, OrderDate, TotalAmount"
            }
        };
    }

    private static ExecutionPlan CreateKeyLookupPlan(
        string tableName = "Orders",
        string schema = "dbo")
    {
        var root = new PlanNode
        {
            Id = 0,
            PhysicalOperator = "Nested Loops",
            LogicalOperator = "Inner Join",
            NodeType = NodeType.NestedLoopJoin,
            Cost = new OperationCost
            {
                SubtreeCost = 3.0,
                TotalCost = 0.5,
                EstimatedRows = 200,
                ActualRows = 200,
                CostPercentage = 10
            }
        };

        root.Children.Add(new PlanNode
        {
            Id = 1,
            PhysicalOperator = "Index Seek",
            LogicalOperator = "Index Seek",
            NodeType = NodeType.IndexSeek,
            Cost = new OperationCost
            {
                SubtreeCost = 0.5,
                TotalCost = 0.5,
                EstimatedRows = 200,
                ActualRows = 200,
                CostPercentage = 20
            },
            Table = new TableReference
            {
                Schema = schema,
                TableName = tableName,
                EstimatedRowCount = 50000
            },
            Index = new IndexReference
            {
                IndexName = "IX_Orders_CustomerId",
                TableName = tableName,
                Columns = new List<string> { "CustomerId" }
            }
        });

        root.Children.Add(new PlanNode
        {
            Id = 2,
            PhysicalOperator = "Key Lookup",
            LogicalOperator = "Key Lookup",
            NodeType = NodeType.KeyLookup,
            Cost = new OperationCost
            {
                SubtreeCost = 2.0,
                TotalCost = 2.0,
                EstimatedRows = 200,
                ActualRows = 200,
                CostPercentage = 70
            },
            Table = new TableReference
            {
                Schema = schema,
                TableName = tableName,
                EstimatedRowCount = 50000
            },
            Index = new IndexReference
            {
                IndexName = "PK_Orders",
                TableName = tableName,
                IsClustered = true
            },
            OutputColumns = "OrderDate, TotalAmount, Status"
        });

        return new ExecutionPlan
        {
            QueryText = $"SELECT OrderDate, TotalAmount, Status FROM {tableName} WHERE CustomerId = 5",
            DatabaseEngine = "SqlServer",
            RootNode = root
        };
    }

    [Fact]
    public void Suggest_TableScan_ShouldRecommendIndex()
    {
        var plan = CreateTableScanPlan();

        var suggestions = _advisor.Suggest(plan);

        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.TableName == "Orders");
    }

    [Fact]
    public void Suggest_KeyLookup_ShouldRecommendCoveringIndex()
    {
        var plan = CreateKeyLookupPlan();

        var suggestions = _advisor.Suggest(plan);

        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s =>
            s.TableName == "Orders" &&
            (s.IncludeColumns.Count > 0 || s.KeyColumns.Count > 0));
    }

    [Fact]
    public void Suggest_ShouldGenerateValidCreateIndexStatement()
    {
        var plan = CreateTableScanPlan();

        var suggestions = _advisor.Suggest(plan);

        suggestions.Should().NotBeEmpty();
        foreach (var suggestion in suggestions)
        {
            suggestion.CreateIndexStatement.Should().Contain("CREATE NONCLUSTERED INDEX");
            suggestion.CreateIndexStatement.Should().Contain(suggestion.TableName);
            suggestion.CreateIndexStatement.Should().EndWith(";");
        }
    }

    [Fact]
    public void Suggest_ShouldSetIndexName()
    {
        var plan = CreateTableScanPlan();

        var suggestions = _advisor.Suggest(plan);

        suggestions.Should().NotBeEmpty();
        foreach (var suggestion in suggestions)
        {
            suggestion.IndexName.Should().StartWith("IX_");
            suggestion.IndexName.Should().Contain("Orders");
        }
    }

    [Fact]
    public void Suggest_ShouldIncludeReason()
    {
        var plan = CreateTableScanPlan();

        var suggestions = _advisor.Suggest(plan);

        suggestions.Should().NotBeEmpty();
        suggestions.Should().AllSatisfy(s =>
            s.Reason.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void Suggest_CleanPlan_ShouldReturnEmptyOrLowImpact()
    {
        var plan = new ExecutionPlan
        {
            QueryText = "SELECT * FROM Students WHERE Id = 1",
            DatabaseEngine = "SqlServer",
            RootNode = new PlanNode
            {
                Id = 0,
                PhysicalOperator = "Clustered Index Seek",
                LogicalOperator = "Clustered Index Seek",
                NodeType = NodeType.ClusteredIndexSeek,
                Cost = new OperationCost
                {
                    SubtreeCost = 0.003,
                    TotalCost = 0.003,
                    EstimatedRows = 1,
                    ActualRows = 1,
                    CostPercentage = 100
                },
                Table = new TableReference
                {
                    Schema = "dbo",
                    TableName = "Students",
                    EstimatedRowCount = 100
                },
                Index = new IndexReference
                {
                    IndexName = "PK_Students",
                    TableName = "Students",
                    IsClustered = true
                }
            }
        };

        var suggestions = _advisor.Suggest(plan);

        suggestions.Where(s => s.Impact >= Severity.High).Should().BeEmpty();
    }

    [Fact]
    public void Suggest_ShouldSetEstimatedImprovement()
    {
        var plan = CreateTableScanPlan();

        var suggestions = _advisor.Suggest(plan);

        suggestions.Should().NotBeEmpty();
        suggestions.Should().AllSatisfy(s =>
            s.EstimatedImprovement.Should().BeGreaterThan(0));
    }
}
