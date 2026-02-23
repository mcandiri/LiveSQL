namespace LiveSQL.Web.Services;

/// <summary>
/// Provides demo execution plans for the Demo page.
/// Falls back to built-in data if LiveSQL.Core.Demo is not yet available.
/// </summary>
public sealed class DemoDataService
{
    public List<DemoPlan> GetDemoPlans()
    {
        return new List<DemoPlan>
        {
            new DemoPlan
            {
                Id = "simple-select",
                Title = "Simple SELECT",
                Description = "Basic table scan with filter predicate. Shows how SQL Server reads an entire table when no useful index exists.",
                Sql = "SELECT * FROM Orders\nWHERE OrderDate > '2024-01-01'\nORDER BY OrderDate DESC",
                Icon = "search",
                Category = "Scan",
                Metrics = new DemoPlanMetrics { TotalCost = 2.45, ElapsedMs = 45, RowsReturned = 1250, OperatorCount = 3 }
            },
            new DemoPlan
            {
                Id = "index-seek",
                Title = "Index Seek + Lookup",
                Description = "Efficient index seek followed by key lookup. Demonstrates the cost of bookmark lookups when the index does not cover all columns.",
                Sql = "SELECT o.OrderId, o.Total, c.Name\nFROM Orders o\nJOIN Customers c ON o.CustomerId = c.Id\nWHERE o.OrderId = 42",
                Icon = "target",
                Category = "Seek",
                Metrics = new DemoPlanMetrics { TotalCost = 0.32, ElapsedMs = 3, RowsReturned = 1, OperatorCount = 5 }
            },
            new DemoPlan
            {
                Id = "hash-join",
                Title = "Hash Join Aggregation",
                Description = "Hash match join with stream aggregate. Common pattern for GROUP BY queries joining multiple tables.",
                Sql = "SELECT c.Country, COUNT(*) AS OrderCount,\n       SUM(o.Total) AS Revenue\nFROM Orders o\nJOIN Customers c ON o.CustomerId = c.Id\nGROUP BY c.Country\nORDER BY Revenue DESC",
                Icon = "layers",
                Category = "Join",
                Metrics = new DemoPlanMetrics { TotalCost = 8.72, ElapsedMs = 120, RowsReturned = 45, OperatorCount = 7 }
            },
            new DemoPlan
            {
                Id = "nested-loop",
                Title = "Nested Loop with Sort",
                Description = "Nested loops join with explicit sort operator. Watch for the sort spill warning when memory grant is insufficient.",
                Sql = "SELECT TOP 100 p.Name, p.Price,\n       c.Name AS Category\nFROM Products p\nJOIN Categories c ON p.CategoryId = c.Id\nWHERE p.Price > 50\nORDER BY p.Price DESC",
                Icon = "repeat",
                Category = "Join",
                Metrics = new DemoPlanMetrics { TotalCost = 4.15, ElapsedMs = 67, RowsReturned = 100, OperatorCount = 6 }
            },
            new DemoPlan
            {
                Id = "subquery-scan",
                Title = "Correlated Subquery",
                Description = "Expensive correlated subquery resulting in a table scan per outer row. A classic N+1 bottleneck pattern.",
                Sql = "SELECT c.Name,\n  (SELECT COUNT(*)\n   FROM Orders o\n   WHERE o.CustomerId = c.Id) AS OrderCount\nFROM Customers c\nWHERE c.IsActive = 1",
                Icon = "alert-triangle",
                Category = "Anti-Pattern",
                Metrics = new DemoPlanMetrics { TotalCost = 24.80, ElapsedMs = 340, RowsReturned = 500, OperatorCount = 8 }
            },
            new DemoPlan
            {
                Id = "merge-join",
                Title = "Merge Join with CTE",
                Description = "Merge join utilizing sorted inputs from index scans. Shows efficient plan when both sides are pre-sorted on the join key.",
                Sql = "WITH TopCustomers AS (\n  SELECT TOP 1000 Id, Name\n  FROM Customers\n  ORDER BY Id\n)\nSELECT tc.Name, o.OrderDate, o.Total\nFROM TopCustomers tc\nJOIN Orders o ON tc.Id = o.CustomerId",
                Icon = "git-merge",
                Category = "Join",
                Metrics = new DemoPlanMetrics { TotalCost = 5.60, ElapsedMs = 89, RowsReturned = 3200, OperatorCount = 7 }
            }
        };
    }

    public FlowDiagramData BuildFlowDiagram(string demoPlanId)
    {
        return demoPlanId switch
        {
            "simple-select" => BuildSimpleSelectDiagram(),
            "index-seek" => BuildIndexSeekDiagram(),
            "hash-join" => BuildHashJoinDiagram(),
            "nested-loop" => BuildNestedLoopDiagram(),
            "subquery-scan" => BuildSubqueryScanDiagram(),
            "merge-join" => BuildMergeJoinDiagram(),
            _ => BuildSimpleSelectDiagram()
        };
    }

    public (List<BottleneckAlert> Bottlenecks, List<IndexSuggestionData> Suggestions) GetAnalysis(string demoPlanId)
    {
        return demoPlanId switch
        {
            "simple-select" => (
                new List<BottleneckAlert>
                {
                    new() { Title = "Table Scan Detected", Description = "Full table scan on Orders table (estimated 50,000 rows). Consider adding an index on OrderDate.", Severity = "High", ImpactPercentage = 85, Recommendation = "Create a nonclustered index on OrderDate column.", RelatedNodeId = 2 }
                },
                new List<IndexSuggestionData>
                {
                    new() { TableName = "Orders", Columns = new() { "OrderDate" }, IncludedColumns = new() { "OrderId", "Total", "CustomerId" }, Reason = "Eliminates table scan for date range filter", EstimatedImprovement = 75 }
                }
            ),
            "index-seek" => (
                new List<BottleneckAlert>
                {
                    new() { Title = "Key Lookup Overhead", Description = "Key lookup accounts for 40% of plan cost. The nonclustered index does not cover all selected columns.", Severity = "Medium", ImpactPercentage = 40, Recommendation = "Add Total to the index as an included column.", RelatedNodeId = 3 }
                },
                new List<IndexSuggestionData>
                {
                    new() { TableName = "Orders", Columns = new() { "OrderId" }, IncludedColumns = new() { "Total", "CustomerId" }, Reason = "Covers all columns to eliminate key lookup", EstimatedImprovement = 35 }
                }
            ),
            "subquery-scan" => (
                new List<BottleneckAlert>
                {
                    new() { Title = "Correlated Subquery N+1", Description = "Table scan executes 500 times (once per outer row). Total cost: 24.80.", Severity = "Critical", ImpactPercentage = 92, Recommendation = "Rewrite as a LEFT JOIN with GROUP BY to eliminate per-row execution.", RelatedNodeId = 5 },
                    new() { Title = "Missing Index on CustomerId", Description = "Orders table lacks an index on CustomerId, forcing repeated table scans.", Severity = "High", ImpactPercentage = 65, Recommendation = "Create an index on Orders(CustomerId).", RelatedNodeId = 6 }
                },
                new List<IndexSuggestionData>
                {
                    new() { TableName = "Orders", Columns = new() { "CustomerId" }, Reason = "Supports efficient lookup for the subquery join condition", EstimatedImprovement = 80 }
                }
            ),
            "hash-join" => (
                new List<BottleneckAlert>
                {
                    new() { Title = "Clustered Index Scan on Orders", Description = "Full clustered index scan reading 50,000 rows. This accounts for 40% of total plan cost.", Severity = "High", ImpactPercentage = 40, Recommendation = "Add a nonclustered index on Orders(CustomerId) to enable a seek instead of a scan.", RelatedNodeId = 6 }
                },
                new List<IndexSuggestionData>
                {
                    new() { TableName = "Orders", Columns = new() { "CustomerId" }, IncludedColumns = new() { "Total" }, Reason = "Supports hash join build input with a seek instead of full scan", EstimatedImprovement = 45 }
                }
            ),
            "nested-loop" => (
                new List<BottleneckAlert>
                {
                    new() { Title = "Sort Spill to Tempdb", Description = "Sort operator spilled to tempdb due to insufficient memory grant. 350 rows estimated but actual memory pressure caused spill.", Severity = "Medium", ImpactPercentage = 20, Recommendation = "Consider adding an index on Products(Price DESC) to avoid explicit sort.", RelatedNodeId = 3 },
                    new() { Title = "Index Scan on Products", Description = "Index scan reads all rows before filtering. 38% of plan cost.", Severity = "High", ImpactPercentage = 38, Recommendation = "Create a filtered index on Products(Price) WHERE Price > 50 or a covering index.", RelatedNodeId = 5 }
                },
                new List<IndexSuggestionData>
                {
                    new() { TableName = "Products", Columns = new() { "Price", "CategoryId" }, IncludedColumns = new() { "Name" }, Reason = "Covers the WHERE and JOIN predicates to eliminate scan and sort", EstimatedImprovement = 55 }
                }
            ),
            "merge-join" => (
                new List<BottleneckAlert>
                {
                    new() { Title = "Large Index Scan on Orders", Description = "Index scan reads 50,000 rows from the Orders table. Accounts for 42% of plan cost.", Severity = "High", ImpactPercentage = 42, Recommendation = "Verify the index IX_Orders_CustomerId covers the needed columns to reduce I/O.", RelatedNodeId = 5 }
                },
                new List<IndexSuggestionData>
                {
                    new() { TableName = "Orders", Columns = new() { "CustomerId" }, IncludedColumns = new() { "OrderDate", "Total" }, Reason = "Covering index eliminates key lookups on Orders table", EstimatedImprovement = 30 }
                }
            ),
            _ => (new List<BottleneckAlert>(), new List<IndexSuggestionData>())
        };
    }

    private FlowDiagramData BuildSimpleSelectDiagram()
    {
        var nodes = new List<FlowNode>
        {
            new() { Id = 1, Label = "SELECT", Operator = "Select", NodeTypeName = "Result", CostPercentage = 0, EstimatedRows = 1250, ActualRows = 1250, TotalCost = 0, X = 400, Y = 50, Color = "#58a6ff", Icon = "result", Depth = 0, ChildIds = new() { 2 } },
            new() { Id = 2, Label = "Sort", Operator = "Sort", NodeTypeName = "Sort", CostPercentage = 15, EstimatedRows = 1250, ActualRows = 1250, TotalCost = 0.37, X = 400, Y = 200, Color = "#d29922", Icon = "sort", Depth = 1, ChildIds = new() { 3 } },
            new() { Id = 3, Label = "Table Scan", Operator = "Table Scan", NodeTypeName = "TableScan", CostPercentage = 85, EstimatedRows = 50000, ActualRows = 50000, TotalCost = 2.08, X = 400, Y = 350, Color = "#f85149", Icon = "scan", IsWarning = true, WarningMessage = "Full table scan - consider adding an index", TableName = "Orders", Predicate = "OrderDate > '2024-01-01'", Depth = 2 }
        };

        var edges = new List<FlowEdge>
        {
            new() { SourceId = 2, TargetId = 1, Rows = 1250, SourceX = 490, SourceY = 200, TargetX = 490, TargetY = 120 },
            new() { SourceId = 3, TargetId = 2, Rows = 50000, SourceX = 490, SourceY = 350, TargetX = 490, TargetY = 270 }
        };

        return new FlowDiagramData { Nodes = nodes, Edges = edges, ViewBoxWidth = 1000, ViewBoxHeight = 500 };
    }

    private FlowDiagramData BuildIndexSeekDiagram()
    {
        var nodes = new List<FlowNode>
        {
            new() { Id = 1, Label = "SELECT", Operator = "Select", NodeTypeName = "Result", CostPercentage = 0, EstimatedRows = 1, ActualRows = 1, TotalCost = 0, X = 400, Y = 50, Color = "#58a6ff", Icon = "result", Depth = 0, ChildIds = new() { 2 } },
            new() { Id = 2, Label = "Nested Loops", Operator = "Nested Loops", NodeTypeName = "NestedLoopJoin", CostPercentage = 5, EstimatedRows = 1, ActualRows = 1, TotalCost = 0.02, X = 400, Y = 180, Color = "#3fb950", Icon = "join", Depth = 1, ChildIds = new() { 3, 4 } },
            new() { Id = 3, Label = "Clustered Index Seek", Operator = "Clustered Index Seek", NodeTypeName = "ClusteredIndexSeek", CostPercentage = 10, EstimatedRows = 1, ActualRows = 1, TotalCost = 0.03, X = 250, Y = 320, Color = "#3fb950", Icon = "seek", IndexName = "PK_Customers", TableName = "Customers", Depth = 2 },
            new() { Id = 4, Label = "Key Lookup", Operator = "Key Lookup", NodeTypeName = "KeyLookup", CostPercentage = 40, EstimatedRows = 1, ActualRows = 1, TotalCost = 0.13, X = 550, Y = 320, Color = "#d29922", Icon = "lookup", IsWarning = true, WarningMessage = "Key lookup - index does not cover all columns", TableName = "Orders", Depth = 2, ChildIds = new() { 5 } },
            new() { Id = 5, Label = "Index Seek", Operator = "Index Seek", NodeTypeName = "IndexSeek", CostPercentage = 45, EstimatedRows = 1, ActualRows = 1, TotalCost = 0.14, X = 550, Y = 460, Color = "#3fb950", Icon = "seek", IndexName = "IX_Orders_OrderId", TableName = "Orders", Predicate = "OrderId = 42", Depth = 3 }
        };

        var edges = new List<FlowEdge>
        {
            new() { SourceId = 2, TargetId = 1, Rows = 1, SourceX = 490, SourceY = 180, TargetX = 490, TargetY = 120 },
            new() { SourceId = 3, TargetId = 2, Rows = 1, SourceX = 340, SourceY = 320, TargetX = 400, TargetY = 250 },
            new() { SourceId = 4, TargetId = 2, Rows = 1, SourceX = 550, SourceY = 320, TargetX = 490, TargetY = 250 },
            new() { SourceId = 5, TargetId = 4, Rows = 1, SourceX = 640, SourceY = 460, TargetX = 640, TargetY = 390 }
        };

        return new FlowDiagramData { Nodes = nodes, Edges = edges, ViewBoxWidth = 1000, ViewBoxHeight = 560 };
    }

    private FlowDiagramData BuildHashJoinDiagram()
    {
        var nodes = new List<FlowNode>
        {
            new() { Id = 1, Label = "SELECT", Operator = "Select", NodeTypeName = "Result", CostPercentage = 0, EstimatedRows = 45, ActualRows = 45, TotalCost = 0, X = 400, Y = 50, Color = "#58a6ff", Icon = "result", Depth = 0, ChildIds = new() { 2 } },
            new() { Id = 2, Label = "Sort", Operator = "Sort", NodeTypeName = "Sort", CostPercentage = 5, EstimatedRows = 45, ActualRows = 45, TotalCost = 0.44, X = 400, Y = 170, Color = "#3fb950", Icon = "sort", Depth = 1, ChildIds = new() { 3 } },
            new() { Id = 3, Label = "Stream Aggregate", Operator = "Stream Aggregate", NodeTypeName = "StreamAggregate", CostPercentage = 8, EstimatedRows = 45, ActualRows = 45, TotalCost = 0.70, X = 400, Y = 290, Color = "#3fb950", Icon = "aggregate", Depth = 2, ChildIds = new() { 4 } },
            new() { Id = 4, Label = "Hash Match", Operator = "Hash Match", NodeTypeName = "HashJoin", CostPercentage = 35, EstimatedRows = 10000, ActualRows = 10000, TotalCost = 3.05, X = 400, Y = 410, Color = "#d29922", Icon = "join", Depth = 3, ChildIds = new() { 5, 6 } },
            new() { Id = 5, Label = "Table Scan", Operator = "Table Scan", NodeTypeName = "TableScan", CostPercentage = 12, EstimatedRows = 500, ActualRows = 500, TotalCost = 1.05, X = 230, Y = 540, Color = "#db6d28", Icon = "scan", TableName = "Customers", Depth = 4 },
            new() { Id = 6, Label = "Clustered Index Scan", Operator = "Clustered Index Scan", NodeTypeName = "ClusteredIndexScan", CostPercentage = 40, EstimatedRows = 50000, ActualRows = 50000, TotalCost = 3.48, X = 570, Y = 540, Color = "#f85149", Icon = "scan", TableName = "Orders", IndexName = "PK_Orders", Depth = 4 },
        };
        var edges = new List<FlowEdge>
        {
            new() { SourceId = 2, TargetId = 1, Rows = 45, SourceX = 490, SourceY = 170, TargetX = 490, TargetY = 120 },
            new() { SourceId = 3, TargetId = 2, Rows = 45, SourceX = 490, SourceY = 290, TargetX = 490, TargetY = 240 },
            new() { SourceId = 4, TargetId = 3, Rows = 10000, SourceX = 490, SourceY = 410, TargetX = 490, TargetY = 360 },
            new() { SourceId = 5, TargetId = 4, Rows = 500, SourceX = 320, SourceY = 540, TargetX = 400, TargetY = 480 },
            new() { SourceId = 6, TargetId = 4, Rows = 50000, SourceX = 570, SourceY = 540, TargetX = 490, TargetY = 480 }
        };
        return new FlowDiagramData { Nodes = nodes, Edges = edges, ViewBoxWidth = 1000, ViewBoxHeight = 650 };
    }

    private FlowDiagramData BuildNestedLoopDiagram()
    {
        var nodes = new List<FlowNode>
        {
            new() { Id = 1, Label = "SELECT", Operator = "Select", NodeTypeName = "Result", CostPercentage = 0, EstimatedRows = 100, ActualRows = 100, TotalCost = 0, X = 400, Y = 50, Color = "#58a6ff", Icon = "result", Depth = 0, ChildIds = new() { 2 } },
            new() { Id = 2, Label = "Top", Operator = "Top", NodeTypeName = "TopN", CostPercentage = 2, EstimatedRows = 100, ActualRows = 100, TotalCost = 0.08, X = 400, Y = 170, Color = "#3fb950", Icon = "top", Depth = 1, ChildIds = new() { 3 } },
            new() { Id = 3, Label = "Sort", Operator = "Sort", NodeTypeName = "Sort", CostPercentage = 20, EstimatedRows = 350, ActualRows = 350, TotalCost = 0.83, X = 400, Y = 290, Color = "#d29922", Icon = "sort", IsWarning = true, WarningMessage = "Sort spill to tempdb", Depth = 2, ChildIds = new() { 4 } },
            new() { Id = 4, Label = "Nested Loops", Operator = "Nested Loops", NodeTypeName = "NestedLoopJoin", CostPercentage = 30, EstimatedRows = 350, ActualRows = 350, TotalCost = 1.25, X = 400, Y = 410, Color = "#db6d28", Icon = "join", Depth = 3, ChildIds = new() { 5, 6 } },
            new() { Id = 5, Label = "Index Scan", Operator = "Index Scan", NodeTypeName = "IndexScan", CostPercentage = 38, EstimatedRows = 350, ActualRows = 350, TotalCost = 1.58, X = 250, Y = 540, Color = "#f85149", Icon = "scan", TableName = "Products", Predicate = "Price > 50", Depth = 4 },
            new() { Id = 6, Label = "Clustered Index Seek", Operator = "Clustered Index Seek", NodeTypeName = "ClusteredIndexSeek", CostPercentage = 10, EstimatedRows = 1, ActualRows = 1, TotalCost = 0.41, X = 550, Y = 540, Color = "#3fb950", Icon = "seek", TableName = "Categories", IndexName = "PK_Categories", Depth = 4 }
        };
        var edges = new List<FlowEdge>
        {
            new() { SourceId = 2, TargetId = 1, Rows = 100, SourceX = 490, SourceY = 170, TargetX = 490, TargetY = 120 },
            new() { SourceId = 3, TargetId = 2, Rows = 100, SourceX = 490, SourceY = 290, TargetX = 490, TargetY = 240 },
            new() { SourceId = 4, TargetId = 3, Rows = 350, SourceX = 490, SourceY = 410, TargetX = 490, TargetY = 360 },
            new() { SourceId = 5, TargetId = 4, Rows = 350, SourceX = 340, SourceY = 540, TargetX = 400, TargetY = 480 },
            new() { SourceId = 6, TargetId = 4, Rows = 1, SourceX = 550, SourceY = 540, TargetX = 490, TargetY = 480 }
        };
        return new FlowDiagramData { Nodes = nodes, Edges = edges, ViewBoxWidth = 1000, ViewBoxHeight = 650 };
    }

    private FlowDiagramData BuildSubqueryScanDiagram()
    {
        var nodes = new List<FlowNode>
        {
            new() { Id = 1, Label = "SELECT", Operator = "Select", NodeTypeName = "Result", CostPercentage = 0, EstimatedRows = 500, ActualRows = 500, TotalCost = 0, X = 400, Y = 50, Color = "#58a6ff", Icon = "result", Depth = 0, ChildIds = new() { 2 } },
            new() { Id = 2, Label = "Compute Scalar", Operator = "Compute Scalar", NodeTypeName = "Compute", CostPercentage = 2, EstimatedRows = 500, ActualRows = 500, TotalCost = 0.50, X = 400, Y = 170, Color = "#3fb950", Icon = "compute", Depth = 1, ChildIds = new() { 3, 4 } },
            new() { Id = 3, Label = "Clustered Index Scan", Operator = "Clustered Index Scan", NodeTypeName = "ClusteredIndexScan", CostPercentage = 6, EstimatedRows = 500, ActualRows = 500, TotalCost = 1.49, X = 200, Y = 310, Color = "#3fb950", Icon = "scan", TableName = "Customers", Predicate = "IsActive = 1", Depth = 2 },
            new() { Id = 4, Label = "Stream Aggregate", Operator = "Stream Aggregate", NodeTypeName = "StreamAggregate", CostPercentage = 5, EstimatedRows = 1, ActualRows = 1, TotalCost = 1.24, X = 600, Y = 310, Color = "#d29922", Icon = "aggregate", Depth = 2, ChildIds = new() { 5 } },
            new() { Id = 5, Label = "Nested Loops", Operator = "Nested Loops", NodeTypeName = "NestedLoopJoin", CostPercentage = 15, EstimatedRows = 20, ActualRows = 20, TotalCost = 3.72, X = 600, Y = 440, Color = "#db6d28", Icon = "join", IsWarning = true, WarningMessage = "Executes 500 times (once per outer row)", Depth = 3, ChildIds = new() { 6 } },
            new() { Id = 6, Label = "Table Scan", Operator = "Table Scan", NodeTypeName = "TableScan", CostPercentage = 72, EstimatedRows = 50000, ActualRows = 50000, TotalCost = 17.85, X = 600, Y = 570, Color = "#f85149", Icon = "scan", IsWarning = true, WarningMessage = "Full table scan executed 500 times!", TableName = "Orders", Predicate = "CustomerId = [outer].Id", Depth = 4 }
        };
        var edges = new List<FlowEdge>
        {
            new() { SourceId = 2, TargetId = 1, Rows = 500, SourceX = 490, SourceY = 170, TargetX = 490, TargetY = 120 },
            new() { SourceId = 3, TargetId = 2, Rows = 500, SourceX = 290, SourceY = 310, TargetX = 400, TargetY = 240 },
            new() { SourceId = 4, TargetId = 2, Rows = 1, SourceX = 600, SourceY = 310, TargetX = 490, TargetY = 240 },
            new() { SourceId = 5, TargetId = 4, Rows = 20, SourceX = 690, SourceY = 440, TargetX = 690, TargetY = 380 },
            new() { SourceId = 6, TargetId = 5, Rows = 50000, SourceX = 690, SourceY = 570, TargetX = 690, TargetY = 510 }
        };
        return new FlowDiagramData { Nodes = nodes, Edges = edges, ViewBoxWidth = 1000, ViewBoxHeight = 680 };
    }

    private FlowDiagramData BuildMergeJoinDiagram()
    {
        var nodes = new List<FlowNode>
        {
            new() { Id = 1, Label = "SELECT", Operator = "Select", NodeTypeName = "Result", CostPercentage = 0, EstimatedRows = 3200, ActualRows = 3200, TotalCost = 0, X = 400, Y = 50, Color = "#58a6ff", Icon = "result", Depth = 0, ChildIds = new() { 2 } },
            new() { Id = 2, Label = "Merge Join", Operator = "Merge Join", NodeTypeName = "MergeJoin", CostPercentage = 25, EstimatedRows = 3200, ActualRows = 3200, TotalCost = 1.40, X = 400, Y = 190, Color = "#d29922", Icon = "join", Depth = 1, ChildIds = new() { 3, 5 } },
            new() { Id = 3, Label = "Top / Sort", Operator = "Top", NodeTypeName = "TopN", CostPercentage = 15, EstimatedRows = 1000, ActualRows = 1000, TotalCost = 0.84, X = 220, Y = 340, Color = "#3fb950", Icon = "top", Depth = 2, ChildIds = new() { 4 } },
            new() { Id = 4, Label = "Clustered Index Scan", Operator = "Clustered Index Scan", NodeTypeName = "ClusteredIndexScan", CostPercentage = 18, EstimatedRows = 5000, ActualRows = 5000, TotalCost = 1.01, X = 220, Y = 480, Color = "#db6d28", Icon = "scan", TableName = "Customers", IndexName = "PK_Customers", Depth = 3 },
            new() { Id = 5, Label = "Index Scan", Operator = "Index Scan", NodeTypeName = "IndexScan", CostPercentage = 42, EstimatedRows = 50000, ActualRows = 50000, TotalCost = 2.35, X = 580, Y = 340, Color = "#f85149", Icon = "scan", TableName = "Orders", IndexName = "IX_Orders_CustomerId", Depth = 2 }
        };
        var edges = new List<FlowEdge>
        {
            new() { SourceId = 2, TargetId = 1, Rows = 3200, SourceX = 490, SourceY = 190, TargetX = 490, TargetY = 120 },
            new() { SourceId = 3, TargetId = 2, Rows = 1000, SourceX = 310, SourceY = 340, TargetX = 400, TargetY = 260 },
            new() { SourceId = 4, TargetId = 3, Rows = 5000, SourceX = 310, SourceY = 480, TargetX = 310, TargetY = 410 },
            new() { SourceId = 5, TargetId = 2, Rows = 50000, SourceX = 580, SourceY = 340, TargetX = 490, TargetY = 260 }
        };
        return new FlowDiagramData { Nodes = nodes, Edges = edges, ViewBoxWidth = 1000, ViewBoxHeight = 580 };
    }
}
