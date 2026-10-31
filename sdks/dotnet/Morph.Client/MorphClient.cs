using Morph.Client.Internal;

namespace Morph.Client;

/// <summary>
/// Thin HttpClient facade for Morph Platform + Runtime APIs.
/// </summary>
public sealed class MorphClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly MorphHttp _http;

    public MorphClient(MorphClientOptions options, HttpClient? httpClient = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (httpClient is null)
        {
            _httpClient = new HttpClient { BaseAddress = options.BaseAddress };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
            _httpClient.BaseAddress ??= options.BaseAddress;
        }

        _http = new MorphHttp(_httpClient, Options);
        Auth = new AuthClient(_http);
        Tenancy = new TenancyClient(_http);
        Definitions = new DefinitionsClient(_http);
        Data = new DataClient(_http);
        Files = new FilesClient(_http);
        Access = new AccessClient(_http);
        Migrations = new MigrationsClient(_http);
        Processes = new ProcessesClient(_http);
    }

    public MorphClientOptions Options { get; }

    public AuthClient Auth { get; }
    public TenancyClient Tenancy { get; }
    public DefinitionsClient Definitions { get; }
    public DataClient Data { get; }
    public FilesClient Files { get; }
    public AccessClient Access { get; }
    public MigrationsClient Migrations { get; }
    public ProcessesClient Processes { get; }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
