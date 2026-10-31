using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.BuildingBlocks.Time;
using Morph.Functions.Runtime.Ports;
using Morph.Functions.Runtime.Services;

namespace Morph.Functions.Runtime.DependencyInjection;

public static class FunctionsRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddMorphFunctionsRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IFunctionRegistry, InMemoryFunctionRegistry>();
        services.TryAddSingleton<IFunctionInvoker, StubFunctionInvoker>();
        services.TryAddScoped<FunctionRuntimeService>();
        return services;
    }
}
