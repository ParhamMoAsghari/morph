using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.EntityEngine.Ports;
using Morph.Infrastructure.Persistence.EntityEngine;
using Morph.Infrastructure.Storage;

namespace Morph.Infrastructure.DependencyInjection;

public static class EntityEnginePersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Replaces Entity Engine in-memory ports with EF Core + Npgsql implementations.
    /// </summary>
    public static IServiceCollection AddMorphEntityEnginePersistence(this IServiceCollection services)
    {
        services.RemoveAll<IDefinitionStore>();
        services.RemoveAll<IMigrationRunStore>();
        services.RemoveAll<IStorageMappingStore>();
        services.RemoveAll<IDdlExecutor>();

        services.AddScoped<IDefinitionStore, EfDefinitionStore>();
        services.AddScoped<IMigrationRunStore, EfMigrationRunStore>();
        services.AddScoped<IStorageMappingStore, EfStorageMappingStore>();
        services.AddScoped<IDdlExecutor, NpgsqlDdlExecutor>();

        return services;
    }
}
