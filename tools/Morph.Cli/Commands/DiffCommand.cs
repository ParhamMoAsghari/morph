using Morph.Cli.Config;
using Morph.Cli.Schema;

namespace Morph.Cli.Commands;

internal static class DiffCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
        var exitCode = ArgUtil.HasFlag(args, "--exit-code");

        Guid envId;
        try
        {
            (_, envId) = CliClientFactory.RequireLink(root, config);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var local = ProjectConfigStore.LoadAllDefinitions(root);
        using var client = CliClientFactory.Create(root, config);

        try
        {
            var remote = await client.Definitions.ListAsync(envId);
            var remoteBySlug = remote.ToDictionary(r => r.Slug, StringComparer.OrdinalIgnoreCase);
            var differences = 0;

            foreach (var ir in local)
            {
                if (!remoteBySlug.TryGetValue(ir.Slug, out var rem))
                {
                    Console.WriteLine($"+ local-only  {ir.Slug}");
                    differences++;
                    continue;
                }

                if (rem.CurrentPublishedVersionId is null)
                {
                    Console.WriteLine($"~ remote draft {ir.Slug} (no published version to compare)");
                    differences++;
                    continue;
                }

                var version = await client.Definitions.GetVersionAsync(envId, rem.CurrentPublishedVersionId.Value);
                var remoteIr = new DefinitionIr
                {
                    Kind = version.Kind,
                    Slug = version.Slug,
                    DisplayName = version.DisplayName,
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
                    ComponentUses = version.ComponentUses.Select(c => new ComponentUseIr
                    {
                        ComponentUseId = c.ComponentUseId,
                        Alias = c.Alias,
                        TargetDefinitionId = c.TargetDefinitionId,
                        PinnedVersionId = c.PinnedVersionId,
                        Mode = c.Mode,
                        IsRequired = c.IsRequired,
                        PinLatest = false
                    }).ToList()
                };

                var left = MorphYamlSerializer.Canonicalize(StripIdsForLooseCompare(ir));
                var right = MorphYamlSerializer.Canonicalize(StripIdsForLooseCompare(remoteIr));
                if (!string.Equals(left, right, StringComparison.Ordinal))
                {
                    Console.WriteLine($"~ changed     {ir.Slug}");
                    differences++;
                }
                else
                {
                    Console.WriteLine($"= same        {ir.Slug}");
                }
            }

            foreach (var rem in remote)
            {
                if (local.All(l => !string.Equals(l.Slug, rem.Slug, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"- remote-only {rem.Slug}");
                    differences++;
                }
            }

            if (differences == 0)
            {
                Console.WriteLine("No differences.");
            }

            return exitCode && differences > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Diff failed: {ex.Message}");
            return 1;
        }
    }

    private static DefinitionIr StripIdsForLooseCompare(DefinitionIr ir) => new()
    {
        Kind = ir.Kind,
        Slug = ir.Slug,
        DisplayName = ir.DisplayName,
        Fields = ir.Fields.Select(f => new FieldIr
        {
            Slug = f.Slug,
            DisplayName = f.DisplayName,
            DataType = f.DataType,
            IsRequired = f.IsRequired,
            IsIndexed = f.IsIndexed,
            IsUnique = f.IsUnique,
            Encrypt = f.Encrypt
        }).ToList(),
        ComponentUses = ir.ComponentUses.Select(c => new ComponentUseIr
        {
            Alias = c.Alias,
            TargetSlug = c.TargetSlug,
            Mode = c.Mode,
            IsRequired = c.IsRequired
        }).ToList()
    };
}
