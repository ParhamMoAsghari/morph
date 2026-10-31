using System.Text.Json;
using Morph.Cli.Config;
using Morph.Cli.Schema;
using Morph.Client;
using Morph.Client.Models;

namespace Morph.Cli.Commands;

internal static class PushCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
        Guid projectId;
        Guid envId;
        try
        {
            (projectId, envId) = CliClientFactory.RequireLink(root, config);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var definitions = ProjectConfigStore.LoadAllDefinitions(root);
        using var client = CliClientFactory.Create(root, config);
        try
        {
            if (definitions.Count == 0)
            {
                Console.WriteLine("No definition YAML files under morph/definitions.");
            }
            else
            {
                var ordered = TopoSort(definitions);
                var slugToIds = new Dictionary<string, (Guid DefinitionId, Guid VersionId)>(StringComparer.OrdinalIgnoreCase);

                // Seed with remote
                foreach (var remote in await client.Definitions.ListAsync(envId))
                {
                    if (remote.CurrentPublishedVersionId is { } pub)
                    {
                        slugToIds[remote.Slug] = (remote.DefinitionId, pub);
                    }
                }

                foreach (var ir in ordered)
                {
                    await PushDefinitionAsync(client, envId, ir, slugToIds, root);
                }
            }

            await PushPoliciesAsync(client, projectId, envId, root);

            Console.WriteLine("Push complete.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Push failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task PushDefinitionAsync(
        MorphClient client,
        Guid envId,
        DefinitionIr ir,
        Dictionary<string, (Guid DefinitionId, Guid VersionId)> slugToIds,
        string root)
    {
        if (slugToIds.ContainsKey(ir.Slug))
        {
            Console.WriteLine($"skip  {ir.Slug} (already exists remotely — use drafts/evolve for updates in MVP)");
            // Still rewrite local ids from remote
            var existing = slugToIds[ir.Slug];
            ir.DefinitionId = existing.DefinitionId;
            ir.VersionId = existing.VersionId;
            PersistDefinition(root, ir);
            return;
        }

        CreateDefinitionResponse created;
        if (ir.ComponentUses.Count == 0)
        {
            var fields = ir.Fields.Select(ToFieldDto).ToList();
            created = await client.Definitions.CreateAsync(envId, new CreateDefinitionRequest(
                ir.Slug,
                ir.DisplayName,
                ir.Kind,
                fields));
            Console.WriteLine($"create {ir.Slug} → {created.DefinitionId} v={created.VersionId}");
        }
        else
        {
            var uses = new List<ComponentUseDto>();
            foreach (var use in ir.ComponentUses)
            {
                var targetSlug = use.TargetSlug;
                if (string.IsNullOrWhiteSpace(targetSlug) && use.TargetDefinitionId is { } tid)
                {
                    targetSlug = slugToIds.FirstOrDefault(kv => kv.Value.DefinitionId == tid).Key;
                }

                if (string.IsNullOrWhiteSpace(targetSlug) || !slugToIds.TryGetValue(targetSlug, out var target))
                {
                    throw new InvalidOperationException(
                        $"Definition '{ir.Slug}' references unknown component '{targetSlug ?? use.TargetDefinitionId?.ToString()}'.");
                }

                uses.Add(new ComponentUseDto
                {
                    ComponentUseId = use.ComponentUseId ?? Guid.NewGuid(),
                    Alias = use.Alias,
                    TargetDefinitionId = target.DefinitionId,
                    PinnedVersionId = use.PinnedVersionId ?? target.VersionId,
                    Mode = use.Mode,
                    IsRequired = use.IsRequired
                });
            }

            created = await client.Definitions.ComposeAsync(envId, new ComposeDefinitionRequest(
                ir.Slug,
                ir.DisplayName,
                ir.Kind,
                uses,
                ir.Fields.Select(ToFieldDto).ToList(),
                ir.ConflictResolutions.Select(c => new ConflictResolutionDto
                {
                    ConflictPath = c.ConflictPath,
                    WinningFieldId = c.WinningFieldId
                }).ToList()));
            Console.WriteLine($"compose {ir.Slug} → {created.DefinitionId} v={created.VersionId}");
        }

        slugToIds[ir.Slug] = (created.DefinitionId, created.VersionId);
        ir.DefinitionId = created.DefinitionId;
        ir.VersionId = created.VersionId;
        PersistDefinition(root, ir);
    }

    private static void PersistDefinition(string root, DefinitionIr ir)
    {
        var files = ProjectConfigStore.EnumerateDefinitionFiles(root);
        var match = files.FirstOrDefault(f =>
        {
            try
            {
                return string.Equals(MorphYamlSerializer.LoadDefinition(f).Slug, ir.Slug, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });

        var path = match ?? Path.Combine(root, ProjectPaths.DefinitionsDir, $"{ir.Slug}.{ir.Kind.ToLowerInvariant()}.yaml");
        MorphYamlSerializer.SaveDefinition(path, ir);
    }

    private static async Task PushPoliciesAsync(MorphClient client, Guid projectId, Guid envId, string root)
    {
        // Access policies are in-memory on Host MVP — stale local policyIds must be reconciled
        // after restart instead of silently skipped.
        var remote = await client.Access.ListPoliciesAsync(projectId);
        var byId = remote.ToDictionary(p => p.PolicyId);
        var byName = remote
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var file in ProjectConfigStore.EnumeratePolicyFiles(root))
        {
            var ir = MorphYamlSerializer.LoadPolicy(file);
            PolicyDto? existing = null;
            if (ir.PolicyId is { } localId && localId != Guid.Empty && byId.TryGetValue(localId, out var byIdHit))
            {
                existing = byIdHit;
            }
            else if (byName.TryGetValue(ir.Name, out var byNameHit))
            {
                existing = byNameHit;
            }

            if (existing is not null)
            {
                if (ir.PolicyId != existing.PolicyId)
                {
                    ir.PolicyId = existing.PolicyId;
                    MorphYamlSerializer.SavePolicy(file, ir);
                }

                if (ir.Publish && !IsPublished(existing.Status))
                {
                    await client.Access.PublishPolicyAsync(projectId, existing.PolicyId);
                    Console.WriteLine($"published policy {ir.Name} ({existing.PolicyId})");
                }
                else
                {
                    Console.WriteLine($"skip  policy {ir.Name} (present remotely, {existing.Status})");
                }

                continue;
            }

            JsonElement? condition = null;
            if (ir.Condition is not null)
            {
                var json = JsonSerializer.Serialize(ir.Condition);
                condition = JsonSerializer.Deserialize<JsonElement>(json);
            }

            var created = await client.Access.CreatePolicyAsync(projectId, new CreatePolicyRequest(
                ir.Name,
                ir.Effect,
                ir.Actions,
                ir.Resources,
                condition,
                ir.Priority,
                envId,
                ir.FieldMaskOnly,
                ir.EmitRowFilterObligation));

            Console.WriteLine($"policy {ir.Name} → {created.PolicyId}");
            ir.PolicyId = created.PolicyId;
            MorphYamlSerializer.SavePolicy(file, ir);
            byId[created.PolicyId] = created;
            byName[ir.Name] = created;

            if (ir.Publish)
            {
                await client.Access.PublishPolicyAsync(projectId, created.PolicyId);
                Console.WriteLine($"  published {ir.Name}");
            }
        }
    }

    private static bool IsPublished(string? status) =>
        string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "1", StringComparison.Ordinal);

    private static FieldDto ToFieldDto(FieldIr f) => new()
    {
        FieldId = f.FieldId ?? Guid.NewGuid(),
        Slug = f.Slug,
        DisplayName = string.IsNullOrWhiteSpace(f.DisplayName) ? f.Slug : f.DisplayName,
        DataType = f.DataType,
        IsRequired = f.IsRequired,
        IsIndexed = f.IsIndexed,
        IsUnique = f.IsUnique,
        Encrypt = f.Encrypt
    };

    private static List<DefinitionIr> TopoSort(IReadOnlyList<DefinitionIr> defs)
    {
        var bySlug = defs.ToDictionary(d => d.Slug, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DefinitionIr>();

        void Visit(DefinitionIr d)
        {
            if (!visited.Add(d.Slug))
            {
                return;
            }

            if (!visiting.Add(d.Slug))
            {
                throw new InvalidOperationException($"Cycle detected at definition '{d.Slug}'.");
            }

            foreach (var use in d.ComponentUses)
            {
                var target = use.TargetSlug;
                if (!string.IsNullOrWhiteSpace(target) && bySlug.TryGetValue(target, out var dep))
                {
                    Visit(dep);
                }
            }

            visiting.Remove(d.Slug);
            result.Add(d);
        }

        foreach (var d in defs.OrderBy(x => x.ComponentUses.Count).ThenBy(x => x.Slug, StringComparer.Ordinal))
        {
            Visit(d);
        }

        return result;
    }
}
