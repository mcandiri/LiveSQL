using LiveSQL.Core.Models;

namespace LiveSQL.Core.Analysis;

public sealed class BottleneckDetector
{
    private const double LargeTableRowThreshold = 1000;
    private const double RowEstimateSkewThreshold = 10.0;
    private const double ExpensiveSortRowThreshold = 10_000;
    private const double ExpensiveHashJoinCostThreshold = 30.0;
    private const double DominantOperationCostThreshold = 50.0;

    public List<BottleneckInfo> Detect(ExecutionPlan plan)
    {
        var bottlenecks = new List<BottleneckInfo>();

        foreach (var node in plan.AllNodes)
        {
            DetectTableScansOnLargeTables(node, bottlenecks);
            DetectKeyLookups(node, bottlenecks);
            DetectInaccurateRowEstimates(node, bottlenecks);
            DetectExpensiveSorts(node, bottlenecks);
            DetectExpensiveHashJoins(node, bottlenecks);
            DetectDominantOperations(node, bottlenecks);
        }

        return bottlenecks
            .OrderByDescending(b => b.Severity)
            .ThenByDescending(b => b.ImpactPercentage)
            .ToList();
    }

    private static void DetectTableScansOnLargeTables(PlanNode node, List<BottleneckInfo> bottlenecks)
    {
        var scanTypes = new[] { NodeType.TableScan, NodeType.SeqScan, NodeType.ClusteredIndexScan };
        if (!scanTypes.Contains(node.NodeType)) return;

        var rows = Math.Max(node.Cost.EstimatedRows, node.Cost.ActualRows);
        if (rows < LargeTableRowThreshold) return;

        var severity = rows switch
        {
            >= 100_000 => Severity.Critical,
            >= 10_000 => Severity.High,
            >= 5_000 => Severity.Medium,
            _ => Severity.Low
        };

        var tableName = node.Table?.TableName ?? "unknown table";
        bottlenecks.Add(new BottleneckInfo
        {
            Title = $"Table Scan on {tableName}",
            Description = $"Full table scan reading {rows:N0} rows from {tableName}. " +
                          $"This forces the database to read every row in the table.",
            Severity = severity,
            RelatedNode = node,
            Recommendation = $"Add an index on {tableName} covering the filter columns " +
                             $"to allow an Index Seek instead of a Table Scan.",
            ImpactPercentage = node.Cost.CostPercentage
        });
    }

    private static void DetectKeyLookups(PlanNode node, List<BottleneckInfo> bottlenecks)
    {
        if (node.NodeType != NodeType.KeyLookup) return;

        var rows = Math.Max(node.Cost.EstimatedRows, node.Cost.ActualRows);
        var severity = rows switch
        {
            >= 10_000 => Severity.High,
            >= 1_000 => Severity.Medium,
            _ => Severity.Low
        };

        var tableName = node.Table?.TableName ?? "unknown table";
        bottlenecks.Add(new BottleneckInfo
        {
            Title = $"Key Lookup on {tableName}",
            Description = $"Key Lookup performing {rows:N0} lookups into the clustered index. " +
                          $"Each lookup requires an additional I/O operation.",
            Severity = severity,
            RelatedNode = node,
            Recommendation = "Add the required columns as INCLUDE columns in the non-clustered index " +
                             "to create a covering index and eliminate the Key Lookup.",
            ImpactPercentage = node.Cost.CostPercentage
        });
    }

    private static void DetectInaccurateRowEstimates(PlanNode node, List<BottleneckInfo> bottlenecks)
    {
        if (node.Cost.EstimatedRows <= 0 || node.Cost.ActualRows <= 0) return;

        var ratio = node.Cost.RowEstimateRatio;
        if (ratio >= 1.0 / RowEstimateSkewThreshold && ratio <= RowEstimateSkewThreshold) return;

        var severity = ratio > 100 || ratio < 0.01 ? Severity.High : Severity.Medium;

        bottlenecks.Add(new BottleneckInfo
        {
            Title = $"Inaccurate Row Estimate on {node.Label}",
            Description = $"Estimated {node.Cost.EstimatedRows:N0} rows but actual was {node.Cost.ActualRows:N0} " +
                          $"(ratio: {ratio:F1}x). This can cause the optimizer to choose a suboptimal plan.",
            Severity = severity,
            RelatedNode = node,
            Recommendation = "Update statistics on the involved tables using UPDATE STATISTICS or ANALYZE. " +
                             "Consider adding multi-column statistics if filtering on multiple columns.",
            ImpactPercentage = node.Cost.CostPercentage
        });
    }

    private static void DetectExpensiveSorts(PlanNode node, List<BottleneckInfo> bottlenecks)
    {
        if (node.NodeType != NodeType.Sort) return;

        var rows = Math.Max(node.Cost.EstimatedRows, node.Cost.ActualRows);
        if (rows < ExpensiveSortRowThreshold) return;

        var severity = rows switch
        {
            >= 1_000_000 => Severity.Critical,
            >= 100_000 => Severity.High,
            _ => Severity.Medium
        };

        bottlenecks.Add(new BottleneckInfo
        {
            Title = $"Expensive Sort ({rows:N0} rows)",
            Description = $"Sort operation processing {rows:N0} rows. " +
                          $"Large sorts consume significant memory and may spill to disk (TempDb).",
            Severity = severity,
            RelatedNode = node,
            Recommendation = "Consider adding an index that provides the data in the required sort order, " +
                             "or reduce the data set before sorting using more selective filters.",
            ImpactPercentage = node.Cost.CostPercentage
        });
    }

    private static void DetectExpensiveHashJoins(PlanNode node, List<BottleneckInfo> bottlenecks)
    {
        if (node.NodeType != NodeType.HashJoin && node.NodeType != NodeType.Hash) return;

        if (node.Cost.CostPercentage < ExpensiveHashJoinCostThreshold) return;

        bottlenecks.Add(new BottleneckInfo
        {
            Title = $"Expensive Hash Join ({node.Cost.CostPercentage:F1}% of total cost)",
            Description = $"Hash Join consuming {node.Cost.CostPercentage:F1}% of total query cost. " +
                          $"Hash Joins build an in-memory hash table which can be expensive for large data sets.",
            Severity = Severity.High,
            RelatedNode = node,
            Recommendation = "Ensure join columns are indexed. If the data sets are large, " +
                             "consider rewriting the query to reduce the number of rows before the join.",
            ImpactPercentage = node.Cost.CostPercentage
        });
    }

    private static void DetectDominantOperations(PlanNode node, List<BottleneckInfo> bottlenecks)
    {
        if (node.Cost.CostPercentage < DominantOperationCostThreshold) return;

        // Don't duplicate if already reported as a more specific bottleneck
        var scanTypes = new[] { NodeType.TableScan, NodeType.SeqScan, NodeType.ClusteredIndexScan };
        if (scanTypes.Contains(node.NodeType)) return;
        if (node.NodeType == NodeType.KeyLookup) return;
        if (node.NodeType == NodeType.Sort) return;
        if (node.NodeType == NodeType.HashJoin || node.NodeType == NodeType.Hash) return;

        bottlenecks.Add(new BottleneckInfo
        {
            Title = $"Dominant Operation: {node.Label} ({node.Cost.CostPercentage:F1}%)",
            Description = $"{node.Label} consumes {node.Cost.CostPercentage:F1}% of the total query cost, " +
                          $"making it the dominant operation in this plan.",
            Severity = node.Cost.CostPercentage >= 70 ? Severity.High : Severity.Medium,
            RelatedNode = node,
            Recommendation = "Investigate whether this operation can be optimized through indexing, " +
                             "query rewriting, or reducing the input data set.",
            ImpactPercentage = node.Cost.CostPercentage
        });
    }
}
