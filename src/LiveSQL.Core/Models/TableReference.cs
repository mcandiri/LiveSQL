namespace LiveSQL.Core.Models;

public sealed class TableReference
{
    public string Schema { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public long EstimatedRowCount { get; set; }

    public string FullName => string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";

    public override string ToString() =>
        string.IsNullOrEmpty(Alias) ? FullName : $"{FullName} AS {Alias}";
}
