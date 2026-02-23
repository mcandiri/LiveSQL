using FluentAssertions;
using LiveSQL.Core.Demo;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Tests.Demo;

public class DemoDataProviderTests
{
    [Fact]
    public void SimpleSelect_ShouldReturnValidPlan()
    {
        var plan = SamplePlans.SimpleSelect();

        plan.Should().NotBeNull();
        plan.RootNode.Should().NotBeNull();
        plan.QueryText.Should().NotBeNullOrEmpty();
        plan.DatabaseEngine.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TableScanProblem_ShouldReturnValidPlan()
    {
        var plan = SamplePlans.TableScanProblem();

        plan.Should().NotBeNull();
        plan.RootNode.Should().NotBeNull();
        plan.QueryText.Should().Contain("Students");
    }

    [Fact]
    public void MissingIndex_ShouldReturnValidPlan()
    {
        var plan = SamplePlans.MissingIndex();

        plan.Should().NotBeNull();
        plan.RootNode.Should().NotBeNull();
        plan.QueryText.Should().Contain("Enrollments");
    }

    [Fact]
    public void ComplexJoin_ShouldReturnValidPlan()
    {
        var plan = SamplePlans.ComplexJoin();

        plan.Should().NotBeNull();
        plan.RootNode.Should().NotBeNull();
        plan.TotalNodes.Should().BeGreaterThan(3);
    }

    [Fact]
    public void SortAndAggregate_ShouldReturnValidPlan()
    {
        var plan = SamplePlans.SortAndAggregate();

        plan.Should().NotBeNull();
        plan.RootNode.Should().NotBeNull();
        plan.RootNode.NodeType.Should().Be(NodeType.Sort);
    }

    [Fact]
    public void BeforeAfterComparison_ShouldReturnTwoPlans()
    {
        var (before, after) = SamplePlans.BeforeAfterComparison();

        before.Should().NotBeNull();
        after.Should().NotBeNull();
        before.RootNode.Should().NotBeNull();
        after.RootNode.Should().NotBeNull();
    }

    [Fact]
    public void AllPlans_ShouldHaveRootNodes()
    {
        var plans = new List<ExecutionPlan>
        {
            SamplePlans.SimpleSelect(),
            SamplePlans.TableScanProblem(),
            SamplePlans.MissingIndex(),
            SamplePlans.ComplexJoin(),
            SamplePlans.SortAndAggregate()
        };
        var (before, after) = SamplePlans.BeforeAfterComparison();
        plans.Add(before);
        plans.Add(after);

        plans.Should().AllSatisfy(p =>
        {
            p.RootNode.Should().NotBeNull();
            p.TotalNodes.Should().BeGreaterOrEqualTo(1);
        });
    }

    [Fact]
    public void AllPlans_ShouldHaveValidNodeTypes()
    {
        var plans = new List<ExecutionPlan>
        {
            SamplePlans.SimpleSelect(),
            SamplePlans.TableScanProblem(),
            SamplePlans.MissingIndex(),
            SamplePlans.ComplexJoin(),
            SamplePlans.SortAndAggregate()
        };

        foreach (var plan in plans)
        {
            plan.AllNodes.Should().AllSatisfy(node =>
            {
                node.PhysicalOperator.Should().NotBeNullOrEmpty();
                node.NodeType.Should().BeOneOf(Enum.GetValues<NodeType>());
            });
        }
    }

    [Fact]
    public void AllPlans_ShouldHaveReasonableCosts()
    {
        var plans = new List<ExecutionPlan>
        {
            SamplePlans.SimpleSelect(),
            SamplePlans.TableScanProblem(),
            SamplePlans.MissingIndex(),
            SamplePlans.ComplexJoin(),
            SamplePlans.SortAndAggregate()
        };

        foreach (var plan in plans)
        {
            plan.AllNodes.Should().AllSatisfy(node =>
            {
                node.Cost.SubtreeCost.Should().BeGreaterOrEqualTo(0);
                node.Cost.EstimatedRows.Should().BeGreaterOrEqualTo(0);
            });
        }
    }

    [Fact]
    public void SimpleSelect_ShouldHaveNoHighSeverityBottlenecks()
    {
        var plan = SamplePlans.SimpleSelect();

        plan.Bottlenecks.Where(b => b.Severity >= Severity.High).Should().BeEmpty();
    }

    [Fact]
    public void TableScanProblem_ShouldHaveBottlenecksDetected()
    {
        var plan = SamplePlans.TableScanProblem();

        plan.Bottlenecks.Should().NotBeEmpty();
        plan.Bottlenecks.Should().Contain(b => b.Severity >= Severity.High);
    }

    [Fact]
    public void TableScanProblem_ShouldHaveIndexSuggestions()
    {
        var plan = SamplePlans.TableScanProblem();

        plan.IndexSuggestions.Should().NotBeEmpty();
    }
}
