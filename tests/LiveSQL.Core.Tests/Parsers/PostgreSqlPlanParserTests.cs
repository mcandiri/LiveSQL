using FluentAssertions;
using LiveSQL.Core.Models;
using LiveSQL.Core.Parsers;

namespace LiveSQL.Core.Tests.Parsers;

public class PostgreSqlPlanParserTests
{
    private const string SimpleIndexScanPlan = @"[
  {
    ""Plan"": {
      ""Node Type"": ""Index Scan"",
      ""Relation Name"": ""students"",
      ""Schema"": ""public"",
      ""Index Name"": ""pk_students"",
      ""Scan Direction"": ""Forward"",
      ""Total Cost"": 8.29,
      ""Plan Rows"": 1,
      ""Actual Rows"": 1,
      ""Actual Total Time"": 0.05,
      ""Plan Width"": 64
    }
  }
]";

    private const string NestedJoinPlan = @"[
  {
    ""Plan"": {
      ""Node Type"": ""Hash Join"",
      ""Join Type"": ""Inner"",
      ""Total Cost"": 120.50,
      ""Plan Rows"": 500,
      ""Actual Rows"": 480,
      ""Actual Total Time"": 15.30,
      ""Plans"": [
        {
          ""Node Type"": ""Seq Scan"",
          ""Relation Name"": ""students"",
          ""Schema"": ""public"",
          ""Total Cost"": 22.00,
          ""Plan Rows"": 1200,
          ""Actual Rows"": 1200,
          ""Actual Total Time"": 2.10
        },
        {
          ""Node Type"": ""Hash"",
          ""Total Cost"": 45.00,
          ""Plan Rows"": 500,
          ""Actual Rows"": 500,
          ""Actual Total Time"": 5.40,
          ""Plans"": [
            {
              ""Node Type"": ""Index Scan"",
              ""Relation Name"": ""enrollments"",
              ""Schema"": ""public"",
              ""Index Name"": ""idx_enrollments_student_id"",
              ""Total Cost"": 30.00,
              ""Plan Rows"": 500,
              ""Actual Rows"": 500,
              ""Actual Total Time"": 3.20
            }
          ]
        }
      ]
    }
  }
]";

    private const string PlanWithActualRowsMismatch = @"[
  {
    ""Plan"": {
      ""Node Type"": ""Seq Scan"",
      ""Relation Name"": ""orders"",
      ""Schema"": ""public"",
      ""Total Cost"": 450.00,
      ""Plan Rows"": 10,
      ""Actual Rows"": 50000,
      ""Actual Total Time"": 120.50
    }
  }
]";

    private readonly PostgreSqlPlanParser _parser = new();

    [Fact]
    public void EngineType_ShouldBePostgreSql()
    {
        _parser.EngineType.Should().Be("PostgreSQL");
    }

    [Fact]
    public void CanParse_ShouldReturnTrue_ForValidJson()
    {
        _parser.CanParse(SimpleIndexScanPlan).Should().BeTrue();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_ForXmlInput()
    {
        _parser.CanParse(@"<ShowPlanXML xmlns=""http://schemas.microsoft.com/sqlserver/2004/07/showplan""></ShowPlanXML>")
            .Should().BeFalse();
    }

    [Fact]
    public void CanParse_ShouldReturnFalse_ForEmptyInput()
    {
        _parser.CanParse("").Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_SimplePlan_ShouldReturnValidPlan()
    {
        var plan = await _parser.ParseAsync(SimpleIndexScanPlan, CancellationToken.None);

        plan.Should().NotBeNull();
        plan.DatabaseEngine.Should().Be("PostgreSQL");
        plan.RootNode.Should().NotBeNull();
        plan.RootNode.PhysicalOperator.Should().Contain("Index Scan");
        plan.RootNode.Cost.TotalCost.Should().BeApproximately(8.29, 0.01);
        plan.RootNode.Cost.EstimatedRows.Should().Be(1);
        plan.RootNode.Cost.ActualRows.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_SimplePlan_ShouldParseTableInfo()
    {
        var plan = await _parser.ParseAsync(SimpleIndexScanPlan, CancellationToken.None);

        plan.RootNode.Table.Should().NotBeNull();
        plan.RootNode.Table!.TableName.Should().Be("students");
        plan.RootNode.Index.Should().NotBeNull();
        plan.RootNode.Index!.IndexName.Should().Be("pk_students");
    }

    [Fact]
    public async Task ParseAsync_SimplePlan_ShouldMapCorrectNodeType()
    {
        var plan = await _parser.ParseAsync(SimpleIndexScanPlan, CancellationToken.None);

        plan.RootNode.NodeType.Should().Be(NodeType.IndexScan);
    }

    [Fact]
    public async Task ParseAsync_NestedJoinPlan_ShouldBuildTree()
    {
        var plan = await _parser.ParseAsync(NestedJoinPlan, CancellationToken.None);

        plan.Should().NotBeNull();
        plan.RootNode.PhysicalOperator.Should().Contain("Hash Join");
        plan.RootNode.Children.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_NestedJoinPlan_ShouldCountAllNodes()
    {
        var plan = await _parser.ParseAsync(NestedJoinPlan, CancellationToken.None);

        // Hash Join -> (Seq Scan, Hash -> Index Scan) = 4 nodes
        plan.TotalNodes.Should().Be(4);
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_ShouldThrow()
    {
        var act = () => _parser.ParseAsync("this is not json {{{", CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ParseAsync_PlanWithActualRows_ShouldParseRowCounts()
    {
        var plan = await _parser.ParseAsync(PlanWithActualRowsMismatch, CancellationToken.None);

        plan.RootNode.Cost.EstimatedRows.Should().Be(10);
        plan.RootNode.Cost.ActualRows.Should().Be(50000);
    }

    [Fact]
    public async Task ParseAsync_ShouldSetRawPlan()
    {
        var plan = await _parser.ParseAsync(SimpleIndexScanPlan, CancellationToken.None);

        plan.RawPlan.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_NestedPlan_ShouldSetDepthOnNodes()
    {
        var plan = await _parser.ParseAsync(NestedJoinPlan, CancellationToken.None);

        plan.RootNode.Depth.Should().Be(0);
        plan.RootNode.Children.Should().AllSatisfy(child =>
            child.Depth.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ParseAsync_ShouldComputeCostPercentages()
    {
        var plan = await _parser.ParseAsync(NestedJoinPlan, CancellationToken.None);

        plan.AllNodes.Should().Contain(n => n.Cost.CostPercentage > 0);
    }
}
