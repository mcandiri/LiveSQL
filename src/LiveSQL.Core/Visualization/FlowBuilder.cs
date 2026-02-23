using LiveSQL.Core.Models;

namespace LiveSQL.Core.Visualization;

public sealed class FlowBuilder
{
    private readonly FlowLayout _layout;
    private readonly ColorMapper _colorMapper;

    public FlowBuilder(FlowLayout layout, ColorMapper colorMapper)
    {
        _layout = layout;
        _colorMapper = colorMapper;
    }

    public FlowData Build(ExecutionPlan plan)
    {
        var layoutResult = _layout.ComputeLayout(plan.RootNode);
        var flowData = new FlowData
        {
            CanvasWidth = layoutResult.CanvasWidth,
            CanvasHeight = layoutResult.CanvasHeight
        };

        // Build flow nodes
        int animationOrder = 0;
        foreach (var node in plan.AllNodes)
        {
            if (!layoutResult.NodePositions.TryGetValue(node.Id, out var pos))
                continue;

            var flowNode = new FlowNode
            {
                Id = node.Id,
                Label = node.Label,
                Subtitle = BuildSubtitle(node),
                X = pos.X,
                Y = pos.Y,
                Width = pos.Width,
                Height = pos.Height,
                Color = _colorMapper.MapCostToColor(node.Cost.CostPercentage),
                BorderColor = _colorMapper.MapNodeTypeToBorderColor(node.NodeType),
                CostPercentage = node.Cost.CostPercentage,
                IsWarning = node.IsWarning,
                WarningMessage = node.WarningMessage,
                AnimationDelay = animationOrder * 0.15,
                AnimationDuration = 0.5,
                Icon = _colorMapper.MapNodeTypeToIcon(node.NodeType),
                NodeType = node.NodeType,
                EstimatedRows = node.Cost.EstimatedRows,
                ActualRows = node.Cost.ActualRows,
                Predicate = node.Predicate,
                TableName = node.Table?.TableName ?? string.Empty,
                IndexName = node.Index?.IndexName ?? string.Empty
            };

            flowData.Nodes.Add(flowNode);
            animationOrder++;
        }

        // Build flow edges
        foreach (var edge in layoutResult.EdgeConnections)
        {
            var sourceNode = plan.AllNodes.FirstOrDefault(n => n.Id == edge.SourceId);
            var targetNode = plan.AllNodes.FirstOrDefault(n => n.Id == edge.TargetId);

            var rows = targetNode?.Cost.ActualRows > 0
                ? targetNode.Cost.ActualRows
                : targetNode?.Cost.EstimatedRows ?? 0;

            var flowEdge = new FlowEdge
            {
                SourceId = edge.SourceId,
                TargetId = edge.TargetId,
                RowCount = rows,
                Label = FormatRowCount(rows),
                Thickness = _colorMapper.MapRowCountToEdgeThickness(rows),
                SourceX = edge.SourceX,
                SourceY = edge.SourceY,
                TargetX = edge.TargetX,
                TargetY = edge.TargetY,
                AnimationDelay = (animationOrder + flowData.Edges.Count) * 0.1,
                Color = sourceNode?.IsWarning == true ? ColorMapper.Orange : ColorMapper.NeutralGray
            };

            flowData.Edges.Add(flowEdge);
        }

        return flowData;
    }

    private static string BuildSubtitle(PlanNode node)
    {
        var parts = new List<string>();

        if (node.Table != null && !string.IsNullOrEmpty(node.Table.TableName))
            parts.Add(node.Table.TableName);

        if (node.Index != null && !string.IsNullOrEmpty(node.Index.IndexName))
            parts.Add(node.Index.IndexName);

        if (node.Cost.ActualRows > 0)
            parts.Add($"{FormatRowCount(node.Cost.ActualRows)} rows");
        else if (node.Cost.EstimatedRows > 0)
            parts.Add($"~{FormatRowCount(node.Cost.EstimatedRows)} rows");

        parts.Add($"{node.Cost.CostPercentage:F1}%");

        return string.Join(" | ", parts);
    }

    private static string FormatRowCount(double rows) => rows switch
    {
        >= 1_000_000 => $"{rows / 1_000_000:F1}M",
        >= 1_000 => $"{rows / 1_000:F1}K",
        _ => $"{rows:F0}"
    };
}

public sealed class FlowData
{
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }
}
