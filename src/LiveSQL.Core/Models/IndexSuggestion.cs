namespace LiveSQL.Core.Models;

public sealed class IndexSuggestion
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public List<string> KeyColumns { get; set; } = new();
    public List<string> IncludeColumns { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public double EstimatedImprovement { get; set; }
    public Severity Impact { get; set; }

    public string IndexName
    {
        get
        {
            var cols = string.Join("_", KeyColumns);
            return $"IX_{TableName}_{cols}";
        }
    }

    public string CreateIndexStatement
    {
        get
        {
            var fullTable = string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";
            var keyCols = string.Join(", ", KeyColumns);
            var statement = $"CREATE NONCLUSTERED INDEX [{IndexName}]\nON [{fullTable}] ({keyCols})";

            if (IncludeColumns.Count > 0)
            {
                var includeCols = string.Join(", ", IncludeColumns.Select(c => $"[{c}]"));
                statement += $"\nINCLUDE ({includeCols})";
            }

            return statement + ";";
        }
    }
}
