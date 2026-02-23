using LiveSQL.Core.Analysis;
using LiveSQL.Core.Models;
using LiveSQL.Core.Visualization;

namespace LiveSQL.Core.Demo;

public sealed class DemoDataProvider
{
    private readonly QueryAnalyzer _analyzer;
    private readonly FlowBuilder _flowBuilder;
    private readonly PlanComparator _comparator;

    public DemoDataProvider(QueryAnalyzer analyzer, FlowBuilder flowBuilder, PlanComparator comparator)
    {
        _analyzer = analyzer;
        _flowBuilder = flowBuilder;
        _comparator = comparator;
    }

    public IReadOnlyList<DemoPlan> GetAllDemoPlans()
    {
        return new List<DemoPlan>
        {
            new DemoPlan
            {
                Id = "simple-select",
                Name = "Simple Select",
                Description = "Fast point lookup using a clustered index seek on the primary key.",
                Category = DemoCategory.Good,
                Plan = SamplePlans.SimpleSelect(),
                FlowData = _flowBuilder.Build(SamplePlans.SimpleSelect())
            },
            new DemoPlan
            {
                Id = "table-scan-problem",
                Name = "Table Scan Problem",
                Description = "Full table scan caused by a LIKE pattern with leading wildcard, reading all 50K rows.",
                Category = DemoCategory.Bad,
                Plan = SamplePlans.TableScanProblem(),
                FlowData = _flowBuilder.Build(SamplePlans.TableScanProblem())
            },
            new DemoPlan
            {
                Id = "missing-index",
                Name = "Missing Index",
                Description = "Table scan reading 200K rows for a single result. A composite index would make this instant.",
                Category = DemoCategory.Bad,
                Plan = SamplePlans.MissingIndex(),
                FlowData = _flowBuilder.Build(SamplePlans.MissingIndex())
            },
            new DemoPlan
            {
                Id = "complex-join",
                Name = "Complex Join",
                Description = "Four-table join combining Students, Enrollments, Courses, and Grades with multiple join types.",
                Category = DemoCategory.Mixed,
                Plan = SamplePlans.ComplexJoin(),
                FlowData = _flowBuilder.Build(SamplePlans.ComplexJoin())
            },
            new DemoPlan
            {
                Id = "sort-aggregate",
                Name = "Sort & Aggregate",
                Description = "GROUP BY and ORDER BY query with Hash Aggregate and Sort bottlenecks.",
                Category = DemoCategory.Bad,
                Plan = SamplePlans.SortAndAggregate(),
                FlowData = _flowBuilder.Build(SamplePlans.SortAndAggregate())
            },
            GetBeforeAfterDemo()
        };
    }

    public DemoPlan GetDemoPlanById(string id)
    {
        return GetAllDemoPlans().FirstOrDefault(p => p.Id == id)
            ?? throw new ArgumentException($"Demo plan '{id}' not found.", nameof(id));
    }

    private DemoPlan GetBeforeAfterDemo()
    {
        var (before, after) = SamplePlans.BeforeAfterComparison();
        var comparison = _comparator.Compare(before, after);

        return new DemoPlan
        {
            Id = "before-after",
            Name = "Before / After Comparison",
            Description = "Same query with and without indexes showing dramatic improvement (340x cost reduction).",
            Category = DemoCategory.Comparison,
            Plan = before,
            ComparisonPlan = after,
            ComparisonResult = comparison,
            FlowData = _flowBuilder.Build(before),
            ComparisonFlowData = _flowBuilder.Build(after)
        };
    }
}

public sealed class DemoPlan
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DemoCategory Category { get; set; }
    public ExecutionPlan Plan { get; set; } = null!;
    public ExecutionPlan? ComparisonPlan { get; set; }
    public PlanComparisonResult? ComparisonResult { get; set; }
    public FlowData FlowData { get; set; } = null!;
    public FlowData? ComparisonFlowData { get; set; }
}

public enum DemoCategory
{
    Good,
    Bad,
    Mixed,
    Comparison
}
