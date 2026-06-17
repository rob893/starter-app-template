using System;
using StarterApp.API.Constants;
using StarterApp.API.Models.Settings;
using StarterApp.API.Services.Email;
using StarterApp.API.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StarterApp.API.ApplicationStartup.ServiceCollectionExtensions;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<EmailSettings>(config.GetSection(ConfigurationKeys.Email));

        services.AddSingleton<IAcsEmailClientFactory, AcsEmailClientFactory>()
            .AddScoped<IEmailService, AcsEmailService>()
            .AddSingleton<IEmailTemplateService, EmailTemplateService>();

        return services;
    }
}