namespace LiveSQL.Core.Models;

public enum NodeType
{
    // SQL Server - Scan operations
    TableScan,
    ClusteredIndexScan,
    IndexScan,

    // SQL Server - Seek operations
    IndexSeek,
    ClusteredIndexSeek,

    // SQL Server - Lookup
    KeyLookup,

    // Join operations
    NestedLoopJoin,
    HashJoin,
    MergeJoin,

    // Aggregate operations
    StreamAggregate,
    HashAggregate,

    // Sort and filter
    Sort,
    Filter,
    TopN,
    Distinct,

    // Compute
    Compute,

    // DML operations
    Insert,
    Update,
    Delete,

    // PostgreSQL - Scan operations
    SeqScan,
    BitmapHeapScan,
    BitmapIndexScan,
    CTEScan,

    // PostgreSQL - Join/Hash
    Hash,

    // PostgreSQL - Other
    Result,
    Append,
    Limit,
    Materialize,
    Unique,
    SetOp,
    WindowAgg,
    SubqueryScan
}
