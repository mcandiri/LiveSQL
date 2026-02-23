using LiveSQL.Core.Models;

namespace LiveSQL.Core.Demo;

public static class SamplePlans
{
    public static ExecutionPlan SimpleSelect()
    {
        var root = new PlanNode
        {
            Id = 0,
            Label = "SELECT",
            PhysicalOperator = "SELECT",
            LogicalOperator = "SELECT",
            NodeType = NodeType.Compute,
            Depth = 0,
            Cost = new OperationCost
            {
                TotalCost = 0.003,
                SubtreeCost = 0.003,
                CpuCost = 0.001,
                IoCost = 0.002,
                EstimatedRows = 1,
                ActualRows = 1,
                CostPercentage = 3.0
            },
            Children = new List<PlanNode>
            {
                new PlanNode
                {
                    Id = 1,
                    Label = "Clustered Index Seek",
                    PhysicalOperator = "Clustered Index Seek",
                    LogicalOperator = "Clustered Index Seek",
                    NodeType = NodeType.ClusteredIndexSeek,
                    Depth = 1,
                    Cost = new OperationCost
                    {
                        TotalCost = 0.003,
                        SubtreeCost = 0.003,
                        CpuCost = 0.001,
                        IoCost = 0.002,
                        EstimatedRows = 1,
                        ActualRows = 1,
                        CostPercentage = 97.0
                    },
                    Table = new TableReference
                    {
                        Schema = "dbo",
                        TableName = "Students",
                        EstimatedRowCount = 50000
                    },
                    Index = new IndexReference
                    {
                        IndexName = "PK_Students",
                        TableName = "Students",
                        Columns = new List<string> { "Id" },
                        IsClustered = true,
                        IsUnique = true
                    },
                    Predicate = "[Students].[Id] = @1"
                }
            }
        };

        return new ExecutionPlan
        {
            QueryText = "SELECT * FROM Students WHERE Id = 1",
            DatabaseEngine = "SQL Server",
            RootNode = root,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(1),
                CpuTime = TimeSpan.FromMilliseconds(0.5),
                LogicalReads = 3,
                PhysicalReads = 0,
                RowsAffected = 1,
                TotalCost = 0.003,
                TotalOperators = 2
            }
        };
    }

    public static ExecutionPlan TableScanProblem()
    {
        var root = new PlanNode
        {
            Id = 0,
            Label = "SELECT",
            PhysicalOperator = "SELECT",
            LogicalOperator = "SELECT",
            NodeType = NodeType.Compute,
            Depth = 0,
            Cost = new OperationCost
            {
                TotalCost = 0.05,
                SubtreeCost = 5.23,
                CpuCost = 0.05,
                IoCost = 0.0,
                EstimatedRows = 8500,
                ActualRows = 8347,
                CostPercentage = 1.0
            },
            Children = new List<PlanNode>
            {
                new PlanNode
                {
                    Id = 1,
                    Label = "Filter",
                    PhysicalOperator = "Filter",
                    LogicalOperator = "Filter",
                    NodeType = NodeType.Filter,
                    Depth = 1,
                    Cost = new OperationCost
                    {
                        TotalCost = 0.15,
                        SubtreeCost = 5.18,
                        CpuCost = 0.15,
                        IoCost = 0.0,
                        EstimatedRows = 8500,
                        ActualRows = 8347,
                        CostPercentage = 3.0
                    },
                    Predicate = "[Students].[Email] LIKE '%@gmail.com'",
                    Children = new List<PlanNode>
                    {
                        new PlanNode
                        {
                            Id = 2,
                            Label = "Table Scan",
                            PhysicalOperator = "Table Scan",
                            LogicalOperator = "Table Scan",
                            NodeType = NodeType.TableScan,
                            Depth = 2,
                            IsWarning = true,
                            WarningMessage = "Full Table Scan on Students (50,000 rows)",
                            Cost = new OperationCost
                            {
                                TotalCost = 5.03,
                                SubtreeCost = 5.03,
                                CpuCost = 0.55,
                                IoCost = 4.48,
                                EstimatedRows = 50000,
                                ActualRows = 50000,
                                CostPercentage = 96.0
                            },
                            Table = new TableReference
                            {
                                Schema = "dbo",
                                TableName = "Students",
                                EstimatedRowCount = 50000
                            }
                        }
                    }
                }
            }
        };

        var plan = new ExecutionPlan
        {
            QueryText = "SELECT * FROM Students WHERE Email LIKE '%@gmail.com'",
            DatabaseEngine = "SQL Server",
            RootNode = root,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(450),
                CpuTime = TimeSpan.FromMilliseconds(320),
                LogicalReads = 50000,
                PhysicalReads = 1200,
                RowsAffected = 8347,
                TotalCost = 5.23,
                TotalOperators = 3
            },
            Bottlenecks = new List<BottleneckInfo>
            {
                new BottleneckInfo
                {
                    Title = "Table Scan on Students",
                    Description = "Full table scan reading 50,000 rows from Students. " +
                                  "The LIKE '%@gmail.com' pattern with leading wildcard prevents index usage.",
                    Severity = Severity.Critical,
                    RelatedNode = root.Children[0].Children[0],
                    Recommendation = "Consider using a full-text index, a computed column with a reversed email, " +
                                     "or restructure the query to avoid leading wildcards.",
                    ImpactPercentage = 96.0
                }
            },
            IndexSuggestions = new List<IndexSuggestion>
            {
                new IndexSuggestion
                {
                    TableName = "Students",
                    Schema = "dbo",
                    KeyColumns = new List<string> { "Email" },
                    IncludeColumns = new List<string> { "FirstName", "LastName", "EnrollmentDate" },
                    Reason = "Table Scan on Students. Note: Leading wildcard LIKE '%...' cannot use a standard B-tree index. " +
                             "Consider a full-text index or computed column approach.",
                    EstimatedImprovement = 85,
                    Impact = Severity.High
                }
            }
        };

        return plan;
    }

    public static ExecutionPlan MissingIndex()
    {
        var root = new PlanNode
        {
            Id = 0,
            Label = "SELECT",
            PhysicalOperator = "SELECT",
            LogicalOperator = "SELECT",
            NodeType = NodeType.Compute,
            Depth = 0,
            Cost = new OperationCost
            {
                TotalCost = 0.02,
                SubtreeCost = 3.45,
                CpuCost = 0.02,
                IoCost = 0.0,
                EstimatedRows = 1,
                ActualRows = 1,
                CostPercentage = 0.6
            },
            Children = new List<PlanNode>
            {
                new PlanNode
                {
                    Id = 1,
                    Label = "Table Scan",
                    PhysicalOperator = "Table Scan",
                    LogicalOperator = "Table Scan",
                    NodeType = NodeType.TableScan,
                    Depth = 1,
                    IsWarning = true,
                    WarningMessage = "Missing index: an Index Seek would be 100x faster",
                    Cost = new OperationCost
                    {
                        TotalCost = 3.43,
                        SubtreeCost = 3.43,
                        CpuCost = 0.35,
                        IoCost = 3.08,
                        EstimatedRows = 200000,
                        ActualRows = 200000,
                        CostPercentage = 99.4
                    },
                    Table = new TableReference
                    {
                        Schema = "dbo",
                        TableName = "Enrollments",
                        EstimatedRowCount = 200000
                    },
                    Predicate = "[Enrollments].[StudentId] = @1 AND [Enrollments].[CourseId] = @2"
                }
            }
        };

        return new ExecutionPlan
        {
            QueryText = "SELECT * FROM Enrollments WHERE StudentId = 5 AND CourseId = 3",
            DatabaseEngine = "SQL Server",
            RootNode = root,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(890),
                CpuTime = TimeSpan.FromMilliseconds(650),
                LogicalReads = 200000,
                PhysicalReads = 5400,
                RowsAffected = 1,
                TotalCost = 3.45,
                TotalOperators = 2
            },
            Bottlenecks = new List<BottleneckInfo>
            {
                new BottleneckInfo
                {
                    Title = "Table Scan on Enrollments",
                    Description = "Full table scan reading 200,000 rows to find just 1 row. " +
                                  "A composite index on (StudentId, CourseId) would reduce this to a single seek.",
                    Severity = Severity.Critical,
                    RelatedNode = root.Children[0],
                    Recommendation = "Create a nonclustered index on Enrollments(StudentId, CourseId) " +
                                     "to enable an Index Seek instead of a full Table Scan.",
                    ImpactPercentage = 99.4
                }
            },
            IndexSuggestions = new List<IndexSuggestion>
            {
                new IndexSuggestion
                {
                    TableName = "Enrollments",
                    Schema = "dbo",
                    KeyColumns = new List<string> { "StudentId", "CourseId" },
                    IncludeColumns = new List<string> { "EnrollmentDate", "Grade" },
                    Reason = "Table Scan reading 200,000 rows for a single row result. " +
                             "Composite index would enable Index Seek (estimated 100x improvement).",
                    EstimatedImprovement = 99,
                    Impact = Severity.Critical
                }
            }
        };
    }

    public static ExecutionPlan ComplexJoin()
    {
        var gradesSeek = new PlanNode
        {
            Id = 6,
            Label = "Clustered Index Seek",
            PhysicalOperator = "Clustered Index Seek",
            LogicalOperator = "Clustered Index Seek",
            NodeType = NodeType.ClusteredIndexSeek,
            Depth = 3,
            Cost = new OperationCost
            {
                TotalCost = 0.15,
                SubtreeCost = 0.15,
                CpuCost = 0.05,
                IoCost = 0.10,
                EstimatedRows = 500,
                ActualRows = 487,
                CostPercentage = 5.0
            },
            Table = new TableReference { Schema = "dbo", TableName = "Grades", EstimatedRowCount = 500000 },
            Index = new IndexReference
            {
                IndexName = "IX_Grades_EnrollmentId",
                TableName = "Grades",
                Columns = new List<string> { "EnrollmentId" }
            },
            Predicate = "[Grades].[EnrollmentId] = [Enrollments].[Id]"
        };

        var coursesSeek = new PlanNode
        {
            Id = 5,
            Label = "Index Seek",
            PhysicalOperator = "Index Seek",
            LogicalOperator = "Index Seek",
            NodeType = NodeType.IndexSeek,
            Depth = 3,
            Cost = new OperationCost
            {
                TotalCost = 0.08,
                SubtreeCost = 0.08,
                CpuCost = 0.03,
                IoCost = 0.05,
                EstimatedRows = 50,
                ActualRows = 48,
                CostPercentage = 2.7
            },
            Table = new TableReference { Schema = "dbo", TableName = "Courses", EstimatedRowCount = 500 },
            Index = new IndexReference
            {
                IndexName = "PK_Courses",
                TableName = "Courses",
                Columns = new List<string> { "Id" },
                IsClustered = true
            },
            Predicate = "[Courses].[Id] = [Enrollments].[CourseId]"
        };

        var enrollmentsScan = new PlanNode
        {
            Id = 4,
            Label = "Index Scan",
            PhysicalOperator = "Index Scan",
            LogicalOperator = "Index Scan",
            NodeType = NodeType.IndexScan,
            Depth = 3,
            Cost = new OperationCost
            {
                TotalCost = 0.85,
                SubtreeCost = 0.85,
                CpuCost = 0.22,
                IoCost = 0.63,
                EstimatedRows = 5000,
                ActualRows = 4823,
                CostPercentage = 28.3
            },
            Table = new TableReference { Schema = "dbo", TableName = "Enrollments", EstimatedRowCount = 200000 },
            Index = new IndexReference
            {
                IndexName = "IX_Enrollments_StudentId",
                TableName = "Enrollments",
                Columns = new List<string> { "StudentId" }
            }
        };

        var studentsSeek = new PlanNode
        {
            Id = 3,
            Label = "Clustered Index Seek",
            PhysicalOperator = "Clustered Index Seek",
            LogicalOperator = "Clustered Index Seek",
            NodeType = NodeType.ClusteredIndexSeek,
            Depth = 2,
            Cost = new OperationCost
            {
                TotalCost = 0.003,
                SubtreeCost = 0.003,
                CpuCost = 0.001,
                IoCost = 0.002,
                EstimatedRows = 1,
                ActualRows = 1,
                CostPercentage = 0.1
            },
            Table = new TableReference { Schema = "dbo", TableName = "Students", EstimatedRowCount = 50000 },
            Index = new IndexReference
            {
                IndexName = "PK_Students",
                TableName = "Students",
                Columns = new List<string> { "Id" },
                IsClustered = true,
                IsUnique = true
            },
            Predicate = "[Students].[Id] = @StudentId"
        };

        var nestedLoop = new PlanNode
        {
            Id = 2,
            Label = "Nested Loop Join",
            PhysicalOperator = "Nested Loops",
            LogicalOperator = "Inner Join",
            NodeType = NodeType.NestedLoopJoin,
            Depth = 2,
            Cost = new OperationCost
            {
                TotalCost = 0.12,
                SubtreeCost = 1.12,
                CpuCost = 0.12,
                IoCost = 0.0,
                EstimatedRows = 500,
                ActualRows = 487,
                CostPercentage = 4.0
            },
            Children = new List<PlanNode> { enrollmentsScan, coursesSeek }
        };

        var hashJoin = new PlanNode
        {
            Id = 1,
            Label = "Hash Join",
            PhysicalOperator = "Hash Match",
            LogicalOperator = "Inner Join",
            NodeType = NodeType.HashJoin,
            Depth = 1,
            Cost = new OperationCost
            {
                TotalCost = 0.95,
                SubtreeCost = 2.85,
                CpuCost = 0.55,
                IoCost = 0.40,
                EstimatedRows = 487,
                ActualRows = 487,
                CostPercentage = 31.7
            },
            Children = new List<PlanNode> { nestedLoop, gradesSeek }
        };

        var root = new PlanNode
        {
            Id = 0,
            Label = "Nested Loop Join",
            PhysicalOperator = "Nested Loops",
            LogicalOperator = "Inner Join",
            NodeType = NodeType.NestedLoopJoin,
            Depth = 0,
            Cost = new OperationCost
            {
                TotalCost = 0.15,
                SubtreeCost = 3.0,
                CpuCost = 0.15,
                IoCost = 0.0,
                EstimatedRows = 487,
                ActualRows = 487,
                CostPercentage = 5.0
            },
            Children = new List<PlanNode> { hashJoin, studentsSeek }
        };

        return new ExecutionPlan
        {
            QueryText = @"SELECT s.FirstName, s.LastName, c.CourseName, e.EnrollmentDate, g.Score
FROM Students s
INNER JOIN Enrollments e ON s.Id = e.StudentId
INNER JOIN Courses c ON e.CourseId = c.Id
INNER JOIN Grades g ON e.Id = g.EnrollmentId
WHERE s.Id = @StudentId",
            DatabaseEngine = "SQL Server",
            RootNode = root,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(85),
                CpuTime = TimeSpan.FromMilliseconds(62),
                LogicalReads = 5540,
                PhysicalReads = 120,
                RowsAffected = 487,
                TotalCost = 3.0,
                TotalOperators = 7
            },
            Bottlenecks = new List<BottleneckInfo>
            {
                new BottleneckInfo
                {
                    Title = "Expensive Hash Join (31.7% of total cost)",
                    Description = "Hash Join consuming 31.7% of total query cost between Enrollments and Grades.",
                    Severity = Severity.High,
                    RelatedNode = hashJoin,
                    Recommendation = "Ensure join columns are indexed. Consider adding an index on Grades(EnrollmentId).",
                    ImpactPercentage = 31.7
                },
                new BottleneckInfo
                {
                    Title = "Index Scan on Enrollments",
                    Description = "Index Scan reading 4,823 rows from Enrollments where a seek might be possible.",
                    Severity = Severity.Medium,
                    RelatedNode = enrollmentsScan,
                    Recommendation = "Consider adding a composite index on Enrollments(StudentId, CourseId) with INCLUDE columns.",
                    ImpactPercentage = 28.3
                }
            }
        };
    }

    public static ExecutionPlan SortAndAggregate()
    {
        var enrollmentsScan = new PlanNode
        {
            Id = 4,
            Label = "Table Scan",
            PhysicalOperator = "Table Scan",
            LogicalOperator = "Table Scan",
            NodeType = NodeType.TableScan,
            Depth = 3,
            Cost = new OperationCost
            {
                TotalCost = 2.10,
                SubtreeCost = 2.10,
                CpuCost = 0.22,
                IoCost = 1.88,
                EstimatedRows = 200000,
                ActualRows = 200000,
                CostPercentage = 30.0
            },
            Table = new TableReference { Schema = "dbo", TableName = "Enrollments", EstimatedRowCount = 200000 },
            IsWarning = true,
            WarningMessage = "Table Scan on large table"
        };

        var coursesScan = new PlanNode
        {
            Id = 3,
            Label = "Clustered Index Scan",
            PhysicalOperator = "Clustered Index Scan",
            LogicalOperator = "Clustered Index Scan",
            NodeType = NodeType.ClusteredIndexScan,
            Depth = 3,
            Cost = new OperationCost
            {
                TotalCost = 0.05,
                SubtreeCost = 0.05,
                CpuCost = 0.02,
                IoCost = 0.03,
                EstimatedRows = 500,
                ActualRows = 500,
                CostPercentage = 0.7
            },
            Table = new TableReference { Schema = "dbo", TableName = "Courses", EstimatedRowCount = 500 },
            Index = new IndexReference { IndexName = "PK_Courses", TableName = "Courses", IsClustered = true }
        };

        var hashJoin = new PlanNode
        {
            Id = 2,
            Label = "Hash Join",
            PhysicalOperator = "Hash Match",
            LogicalOperator = "Inner Join",
            NodeType = NodeType.HashJoin,
            Depth = 2,
            Cost = new OperationCost
            {
                TotalCost = 1.20,
                SubtreeCost = 3.35,
                CpuCost = 0.80,
                IoCost = 0.40,
                EstimatedRows = 200000,
                ActualRows = 200000,
                CostPercentage = 17.1
            },
            Children = new List<PlanNode> { coursesScan, enrollmentsScan }
        };

        var hashAggregate = new PlanNode
        {
            Id = 1,
            Label = "Hash Aggregate",
            PhysicalOperator = "Hash Match",
            LogicalOperator = "Aggregate",
            NodeType = NodeType.HashAggregate,
            Depth = 1,
            Cost = new OperationCost
            {
                TotalCost = 1.80,
                SubtreeCost = 5.15,
                CpuCost = 1.20,
                IoCost = 0.60,
                EstimatedRows = 500,
                ActualRows = 487,
                CostPercentage = 25.7
            },
            Children = new List<PlanNode> { hashJoin }
        };

        var sort = new PlanNode
        {
            Id = 0,
            Label = "Sort",
            PhysicalOperator = "Sort",
            LogicalOperator = "Sort",
            NodeType = NodeType.Sort,
            Depth = 0,
            IsWarning = true,
            WarningMessage = "Expensive sort on 487 grouped rows",
            Cost = new OperationCost
            {
                TotalCost = 1.85,
                SubtreeCost = 7.00,
                CpuCost = 0.95,
                IoCost = 0.90,
                EstimatedRows = 500,
                ActualRows = 487,
                CostPercentage = 26.4
            },
            Children = new List<PlanNode> { hashAggregate }
        };

        return new ExecutionPlan
        {
            QueryText = @"SELECT c.CourseName, COUNT(*) AS StudentCount, AVG(g.Score) AS AvgScore
FROM Courses c
INNER JOIN Enrollments e ON c.Id = e.CourseId
INNER JOIN Grades g ON e.Id = g.EnrollmentId
GROUP BY c.CourseName
ORDER BY StudentCount DESC",
            DatabaseEngine = "SQL Server",
            RootNode = sort,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(520),
                CpuTime = TimeSpan.FromMilliseconds(410),
                LogicalReads = 201000,
                PhysicalReads = 3200,
                RowsAffected = 487,
                TotalCost = 7.00,
                TotalOperators = 5
            },
            Bottlenecks = new List<BottleneckInfo>
            {
                new BottleneckInfo
                {
                    Title = "Table Scan on Enrollments",
                    Description = "Full table scan reading 200,000 rows from Enrollments.",
                    Severity = Severity.Critical,
                    RelatedNode = enrollmentsScan,
                    Recommendation = "Add an index on Enrollments(CourseId) to support the join operation.",
                    ImpactPercentage = 30.0
                },
                new BottleneckInfo
                {
                    Title = "Expensive Sort (487 rows after aggregation)",
                    Description = "Sort operation on aggregated results. The sort consumes 26.4% of total cost.",
                    Severity = Severity.Medium,
                    RelatedNode = sort,
                    Recommendation = "Consider adding an index that provides pre-sorted results.",
                    ImpactPercentage = 26.4
                },
                new BottleneckInfo
                {
                    Title = "Hash Aggregate (25.7% cost)",
                    Description = "Hash Aggregate building hash table for GROUP BY operation on 200,000 rows.",
                    Severity = Severity.Medium,
                    RelatedNode = hashAggregate,
                    Recommendation = "Reducing the input rows through better indexing would decrease aggregation cost.",
                    ImpactPercentage = 25.7
                }
            },
            IndexSuggestions = new List<IndexSuggestion>
            {
                new IndexSuggestion
                {
                    TableName = "Enrollments",
                    Schema = "dbo",
                    KeyColumns = new List<string> { "CourseId" },
                    IncludeColumns = new List<string> { "StudentId", "Id" },
                    Reason = "Full Table Scan on Enrollments for join with Courses. " +
                             "An index on CourseId would reduce I/O significantly.",
                    EstimatedImprovement = 70,
                    Impact = Severity.High
                }
            }
        };
    }

    public static (ExecutionPlan Before, ExecutionPlan After) BeforeAfterComparison()
    {
        // BEFORE: Without indexes - full table scan
        var beforeRoot = new PlanNode
        {
            Id = 0,
            Label = "SELECT",
            PhysicalOperator = "SELECT",
            LogicalOperator = "SELECT",
            NodeType = NodeType.Compute,
            Depth = 0,
            Cost = new OperationCost
            {
                TotalCost = 0.05,
                SubtreeCost = 8.50,
                CpuCost = 0.05,
                EstimatedRows = 15,
                ActualRows = 12,
                CostPercentage = 0.6
            },
            Children = new List<PlanNode>
            {
                new PlanNode
                {
                    Id = 1,
                    Label = "Nested Loop Join",
                    PhysicalOperator = "Nested Loops",
                    LogicalOperator = "Inner Join",
                    NodeType = NodeType.NestedLoopJoin,
                    Depth = 1,
                    Cost = new OperationCost
                    {
                        TotalCost = 0.10,
                        SubtreeCost = 8.45,
                        CpuCost = 0.10,
                        EstimatedRows = 15,
                        ActualRows = 12,
                        CostPercentage = 1.2
                    },
                    Children = new List<PlanNode>
                    {
                        new PlanNode
                        {
                            Id = 2,
                            Label = "Table Scan",
                            PhysicalOperator = "Table Scan",
                            LogicalOperator = "Table Scan",
                            NodeType = NodeType.TableScan,
                            Depth = 2,
                            IsWarning = true,
                            WarningMessage = "Full Table Scan on Enrollments",
                            Cost = new OperationCost
                            {
                                TotalCost = 3.43,
                                SubtreeCost = 3.43,
                                CpuCost = 0.35,
                                IoCost = 3.08,
                                EstimatedRows = 200000,
                                ActualRows = 200000,
                                CostPercentage = 40.4
                            },
                            Table = new TableReference { Schema = "dbo", TableName = "Enrollments", EstimatedRowCount = 200000 },
                            Predicate = "[Enrollments].[StudentId] = @StudentId"
                        },
                        new PlanNode
                        {
                            Id = 3,
                            Label = "Table Scan",
                            PhysicalOperator = "Table Scan",
                            LogicalOperator = "Table Scan",
                            NodeType = NodeType.TableScan,
                            Depth = 2,
                            IsWarning = true,
                            WarningMessage = "Full Table Scan on Courses",
                            Cost = new OperationCost
                            {
                                TotalCost = 4.82,
                                SubtreeCost = 4.82,
                                CpuCost = 0.55,
                                IoCost = 4.27,
                                EstimatedRows = 500,
                                ActualRows = 500,
                                CostPercentage = 56.7
                            },
                            Table = new TableReference { Schema = "dbo", TableName = "Courses", EstimatedRowCount = 500 },
                            Predicate = "[Courses].[DepartmentId] = @DeptId"
                        }
                    }
                }
            }
        };

        var before = new ExecutionPlan
        {
            QueryText = @"SELECT s.FirstName, c.CourseName, e.EnrollmentDate
FROM Students s
INNER JOIN Enrollments e ON s.Id = e.StudentId
INNER JOIN Courses c ON e.CourseId = c.Id
WHERE s.Id = @StudentId AND c.DepartmentId = @DeptId",
            DatabaseEngine = "SQL Server",
            RootNode = beforeRoot,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(1200),
                CpuTime = TimeSpan.FromMilliseconds(890),
                LogicalReads = 200500,
                PhysicalReads = 6200,
                RowsAffected = 12,
                TotalCost = 8.50,
                TotalOperators = 4
            },
            Bottlenecks = new List<BottleneckInfo>
            {
                new BottleneckInfo
                {
                    Title = "Table Scan on Courses",
                    Description = "Full table scan on Courses reading all 500 rows.",
                    Severity = Severity.High,
                    ImpactPercentage = 56.7
                },
                new BottleneckInfo
                {
                    Title = "Table Scan on Enrollments",
                    Description = "Full table scan on Enrollments reading all 200,000 rows.",
                    Severity = Severity.Critical,
                    ImpactPercentage = 40.4
                }
            }
        };

        // AFTER: With proper indexes - index seeks
        var afterRoot = new PlanNode
        {
            Id = 0,
            Label = "SELECT",
            PhysicalOperator = "SELECT",
            LogicalOperator = "SELECT",
            NodeType = NodeType.Compute,
            Depth = 0,
            Cost = new OperationCost
            {
                TotalCost = 0.001,
                SubtreeCost = 0.025,
                CpuCost = 0.001,
                EstimatedRows = 15,
                ActualRows = 12,
                CostPercentage = 4.0
            },
            Children = new List<PlanNode>
            {
                new PlanNode
                {
                    Id = 1,
                    Label = "Nested Loop Join",
                    PhysicalOperator = "Nested Loops",
                    LogicalOperator = "Inner Join",
                    NodeType = NodeType.NestedLoopJoin,
                    Depth = 1,
                    Cost = new OperationCost
                    {
                        TotalCost = 0.002,
                        SubtreeCost = 0.024,
                        CpuCost = 0.002,
                        EstimatedRows = 15,
                        ActualRows = 12,
                        CostPercentage = 8.0
                    },
                    Children = new List<PlanNode>
                    {
                        new PlanNode
                        {
                            Id = 2,
                            Label = "Index Seek",
                            PhysicalOperator = "Index Seek",
                            LogicalOperator = "Index Seek",
                            NodeType = NodeType.IndexSeek,
                            Depth = 2,
                            Cost = new OperationCost
                            {
                                TotalCost = 0.008,
                                SubtreeCost = 0.008,
                                CpuCost = 0.003,
                                IoCost = 0.005,
                                EstimatedRows = 15,
                                ActualRows = 12,
                                CostPercentage = 32.0
                            },
                            Table = new TableReference { Schema = "dbo", TableName = "Enrollments", EstimatedRowCount = 200000 },
                            Index = new IndexReference
                            {
                                IndexName = "IX_Enrollments_StudentId",
                                TableName = "Enrollments",
                                Columns = new List<string> { "StudentId" }
                            },
                            Predicate = "[Enrollments].[StudentId] = @StudentId"
                        },
                        new PlanNode
                        {
                            Id = 3,
                            Label = "Index Seek",
                            PhysicalOperator = "Index Seek",
                            LogicalOperator = "Index Seek",
                            NodeType = NodeType.IndexSeek,
                            Depth = 2,
                            Cost = new OperationCost
                            {
                                TotalCost = 0.012,
                                SubtreeCost = 0.012,
                                CpuCost = 0.005,
                                IoCost = 0.007,
                                EstimatedRows = 20,
                                ActualRows = 18,
                                CostPercentage = 48.0
                            },
                            Table = new TableReference { Schema = "dbo", TableName = "Courses", EstimatedRowCount = 500 },
                            Index = new IndexReference
                            {
                                IndexName = "IX_Courses_DepartmentId",
                                TableName = "Courses",
                                Columns = new List<string> { "DepartmentId" }
                            },
                            Predicate = "[Courses].[DepartmentId] = @DeptId"
                        }
                    }
                }
            }
        };

        var after = new ExecutionPlan
        {
            QueryText = @"SELECT s.FirstName, c.CourseName, e.EnrollmentDate
FROM Students s
INNER JOIN Enrollments e ON s.Id = e.StudentId
INNER JOIN Courses c ON e.CourseId = c.Id
WHERE s.Id = @StudentId AND c.DepartmentId = @DeptId",
            DatabaseEngine = "SQL Server",
            RootNode = afterRoot,
            Metrics = new QueryMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(3),
                CpuTime = TimeSpan.FromMilliseconds(2),
                LogicalReads = 35,
                PhysicalReads = 0,
                RowsAffected = 12,
                TotalCost = 0.025,
                TotalOperators = 4
            }
        };

        return (before, after);
    }
}
