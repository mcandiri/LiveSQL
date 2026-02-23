namespace LiveSQL.Core.Models;

public sealed class ExecutionPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string QueryText { get; set; } = string.Empty;
    public string DatabaseEngine { get; set; } = string.Empty;
    public PlanNode RootNode { get; set; } = new();
    public QueryMetrics Metrics { get; set; } = new();
    public List<BottleneckInfo> Bottlenecks { get; set; } = new();
    public List<IndexSuggestion> IndexSuggestions { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string RawPlan { get; set; } = string.Empty;

    public IEnumerable<PlanNode> AllNodes => RootNode.DescendantsAndSelf();

    public int TotalNodes => RootNode.TotalNodeCount();

    public double MostExpensiveOperationCost =>
        AllNodes.Max(n => n.Cost.CostPercentage);
}
