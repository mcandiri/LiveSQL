using LiveSQL.Core.Models;

namespace LiveSQL.Core.Analysis;

public sealed class QueryAnalyzer : IQueryAnalyzer
{
    private readonly CostAnalyzer _costAnalyzer;
    private readonly BottleneckDetector _bottleneckDetector;
    private readonly IndexAdvisor _indexAdvisor;

    public QueryAnalyzer(CostAnalyzer costAnalyzer, BottleneckDetector bottleneckDetector, IndexAdvisor indexAdvisor)
    {
        _costAnalyzer = costAnalyzer;
        _bottleneckDetector = bottleneckDetector;
        _indexAdvisor = indexAdvisor;
    }

    public Task<ExecutionPlan> AnalyzeAsync(ExecutionPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Detect bottlenecks
        plan.Bottlenecks = _bottleneckDetector.Detect(plan);

        // Suggest indexes
        plan.IndexSuggestions = _indexAdvisor.Suggest(plan);

        // Mark warning nodes based on bottleneck analysis
        MarkWarningNodes(plan);

        // Compute additional metrics
        ComputeMetrics(plan);

        return Task.FromResult(plan);
    }

    private static void MarkWarningNodes(ExecutionPlan plan)
    {
        foreach (var bottleneck in plan.Bottlenecks.Where(b => b.Severity >= Severity.High))
        {
            if (bottleneck.RelatedNode != null)
            {
                bottleneck.RelatedNode.IsWarning = true;
                if (string.IsNullOrEmpty(bottleneck.RelatedNode.WarningMessage))
                {
                    bottleneck.RelatedNode.WarningMessage = bottleneck.Title;
                }
            }
        }
    }

    private static void ComputeMetrics(ExecutionPlan plan)
    {
        var allNodes = plan.AllNodes.ToList();

        plan.Metrics.TotalOperators = allNodes.Count;

        // Count parallel operators (those with executions > 1)
        plan.Metrics.ParallelOperators = allNodes.Count(n => n.Cost.Executions > 1);

        // Calculate total logical reads (sum of estimated rows across scan operations)
        var scanTypes = new[]
        {
            NodeType.TableScan, NodeType.ClusteredIndexScan,
            NodeType.IndexScan, NodeType.SeqScan
        };
        plan.Metrics.LogicalReads = (long)allNodes
            .Where(n => scanTypes.Contains(n.NodeType))
            .Sum(n => Math.Max(n.Cost.EstimatedRows, n.Cost.ActualRows));
    }
}
