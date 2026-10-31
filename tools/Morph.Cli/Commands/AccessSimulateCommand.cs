using Morph.Cli.Config;
using Morph.Client;

namespace Morph.Cli.Commands;

internal static class AccessSimulateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);

        var principal = ArgUtil.GetOption(args, "--principal") ?? ArgUtil.GetOption(args, "--sub");
        var action = ArgUtil.GetOption(args, "--action") ?? "read";
        var resource = ArgUtil.GetOption(args, "--resource") ?? "entity:*";

        if (string.IsNullOrWhiteSpace(principal))
        {
            Console.Error.WriteLine("Usage: morph access simulate --principal <sub> --action <action> --resource <pattern>");
            return 1;
        }

        using var client = CliClientFactory.Create(root, config);
        try
        {
            var result = await client.Access.SimulateAsync(new
            {
                principalId = principal,
                action,
                resource,
                environmentId = config.EnvironmentId
            });
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
            return 0;
        }
        catch (MorphApiException ex) when (ex.StatusCode is 404 or 405 or 501)
        {
            PrintStub(principal, action, resource,
                "Access simulate API is not available on this Morph.Host yet (POST /v1/access/simulate).");
            return 0;
        }
        catch (HttpRequestException)
        {
            PrintStub(principal, action, resource,
                "Morph.Host unreachable — stubbing access simulate locally (start Host to call the API).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Access simulate failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintStub(string principal, string action, string resource, string reason)
    {
        Console.Error.WriteLine(reason);
        Console.Error.WriteLine("Stub: would evaluate RBAC+ABAC+PBAC for:");
        Console.WriteLine($"  principal={principal}");
        Console.WriteLine($"  action={action}");
        Console.WriteLine($"  resource={resource}");
    }
}
