using LiveSQL.Core.Models;

namespace LiveSQL.Core.Analysis;

public sealed class IndexAdvisor
{
    public List<IndexSuggestion> Suggest(ExecutionPlan plan)
    {
        var suggestions = new List<IndexSuggestion>();

        foreach (var node in plan.AllNodes)
        {
            SuggestForTableScan(node, suggestions);
            SuggestForKeyLookup(node, plan, suggestions);
        }

        return DeduplicateSuggestions(suggestions);
    }

    private static void SuggestForTableScan(PlanNode node, List<IndexSuggestion> suggestions)
    {
        var scanTypes = new[] { NodeType.TableScan, NodeType.SeqScan, NodeType.ClusteredIndexScan };
        if (!scanTypes.Contains(node.NodeType)) return;
        if (node.Table == null) return;

        var rows = Math.Max(node.Cost.EstimatedRows, node.Cost.ActualRows);
        if (rows < 100) return;

        var keyColumns = ExtractFilterColumns(node.Predicate);
        if (keyColumns.Count == 0)
        {
            // If no predicate columns found, suggest based on output columns
            keyColumns = ExtractOutputColumns(node.OutputColumns, maxColumns: 2);
        }

        if (keyColumns.Count == 0) return;

        var includeColumns = ExtractOutputColumns(node.OutputColumns)
            .Where(c => !keyColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var improvement = EstimateImprovement(rows, keyColumns.Count);

        suggestions.Add(new IndexSuggestion
        {
            TableName = node.Table.TableName,
            Schema = node.Table.Schema,
            KeyColumns = keyColumns,
            IncludeColumns = includeColumns.Take(5).ToList(),
            Reason = $"Table Scan on {node.Table.TableName} reading {rows:N0} rows. " +
                     $"An index on ({string.Join(", ", keyColumns)}) would allow an Index Seek.",
            EstimatedImprovement = improvement,
            Impact = improvement >= 90 ? Severity.Critical :
                     improvement >= 50 ? Severity.High :
                     improvement >= 20 ? Severity.Medium : Severity.Low
        });
    }

    private static void SuggestForKeyLookup(PlanNode node, ExecutionPlan plan, List<IndexSuggestion> suggestions)
    {
        if (node.NodeType != NodeType.KeyLookup) return;
        if (node.Table == null) return;

        // Find the related Index Seek node (should be a sibling)
        var seekNode = plan.AllNodes
            .FirstOrDefault(n =>
                (n.NodeType == NodeType.IndexSeek || n.NodeType == NodeType.ClusteredIndexSeek) &&
                n.Table?.TableName == node.Table.TableName);

        var existingIndexColumns = seekNode?.Index?.Columns ?? new List<string>();
        var missingColumns = ExtractOutputColumns(node.OutputColumns)
            .Where(c => !existingIndexColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingColumns.Count == 0) return;

        var indexName = seekNode?.Index?.IndexName ?? "the existing index";
        suggestions.Add(new IndexSuggestion
        {
            TableName = node.Table.TableName,
            Schema = node.Table.Schema,
            KeyColumns = existingIndexColumns.Count > 0 ? existingIndexColumns : new List<string> { "Id" },
            IncludeColumns = missingColumns.Take(5).ToList(),
            Reason = $"Key Lookup on {node.Table.TableName} for {missingColumns.Count} columns " +
                     $"not covered by {indexName}. Adding INCLUDE columns eliminates the lookup.",
            EstimatedImprovement = 40,
            Impact = Severity.Medium
        });
    }

    private static List<string> ExtractFilterColumns(string predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate)) return new List<string>();

        var columns = new List<string>();
        // Extract column names from common predicate patterns like [Table].[Column] = value
        var parts = predicate.Split(new[] { "AND", "OR", "and", "or" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // Pattern: [Schema].[Table].[Column] or Table.Column or just Column
            var segments = trimmed.Split(new[] { '=', '>', '<', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                var clean = segment.Trim().Trim('[', ']', '(', ')').Trim();
                if (clean.Contains('.'))
                {
                    var colParts = clean.Split('.');
                    var col = colParts.Last().Trim('[', ']');
                    if (!string.IsNullOrEmpty(col) && !IsLiteral(col) && !IsKeyword(col))
                    {
                        columns.Add(col);
                        break; // Only take column name from each predicate part
                    }
                }
            }
        }

        return columns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractOutputColumns(string outputColumns, int maxColumns = 10)
    {
        if (string.IsNullOrWhiteSpace(outputColumns)) return new List<string>();

        return outputColumns
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c =>
            {
                var trimmed = c.Trim().Trim('[', ']');
                return trimmed.Contains('.') ? trimmed.Split('.').Last().Trim('[', ']') : trimmed;
            })
            .Where(c => !string.IsNullOrEmpty(c) && !IsLiteral(c) && !IsKeyword(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxColumns)
            .ToList();
    }

    private static double EstimateImprovement(double rowCount, int keyColumnCount)
    {
        // Rough estimate: index seek selectivity improvement
        if (rowCount <= 100) return 20;
        if (rowCount <= 1000) return 50 + keyColumnCount * 5;
        if (rowCount <= 10_000) return 80 + keyColumnCount * 2;
        return 95 + Math.Min(keyColumnCount, 4);
    }

    private static bool IsLiteral(string value)
    {
        return int.TryParse(value, out _) ||
               double.TryParse(value, out _) ||
               value.StartsWith("'") ||
               value.StartsWith("N'") ||
               value.Equals("NULL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKeyword(string value)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LIKE", "IN", "NOT", "IS", "BETWEEN", "EXISTS", "ANY", "ALL",
            "ASC", "DESC", "SELECT", "FROM", "WHERE", "JOIN", "ON"
        };
        return keywords.Contains(value);
    }

    private static List<IndexSuggestion> DeduplicateSuggestions(List<IndexSuggestion> suggestions)
    {
        var unique = new List<IndexSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var suggestion in suggestions.OrderByDescending(s => s.EstimatedImprovement))
        {
            var key = $"{suggestion.TableName}:{string.Join(",", suggestion.KeyColumns)}";
            if (seen.Add(key))
            {
                unique.Add(suggestion);
            }
        }

        return unique;
    }
}
