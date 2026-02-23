namespace LiveSQL.Core.Models;

public sealed class BottleneckInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public PlanNode? RelatedNode { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public double ImpactPercentage { get; set; }
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}
