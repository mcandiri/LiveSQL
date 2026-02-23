namespace LiveSQL.Web.Services;

/// <summary>
/// View models for the flow diagram visualization.
/// These map from LiveSQL.Core models to renderable SVG data.
/// </summary>
public sealed class FlowNode
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string NodeTypeName { get; set; } = string.Empty;
    public double CostPercentage { get; set; }
    public double EstimatedRows { get; set; }
    public double ActualRows { get; set; }
    public double TotalCost { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 70;
    public string Color { get; set; } = "#58a6ff";
    public string Icon { get; set; } = "table";
    public bool IsWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? IndexName { get; set; }
    public string? Predicate { get; set; }
    public int Depth { get; set; }
    public List<int> ChildIds { get; set; } = new();
}

public sealed class FlowEdge
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public double Rows { get; set; }
    public double SourceX { get; set; }
    public double SourceY { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }
}

public sealed class FlowDiagramData
{
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();
    public double ViewBoxWidth { get; set; } = 1200;
    public double ViewBoxHeight { get; set; } = 800;
}

public sealed class DemoPlan
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string Icon { get; set; } = "search";
    public string Category { get; set; } = "Query";
    public DemoPlanMetrics Metrics { get; set; } = new();
}

public sealed class DemoPlanMetrics
{
    public double TotalCost { get; set; }
    public double ElapsedMs { get; set; }
    public long RowsReturned { get; set; }
    public int OperatorCount { get; set; }
}

public sealed class BottleneckAlert
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Recommendation { get; set; } = string.Empty;
    public double ImpactPercentage { get; set; }
    public int? RelatedNodeId { get; set; }
}

public sealed class IndexSuggestionData
{
    public string TableName { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<string> IncludedColumns { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public double EstimatedImprovement { get; set; }

    public string CreateIndexSql
    {
        get
        {
            var idxName = $"IX_{TableName}_{string.Join("_", Columns)}";
            var cols = string.Join(", ", Columns);
            var sql = $"CREATE NONCLUSTERED INDEX [{idxName}]\nON [{TableName}] ({cols})";
            if (IncludedColumns.Count > 0)
                sql += $"\nINCLUDE ({string.Join(", ", IncludedColumns)})";
            return sql;
        }
    }
}
