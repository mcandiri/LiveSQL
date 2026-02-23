using LiveSQL.Core.Models;

namespace LiveSQL.Core.Parsers;

public sealed class PlanNormalizer
{
    private readonly IEnumerable<IPlanParser> _parsers;

    public PlanNormalizer(IEnumerable<IPlanParser> parsers)
    {
        _parsers = parsers;
    }

    public async Task<ExecutionPlan> NormalizeAsync(string rawPlan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var parser = _parsers.FirstOrDefault(p => p.CanParse(rawPlan))
            ?? throw new InvalidOperationException(
                "No parser found for the given execution plan format. " +
                "Supported formats: SQL Server XML (SET STATISTICS XML ON), " +
                "PostgreSQL JSON (EXPLAIN (ANALYZE, FORMAT JSON)).");

        var plan = await parser.ParseAsync(rawPlan, ct);

        // Normalize node labels for consistency
        NormalizeLabels(plan.RootNode);

        // Ensure all nodes have cost percentages
        EnsureCostPercentages(plan);

        // Assign IDs sequentially if needed
        ReassignIds(plan.RootNode);

        return plan;
    }

    private static void NormalizeLabels(PlanNode node)
    {
        foreach (var n in node.DescendantsAndSelf())
        {
            if (string.IsNullOrEmpty(n.Label))
            {
                n.Label = n.PhysicalOperator;
            }

            // Normalize common PostgreSQL names to match SQL Server conventions in labels
            n.Label = n.NodeType switch
            {
                NodeType.SeqScan => "Table Scan",
                NodeType.BitmapHeapScan => "Bitmap Heap Scan",
                NodeType.BitmapIndexScan => "Bitmap Index Scan",
                NodeType.CTEScan => "CTE Scan",
                NodeType.Hash => "Hash",
                NodeType.Result => "Result",
                NodeType.Append => "Append",
                NodeType.Limit => "Top",
                NodeType.Materialize => "Materialize",
                NodeType.Unique => "Distinct",
                NodeType.WindowAgg => "Window Aggregate",
                NodeType.SubqueryScan => "Subquery Scan",
                _ => n.Label
            };
        }
    }

    private static void EnsureCostPercentages(ExecutionPlan plan)
    {
        var totalCost = plan.Metrics.TotalCost;
        if (totalCost <= 0)
        {
            totalCost = plan.AllNodes.Sum(n => n.Cost.TotalCost);
            plan.Metrics.TotalCost = totalCost;
        }

        if (totalCost <= 0) return;

        // Verify percentages sum roughly to 100
        var totalPercentage = plan.AllNodes.Sum(n => n.Cost.CostPercentage);
        if (totalPercentage < 1.0)
        {
            foreach (var n in plan.AllNodes)
            {
                n.Cost.CostPercentage = (n.Cost.TotalCost / totalCost) * 100.0;
            }
        }
    }

    private static void ReassignIds(PlanNode root)
    {
        int id = 0;
        foreach (var node in root.DescendantsAndSelf())
        {
            node.Id = id++;
        }
    }
}
