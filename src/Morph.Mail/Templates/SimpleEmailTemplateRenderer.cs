namespace Morph.Mail.Templates;

/// <summary>
/// Minimal {{key}} template renderer (no external templating dependency).
/// </summary>
public sealed class SimpleEmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly Dictionary<string, (string Subject, string Html, string Text)> Templates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["verify-email"] = (
                "Verify your Morph email",
                """
                <html><body>
                <p>Hi {{display_name}},</p>
                <p>Confirm your Morph account by opening this link:</p>
                <p><a href="{{verify_url}}">{{verify_url}}</a></p>
                <p>If you did not register, ignore this message.</p>
                </body></html>
                """,
                """
                Hi {{display_name}},

                Confirm your Morph account:
                {{verify_url}}

                If you did not register, ignore this message.
                """),
            ["reset-password"] = (
                "Reset your Morph password",
                """
                <html><body>
                <p>Hi {{display_name}},</p>
                <p>Reset your password using this link (expires soon):</p>
                <p><a href="{{reset_url}}">{{reset_url}}</a></p>
                <p>If you did not request a reset, ignore this message.</p>
                </body></html>
                """,
                """
                Hi {{display_name}},

                Reset your password:
                {{reset_url}}

                If you did not request a reset, ignore this message.
                """)
        };

    public Task<(string Subject, string HtmlBody, string TextBody)> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Templates.TryGetValue(templateName, out var tpl))
        {
            throw new InvalidOperationException($"Unknown email template '{templateName}'.");
        }

        return Task.FromResult((
            Render(tpl.Subject, model),
            Render(tpl.Html, model),
            Render(tpl.Text, model)));
    }

    private static string Render(string source, IReadOnlyDictionary<string, object?> model)
    {
        var result = source;
        foreach (var (key, value) in model)
        {
            result = result.Replace("{{" + key + "}}", value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
