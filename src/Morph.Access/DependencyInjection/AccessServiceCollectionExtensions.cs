using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Access.Evaluation;
using Morph.Access.Persistence;
using Morph.Access.Ports;
using Morph.Access.Services;

namespace Morph.Access.DependencyInjection;

public static class AccessServiceCollectionExtensions
{
    public static IServiceCollection AddMorphAccess(this IServiceCollection services)
    {
        services.TryAddSingleton<IRoleStore, InMemoryRoleStore>();
        services.TryAddSingleton<IPolicyStore, InMemoryPolicyStore>();
        services.TryAddSingleton<RoleHierarchyExpander>();
        services.TryAddScoped<IAccessEvaluator, AccessEvaluator>();
        services.TryAddScoped<AccessAdminService>();
        return services;
    }
}
