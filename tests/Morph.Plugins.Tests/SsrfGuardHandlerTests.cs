using System.Net;
using FluentAssertions;
using Morph.BuildingBlocks.Security;
using Morph.Plugins.Http;
using Xunit;

namespace Morph.Plugins.Tests;

public sealed class SsrfGuardHandlerTests
{
    [Theory]
    [InlineData("http://127.0.0.1/x")]
    [InlineData("http://10.0.0.5/x")]
    [InlineData("http://192.168.1.1/x")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("http://0.0.0.0/x")]
    [InlineData("ftp://example.com/x")]
    public async Task EnsurePublicEndpoint_blocks_private_literals(string uri)
    {
        var act = async () => await SsrfGuardHandler.EnsurePublicEndpointAsync(new Uri(uri), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EnsurePublicEndpoint_allows_public_https_hostname_shape()
    {
        var act = async () => await SsrfGuardHandler.EnsurePublicEndpointAsync(
            new Uri("https://example.com/api"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.0.1")]
    [InlineData("169.254.1.1")]
    public void IsBlockedAddress_detects_private_ipv4(string ip)
    {
        SsrfGuard.IsBlockedAddress(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Fact]
    public void IsBlockedAddress_allows_public_ipv4()
    {
        SsrfGuard.IsBlockedAddress(IPAddress.Parse("8.8.8.8")).Should().BeFalse();
    }

    [Fact]
    public async Task Handler_blocks_loopback_before_send()
    {
        var inner = new CountingHandler();
        var handler = new SsrfGuardHandler { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);
        var act = async () => await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1/"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        inner.SendCount.Should().Be(0);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
