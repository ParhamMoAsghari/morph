namespace Morph.Cli.Commands;

internal static class DevCommand
{
    public static int Run(string[] args)
    {
        _ = args;
        Console.WriteLine("""
            morph dev — local stack helper (MVP stub)

            Preferred local workflow:
              1. cd deploy && docker compose up -d postgres redis mailpit
              2. docker compose --profile storage up -d   # optional MinIO
              3. dotnet run --project src/Morph.Host --launch-profile http
              4. morph login --email … --password …
              5. morph push && morph publish && morph migrate --apply

            Full Compose-orchestrated `morph dev` (auto start Host) lands after Phase 5 MVP.
            """);
        return 0;
    }
}
