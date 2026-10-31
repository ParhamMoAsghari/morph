using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Hosting;
using Morph.Plugins.Http;
using Morph.Plugins.Process;
using Morph.Plugins.Sms;
using Morph.Plugins.Webhooks;
using Xunit;

namespace Morph.Plugins.Tests;

public sealed class PermissionGatedPluginBuilderTests
{
    [Fact]
    public void AddProcessActivity_without_permission_throws()
    {
        var builder = CreateBuilder(permissions: []);

        var act = () => builder.AddProcessActivity<FakeActivity>("plugin:test.activity");

        act.Should().Throw<PluginPermissionException>()
            .Where(ex => ex.Permission == PluginPermissions.ProcessActivities);
    }

    [Fact]
    public void AddProcessActivity_with_permission_registers()
    {
        var activities = new ProcessActivityRegistry();
        var builder = CreateBuilder(
            permissions: [PluginPermissions.ProcessActivities],
            activities: activities);

        builder.AddProcessActivity<FakeActivity>("plugin:test.activity");

        activities.TryGet("plugin:test.activity", out var descriptor).Should().BeTrue();
        descriptor.PluginId.Should().Be("morph.test");
        descriptor.ActivityType.Should().Be(typeof(FakeActivity));
    }

    [Fact]
    public void AddSmsProvider_without_permission_throws()
    {
        var builder = CreateBuilder(permissions: [PluginPermissions.ProcessActivities]);

        var act = () => builder.AddSmsProvider<FakeSmsProvider>();

        act.Should().Throw<PluginPermissionException>()
            .Where(ex => ex.Permission == PluginPermissions.SmsSend);
    }

    [Fact]
    public void GetDataClient_without_data_permission_throws()
    {
        var builder = CreateBuilder(permissions: [PluginPermissions.HttpOutbound]);

        var act = () => builder.GetDataClient();

        act.Should().Throw<PluginPermissionException>();
    }

    [Fact]
    public async Task GetDataClient_with_read_only_denies_write()
    {
        var builder = CreateBuilder(permissions: [PluginPermissions.DataRead]);

        var client = builder.GetDataClient();

        var act = async () => await client.UpsertAsync(
            "Thing",
            "1",
            new Dictionary<string, object?>(),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PluginPermissionException>();
        ex.Which.Permission.Should().Be(PluginPermissions.DataWrite);
    }

    [Fact]
    public void AddInboundWebhook_with_permission_registers()
    {
        var webhooks = new InboundWebhookRegistry();
        var builder = CreateBuilder(
            permissions: [PluginPermissions.HttpInbound],
            webhooks: webhooks);

        builder.AddInboundWebhook("updates", new FakeWebhook());

        webhooks.TryResolve("morph.test", "updates", out _).Should().BeTrue();
    }

    private static PermissionGatedPluginBuilder CreateBuilder(
        IEnumerable<string> permissions,
        IProcessActivityRegistry? activities = null,
        ISmsProviderRegistry? sms = null,
        IInboundWebhookRegistry? webhooks = null,
        IPluginHttpRouteRegistry? routes = null)
    {
        return new PermissionGatedPluginBuilder(
            "morph.test",
            new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
            activities ?? new ProcessActivityRegistry(),
            sms ?? new SmsProviderRegistry(),
            webhooks ?? new InboundWebhookRegistry(),
            routes ?? new PluginHttpRouteRegistry(),
            new SsrfSafeHttpClientFactory(new ThrowingHttpClientFactory()));
    }

    private sealed class FakeActivity : IProcessActivity
    {
        public string TypeName => "plugin:test.activity";

        public Task ExecuteAsync(IProcessActivityContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeSmsProvider : ISmsProvider
    {
        public string Name => "fake";

        public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(new SmsSendResult(true));
    }

    private sealed class FakeWebhook : IInboundWebhookHandler
    {
        public Task HandleAsync(HttpContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new NotSupportedException("HTTP not used in these tests.");
    }
}
