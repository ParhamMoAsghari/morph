using Morph.Cli;
using Morph.Cli.Commands;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        PrintHelp();
        return args.Length == 0 ? 1 : 0;
    }

    var command = args[0].ToLowerInvariant();
    var rest = args.Skip(1).ToArray();

    return command switch
    {
        "plugin" => PluginCommands.Run(args),
        "init" => InitCommand.Run(rest),
        "login" => await LoginCommand.RunAsync(rest),
        "link" => LinkCommand.Run(rest),
        "pull" => await PullCommand.RunAsync(rest),
        "push" => await PushCommand.RunAsync(rest),
        "diff" => await DiffCommand.RunAsync(rest),
        "publish" => await PublishCommand.RunAsync(rest),
        "migrate" => await MigrateCommand.RunAsync(rest),
        "generate" => await GenerateCommand.RunAsync(rest),
        "dev" => DevCommand.Run(rest),
        "access" => await RunAccessAsync(rest),
        "process" => await ProcessCommands.RunAsync(rest),
        _ => Unknown(command)
    };
}

static async Task<int> RunAccessAsync(string[] args)
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        Console.WriteLine("Usage: morph access simulate --principal <sub> --action <action> --resource <pattern>");
        return args.Length == 0 ? 1 : 0;
    }

    return args[0].ToLowerInvariant() switch
    {
        "simulate" => await AccessSimulateCommand.RunAsync(args.Skip(1).ToArray()),
        _ => Unknown($"access {args[0]}")
    };
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintHelp();
    return 1;
}

static bool IsHelp(string value) => value is "-h" or "--help" or "help";

static void PrintHelp()
{
    Console.WriteLine("""
        Morph CLI — code-first Morph BaaS

        Usage:
          morph init [--name <name>] [--api <url>] [--force]
          morph login --email <email> --password <password>
          morph link --project <guid> --env <guid> [--api <url>]
          morph pull
          morph push
          morph diff [--exit-code]
          morph publish [--slug <slug>] [--version <guid>]
          morph migrate [--to <versionId>] [--from <versionId>] [--plan|--apply] [--slug <slug>]
          morph generate [--lang ts|cs|all] [--check] [--from <effective-model.json>]
          morph access simulate --principal <sub> --action <action> --resource <pattern>
          morph process push|diff|publish|run|logs
          morph dev
          morph plugin list|install …

        Environment:
          MORPH_Plugins__Path              Plugins directory
          MORPH_Auth__PlatformClientSecret OpenIddict client secret (login)
        """);
}
