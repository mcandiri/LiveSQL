using Microsoft.Data.SqlClient;

namespace LiveSQL.Core.Connectors;

public sealed class SqlServerConnector : IDatabaseConnector
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSeconds;
    private bool _disposed;

    public string ProviderName => "SqlServer";

    public SqlServerConnector(ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Provider != DatabaseProvider.SqlServer)
            throw new ArgumentException(
                $"Expected provider {DatabaseProvider.SqlServer} but got {config.Provider}.",
                nameof(config));

        _connectionString = config.ConnectionString;
        _commandTimeoutSeconds = config.CommandTimeoutSeconds;
    }

    public async Task<string> GetRawExecutionPlanAsync(string query, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        // Enable XML statistics to capture the execution plan.
        await using (var enableCmd = connection.CreateCommand())
        {
            enableCmd.CommandTimeout = _commandTimeoutSeconds;
            enableCmd.CommandText = "SET STATISTICS XML ON";
            await enableCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = _commandTimeoutSeconds;
        command.CommandText = query;

        string? xmlPlan = null;

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        // The query may produce multiple result sets.
        // The XML execution plan is returned as the last result set
        // with a single row and single column containing the XML string.
        do
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                // For each result set, try to capture the single-column string value.
                // The last result set that looks like XML will be the plan.
                if (reader.FieldCount == 1)
                {
                    var value = reader.GetString(0);
                    if (value.StartsWith("<", StringComparison.Ordinal))
                    {
                        xmlPlan = value;
                    }
                }
            }
        }
        while (await reader.NextResultAsync(ct).ConfigureAwait(false));

        if (string.IsNullOrEmpty(xmlPlan))
            throw new InvalidOperationException(
                "SQL Server did not return an XML execution plan. " +
                "Ensure the query is valid and SET STATISTICS XML is supported.");

        return xmlPlan;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await using var connection = new SqlConnection(_connectionString);
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
