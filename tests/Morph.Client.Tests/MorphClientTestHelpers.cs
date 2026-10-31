using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morph.Client.Tests;

internal static class MorphClientTestHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    internal static void RoundTripJson<T>(T value, Action<T, T>? assert = null)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var copy = JsonSerializer.Deserialize<T>(json, JsonOptions)!;
        assert?.Invoke(value, copy);
    }

    internal sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request, cancellationToken));
    }

    internal static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    internal static HttpResponseMessage EmptyOk() => new(HttpStatusCode.OK);

    internal static MorphClient CreateClient(
        HttpMessageHandler handler,
        Action<MorphClientOptions>? configure = null)
    {
        var options = new MorphClientOptions { BaseAddress = new Uri("http://morph.test/") };
        configure?.Invoke(options);
        var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = options.BaseAddress };
        return new MorphClient(options, http);
    }
}
