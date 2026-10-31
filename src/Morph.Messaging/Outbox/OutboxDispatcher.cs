using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Morph.Messaging.Outbox;

/// <summary>
/// Polls the transactional outbox on an interval. Hangfire also drains via <see cref="IOutboxDispatchService"/>.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IOutboxDispatchService _dispatch;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly TimeSpan _pollInterval;

    public OutboxDispatcher(
        IOutboxDispatchService dispatch,
        ILogger<OutboxDispatcher> logger,
        TimeSpan? pollInterval = null)
    {
        _dispatch = dispatch;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox dispatcher started (interval={Interval}).", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _dispatch.DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher iteration failed.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
