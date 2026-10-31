using Morph.Cli.Config;

namespace Morph.Cli.Commands;

internal static class MigrateCommand
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

        var toVersion = ArgUtil.GetGuidOption(args, "--to") ?? ArgUtil.GetGuidOption(args, "--version");
        var fromVersion = ArgUtil.GetGuidOption(args, "--from");
        var planOnly = ArgUtil.HasFlag(args, "--plan") || ArgUtil.HasFlag(args, "plan");
        var apply = ArgUtil.HasFlag(args, "--apply") || ArgUtil.HasFlag(args, "apply");
        var slug = ArgUtil.GetOption(args, "--slug");

        using var client = CliClientFactory.Create(root, config);
        try
        {
            if (toVersion is null && !string.IsNullOrWhiteSpace(slug))
            {
                var local = ProjectConfigStore.LoadAllDefinitions(root)
                    .FirstOrDefault(d => string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase));
                toVersion = local?.VersionId;
            }

            if (toVersion is null)
            {
                // Apply all local definitions that have version ids (create plans)
                var locals = ProjectConfigStore.LoadAllDefinitions(root)
                    .Where(d => d.VersionId is not null)
                    .ToList();
                if (locals.Count == 0)
                {
                    Console.Error.WriteLine("Usage: morph migrate --to <versionId> [--from <versionId>] [--plan|--apply]");
                    Console.Error.WriteLine("   or: morph migrate --slug <slug> --apply");
                    return 1;
                }

                foreach (var ir in locals)
                {
                    await RunOneAsync(client, envId, ir.VersionId!.Value, fromVersion, planOnly || !apply, apply || !planOnly);
                }

                return 0;
            }

            await RunOneAsync(client, envId, toVersion.Value, fromVersion, planOnly || !apply, apply || !planOnly);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Migrate failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task RunOneAsync(
        Morph.Client.MorphClient client,
        Guid envId,
        Guid toVersionId,
        Guid? fromVersionId,
        bool showPlan,
        bool doApply)
    {
        if (showPlan)
        {
            var plan = await client.Migrations.PlanAsync(envId, toVersionId, fromVersionId);
            Console.WriteLine($"Plan {plan.PlanId} → {plan.Table} ({plan.Stages.Count} stages)");
            foreach (var stage in plan.Stages)
            {
                Console.WriteLine($"  [{stage.Ordinal}] {stage.Stage}/{stage.Kind}");
            }
        }

        if (doApply)
        {
            var run = await client.Migrations.ApplyAsync(envId, toVersionId, fromVersionId);
            Console.WriteLine($"Applied run={run.RunId} status={run.Status}");
            if (!string.IsNullOrWhiteSpace(run.FailureMessage))
            {
                Console.Error.WriteLine(run.FailureMessage);
            }
        }
    }
}
