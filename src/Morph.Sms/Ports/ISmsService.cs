using Morph.BuildingBlocks.Results;
using Morph.Plugins.Abstractions;

namespace Morph.Sms.Ports;

public interface ISmsService
{
    Task<Result<SmsSendResult>> SendAsync(
        string to,
        string body,
        string? from = null,
        CancellationToken cancellationToken = default);
}

public sealed class SendSmsRequest
{
    public string? To { get; init; }

    public string? Body { get; init; }

    /// <summary>Alias accepted by the Dev Panel / SDKs.</summary>
    public string? Message { get; init; }

    public string? From { get; init; }
}
