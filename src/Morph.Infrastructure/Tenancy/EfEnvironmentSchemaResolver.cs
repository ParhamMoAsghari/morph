using Microsoft.EntityFrameworkCore;
using Morph.DataPlane.Cache;
using Morph.Infrastructure.Persistence;

namespace Morph.Infrastructure.Tenancy;

public sealed class EfEnvironmentSchemaResolver : IEnvironmentSchemaResolver
{
    private readonly PlatformDbContext _db;

    public EfEnvironmentSchemaResolver(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<EnvironmentSchemaInfo?> ResolveAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var env = await _db.Environments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == environmentId, cancellationToken)
            .ConfigureAwait(false);

        if (env is null || string.IsNullOrWhiteSpace(env.SchemaName))
        {
            return null;
        }

        return new EnvironmentSchemaInfo(env.Id, env.ProjectId, env.SchemaName);
    }
}
