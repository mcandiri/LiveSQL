namespace LiveSQL.Core.Models;

public sealed class QueryMetrics
{
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan CpuTime { get; set; }
    public long LogicalReads { get; set; }
    public long PhysicalReads { get; set; }
    public long RowsAffected { get; set; }
    public double TotalCost { get; set; }
    public int TotalOperators { get; set; }
    public int ParallelOperators { get; set; }
    public double MemoryGrantMB { get; set; }
    public int DegreeOfParallelism { get; set; } = 1;
}
