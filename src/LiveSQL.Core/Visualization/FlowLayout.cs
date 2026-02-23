using LiveSQL.Core.Models;

namespace LiveSQL.Core.Visualization;

/// <summary>
/// Simplified Sugiyama-style layered tree layout algorithm.
/// Assigns layers top-to-bottom and positions nodes to minimize edge crossings.
/// </summary>
public sealed class FlowLayout
{
    private const double NodeWidth = 180;
    private const double NodeHeight = 80;
    private const double HorizontalSpacing = 60;
    private const double VerticalSpacing = 100;
    private const double PaddingLeft = 40;
    private const double PaddingTop = 40;

    public FlowLayoutResult ComputeLayout(PlanNode root)
    {
        var result = new FlowLayoutResult();

        // Step 1: Assign layers (BFS from root)
        var layers = AssignLayers(root);

        // Step 2: Order nodes within each layer to minimize crossings
        OrderNodesInLayers(layers);

        // Step 3: Assign X, Y positions
        AssignPositions(layers, result);

        // Step 4: Compute canvas size
        result.CanvasWidth = layers.Values
            .SelectMany(l => l)
            .Max(n => n.X + n.Width) + PaddingLeft * 2;
        result.CanvasHeight = layers.Keys.Max() * (NodeHeight + VerticalSpacing) + NodeHeight + PaddingTop * 2;

        return result;
    }

    private static Dictionary<int, List<LayoutNode>> AssignLayers(PlanNode root)
    {
        var layers = new Dictionary<int, List<LayoutNode>>();
        var queue = new Queue<(PlanNode node, int layer)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (node, layer) = queue.Dequeue();

            if (!layers.ContainsKey(layer))
                layers[layer] = new List<LayoutNode>();

            var layoutNode = new LayoutNode
            {
                PlanNodeId = node.Id,
                Layer = layer,
                PlanNode = node,
                ChildIds = node.Children.Select(c => c.Id).ToList()
            };
            layers[layer].Add(layoutNode);

            foreach (var child in node.Children)
            {
                queue.Enqueue((child, layer + 1));
            }
        }

        return layers;
    }

    private static void OrderNodesInLayers(Dictionary<int, List<LayoutNode>> layers)
    {
        // For each layer after the first, order based on parent positions
        for (int layer = 1; layer <= layers.Keys.Max(); layer++)
        {
            if (!layers.ContainsKey(layer)) continue;
            var prevLayer = layers.GetValueOrDefault(layer - 1);
            if (prevLayer == null) continue;

            foreach (var node in layers[layer])
            {
                // Find parent in previous layer
                var parent = prevLayer.FirstOrDefault(p => p.ChildIds.Contains(node.PlanNodeId));
                node.ParentOrder = parent != null ? prevLayer.IndexOf(parent) : 0;
            }

            layers[layer] = layers[layer].OrderBy(n => n.ParentOrder).ToList();
        }
    }

    private void AssignPositions(Dictionary<int, List<LayoutNode>> layers, FlowLayoutResult result)
    {
        // Find max nodes in any layer to compute centering
        var maxNodesInLayer = layers.Values.Max(l => l.Count);

        foreach (var (layer, nodes) in layers)
        {
            var totalWidth = nodes.Count * NodeWidth + (nodes.Count - 1) * HorizontalSpacing;
            var maxWidth = maxNodesInLayer * NodeWidth + (maxNodesInLayer - 1) * HorizontalSpacing;
            var startX = PaddingLeft + (maxWidth - totalWidth) / 2;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node.X = startX + i * (NodeWidth + HorizontalSpacing);
                node.Y = PaddingTop + layer * (NodeHeight + VerticalSpacing);
                node.Width = NodeWidth;
                node.Height = NodeHeight;

                result.NodePositions[node.PlanNodeId] = new NodePosition
                {
                    X = node.X,
                    Y = node.Y,
                    Width = node.Width,
                    Height = node.Height,
                    Layer = layer,
                    OrderInLayer = i
                };
            }
        }

        // Compute edge connection points
        foreach (var (layer, nodes) in layers)
        {
            foreach (var node in nodes)
            {
                foreach (var childId in node.ChildIds)
                {
                    if (result.NodePositions.TryGetValue(childId, out var childPos))
                    {
                        result.EdgeConnections.Add(new EdgeConnection
                        {
                            SourceId = node.PlanNodeId,
                            TargetId = childId,
                            SourceX = node.X + node.Width / 2,
                            SourceY = node.Y + node.Height,
                            TargetX = childPos.X + childPos.Width / 2,
                            TargetY = childPos.Y
                        });
                    }
                }
            }
        }
    }

    private class LayoutNode
    {
        public int PlanNodeId { get; set; }
        public int Layer { get; set; }
        public PlanNode PlanNode { get; set; } = null!;
        public List<int> ChildIds { get; set; } = new();
        public int ParentOrder { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}

public sealed class FlowLayoutResult
{
    public Dictionary<int, NodePosition> NodePositions { get; set; } = new();
    public List<EdgeConnection> EdgeConnections { get; set; } = new();
    public double CanvasWidth { get; set; }
    public double CanvasHeight { get; set; }
}

public sealed class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int Layer { get; set; }
    public int OrderInLayer { get; set; }
}

public sealed class EdgeConnection
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public double SourceX { get; set; }
    public double SourceY { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }
}
