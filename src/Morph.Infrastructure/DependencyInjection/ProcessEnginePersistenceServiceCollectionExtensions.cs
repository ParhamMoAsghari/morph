using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Infrastructure.Persistence.ProcessEngine;
using Morph.ProcessEngine.Ports;

namespace Morph.Infrastructure.DependencyInjection;

public static class ProcessEnginePersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Replaces Process Engine in-memory ports with EF Core implementations.
    /// </summary>
    public static IServiceCollection AddMorphProcessEnginePersistence(this IServiceCollection services)
    {
        services.RemoveAll<IProcessDefinitionStore>();
        services.RemoveAll<IProcessInstanceStore>();

        services.AddScoped<IProcessDefinitionStore, EfProcessDefinitionStore>();
        services.AddScoped<IProcessInstanceStore, EfProcessInstanceStore>();

        return services;
    }
}
