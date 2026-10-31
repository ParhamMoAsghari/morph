using Morph.Messaging.Outbox;

namespace Morph.Infrastructure.Jobs;

/// <summary>
/// Hangfire entry point that drains a batch of outbox messages (including mail.send).
/// </summary>
public sealed class EmailSendJob
{
    public const string JobId = "morph-email-send-drain";

    private readonly IOutboxDispatchService _dispatch;

    public EmailSendJob(IOutboxDispatchService dispatch)
    {
        _dispatch = dispatch;
    }

    public Task Execute() => _dispatch.DispatchBatchAsync(CancellationToken.None);
}
