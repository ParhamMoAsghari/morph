namespace Morph.Cli.Commands;

internal static class ArgUtil
{
    public static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    public static Guid? GetGuidOption(string[] args, string name)
    {
        var raw = GetOption(args, name);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
