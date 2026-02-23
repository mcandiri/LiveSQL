namespace LiveSQL.Core.Connectors;

public sealed class ConnectorFactory
{
    public IDatabaseConnector CreateConnector(ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerConnector(config),
            DatabaseProvider.PostgreSql => new PostgreSqlConnector(config),
            _ => throw new ArgumentOutOfRangeException(
                nameof(config),
                config.Provider,
                $"Unsupported database provider: {config.Provider}")
        };
    }
}
