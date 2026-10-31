using Morph.Cli.Config;
using Morph.Cli.Schema;
using Morph.Client.Models;

namespace Morph.Cli.Commands;

internal static class PullCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
        Guid envId;
        Guid projectId;
        try
        {
            (projectId, envId) = CliClientFactory.RequireLink(root, config);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.DefinitionsDir));
        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.PoliciesDir));

        using var client = CliClientFactory.Create(root, config);
        try
        {
            var remotes = await client.Definitions.ListAsync(envId);
            var slugToVersion = new Dictionary<Guid, DefinitionSummary>();
            foreach (var d in remotes)
            {
                slugToVersion[d.DefinitionId] = d;
            }

            foreach (var remote in remotes)
            {
                if (remote.CurrentPublishedVersionId is null)
                {
                    Console.WriteLine($"skip  {remote.Slug} (no published version)");
                    continue;
                }

                var version = await client.Definitions.GetVersionAsync(envId, remote.CurrentPublishedVersionId.Value);
                var ir = ToIr(version, remotes);
                var fileName = $"{ir.Slug}.{ir.Kind.ToLowerInvariant()}.yaml";
                var path = Path.Combine(root, ProjectPaths.DefinitionsDir, fileName);
                MorphYamlSerializer.SaveDefinition(path, ir);
                Console.WriteLine($"wrote {fileName}");
            }

            var policies = await client.Access.ListPoliciesAsync(projectId);
            foreach (var policy in policies)
            {
                var ir = new PolicyIr
                {
                    Name = policy.Name,
                    PolicyId = policy.PolicyId,
                    Effect = policy.Effect,
                    Actions = policy.Actions,
                    Resources = policy.Resources,
                    Publish = string.Equals(policy.Status, "Published", StringComparison.OrdinalIgnoreCase)
                };
                var safe = string.Join(".", policy.Name.Split(Path.GetInvalidFileNameChars()));
                var path = Path.Combine(root, ProjectPaths.PoliciesDir, $"{safe}.policy.yaml");
                MorphYamlSerializer.SavePolicy(path, ir);
                Console.WriteLine($"wrote policies/{Path.GetFileName(path)}");
            }

            Console.WriteLine("Pull complete.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Pull failed: {ex.Message}");
            return 1;
        }
    }

    private static DefinitionIr ToIr(DefinitionVersionDto version, IReadOnlyList<DefinitionSummary> all)
    {
        var byId = all.ToDictionary(d => d.DefinitionId);
        return new DefinitionIr
        {
            Kind = version.Kind,
            Slug = version.Slug,
            DisplayName = version.DisplayName,
            DefinitionId = version.DefinitionId,
            VersionId = version.VersionId,
            Fields = version.Fields.Select(f => new FieldIr
            {
                FieldId = f.FieldId,
                Slug = f.Slug,
                DisplayName = f.DisplayName,
                DataType = f.DataType,
                IsRequired = f.IsRequired,
                IsIndexed = f.IsIndexed,
                IsUnique = f.IsUnique,
                Encrypt = f.Encrypt
            }).ToList(),
            ComponentUses = version.ComponentUses.Select(c =>
            {
                byId.TryGetValue(c.TargetDefinitionId, out var target);
                return new ComponentUseIr
                {
                    ComponentUseId = c.ComponentUseId,
                    Alias = c.Alias,
                    TargetSlug = target?.Slug,
                    TargetDefinitionId = c.TargetDefinitionId,
                    PinnedVersionId = c.PinnedVersionId,
                    PinLatest = false,
                    Mode = c.Mode,
                    IsRequired = c.IsRequired
                };
            }).ToList(),
            ConflictResolutions = version.ConflictResolutions.Select(c => new ConflictResolutionIr
            {
                ConflictPath = c.ConflictPath,
                WinningFieldId = c.WinningFieldId
            }).ToList()
        };
    }
}
