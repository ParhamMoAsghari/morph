using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Morph.Plugins.Abstractions;

namespace Morph.Addon.Bale;

/// <summary>
/// Accepts Bale update payloads at POST /plugins/v1/morph.bale/updates.
/// Signature verification uses project secrets when Morph.Crypto injection is available.
/// </summary>
public sealed class BaleWebhookHandler : IInboundWebhookHandler
{
    public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Stub: log-shaped acknowledgement. Process/Function triggers arrive with Process Engine.
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsJsonAsync(
            new
            {
                ok = true,
                received = true,
                updateId = document.RootElement.TryGetProperty("update_id", out var id)
                    ? id.ToString()
                    : null
            },
            cancellationToken).ConfigureAwait(false);
    }
}
