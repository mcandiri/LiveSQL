using LiveSQL.Core.Models;

namespace LiveSQL.Core.Analysis;

public sealed class CostAnalyzer
{
    public List<PlanNode> FindExpensiveOperations(ExecutionPlan plan, double thresholdPercent = 20.0)
    {
        return plan.AllNodes
            .Where(n => n.Cost.CostPercentage >= thresholdPercent)
            .OrderByDescending(n => n.Cost.CostPercentage)
            .ToList();
    }

    public PlanNode? FindMostExpensiveNode(ExecutionPlan plan)
    {
        return plan.AllNodes
            .OrderByDescending(n => n.Cost.CostPercentage)
            .FirstOrDefault();
    }

    public List<PlanNode> FindHighCpuOperations(ExecutionPlan plan)
    {
        return plan.AllNodes
            .Where(n => n.Cost.CpuCost > 0 && n.Cost.CpuCost > n.Cost.IoCost * 2)
            .OrderByDescending(n => n.Cost.CpuCost)
            .ToList();
    }

    public List<PlanNode> FindHighIoOperations(ExecutionPlan plan)
    {
        return plan.AllNodes
            .Where(n => n.Cost.IoCost > 0 && n.Cost.IoCost > n.Cost.CpuCost * 2)
            .OrderByDescending(n => n.Cost.IoCost)
            .ToList();
    }

    public double ComputeTotalScanCostPercentage(ExecutionPlan plan)
    {
        var scanTypes = new[]
        {
            NodeType.TableScan, NodeType.ClusteredIndexScan,
            NodeType.IndexScan, NodeType.SeqScan
        };

        return plan.AllNodes
            .Where(n => scanTypes.Contains(n.NodeType))
            .Sum(n => n.Cost.CostPercentage);
    }
}
