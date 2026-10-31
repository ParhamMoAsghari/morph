using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Mail.Handlers;
using Morph.Mail.Options;
using Morph.Mail.Ports;
using Morph.Mail.Services;
using Morph.Mail.Smtp;
using Morph.Mail.Templates;
using Morph.Messaging.DependencyInjection;

namespace Morph.Mail.DependencyInjection;

public static class MailServiceCollectionExtensions
{
    public static IServiceCollection AddMorphMail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MailSenderOptions>(configuration.GetSection(MailSenderOptions.SectionName));

        // Also bind nested Morph:Mail from MorphOptions-shaped config.
        services.Configure<MailSenderOptions>(configuration.GetSection("Morph:Mail"));

        services.TryAddSingleton<IEmailTemplateRenderer, SimpleEmailTemplateRenderer>();
        services.TryAddScoped<IMailSender, MailKitMailSender>();
        services.TryAddScoped<MailService>();
        services.AddOutboxHandler<SendEmailOutboxHandler>();
        return services;
    }
}
