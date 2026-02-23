using LiveSQL.Core.Models;

namespace LiveSQL.Core.Parsers;

public interface IPlanParser
{
    string EngineType { get; }
    bool CanParse(string rawPlan);
    Task<ExecutionPlan> ParseAsync(string rawPlan, CancellationToken ct);
}
