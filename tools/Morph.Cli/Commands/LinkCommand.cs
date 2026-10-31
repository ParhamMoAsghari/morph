using Morph.Cli.Config;

namespace Morph.Cli.Commands;

internal static class LinkCommand
{
    public static int Run(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);

        var projectId = ArgUtil.GetGuidOption(args, "--project");
        var envId = ArgUtil.GetGuidOption(args, "--env") ?? ArgUtil.GetGuidOption(args, "--environment");
        var orgId = ArgUtil.GetGuidOption(args, "--org");
        var api = ArgUtil.GetOption(args, "--api") ?? config.ApiUrl;

        if (projectId is null || envId is null)
        {
            Console.Error.WriteLine("Usage: morph link --project <guid> --env <guid> [--org <guid>] [--api <url>]");
            return 1;
        }

        var link = new LinkState
        {
            ProjectId = projectId.Value,
            EnvironmentId = envId.Value,
            OrganizationId = orgId,
            ApiUrl = api,
            LinkedAt = DateTimeOffset.UtcNow
        };
        ProjectConfigStore.SaveLink(root, link);

        config.ProjectId = projectId;
        config.EnvironmentId = envId;
        config.OrganizationId = orgId;
        config.ApiUrl = api;
        ProjectConfigStore.Save(root, config);

        Console.WriteLine($"Linked project={projectId} env={envId} api={api}");
        return 0;
    }
}
