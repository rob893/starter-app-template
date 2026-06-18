using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarterApp.API.Constants;
using StarterApp.API.Data;
using StarterApp.API.Models.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace StarterApp.API.ApplicationStartup.ServiceCollectionExtensions;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<PostgresSettings>(config.GetSection(ConfigurationKeys.Postgres));

        var settings = config.GetSection(ConfigurationKeys.Postgres)?.Get<PostgresSettings>()
            ?? throw new InvalidOperationException($"Missing {ConfigurationKeys.Postgres} section in configuration.");

        var dbJsonOptions = new JsonSerializerOptions();
        dbJsonOptions.Converters.Add(new JsonStringEnumConverter());

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(settings.DefaultConnection);
        dataSourceBuilder.EnableDynamicJson().ConfigureJsonOptions(dbJsonOptions); ;
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContextPool<DataContext>(
            dbContextOptions =>
            {
                dbContextOptions
                    .UseNpgsql(dataSource, options =>
                    {
                        options.EnableRetryOnFailure();
                        options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });

                if (settings.EnableDetailedErrors)
                {
                    dbContextOptions.EnableDetailedErrors();
                }

                if (settings.EnableSensitiveDataLogging)
                {
                    dbContextOptions.EnableSensitiveDataLogging();
                }
            },
            poolSize: 128
        );

        services.AddTransient<IDatabaseSeeder, DatabaseSeeder>();

        return services;
    }
}