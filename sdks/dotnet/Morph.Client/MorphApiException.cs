namespace Morph.Client;

public sealed class MorphApiException : Exception
{
    public MorphApiException(int statusCode, string? body, string? message = null)
        : base(message ?? $"Morph API returned {(int)statusCode}: {Truncate(body)}")
    {
        StatusCode = statusCode;
        ResponseBody = body;
    }

    public int StatusCode { get; }

    public string? ResponseBody { get; }

    private static string Truncate(string? body) =>
        string.IsNullOrWhiteSpace(body) ? "(empty)" :
        body.Length <= 400 ? body : body[..400] + "…";
}
