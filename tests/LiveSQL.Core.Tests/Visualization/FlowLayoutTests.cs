using FluentAssertions;
using LiveSQL.Core.Models;
using LiveSQL.Core.Visualization;

namespace LiveSQL.Core.Tests.Visualization;

public class FlowLayoutTests
{
    private readonly FlowLayout _layout = new();

    private static PlanNode CreateSingleNode()
    {
        return new PlanNode
        {
            Id = 0,
            Label = "Result",
            PhysicalOperator = "Constant Scan",
            NodeType = NodeType.Result,
            Cost = new OperationCost { CostPercentage = 100 }
        };
    }

    private static PlanNode CreateTreeNode()
    {
        var root = new PlanNode
        {
            Id = 0,
            Label = "Hash Match",
            PhysicalOperator = "Hash Match",
            NodeType = NodeType.HashJoin,
            Depth = 0,
            Cost = new OperationCost { CostPercentage = 20 }
        };

        root.Children.Add(new PlanNode
        {
            Id = 1,
            Label = "Clustered Index Scan",
            PhysicalOperator = "Clustered Index Scan",
            NodeType = NodeType.ClusteredIndexScan,
            Depth = 1,
            Cost = new OperationCost { CostPercentage = 30 }
        });

        root.Children.Add(new PlanNode
        {
            Id = 2,
            Label = "Index Seek",
            PhysicalOperator = "Index Seek",
            NodeType = NodeType.IndexSeek,
            Depth = 1,
            Cost = new OperationCost { CostPercentage = 50 }
        });

        return root;
    }

    private static PlanNode CreateDeepTree()
    {
        var level2a = new PlanNode
        {
            Id = 3, Label = "Scan A", PhysicalOperator = "Index Scan",
            NodeType = NodeType.IndexScan, Depth = 2,
            Cost = new OperationCost { CostPercentage = 25 }
        };
        var level2b = new PlanNode
        {
            Id = 4, Label = "Scan B", PhysicalOperator = "Index Scan",
            NodeType = NodeType.IndexScan, Depth = 2,
            Cost = new OperationCost { CostPercentage = 25 }
        };

        var level1a = new PlanNode
        {
            Id = 1, Label = "Nested Loops", PhysicalOperator = "Nested Loops",
            NodeType = NodeType.NestedLoopJoin, Depth = 1,
            Cost = new OperationCost { CostPercentage = 20 },
            Children = new List<PlanNode> { level2a, level2b }
        };
        var level1b = new PlanNode
        {
            Id = 2, Label = "Hash", PhysicalOperator = "Hash",
            NodeType = NodeType.Hash, Depth = 1,
            Cost = new OperationCost { CostPercentage = 10 }
        };

        return new PlanNode
        {
            Id = 0, Label = "Merge Join", PhysicalOperator = "Merge Join",
            NodeType = NodeType.MergeJoin, Depth = 0,
            Cost = new OperationCost { CostPercentage = 20 },
            Children = new List<PlanNode> { level1a, level1b }
        };
    }

    [Fact]
    public void ComputeLayout_SingleNode_ShouldPositionAtTopLeft()
    {
        var root = CreateSingleNode();

        var result = _layout.ComputeLayout(root);

        result.NodePositions.Should().HaveCount(1);
        var pos = result.NodePositions[0];
        pos.X.Should().BeGreaterOrEqualTo(0);
        pos.Y.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ComputeLayout_TreePlan_ShouldPositionAllNodes()
    {
        var root = CreateTreeNode();

        var result = _layout.ComputeLayout(root);

        result.NodePositions.Should().HaveCount(3);
    }

    [Fact]
    public void ComputeLayout_TreePlan_ChildrenShouldBeBelow()
    {
        var root = CreateTreeNode();

        var result = _layout.ComputeLayout(root);

        var rootPos = result.NodePositions[0];
        var child1Pos = result.NodePositions[1];
        var child2Pos = result.NodePositions[2];

        // Children should be below the parent (higher Y)
        child1Pos.Y.Should().BeGreaterThan(rootPos.Y);
        child2Pos.Y.Should().BeGreaterThan(rootPos.Y);
    }

    [Fact]
    public void ComputeLayout_TreePlan_SiblingsShouldNotOverlap()
    {
        var root = CreateTreeNode();

        var result = _layout.ComputeLayout(root);

        var child1Pos = result.NodePositions[1];
        var child2Pos = result.NodePositions[2];

        // Siblings should be at different X positions
        Math.Abs(child1Pos.X - child2Pos.X).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ComputeLayout_DeepTree_ShouldPositionAllNodes()
    {
        var root = CreateDeepTree();

        var result = _layout.ComputeLayout(root);

        result.NodePositions.Should().HaveCount(5);
    }

    [Fact]
    public void ComputeLayout_ShouldGenerateEdgeConnections()
    {
        var root = CreateTreeNode();

        var result = _layout.ComputeLayout(root);

        result.EdgeConnections.Should().HaveCount(2);
        result.EdgeConnections.Should().Contain(e => e.SourceId == 0 && e.TargetId == 1);
        result.EdgeConnections.Should().Contain(e => e.SourceId == 0 && e.TargetId == 2);
    }

    [Fact]
    public void ComputeLayout_NodesHavePositiveDimensions()
    {
        var root = CreateTreeNode();

        var result = _layout.ComputeLayout(root);

        result.NodePositions.Values.Should().AllSatisfy(pos =>
        {
            pos.Width.Should().BeGreaterThan(0);
            pos.Height.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void ComputeLayout_ShouldSetCanvasDimensions()
    {
        var root = CreateTreeNode();

        var result = _layout.ComputeLayout(root);

        result.CanvasWidth.Should().BeGreaterThan(0);
        result.CanvasHeight.Should().BeGreaterThan(0);
    }
}
