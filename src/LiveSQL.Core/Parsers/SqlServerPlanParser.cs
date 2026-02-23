using System.Xml.Linq;
using LiveSQL.Core.Models;

namespace LiveSQL.Core.Parsers;

public sealed class SqlServerPlanParser : IPlanParser
{
    private static readonly XNamespace Ns =
        "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    public string EngineType => "SQL Server";

    public bool CanParse(string rawPlan)
    {
        return rawPlan.TrimStart().StartsWith("<") &&
               rawPlan.Contains("schemas.microsoft.com/sqlserver/2004/07/showplan");
    }

    public Task<ExecutionPlan> ParseAsync(string rawPlan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var doc = XDocument.Parse(rawPlan);
        var plan = new ExecutionPlan
        {
            DatabaseEngine = EngineType,
            RawPlan = rawPlan
        };

        var stmtSimple = doc.Descendants(Ns + "StmtSimple").FirstOrDefault();
        if (stmtSimple != null)
        {
            plan.QueryText = stmtSimple.Attribute("StatementText")?.Value ?? string.Empty;
            plan.Metrics.TotalCost = ParseDouble(stmtSimple.Attribute("StatementSubTreeCost")?.Value);
            plan.Metrics.RowsAffected = ParseLong(stmtSimple.Attribute("StatementEstRows")?.Value);
        }

        var relOp = stmtSimple?.Descendants(Ns + "RelOp").FirstOrDefault();
        if (relOp != null)
        {
            int nodeId = 0;
            plan.RootNode = ParseRelOp(relOp, 0, ref nodeId);
            ComputeCostPercentages(plan.RootNode, plan.Metrics.TotalCost);
        }

        plan.Metrics.TotalOperators = plan.TotalNodes;

        return Task.FromResult(plan);
    }

    private PlanNode ParseRelOp(XElement relOp, int depth, ref int nodeId)
    {
        var physOp = relOp.Attribute("PhysicalOp")?.Value ?? "Unknown";
        var logOp = relOp.Attribute("LogicalOp")?.Value ?? "Unknown";

        var node = new PlanNode
        {
            Id = nodeId++,
            PhysicalOperator = physOp,
            LogicalOperator = logOp,
            Label = physOp,
            NodeType = MapNodeType(physOp),
            Depth = depth,
            Cost = new OperationCost
            {
                EstimatedRows = ParseDouble(relOp.Attribute("EstimateRows")?.Value),
                ActualRows = ParseDouble(relOp.Attribute("ActualRows")?.Value),
                CpuCost = ParseDouble(relOp.Attribute("EstimateCPU")?.Value),
                IoCost = ParseDouble(relOp.Attribute("EstimateIO")?.Value),
                TotalCost = ParseDouble(relOp.Attribute("EstimateCPU")?.Value) +
                            ParseDouble(relOp.Attribute("EstimateIO")?.Value),
                SubtreeCost = ParseDouble(relOp.Attribute("EstimatedTotalSubtreeCost")?.Value),
                Executions = (int)ParseDouble(relOp.Attribute("EstimateExecutions")?.Value, 1)
            }
        };

        // Parse object references (table/index)
        var objRef = relOp.Descendants(Ns + "Object").FirstOrDefault();
        if (objRef != null)
        {
            node.Table = new TableReference
            {
                Schema = objRef.Attribute("Schema")?.Value ?? "dbo",
                TableName = objRef.Attribute("Table")?.Value?.Trim('[', ']') ?? string.Empty,
                Alias = objRef.Attribute("Alias")?.Value?.Trim('[', ']') ?? string.Empty
            };
            node.Index = new IndexReference
            {
                IndexName = objRef.Attribute("Index")?.Value?.Trim('[', ']') ?? string.Empty,
                TableName = node.Table.TableName
            };
        }

        // Parse predicates
        var seekPredicate = relOp.Descendants(Ns + "ScalarOperator").FirstOrDefault();
        if (seekPredicate != null)
        {
            node.Predicate = seekPredicate.Attribute("ScalarString")?.Value ?? string.Empty;
        }

        // Parse output columns
        var outputList = relOp.Element(Ns + "OutputList");
        if (outputList != null)
        {
            var columns = outputList.Elements(Ns + "ColumnReference")
                .Select(c =>
                {
                    var col = c.Attribute("Column")?.Value ?? string.Empty;
                    var table = c.Attribute("Table")?.Value?.Trim('[', ']') ?? string.Empty;
                    return string.IsNullOrEmpty(table) ? col : $"{table}.{col}";
                })
                .ToList();
            node.OutputColumns = string.Join(", ", columns);
        }

        // Parse warnings
        var warnings = relOp.Descendants(Ns + "Warnings").FirstOrDefault();
        if (warnings != null)
        {
            node.IsWarning = true;
            var noJoinPred = warnings.Element(Ns + "NoJoinPredicate");
            if (noJoinPred != null)
                node.WarningMessage = "No join predicate";

            var spillToTempDb = warnings.Element(Ns + "SpillToTempDb");
            if (spillToTempDb != null)
                node.WarningMessage = "Spill to TempDb detected";
        }

        // Parse missing index hints
        var missingIndex = relOp.Descendants(Ns + "MissingIndexGroup").FirstOrDefault();
        if (missingIndex != null)
        {
            node.IsWarning = true;
            node.WarningMessage = "Missing index detected";
        }

        // Recursively parse child RelOps
        foreach (var childRelOp in relOp.Elements().Descendants(Ns + "RelOp"))
        {
            // Only parse direct child RelOps (not nested grandchildren)
            if (childRelOp.Ancestors(Ns + "RelOp").First() == relOp)
            {
                node.Children.Add(ParseRelOp(childRelOp, depth + 1, ref nodeId));
            }
        }

        return node;
    }

    private static void ComputeCostPercentages(PlanNode node, double totalCost)
    {
        foreach (var n in node.DescendantsAndSelf())
        {
            n.Cost.CostPercentage = totalCost > 0
                ? (n.Cost.TotalCost / totalCost) * 100.0
                : 0;
        }
    }

    private static NodeType MapNodeType(string physicalOp) => physicalOp switch
    {
        "Table Scan" => NodeType.TableScan,
        "Clustered Index Scan" => NodeType.ClusteredIndexScan,
        "Index Scan" => NodeType.IndexScan,
        "Index Seek" => NodeType.IndexSeek,
        "Clustered Index Seek" => NodeType.ClusteredIndexSeek,
        "Key Lookup" => NodeType.KeyLookup,
        "Nested Loops" => NodeType.NestedLoopJoin,
        "Hash Match" when true => NodeType.HashJoin,
        "Merge Join" => NodeType.MergeJoin,
        "Stream Aggregate" => NodeType.StreamAggregate,
        "Hash Aggregate" => NodeType.HashAggregate,
        "Sort" => NodeType.Sort,
        "Filter" => NodeType.Filter,
        "Top" => NodeType.TopN,
        "Distinct Sort" => NodeType.Distinct,
        "Compute Scalar" => NodeType.Compute,
        "Insert" => NodeType.Insert,
        "Update" => NodeType.Update,
        "Delete" => NodeType.Delete,
        _ => NodeType.Compute
    };

    private static double ParseDouble(string? value, double defaultValue = 0) =>
        double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;

    private static long ParseLong(string? value) =>
        long.TryParse(value, out var result) ? result : 0;
}
