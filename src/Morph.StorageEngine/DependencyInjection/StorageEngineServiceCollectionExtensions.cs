using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.StorageEngine.Ports;
using Morph.StorageEngine.Services;

namespace Morph.StorageEngine.DependencyInjection;

public static class StorageEngineServiceCollectionExtensions
{
    public static IServiceCollection AddMorphStorageEngine(
        this IServiceCollection services,
        StorageBackendKind backend = StorageBackendKind.Postgres)
    {
        services.TryAddSingleton<IStorageEngine>(_ => new StorageEngineFacade(backend));
        return services;
    }
}
