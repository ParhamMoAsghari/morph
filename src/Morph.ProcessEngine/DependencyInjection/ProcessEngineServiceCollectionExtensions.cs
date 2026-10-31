using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Plugins.Abstractions;
using Morph.ProcessEngine.Activities;
using Morph.ProcessEngine.Execution;
using Morph.ProcessEngine.Http;
using Morph.ProcessEngine.Persistence;
using Morph.ProcessEngine.Ports;
using Morph.ProcessEngine.Publishing;
using Morph.ProcessEngine.Services;

namespace Morph.ProcessEngine.DependencyInjection;

public static class ProcessEngineServiceCollectionExtensions
{
    public static IServiceCollection AddMorphProcessEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IProcessDefinitionStore, InMemoryProcessDefinitionStore>();
        services.TryAddSingleton<IProcessInstanceStore, InMemoryProcessInstanceStore>();
        services.TryAddSingleton<IProcessActivityRegistry, EmptyProcessActivityRegistry>();
        services.TryAddSingleton<IPluginHttpClientFactory, NullPluginHttpClientFactory>();
        services.TryAddSingleton<IProcessFunctionInvoker, NullProcessFunctionInvoker>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, SetVariableActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, StringConcatActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, StringReplaceActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, StringTrimActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, JsonParseActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, JsonStringifyActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, JsonGetActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, JsonSetActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, JsonMergeActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, HttpRequestActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, MorphDataCreateActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, MorphDataGetActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, MorphDataUpdateActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, MorphDataDeleteActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, MorphDataListActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, DelayActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, AccessAssertActivity>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltinActivity, InvokeFunctionActivity>());

        services.TryAddSingleton<BuiltinActivityCatalog>(sp =>
            new BuiltinActivityCatalog(sp.GetServices<IBuiltinActivity>()));

        services.TryAddScoped<ProcessCompiler>();
        services.TryAddScoped<ProcessRunner>();
        services.TryAddScoped<ProcessDefinitionService>();
        services.TryAddScoped<ProcessRuntimeService>();

        return services;
    }
}
