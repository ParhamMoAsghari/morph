using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Access.DependencyInjection;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Records;
using Morph.DataPlane.Services;
using Morph.DataPlane.Sql;
using Morph.EntityEngine.DependencyInjection;
using Npgsql;

namespace Morph.DataPlane.DependencyInjection;

public static class DataPlaneServiceCollectionExtensions
{
    /// <summary>
    /// Registers DataPlane + Access. Caller must also register <see cref="NpgsqlDataSource"/>
    /// and <see cref="IEnvironmentSchemaResolver"/>.
    /// </summary>
    public static IServiceCollection AddMorphDataPlane(this IServiceCollection services)
    {
        services.AddMorphEntityEngine();
        services.AddMorphAccess();

        services.TryAddSingleton<IPublishedModelCache, MemoryPublishedModelCache>();
        services.TryAddSingleton<GovernedSqlOptions>();
        services.TryAddScoped<IDynamicRecordRepository, NpgsqlDynamicRecordRepository>();
        services.TryAddScoped<DataPlaneService>();
        services.TryAddScoped<GovernedSqlService>();
        return services;
    }

    public static IServiceCollection AddMorphNpgsqlDataSource(
        this IServiceCollection services,
        string connectionString)
    {
        services.TryAddSingleton(NpgsqlDataSource.Create(connectionString));
        return services;
    }
}
