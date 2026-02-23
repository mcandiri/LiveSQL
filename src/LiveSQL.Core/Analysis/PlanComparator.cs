using LiveSQL.Core.Models;

namespace LiveSQL.Core.Analysis;

public sealed class PlanComparator
{
    public PlanComparisonResult Compare(ExecutionPlan before, ExecutionPlan after)
    {
        var result = new PlanComparisonResult
        {
            Before = before,
            After = after,
            CostReduction = ComputeCostReduction(before, after),
            RowReduction = ComputeRowReduction(before, after),
            OperatorCountChange = after.TotalNodes - before.TotalNodes,
            BottleneckReduction = before.Bottlenecks.Count - after.Bottlenecks.Count,
            Improvements = new List<string>(),
            Regressions = new List<string>()
        };

        AnalyzeOperatorChanges(before, after, result);
        AnalyzeCostChanges(before, after, result);
        AnalyzeBottleneckChanges(before, after, result);
        ComputeOverallVerdict(result);

        return result;
    }

    private static double ComputeCostReduction(ExecutionPlan before, ExecutionPlan after)
    {
        if (before.Metrics.TotalCost <= 0) return 0;
        return ((before.Metrics.TotalCost - after.Metrics.TotalCost) / before.Metrics.TotalCost) * 100;
    }

    private static double ComputeRowReduction(ExecutionPlan before, ExecutionPlan after)
    {
        var beforeRows = before.AllNodes.Sum(n => n.Cost.ActualRows > 0 ? n.Cost.ActualRows : n.Cost.EstimatedRows);
        var afterRows = after.AllNodes.Sum(n => n.Cost.ActualRows > 0 ? n.Cost.ActualRows : n.Cost.EstimatedRows);

        if (beforeRows <= 0) return 0;
        return ((beforeRows - afterRows) / beforeRows) * 100;
    }

    private static void AnalyzeOperatorChanges(ExecutionPlan before, ExecutionPlan after, PlanComparisonResult result)
    {
        var beforeTypes = CountNodeTypes(before);
        var afterTypes = CountNodeTypes(after);

        var scanTypes = new[] { NodeType.TableScan, NodeType.SeqScan, NodeType.ClusteredIndexScan };
        var seekTypes = new[] { NodeType.IndexSeek, NodeType.ClusteredIndexSeek };

        var beforeScans = scanTypes.Sum(t => beforeTypes.GetValueOrDefault(t));
        var afterScans = scanTypes.Sum(t => afterTypes.GetValueOrDefault(t));

        var beforeSeeks = seekTypes.Sum(t => beforeTypes.GetValueOrDefault(t));
        var afterSeeks = seekTypes.Sum(t => afterTypes.GetValueOrDefault(t));

        if (afterScans < beforeScans)
            result.Improvements.Add($"Table scans reduced from {beforeScans} to {afterScans}");

        if (afterSeeks > beforeSeeks)
            result.Improvements.Add($"Index seeks increased from {beforeSeeks} to {afterSeeks}");

        if (afterScans > beforeScans)
            result.Regressions.Add($"Table scans increased from {beforeScans} to {afterScans}");

        var beforeLookups = beforeTypes.GetValueOrDefault(NodeType.KeyLookup);
        var afterLookups = afterTypes.GetValueOrDefault(NodeType.KeyLookup);
        if (afterLookups < beforeLookups)
            result.Improvements.Add($"Key lookups eliminated ({beforeLookups} -> {afterLookups})");
    }

    private static void AnalyzeCostChanges(ExecutionPlan before, ExecutionPlan after, PlanComparisonResult result)
    {
        if (result.CostReduction > 50)
            result.Improvements.Add($"Total cost reduced by {result.CostReduction:F1}%");
        else if (result.CostReduction > 10)
            result.Improvements.Add($"Total cost reduced by {result.CostReduction:F1}%");
        else if (result.CostReduction < -10)
            result.Regressions.Add($"Total cost increased by {Math.Abs(result.CostReduction):F1}%");

        if (after.Metrics.ElapsedTime > TimeSpan.Zero && before.Metrics.ElapsedTime > TimeSpan.Zero)
        {
            if (after.Metrics.ElapsedTime < before.Metrics.ElapsedTime)
            {
                var speedup = before.Metrics.ElapsedTime.TotalMilliseconds / after.Metrics.ElapsedTime.TotalMilliseconds;
                result.Improvements.Add($"Execution time {speedup:F1}x faster");
            }
        }
    }

    private static void AnalyzeBottleneckChanges(ExecutionPlan before, ExecutionPlan after, PlanComparisonResult result)
    {
        var resolvedBottlenecks = before.Bottlenecks
            .Where(b => b.Severity >= Severity.High)
            .Count(b => !after.Bottlenecks.Any(ab =>
                ab.Title == b.Title && ab.Severity >= Severity.High));

        if (resolvedBottlenecks > 0)
            result.Improvements.Add($"{resolvedBottlenecks} critical bottleneck(s) resolved");

        var newBottlenecks = after.Bottlenecks
            .Count(ab => ab.Severity >= Severity.High &&
                         !before.Bottlenecks.Any(b => b.Title == ab.Title));

        if (newBottlenecks > 0)
            result.Regressions.Add($"{newBottlenecks} new bottleneck(s) introduced");
    }

    private static void ComputeOverallVerdict(PlanComparisonResult result)
    {
        if (result.CostReduction >= 50 && result.Regressions.Count == 0)
            result.Verdict = ComparisonVerdict.SignificantImprovement;
        else if (result.CostReduction >= 10)
            result.Verdict = ComparisonVerdict.Improved;
        else if (result.CostReduction <= -10)
            result.Verdict = ComparisonVerdict.Regressed;
        else if (result.Improvements.Count > result.Regressions.Count)
            result.Verdict = ComparisonVerdict.SlightlyImproved;
        else
            result.Verdict = ComparisonVerdict.NoChange;
    }

    private static Dictionary<NodeType, int> CountNodeTypes(ExecutionPlan plan)
    {
        return plan.AllNodes
            .GroupBy(n => n.NodeType)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}

public sealed class PlanComparisonResult
{
    public ExecutionPlan Before { get; set; } = null!;
    public ExecutionPlan After { get; set; } = null!;
    public double CostReduction { get; set; }
    public double RowReduction { get; set; }
    public int OperatorCountChange { get; set; }
    public int BottleneckReduction { get; set; }
    public List<string> Improvements { get; set; } = new();
    public List<string> Regressions { get; set; } = new();
    public ComparisonVerdict Verdict { get; set; }
}

public enum ComparisonVerdict
{
    SignificantImprovement,
    Improved,
    SlightlyImproved,
    NoChange,
    Regressed
}
