using LiveSQL.Core.Models;

namespace LiveSQL.Core.Visualization;

public sealed class FlowNode
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 80;
    public string Color { get; set; } = "#3fb950";
    public string BorderColor { get; set; } = "#3fb950";
    public string TextColor { get; set; } = "#ffffff";
    public double CostPercentage { get; set; }
    public bool IsWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
    public double AnimationDelay { get; set; }
    public double AnimationDuration { get; set; } = 0.5;
    public double Opacity { get; set; } = 1.0;
    public string Icon { get; set; } = string.Empty;
    public NodeType NodeType { get; set; }
    public double EstimatedRows { get; set; }
    public double ActualRows { get; set; }
    public string Predicate { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
}
