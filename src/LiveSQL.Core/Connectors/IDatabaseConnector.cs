namespace LiveSQL.Core.Connectors;

/// <summary>
/// Abstraction for database-specific execution plan retrieval.
/// </summary>
public interface IDatabaseConnector : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the raw execution plan for the given SQL query.
    /// For SQL Server this returns XML; for PostgreSQL this returns JSON.
    /// </summary>
    Task<string> GetRawExecutionPlanAsync(string query, CancellationToken ct);

    /// <summary>
    /// Verifies that the underlying connection can be established.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken ct);

    /// <summary>
    /// The name of the database provider (e.g. "SqlServer", "PostgreSql").
    /// </summary>
    string ProviderName { get; }
}
