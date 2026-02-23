using FluentAssertions;
using LiveSQL.Core.Models;
using LiveSQL.Core.Parsers;

namespace LiveSQL.Core.Tests.Parsers;

public class SqlServerPlanParserTests
{
    private const string SimpleIndexSeekPlan = @"<ShowPlanXML xmlns=""http://schemas.microsoft.com/sqlserver/2004/07/showplan"">
  <BatchSequence>
    <Batch>
      <Statements>
        <StmtSimple StatementText=""SELECT * FROM Students WHERE Id = 1"" StatementSubTreeCost=""0.003"">
          <QueryPlan>
            <RelOp NodeId=""0"" PhysicalOp=""Clustered Index Seek"" LogicalOp=""Clustered Index Seek"" EstimateRows=""1"" EstimatedTotalSubtreeCost=""0.003"">
              <RunTimeInformation>
                <RunTimeCountersPerThread ActualRows=""1"" ActualElapsedms=""1""/>
              </RunTimeInformation>
              <IndexScan>
                <Object Database=""SchoolDB"" Schema=""dbo"" Table=""Students"" Index=""PK_Students""/>
              </IndexScan>
            </RelOp>
          </QueryPlan>
        </StmtSimple>
      </Statements>
    </Batch>
  </BatchSequence>
</ShowPlanXML>";

    private const string JoinPlan = @"<ShowPlanXML xmlns=""http://schemas.microsoft.com/sqlserver/2004/07/showplan"">
  <BatchSequence>
    <Batch>
      <Statements>
        <StmtSimple StatementText=""SELECT s.Name, e.Grade FROM Students s JOIN Enrollments e ON s.Id = e.StudentId"" StatementSubTreeCost=""0.45"">
          <QueryPlan>
            <RelOp NodeId=""0"" PhysicalOp=""Hash Match"" LogicalOp=""Inner Join"" EstimateRows=""500"" EstimateCPU=""0.1"" EstimateIO=""0.05"" EstimatedTotalSubtreeCost=""0.45"">
              <Hash>
                <RelOp NodeId=""1"" PhysicalOp=""Clustered Index Scan"" LogicalOp=""Clustered Index Scan"" EstimateRows=""100"" EstimateCPU=""0.02"" EstimateIO=""0.10"" EstimatedTotalSubtreeCost=""0.12"">
                  <IndexScan>
                    <Object Database=""SchoolDB"" Schema=""dbo"" Table=""Students"" Index=""PK_Students""/>
                  </IndexScan>
                </RelOp>
                <RelOp NodeId=""2"" PhysicalOp=""Table Scan"" LogicalOp=""Table Scan"" EstimateRows=""5000"" EstimateCPU=""0.05"" EstimateIO=""0.28"" EstimatedTotalSubtreeCost=""0.33"">
                  <IndexScan>
                    <Object Database=""SchoolDB"" Schema=""dbo"" Table=""Enrollments""/>
                  </IndexScan>
                </RelOp>
              </Hash>
            </RelOp>
          </QueryPlan>
        </StmtSimple>
      </Statements>
    </Batch>
  </BatchSequence>
</ShowPlanXML>";

    private const string PlanWithMissingAttributes = @"<ShowPlanXML xmlns=""http://schemas.microsoft.com/sqlserver/2004/07/showplan"">
  <BatchSequence>
    <Batch>
      <Statements>
        <StmtSimple StatementText=""SELECT 1"">
          <QueryPlan>
            <RelOp NodeId=""0"" PhysicalOp=""Constant Scan"" LogicalOp=""Constant Scan"">
            </RelOp>
          </QueryPlan>
        </StmtSimple>
      </Statements>
    </Batch>
  </BatchSequence>
</ShowPlanXML>";

    private readonly SqlServerPlanParser _parser = new();

    [Fact]
    public void EngineType_ShouldBeSqlServer()
    {
        _parser.EngineType.Should().Be("SQL Server");
    }

    [Fact]
    public void CanParse_ShouldReturnTrue_ForValidXml()
    {
        _parser.CanParse(SimpleIndexSeekPlan).Should().BeTrue();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_ForJsonInput()
    {
        _parser.CanParse(@"[{""Plan"": {}}]").Should().BeFalse();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_ForEmptyInput()
    {
        _parser.CanParse("").Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_SimpleIndexSeek_ShouldReturnValidPlan()
    {
        var plan = await _parser.ParseAsync(SimpleIndexSeekPlan, CancellationToken.None);

        plan.Should().NotBeNull();
        plan.DatabaseEngine.Should().Be("SQL Server");
        plan.RootNode.Should().NotBeNull();
        plan.RootNode.PhysicalOperator.Should().Be("Clustered Index Seek");
        plan.RootNode.Cost.EstimatedRows.Should().Be(1);
        plan.RootNode.Cost.SubtreeCost.Should().BeApproximately(0.003, 0.001);
    }

    [Fact]
    public async Task ParseAsync_SimpleIndexSeek_ShouldParseTableAndIndex()
    {
        var plan = await _parser.ParseAsync(SimpleIndexSeekPlan, CancellationToken.None);

        plan.RootNode.Table.Should().NotBeNull();
        plan.RootNode.Table!.TableName.Should().Be("Students");
        plan.RootNode.Table.Schema.Should().Be("dbo");
        plan.RootNode.Index.Should().NotBeNull();
        plan.RootNode.Index!.IndexName.Should().Be("PK_Students");
    }

    [Fact]
    public async Task ParseAsync_SimpleIndexSeek_ShouldMapCorrectNodeType()
    {
        var plan = await _parser.ParseAsync(SimpleIndexSeekPlan, CancellationToken.None);

        plan.RootNode.NodeType.Should().Be(NodeType.ClusteredIndexSeek);
    }

    [Fact]
    public async Task ParseAsync_JoinPlan_ShouldReturnTreeWithChildren()
    {
        var plan = await _parser.ParseAsync(JoinPlan, CancellationToken.None);

        plan.Should().NotBeNull();
        plan.RootNode.PhysicalOperator.Should().Be("Hash Match");
        plan.RootNode.Children.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_JoinPlan_ShouldCountAllNodes()
    {
        var plan = await _parser.ParseAsync(JoinPlan, CancellationToken.None);

        plan.TotalNodes.Should().Be(3);
        plan.AllNodes.Should().HaveCount(3);
    }

    [Fact]
    public async Task ParseAsync_JoinPlan_ShouldParseChildNodeTypes()
    {
        var plan = await _parser.ParseAsync(JoinPlan, CancellationToken.None);

        plan.RootNode.Children[0].NodeType.Should().Be(NodeType.ClusteredIndexScan);
        plan.RootNode.Children[1].NodeType.Should().Be(NodeType.TableScan);
    }

    [Fact]
    public async Task ParseAsync_InvalidXml_ShouldThrow()
    {
        var act = () => _parser.ParseAsync("this is not xml", CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ParseAsync_MissingAttributes_ShouldHandleGracefully()
    {
        var plan = await _parser.ParseAsync(PlanWithMissingAttributes, CancellationToken.None);

        plan.Should().NotBeNull();
        plan.RootNode.Should().NotBeNull();
        plan.RootNode.PhysicalOperator.Should().Be("Constant Scan");
        plan.RootNode.Cost.EstimatedRows.Should().Be(0);
        plan.RootNode.Cost.SubtreeCost.Should().Be(0);
    }

    [Fact]
    public async Task ParseAsync_ShouldPreserveQueryText()
    {
        var plan = await _parser.ParseAsync(SimpleIndexSeekPlan, CancellationToken.None);

        plan.QueryText.Should().Contain("SELECT");
    }

    [Fact]
    public async Task ParseAsync_ShouldStoreRawPlan()
    {
        var plan = await _parser.ParseAsync(SimpleIndexSeekPlan, CancellationToken.None);

        plan.RawPlan.Should().NotBeNullOrEmpty();
        plan.RawPlan.Should().Contain("ShowPlanXML");
    }

    [Fact]
    public async Task ParseAsync_JoinPlan_ShouldComputeCostPercentages()
    {
        var plan = await _parser.ParseAsync(JoinPlan, CancellationToken.None);

        // With StatementSubTreeCost set and EstimateCPU/IO on nodes, percentages should be computed
        plan.AllNodes.Should().Contain(n => n.Cost.CostPercentage > 0);
    }

    [Fact]
    public async Task ParseAsync_ShouldSetMetrics()
    {
        var plan = await _parser.ParseAsync(SimpleIndexSeekPlan, CancellationToken.None);

        plan.Metrics.TotalOperators.Should().BeGreaterThan(0);
    }
}
