using Npgsql;

namespace LiveSQL.Core.Connectors;

public sealed class PostgreSqlConnector : IDatabaseConnector
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSeconds;
    private bool _disposed;

    public string ProviderName => "PostgreSql";

    public PostgreSqlConnector(ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Provider != DatabaseProvider.PostgreSql)
            throw new ArgumentException(
                $"Expected provider {DatabaseProvider.PostgreSql} but got {config.Provider}.",
                nameof(config));

        _connectionString = config.ConnectionString;
        _commandTimeoutSeconds = config.CommandTimeoutSeconds;
    }

    public async Task<string> GetRawExecutionPlanAsync(string query, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = _commandTimeoutSeconds;
        command.CommandText = $"EXPLAIN (ANALYZE, FORMAT JSON) {query}";

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);

        if (result is null || result is DBNull)
            throw new InvalidOperationException(
                "PostgreSQL did not return an execution plan. " +
                "Ensure the query is valid.");

        return result.ToString()!;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
