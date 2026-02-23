using FluentAssertions;
using LiveSQL.Core.Models;
using LiveSQL.Core.Visualization;

namespace LiveSQL.Core.Tests.Visualization;

public class FlowBuilderTests
{
    private readonly FlowBuilder _builder;

    public FlowBuilderTests()
    {
        var layout = new FlowLayout();
        var colorMapper = new ColorMapper();
        _builder = new FlowBuilder(layout, colorMapper);
    }

    private static ExecutionPlan CreateSingleNodePlan()
    {
        return new ExecutionPlan
        {
            QueryText = "SELECT 1",
            DatabaseEngine = "SQL Server",
            RootNode = new PlanNode
            {
                Id = 0,
                Label = "Clustered Index Seek",
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
                }
            }
        };
    }

    private static ExecutionPlan CreateTreePlan()
    {
        var root = new PlanNode
        {
            Id = 0,
            Label = "Nested Loops",
            PhysicalOperator = "Nested Loops",
            LogicalOperator = "Inner Join",
            NodeType = NodeType.NestedLoopJoin,
            Cost = new OperationCost
            {
                SubtreeCost = 1.0,
                TotalCost = 0.1,
                CostPercentage = 10
            }
        };

        root.Children.Add(new PlanNode
        {
            Id = 1,
            Label = "Index Seek",
            PhysicalOperator = "Index Seek",
            LogicalOperator = "Index Seek",
            NodeType = NodeType.IndexSeek,
            Depth = 1,
            Cost = new OperationCost
            {
                SubtreeCost = 0.5,
                TotalCost = 0.5,
                CostPercentage = 50,
                ActualRows = 100
            }
        });

        root.Children.Add(new PlanNode
        {
            Id = 2,
            Label = "Table Scan",
            PhysicalOperator = "Table Scan",
            LogicalOperator = "Table Scan",
            NodeType = NodeType.TableScan,
            Depth = 1,
            Cost = new OperationCost
            {
                SubtreeCost = 0.4,
                TotalCost = 0.4,
                CostPercentage = 40,
                ActualRows = 5000
            },
            IsWarning = true,
            WarningMessage = "Table scan on large table",
            Table = new TableReference { TableName = "Orders", Schema = "dbo" }
        });

        return new ExecutionPlan
        {
            QueryText = "SELECT * FROM A JOIN B ON A.Id = B.AId",
            DatabaseEngine = "SQL Server",
            RootNode = root
        };
    }

    [Fact]
    public void Build_SingleNode_ShouldReturnOneNodeAndNoEdges()
    {
        var plan = CreateSingleNodePlan();

        var flow = _builder.Build(plan);

        flow.Should().NotBeNull();
        flow.Nodes.Should().HaveCount(1);
        flow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void Build_TreePlan_ShouldReturnCorrectNodeCount()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        flow.Nodes.Should().HaveCount(3);
    }

    [Fact]
    public void Build_TreePlan_ShouldReturnCorrectEdgeCount()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        flow.Edges.Should().HaveCount(2);
    }

    [Fact]
    public void Build_ShouldAssignColorsBasedOnCost()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        // High cost node (50%) and low cost node (10%) should both have colors
        var highCostNode = flow.Nodes.First(n => n.Label == "Index Seek");
        var lowCostNode = flow.Nodes.First(n => n.Label == "Nested Loops");

        highCostNode.Color.Should().NotBeNullOrEmpty();
        lowCostNode.Color.Should().NotBeNullOrEmpty();
        // Different cost percentages should yield different colors
        highCostNode.Color.Should().NotBe(lowCostNode.Color);
    }

    [Fact]
    public void Build_WarningNode_ShouldBeMarked()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        var warningNode = flow.Nodes.First(n => n.Label == "Table Scan");
        warningNode.IsWarning.Should().BeTrue();
        warningNode.WarningMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_ShouldPreserveNodeIdentity()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        flow.Nodes.Select(n => n.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Build_EdgesShouldConnectParentToChild()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        foreach (var edge in flow.Edges)
        {
            flow.Nodes.Should().Contain(n => n.Id == edge.SourceId);
            flow.Nodes.Should().Contain(n => n.Id == edge.TargetId);
        }
    }

    [Fact]
    public void Build_ShouldSetCanvasDimensions()
    {
        var plan = CreateTreePlan();

        var flow = _builder.Build(plan);

        flow.CanvasWidth.Should().BeGreaterThan(0);
        flow.CanvasHeight.Should().BeGreaterThan(0);
    }
}
