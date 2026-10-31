using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Files.Ports;
using Morph.Files.Services;

namespace Morph.Files.DependencyInjection;

public static class FilesServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileService"/>. Caller must also register
    /// <see cref="IObjectStorage"/>, <see cref="IFileMetadataStore"/>, Access, and <c>IClock</c>.
    /// </summary>
    public static IServiceCollection AddMorphFiles(
        this IServiceCollection services,
        string physicalBucket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalBucket);

        services.TryAddScoped(sp =>
        {
            var store = sp.GetRequiredService<IFileMetadataStore>();
            var storage = sp.GetRequiredService<IObjectStorage>();
            var access = sp.GetRequiredService<Access.Evaluation.IAccessEvaluator>();
            var clock = sp.GetRequiredService<BuildingBlocks.Time.IClock>();
            return new FileService(store, storage, access, clock, physicalBucket);
        });

        return services;
    }
}
