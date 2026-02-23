namespace LiveSQL.Core.Visualization;

public sealed class FlowEdge
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public double RowCount { get; set; }
    public string Label { get; set; } = string.Empty;
    public double Thickness { get; set; } = 2.0;
    public string Color { get; set; } = "#8b949e";
    public double AnimationDelay { get; set; }
    public double AnimationDuration { get; set; } = 0.3;
    public bool IsAnimated { get; set; } = true;

    // SVG path points for rendering
    public double SourceX { get; set; }
    public double SourceY { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }

    public string SvgPath
    {
        get
        {
            var midY = (SourceY + TargetY) / 2;
            return $"M {SourceX} {SourceY} C {SourceX} {midY}, {TargetX} {midY}, {TargetX} {TargetY}";
        }
    }
}
