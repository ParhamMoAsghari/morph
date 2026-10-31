using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Sms.Ports;
using Morph.Sms.Services;

namespace Morph.Sms.DependencyInjection;

public static class SmsServiceCollectionExtensions
{
    public static IServiceCollection AddMorphSms(this IServiceCollection services)
    {
        services.TryAddScoped<ISmsService, MorphSmsService>();
        return services;
    }
}
