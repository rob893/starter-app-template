using System;
using StarterApp.API.Constants;
using StarterApp.API.Models.Settings;
using StarterApp.API.Services.Core;
using Microsoft.Extensions.Configuration;
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
    /// <param name="config">The application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddScoped<ICorrelationIdService, CorrelationIdService>();

        services.Configure<ForwardedHeadersSettings>(config.GetSection(ConfigurationKeys.ForwardedHeaders));

        return services;
    }
}