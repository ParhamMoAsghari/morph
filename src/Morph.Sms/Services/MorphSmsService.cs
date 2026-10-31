using Morph.BuildingBlocks.Results;
using Morph.Plugins.Abstractions;
using Morph.Sms.Ports;

namespace Morph.Sms.Services;

/// <summary>
/// Product facade over <see cref="ISmsSender"/> (plugin providers such as Mellipayamak).
/// </summary>
public sealed class MorphSmsService : ISmsService
{
    private readonly ISmsSender _sender;

    public MorphSmsService(ISmsSender sender)
    {
        _sender = sender;
    }

    public async Task<Result<SmsSendResult>> SendAsync(
        string to,
        string body,
        string? from = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return Result.Failure<SmsSendResult>(Error.Validation("SMS 'to' is required."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<SmsSendResult>(Error.Validation("SMS 'body' is required."));
        }

        var result = await _sender
            .SendAsync(new SmsMessage(to.Trim(), body, from?.Trim()), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Result.Failure<SmsSendResult>(
                Error.Failure(result.Error ?? "SMS send failed."));
        }

        return Result.Success(result);
    }
}
