using LiveSQL.Core.Models;

namespace LiveSQL.Core.Analysis;

public interface IQueryAnalyzer
{
    Task<ExecutionPlan> AnalyzeAsync(ExecutionPlan plan, CancellationToken ct);
}
