namespace LiveSQL.Core.Connectors;

public enum DatabaseProvider
{
    SqlServer,
    PostgreSql
}

public sealed class ConnectionConfig
{
    public required string ConnectionString { get; init; }
    public required DatabaseProvider Provider { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int CommandTimeoutSeconds { get; init; } = 30;
}
