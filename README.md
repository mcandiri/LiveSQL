# LiveSQL

> See how your SQL actually executes. Real-time query visualization with bottleneck detection, index recommendations, and animated execution flow.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![CI](https://github.com/livesql/livesql/actions/workflows/ci.yml/badge.svg)](https://github.com/livesql/livesql/actions/workflows/ci.yml)

---

## What It Looks Like

LiveSQL renders your query execution plan as an **animated flow diagram** on a dark-themed canvas. Each operator (Scan, Seek, Join, Sort, Aggregate) is a colored node sized by its relative cost. Edges animate data flow between nodes in real time. A side panel shows operator metrics, row estimates vs. actuals, and detected bottlenecks highlighted in red. The built-in query editor sits above the diagram with syntax highlighting and one-click execution.

---

## Why LiveSQL?

You write SQL. The database does... something. SSMS shows you a confusing execution plan with tiny icons. `EXPLAIN ANALYZE` gives you a wall of text. Neither tells you **where the time actually goes**.

LiveSQL makes it visual, animated, and understandable:

- **See** data flow step by step through every operator
- **Spot** table scans, expensive sorts, and bad estimates instantly
- **Fix** problems with ready-to-run index recommendations
- **Compare** before/after plans side by side

---

## Demo Mode -- Try Without a Database

LiveSQL ships with **6 pre-built example queries** covering common patterns: joins, subqueries, aggregations, CTEs, and window functions. No database connection needed.

```bash
dotnet run --project src/LiveSQL.Web
# Open http://localhost:5092 -> Click "Demo Mode"
```

Pick any example, watch the animated execution flow, and explore the bottleneck analysis -- all running on synthetic plan data.

---

## Features

### Animated Query Flow
Step-by-step animation showing how data flows through each operation. Nodes light up in execution order, edges pulse with row counts, and you can pause, rewind, or step through the plan manually.

### Bottleneck Detection
Automatic detection of performance problems:
- **Table scans** on large tables
- **Expensive sorts** spilling to disk
- **Inaccurate estimates** (estimated vs. actual row counts diverging >10x)
- **Missing indexes** causing unnecessary scans
- **Implicit conversions** hiding in predicates

Each issue is flagged with a severity level and a plain-English explanation.

### Index Recommendations
For every detected missing index, LiveSQL generates a ready-to-run `CREATE INDEX` statement with the recommended key columns and included columns.

### Plan Comparison
Run a query, optimize it, run it again -- then compare both execution plans side by side. Differences are highlighted: removed operators, changed costs, and improved row counts.

### Supported Databases

| Database       | Version | Method                          |
|----------------|---------|---------------------------------|
| SQL Server     | 2016+   | `SET STATISTICS XML ON`         |
| PostgreSQL     | 12+     | `EXPLAIN (ANALYZE, FORMAT JSON)`|

---

## Architecture

```
LiveSQL/
├── src/
│   ├── LiveSQL.Core/            Core library (no UI dependency)
│   │   ├── Models/              PlanNode, ExecutionPlan, QueryMetrics, BottleneckInfo, IndexSuggestion
│   │   ├── Connectors/          IDatabaseConnector, SqlServerConnector, PostgreSqlConnector, ConnectorFactory
│   │   ├── Parsers/             IPlanParser, SqlServerPlanParser (XML), PostgreSqlPlanParser (JSON)
│   │   ├── Analysis/            Bottleneck detection, index suggestion engine
│   │   ├── Visualization/       Layout algorithms for flow diagram node positioning
│   │   ├── Extensions/          Helper extension methods
│   │   └── Demo/                Sample plan data for demo mode
│   │
│   └── LiveSQL.Web/             Blazor Server front-end
│       ├── Components/          Razor components (pages, layout, shared)
│       ├── Services/            FlowData view-models, plan-to-diagram mapping
│       └── wwwroot/             Static assets (CSS)
│
└── tests/
    ├── LiveSQL.Core.Tests/      Unit tests (xUnit + FluentAssertions + Moq)
    └── LiveSQL.Web.Tests/       Integration tests for web layer
```

### Data Flow

```
SQL Query
  │
  ▼
IDatabaseConnector          SqlServerConnector or PostgreSqlConnector
  │  (retrieves raw plan)
  ▼
IPlanParser                 SqlServerPlanParser (XML) or PostgreSqlPlanParser (JSON)
  │  (parses into PlanNode tree)
  ▼
ExecutionPlan               Unified model: nodes, metrics, bottlenecks, index suggestions
  │
  ▼
FlowDiagramData             Layout engine positions nodes for SVG rendering
  │
  ▼
Blazor UI                   Interactive plan viewer with animations
```

**LiveSQL.Core** handles all parsing, analysis, and visualization logic. It is a standalone library with no UI dependencies, making it testable and reusable.

**LiveSQL.Web** is a Blazor Server application that provides the interactive UI. It consumes LiveSQL.Core and renders the flow diagrams using SVG-based Blazor components with animated edges and cost-based color coding.

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8.0 |
| Web UI | Blazor Server (interactive SSR) |
| SQL Server driver | Microsoft.Data.SqlClient 5.2 |
| PostgreSQL driver | Npgsql 8.0 |
| Testing | xUnit 2.5, FluentAssertions 6.12, Moq 4.20 |
| Code coverage | Coverlet 6.0 |
| CI | GitHub Actions |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (Optional) SQL Server 2016+ or PostgreSQL 12+ for live mode

### Quick Start

```bash
git clone https://github.com/livesql/livesql.git
cd livesql/LiveSQL
dotnet build
dotnet run --project src/LiveSQL.Web
```

Open [http://localhost:5092](http://localhost:5092) in your browser.

### Running Tests

```bash
dotnet test
```

Tests cover XML/JSON plan parsing, bottleneck detection rules, index recommendation generation, and the demo data pipeline.

---

## Born From Production

> LiveSQL was built from years of performance tuning SQL queries powering enterprise platforms with millions of rows. The patterns it detects -- missing indexes, estimate skew, unnecessary sorts -- are the same ones that cause real outages and slow dashboards in production systems.

---

## Security

LiveSQL is designed to be **read-only** and safe to point at production databases:

- **Read-only by design.** Uses `SET STATISTICS XML ON` / `EXPLAIN (ANALYZE, FORMAT JSON)` only -- never modifies data or schema
- **Connection strings are never logged.** The connector layer treats connection strings as opaque secrets
- **CancellationToken propagation.** All async operations accept and honor cancellation tokens, allowing users to abort long-running plan retrievals
- **No query results are stored or logged.** Only the execution plan metadata is retained
- **No telemetry or external network calls.** The application runs entirely on your infrastructure
- **Dependency scanning.** The CI pipeline runs `dotnet list package --vulnerable` to flag known vulnerabilities in transitive dependencies

---

## Roadmap

- [ ] MySQL support (`EXPLAIN FORMAT=JSON`)
- [ ] Query history and bookmarks
- [ ] Export plan as image/PDF
- [ ] VS Code extension
- [ ] Dark/Light theme toggle
- [ ] Shareable plan links

---

## Contributing

Contributions are welcome. Please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m 'Add my feature'`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a Pull Request

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
