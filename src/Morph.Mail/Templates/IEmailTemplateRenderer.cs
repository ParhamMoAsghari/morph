namespace Morph.Mail.Templates;

public interface IEmailTemplateRenderer
{
    Task<(string Subject, string HtmlBody, string TextBody)> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default);
}
