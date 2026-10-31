using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Crypto.Options;
using Morph.Crypto.Ports;
using Morph.Crypto.Services;

namespace Morph.Crypto.DependencyInjection;

public static class CryptoServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMorphCrypto"/> (NSec XChaCha20-Poly1305 envelope helpers).
    /// Bind <see cref="MorphCryptoOptions"/> from <c>Morph:Crypto</c> when <paramref name="configuration"/> is provided.
    /// </summary>
    public static IServiceCollection AddMorphCrypto(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddOptions<MorphCryptoOptions>();

        if (configuration is not null)
        {
            services.Configure<MorphCryptoOptions>(configuration.GetSection(MorphCryptoOptions.SectionName));
        }

        services.TryAddSingleton<IMorphCrypto, MorphCryptoService>();
        return services;
    }
}
