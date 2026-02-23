namespace LiveSQL.Core.Models;

public sealed class IndexReference
{
    public string IndexName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<string> IncludedColumns { get; set; } = new();
    public bool IsClustered { get; set; }
    public bool IsUnique { get; set; }

    public override string ToString() =>
        $"{IndexName} on {TableName} ({string.Join(", ", Columns)})";
}
