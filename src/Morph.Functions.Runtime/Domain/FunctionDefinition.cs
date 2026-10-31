namespace Morph.Functions.Runtime.Domain;

public enum FunctionRuntimeKind
{
    TypeScriptStub,
    DotNetStub
}

public enum FunctionTriggerKind
{
    Http,
    Lifecycle,
    Process
}

public sealed class FunctionDefinition
{
    public Guid FunctionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid ProjectId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public FunctionRuntimeKind Runtime { get; set; }

    public FunctionTriggerKind Trigger { get; set; } = FunctionTriggerKind.Http;

    /// <summary>Stub source or entrypoint marker (not executed in a real sandbox yet).</summary>
    public string? Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class FunctionInvokeResult
{
    public required string FunctionSlug { get; init; }

    public bool Succeeded { get; init; }

    public object? Output { get; init; }

    public string? Error { get; init; }

    public TimeSpan Duration { get; init; }
}
