using FluentAssertions;
using LiveSQL.Core.Models;
using LiveSQL.Core.Analysis;
using LiveSQL.Core.Visualization;

namespace LiveSQL.Web.Tests;

/// <summary>
/// Smoke tests verifying core services used by the web layer work correctly.
/// </summary>
public class WebSmokeTests
{
    [Fact]
    public void CoreServices_ShouldInstantiate()
    {
        var costAnalyzer = new CostAnalyzer();
        var bottleneckDetector = new BottleneckDetector();
        var indexAdvisor = new IndexAdvisor();
        var queryAnalyzer = new QueryAnalyzer(costAnalyzer, bottleneckDetector, indexAdvisor);
        var flowLayout = new FlowLayout();
        var colorMapper = new ColorMapper();
        var flowBuilder = new FlowBuilder(flowLayout, colorMapper);

        costAnalyzer.Should().NotBeNull();
        bottleneckDetector.Should().NotBeNull();
        indexAdvisor.Should().NotBeNull();
        queryAnalyzer.Should().NotBeNull();
        flowBuilder.Should().NotBeNull();
    }

    [Fact]
    public void CoreServices_ShouldProcessSamplePlan()
    {
        var costAnalyzer = new CostAnalyzer();
        var bottleneckDetector = new BottleneckDetector();
        var indexAdvisor = new IndexAdvisor();
        var queryAnalyzer = new QueryAnalyzer(costAnalyzer, bottleneckDetector, indexAdvisor);
        var flowLayout = new FlowLayout();
        var colorMapper = new ColorMapper();
        var flowBuilder = new FlowBuilder(flowLayout, colorMapper);

        var plan = LiveSQL.Core.Demo.SamplePlans.SimpleSelect();

        var flowData = flowBuilder.Build(plan);

        flowData.Should().NotBeNull();
        flowData.Nodes.Should().NotBeEmpty();
    }

    [Fact]
    public void ColorMapper_ShouldMapAllSeverities()
    {
        var mapper = new ColorMapper();

        mapper.MapSeverityToColor(Severity.Low).Should().NotBeNullOrEmpty();
        mapper.MapSeverityToColor(Severity.Medium).Should().NotBeNullOrEmpty();
        mapper.MapSeverityToColor(Severity.High).Should().NotBeNullOrEmpty();
        mapper.MapSeverityToColor(Severity.Critical).Should().NotBeNullOrEmpty();
    }
}
