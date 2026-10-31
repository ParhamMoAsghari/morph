using Morph.Cli.Config;

namespace Morph.Cli.Commands;

internal static class PublishCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
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

        var slug = ArgUtil.GetOption(args, "--slug");
        var versionId = ArgUtil.GetGuidOption(args, "--version");

        using var client = CliClientFactory.Create(root, config);
        try
        {
            if (versionId is { } vid)
            {
                var published = await client.Definitions.PublishAsync(envId, vid);
                Console.WriteLine($"Published {published.Slug} v{published.VersionNumber} ({published.VersionId})");
                return 0;
            }

            var definitions = ProjectConfigStore.LoadAllDefinitions(root);
            var remotes = await client.Definitions.ListAsync(envId);
            var targets = definitions
                .Where(d => slug is null || string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targets.Count == 0)
            {
                Console.Error.WriteLine("No matching local definitions. Specify --version <guid> or --slug <slug>.");
                return 1;
            }

            foreach (var ir in targets)
            {
                Guid? toPublish = ir.VersionId;
                if (toPublish is null)
                {
                    var rem = remotes.FirstOrDefault(r =>
                        string.Equals(r.Slug, ir.Slug, StringComparison.OrdinalIgnoreCase));
                    // Prefer draft: list doesn't give drafts — require versionId on IR from push
                    if (rem?.CurrentPublishedVersionId is { } already)
                    {
                        Console.WriteLine($"skip  {ir.Slug} already published ({already})");
                        continue;
                    }

                    Console.Error.WriteLine($"skip  {ir.Slug}: no versionId (run morph push first)");
                    continue;
                }

                var published = await client.Definitions.PublishAsync(envId, toPublish.Value);
                Console.WriteLine($"Published {published.Slug} v{published.VersionNumber} ({published.VersionId}) fingerprint={published.EffectiveModelFingerprint}");
                ir.VersionId = published.VersionId;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Publish failed: {ex.Message}");
            return 1;
        }
    }
}
