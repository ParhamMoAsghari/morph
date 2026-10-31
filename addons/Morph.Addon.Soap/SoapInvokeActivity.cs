using System.Net.Http.Headers;
using System.Text;
using Morph.Plugins.Abstractions;

namespace Morph.Addon.Soap;

/// <summary>
/// Generic SOAP invoke activity. Endpoint/action/body come from process variables.
/// Outbound HTTP goes through the host SSRF-safe client.
/// </summary>
public sealed class SoapInvokeActivity : IProcessActivity
{
    public string TypeName => "plugin:morph.soap.invoke";

    public async Task ExecuteAsync(IProcessActivityContext context, CancellationToken cancellationToken)
    {
        var endpoint = RequireString(context, "soap.endpoint");
        var action = GetString(context, "soap.action");
        var body = RequireString(context, "soap.body");
        var contentType = GetString(context, "soap.contentType") ?? "text/xml; charset=utf-8";

        using var client = context.Http.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (!string.IsNullOrWhiteSpace(action))
        {
            request.Headers.TryAddWithoutValidation("SOAPAction", action);
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        context.SetVariable("soap.statusCode", (int)response.StatusCode);
        context.SetVariable("soap.response", responseBody);
        context.SetVariable("soap.succeeded", response.IsSuccessStatusCode);
    }

    private static string RequireString(IProcessActivityContext context, string key)
    {
        var value = GetString(context, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Variable '{key}' is required for {nameof(SoapInvokeActivity)}.");
        }

        return value;
    }

    private static string? GetString(IProcessActivityContext context, string key) =>
        context.Variables.TryGetValue(key, out var value) ? value?.ToString() : null;
}
