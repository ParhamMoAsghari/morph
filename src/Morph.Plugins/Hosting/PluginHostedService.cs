using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Morph.Plugins.Loading;
using Morph.Plugins.Options;

namespace Morph.Plugins.Hosting;

public sealed class PluginHostedService : IHostedService
{
    private readonly PluginLoader _loader;
    private readonly IOptions<PluginHostOptions> _options;
    private readonly ILogger<PluginHostedService> _logger;

    public PluginHostedService(
        PluginLoader loader,
        IOptions<PluginHostOptions> options,
        ILogger<PluginHostedService> logger)
    {
        _loader = loader;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = _options.Value.Path;
        _logger.LogInformation("Starting Morph plugin host; scanning '{Path}'.", Path.GetFullPath(path));
        await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Plugin host started with {Count} loaded plugin(s).", _loader.Loaded.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
