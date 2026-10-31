using Morph.Cli.Config;

namespace Morph.Cli.Commands;

internal static class LoginCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
        var email = ArgUtil.GetOption(args, "--email") ?? Read("Email: ");
        var password = ArgUtil.GetOption(args, "--password") ?? ReadSecret("Password: ");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Usage: morph login --email <email> --password <password>");
            return 1;
        }

        using var client = CliClientFactory.Create(root, config);
        try
        {
            var token = await client.Auth.LoginAsync(email, password);
            ProjectConfigStore.SaveCredentials(root, new CredentialStore
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                Email = email,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            });

            var me = await client.Auth.GetMeAsync();
            Console.WriteLine($"Logged in as {me.Email ?? email} ({me.Id})");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Login failed: {ex.Message}");
            return 1;
        }
    }

    private static string Read(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace && buffer.Length > 0)
            {
                buffer.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }

        return buffer.ToString();
    }
}
