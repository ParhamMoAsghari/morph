using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.ObjectStore.Ports;
using Morph.ObjectStore.Services;

namespace Morph.ObjectStore.DependencyInjection;

public static class ObjectStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IObjectStore"/>. Requires <c>IObjectStorage</c> (from Morph.Files wiring).
    /// </summary>
    public static IServiceCollection AddMorphObjectStore(this IServiceCollection services)
    {
        services.TryAddScoped<IObjectStore, ObjectStoreService>();
        return services;
    }
}
