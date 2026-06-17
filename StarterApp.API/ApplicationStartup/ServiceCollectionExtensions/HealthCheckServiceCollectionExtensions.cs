using System;
using StarterApp.API.Core.HealthChecks;
using StarterApp.API.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace StarterApp.API.ApplicationStartup.ServiceCollectionExtensions;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddDbContextCheck<DataContext>()
            .AddCheck<VersionHealthCheck>(
                name: "version",
                failureStatus: HealthStatus.Degraded,
                tags: ["version"]);

        return services;
    }
}