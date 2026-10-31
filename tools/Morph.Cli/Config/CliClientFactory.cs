using Morph.Client;

namespace Morph.Cli.Config;

internal static class CliClientFactory
{
    public static MorphClient Create(string root, ProjectConfig? config = null)
    {
        config ??= ProjectConfigStore.Load(root);
        var link = ProjectConfigStore.LoadLink(root);
        var creds = ProjectConfigStore.LoadCredentials(root);

        var apiUrl = link?.ApiUrl ?? config.ApiUrl;
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            apiUrl = "http://localhost:5100";
        }

        var options = new MorphClientOptions
        {
            BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/"),
            AccessToken = creds?.AccessToken,
            EnvironmentId = link?.EnvironmentId ?? config.EnvironmentId,
            PlatformClientSecret = Environment.GetEnvironmentVariable("MORPH_Auth__PlatformClientSecret")
                                   ?? "morph-platform-dev-secret"
        };

        return new MorphClient(options);
    }

    public static (Guid ProjectId, Guid EnvironmentId) RequireLink(string root, ProjectConfig config)
    {
        var link = ProjectConfigStore.LoadLink(root);
        var projectId = link?.ProjectId ?? config.ProjectId;
        var envId = link?.EnvironmentId ?? config.EnvironmentId;
        if (projectId is null || envId is null)
        {
            throw new InvalidOperationException(
                "Project is not linked. Run: morph link --project <id> --env <id>");
        }

        return (projectId.Value, envId.Value);
    }
}
