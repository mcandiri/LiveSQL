namespace LiveSQL.Core.Models;

public sealed class PlanNode
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string PhysicalOperator { get; set; } = string.Empty;
    public string LogicalOperator { get; set; } = string.Empty;
    public NodeType NodeType { get; set; }
    public OperationCost Cost { get; set; } = new();
    public TableReference? Table { get; set; }
    public IndexReference? Index { get; set; }
    public string Predicate { get; set; } = string.Empty;
    public string OutputColumns { get; set; } = string.Empty;
    public int Depth { get; set; }
    public bool IsWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    public List<PlanNode> Children { get; set; } = new();

    public bool IsLeaf => Children.Count == 0;

    public IEnumerable<PlanNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    public int TotalNodeCount() => DescendantsAndSelf().Count();
}
