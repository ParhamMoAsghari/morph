using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Persistence;
using Morph.EntityEngine.Ports;
using Morph.EntityEngine.Publishing;
using Morph.EntityEngine.Services;
using Morph.EntityEngine.Storage;
using Morph.EntityEngine.Validation;

namespace Morph.EntityEngine.DependencyInjection;

public static class EntityEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers Entity Engine domain services. Uses in-memory stores by default;
    /// Morph.Infrastructure replaces persistence / DDL ports with EF/Npgsql implementations.
    /// </summary>
    public static IServiceCollection AddMorphEntityEngine(this IServiceCollection services)
    {
        // Stores default to singleton in-memory; Infrastructure replaces with scoped EF.
        services.TryAddSingleton<IDefinitionStore, InMemoryDefinitionStore>();
        services.TryAddSingleton<IMigrationRunStore, InMemoryMigrationRunStore>();
        services.TryAddSingleton<IStorageMappingStore, InMemoryStorageMappingStore>();
        services.TryAddSingleton<IDdlExecutor, NullDdlExecutor>();

        // Scoped so EF-backed stores (scoped) are safe when Infrastructure replaces ports.
        services.TryAddScoped<DefinitionGraphValidator>();
        services.TryAddScoped<EffectiveModelCompiler>();
        services.TryAddScoped<CompositionOperations>();
        services.TryAddScoped<DecompositionOperations>();
        services.TryAddScoped<DefinitionPublisher>();
        services.TryAddScoped<StorageMapper>();
        services.TryAddScoped<MigrationPlanner>();
        services.TryAddScoped<StagedMigrationExecutor>();
        services.TryAddScoped<EntityDefinitionService>();
        services.TryAddScoped<MigrationService>();
        return services;
    }
}
