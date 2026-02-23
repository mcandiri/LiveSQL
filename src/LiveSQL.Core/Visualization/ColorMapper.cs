using LiveSQL.Core.Models;

namespace LiveSQL.Core.Visualization;

public sealed class ColorMapper
{
    public const string Green = "#3fb950";
    public const string Yellow = "#d29922";
    public const string Orange = "#db6d28";
    public const string Red = "#f85149";
    public const string NeutralGray = "#8b949e";
    public const string BackgroundDark = "#0d1117";
    public const string SurfaceDark = "#161b22";
    public const string BorderDark = "#30363d";

    public string MapCostToColor(double costPercentage) => costPercentage switch
    {
        < 10 => Green,
        < 30 => Yellow,
        < 50 => Orange,
        _ => Red
    };

    public string MapSeverityToColor(Severity severity) => severity switch
    {
        Severity.Low => Green,
        Severity.Medium => Yellow,
        Severity.High => Orange,
        Severity.Critical => Red,
        _ => NeutralGray
    };

    public string MapNodeTypeToIcon(NodeType nodeType) => nodeType switch
    {
        NodeType.TableScan or NodeType.SeqScan => "table-scan",
        NodeType.ClusteredIndexScan => "index-scan",
        NodeType.IndexScan => "index-scan",
        NodeType.IndexSeek or NodeType.ClusteredIndexSeek => "index-seek",
        NodeType.KeyLookup => "key-lookup",
        NodeType.NestedLoopJoin => "nested-loop",
        NodeType.HashJoin => "hash-join",
        NodeType.MergeJoin => "merge-join",
        NodeType.StreamAggregate or NodeType.HashAggregate => "aggregate",
        NodeType.Sort => "sort",
        NodeType.Filter => "filter",
        NodeType.TopN or NodeType.Limit => "top",
        NodeType.Distinct or NodeType.Unique => "distinct",
        NodeType.Compute or NodeType.Result => "compute",
        NodeType.Insert or NodeType.Update or NodeType.Delete => "dml",
        NodeType.BitmapHeapScan or NodeType.BitmapIndexScan => "bitmap-scan",
        NodeType.Hash => "hash",
        NodeType.Materialize => "materialize",
        NodeType.Append => "append",
        NodeType.WindowAgg => "window",
        _ => "operation"
    };

    public string MapNodeTypeToBorderColor(NodeType nodeType) => nodeType switch
    {
        NodeType.TableScan or NodeType.SeqScan => Red,
        NodeType.ClusteredIndexScan or NodeType.IndexScan => Yellow,
        NodeType.IndexSeek or NodeType.ClusteredIndexSeek => Green,
        NodeType.KeyLookup => Orange,
        NodeType.HashJoin => Yellow,
        NodeType.Sort => Yellow,
        _ => NeutralGray
    };

    public double MapRowCountToEdgeThickness(double rowCount) => rowCount switch
    {
        < 10 => 1.5,
        < 100 => 2.0,
        < 1000 => 3.0,
        < 10000 => 4.0,
        _ => 5.0
    };
}
