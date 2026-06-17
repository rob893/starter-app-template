using System;
using StarterApp.API.Services.Core;
using Microsoft.Extensions.DependencyInjection;

namespace StarterApp.API.ApplicationStartup.ServiceCollectionExtensions;

/// <summary>
/// Extension methods for registering core services.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICorrelationIdService, CorrelationIdService>();

        return services;
    }
}