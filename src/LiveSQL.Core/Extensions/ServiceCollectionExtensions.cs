using LiveSQL.Core.Analysis;
using LiveSQL.Core.Demo;
using LiveSQL.Core.Parsers;
using LiveSQL.Core.Visualization;
using Microsoft.Extensions.DependencyInjection;

namespace LiveSQL.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLiveSqlCore(this IServiceCollection services)
    {
        // Parsers
        services.AddSingleton<IPlanParser, SqlServerPlanParser>();
        services.AddSingleton<IPlanParser, PostgreSqlPlanParser>();
        services.AddSingleton<PlanNormalizer>();

        // Analysis
        services.AddSingleton<CostAnalyzer>();
        services.AddSingleton<BottleneckDetector>();
        services.AddSingleton<IndexAdvisor>();
        services.AddSingleton<PlanComparator>();
        services.AddSingleton<IQueryAnalyzer, QueryAnalyzer>();
        services.AddSingleton<QueryAnalyzer>();

        // Visualization
        services.AddSingleton<ColorMapper>();
        services.AddSingleton<FlowLayout>();
        services.AddSingleton<FlowBuilder>();

        // Demo
        services.AddSingleton<DemoDataProvider>();

        return services;
    }
}
