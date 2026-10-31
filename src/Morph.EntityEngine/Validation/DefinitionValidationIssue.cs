namespace Morph.EntityEngine.Validation;

public sealed record DefinitionValidationIssue(string Code, string Message, string? Path = null);
