using System.Text.Json;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Parsers;

public sealed class PostgreSqlPlanParser : IPlanParser
{
    public string EngineType => "PostgreSQL";

    public bool CanParse(string rawPlan)
    {
        var trimmed = rawPlan.TrimStart();
        if (!trimmed.StartsWith("[") && !trimmed.StartsWith("{"))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                return first.TryGetProperty("Plan", out _) ||
                       first.TryGetProperty("plan", out _);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public Task<ExecutionPlan> ParseAsync(string rawPlan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var doc = JsonDocument.Parse(rawPlan);
        var root = doc.RootElement;

        var planElement = root.ValueKind == JsonValueKind.Array
            ? root[0]
            : root;

        var plan = new ExecutionPlan
        {
            DatabaseEngine = EngineType,
            RawPlan = rawPlan
        };

        if (planElement.TryGetProperty("Plan", out var planNode) ||
            planElement.TryGetProperty("plan", out planNode))
        {
            int nodeId = 0;
            plan.RootNode = ParseNode(planNode, 0, ref nodeId);
        }

        // Extract timing from top-level
        if (planElement.TryGetProperty("Planning Time", out var planTime))
        {
            plan.Metrics.CpuTime = TimeSpan.FromMilliseconds(planTime.GetDouble());
        }
        if (planElement.TryGetProperty("Execution Time", out var execTime))
        {
            plan.Metrics.ElapsedTime = TimeSpan.FromMilliseconds(execTime.GetDouble());
        }

        // Compute total cost and percentages
        plan.Metrics.TotalCost = plan.RootNode.Cost.SubtreeCost;
        plan.Metrics.TotalOperators = plan.TotalNodes;
        ComputeCostPercentages(plan.RootNode, plan.Metrics.TotalCost);

        return Task.FromResult(plan);
    }

    private PlanNode ParseNode(JsonElement element, int depth, ref int nodeId)
    {
        var nodeTypeStr = GetString(element, "Node Type");
        var node = new PlanNode
        {
            Id = nodeId++,
            PhysicalOperator = nodeTypeStr,
            LogicalOperator = nodeTypeStr,
            Label = nodeTypeStr,
            NodeType = MapNodeType(nodeTypeStr),
            Depth = depth,
            Cost = new OperationCost
            {
                TotalCost = GetDouble(element, "Total Cost"),
                SubtreeCost = GetDouble(element, "Total Cost"),
                CpuCost = GetDouble(element, "Startup Cost"),
                EstimatedRows = GetDouble(element, "Plan Rows"),
                ActualRows = GetDouble(element, "Actual Rows"),
                Executions = (int)GetDouble(element, "Actual Loops", 1)
            }
        };

        // Parse relation (table)
        if (element.TryGetProperty("Relation Name", out var relName))
        {
            node.Table = new TableReference
            {
                TableName = relName.GetString() ?? string.Empty,
                Schema = GetString(element, "Schema", "public"),
                Alias = GetString(element, "Alias")
            };
        }

        // Parse index
        if (element.TryGetProperty("Index Name", out var idxName))
        {
            node.Index = new IndexReference
            {
                IndexName = idxName.GetString() ?? string.Empty,
                TableName = node.Table?.TableName ?? string.Empty
            };
        }

        // Parse filter/condition
        if (element.TryGetProperty("Filter", out var filter))
        {
            node.Predicate = filter.GetString() ?? string.Empty;
        }
        else if (element.TryGetProperty("Index Cond", out var indexCond))
        {
            node.Predicate = indexCond.GetString() ?? string.Empty;
        }
        else if (element.TryGetProperty("Join Filter", out var joinFilter))
        {
            node.Predicate = joinFilter.GetString() ?? string.Empty;
        }
        else if (element.TryGetProperty("Hash Cond", out var hashCond))
        {
            node.Predicate = hashCond.GetString() ?? string.Empty;
        }

        // Parse output columns
        if (element.TryGetProperty("Output", out var output) &&
            output.ValueKind == JsonValueKind.Array)
        {
            var cols = new List<string>();
            foreach (var col in output.EnumerateArray())
            {
                cols.Add(col.GetString() ?? string.Empty);
            }
            node.OutputColumns = string.Join(", ", cols);
        }

        // Check for rows removed by filter (potential inefficiency)
        var rowsRemoved = GetDouble(element, "Rows Removed by Filter");
        if (rowsRemoved > 0 && node.Cost.ActualRows > 0 &&
            rowsRemoved > node.Cost.ActualRows * 10)
        {
            node.IsWarning = true;
            node.WarningMessage = $"Filter removed {rowsRemoved:N0} rows, only {node.Cost.ActualRows:N0} returned";
        }

        // Parse children (Plans array)
        if (element.TryGetProperty("Plans", out var plans) &&
            plans.ValueKind == JsonValueKind.Array)
        {
            foreach (var childPlan in plans.EnumerateArray())
            {
                node.Children.Add(ParseNode(childPlan, depth + 1, ref nodeId));
            }
        }

        return node;
    }

    private static void ComputeCostPercentages(PlanNode node, double totalCost)
    {
        foreach (var n in node.DescendantsAndSelf())
        {
            // For PostgreSQL, use the node's own cost minus children's cost
            var childrenCost = n.Children.Sum(c => c.Cost.SubtreeCost);
            var ownCost = Math.Max(0, n.Cost.SubtreeCost - childrenCost);
            n.Cost.CostPercentage = totalCost > 0
                ? (ownCost / totalCost) * 100.0
                : 0;
        }
    }

    private static NodeType MapNodeType(string nodeType) => nodeType switch
    {
        "Seq Scan" => NodeType.SeqScan,
        "Index Scan" => NodeType.IndexScan,
        "Index Only Scan" => NodeType.IndexSeek,
        "Bitmap Heap Scan" => NodeType.BitmapHeapScan,
        "Bitmap Index Scan" => NodeType.BitmapIndexScan,
        "CTE Scan" => NodeType.CTEScan,
        "Nested Loop" => NodeType.NestedLoopJoin,
        "Hash Join" => NodeType.HashJoin,
        "Merge Join" => NodeType.MergeJoin,
        "Hash" => NodeType.Hash,
        "Aggregate" => NodeType.HashAggregate,
        "GroupAggregate" => NodeType.StreamAggregate,
        "Sort" => NodeType.Sort,
        "Limit" => NodeType.Limit,
        "Result" => NodeType.Result,
        "Append" => NodeType.Append,
        "Materialize" => NodeType.Materialize,
        "Unique" => NodeType.Unique,
        "SetOp" => NodeType.SetOp,
        "WindowAgg" => NodeType.WindowAgg,
        "Subquery Scan" => NodeType.SubqueryScan,
        _ => NodeType.Compute
    };

    private static string GetString(JsonElement el, string prop, string defaultValue = "")
    {
        return el.TryGetProperty(prop, out var val) ? val.GetString() ?? defaultValue : defaultValue;
    }

    private static double GetDouble(JsonElement el, string prop, double defaultValue = 0)
    {
        if (!el.TryGetProperty(prop, out var val)) return defaultValue;
        return val.ValueKind == JsonValueKind.Number ? val.GetDouble() : defaultValue;
    }
}
