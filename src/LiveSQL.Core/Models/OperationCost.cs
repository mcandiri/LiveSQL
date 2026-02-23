namespace LiveSQL.Core.Models;

public sealed class OperationCost
{
    public double CpuCost { get; set; }
    public double IoCost { get; set; }
    public double TotalCost { get; set; }
    public double SubtreeCost { get; set; }
    public double EstimatedRows { get; set; }
    public double ActualRows { get; set; }
    public int Executions { get; set; } = 1;
    public double CostPercentage { get; set; }

    public double RowEstimateRatio =>
        EstimatedRows > 0 ? ActualRows / EstimatedRows : 0;
}
