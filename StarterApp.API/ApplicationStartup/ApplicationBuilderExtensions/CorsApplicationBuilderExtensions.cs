using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using StarterApp.API.Constants;

namespace StarterApp.API.ApplicationStartup.ApplicationBuilderExtensions;

public static class CorsApplicationBuilderExtensions
{
    private static readonly string[] defaultOrigins = ["*"];

    public static IApplicationBuilder UseAndConfigureCors(this IApplicationBuilder app, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        app.UseCors(header =>
            header.WithOrigins(config.GetSection(ConfigurationKeys.CorsAllowedOrigins).Get<string[]>() ?? defaultOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders(config.GetSection(ConfigurationKeys.CorsExposedHeaders).Get<string[]>() ?? [AppHeaderNames.TokenExpired, AppHeaderNames.CorrelationId]));

        return app;
    }
}