namespace Morph.Access.Domain;

/// <summary>
/// Minimal JSONLogic-like condition AST for ABAC predicates.
/// Supported ops: eq, neq, and, or, not, in, gt, gte, lt, lte.
/// Operand refs: "subject.sub", "subject.roles", "resource.owner_id", "resource.status", "env.name".
/// </summary>
public sealed class ConditionExpression
{
    public required string Op { get; init; }

    /// <summary>Left operand: attribute path or literal.</summary>
    public ConditionOperand? Left { get; init; }

    /// <summary>Right operand: attribute path or literal.</summary>
    public ConditionOperand? Right { get; init; }

    /// <summary>Children for and/or/not.</summary>
    public IReadOnlyList<ConditionExpression>? Children { get; init; }

    public static ConditionExpression Eq(string leftPath, string rightPath) => new()
    {
        Op = "eq",
        Left = ConditionOperand.Attr(leftPath),
        Right = ConditionOperand.Attr(rightPath)
    };

    public static ConditionExpression EqLiteral(string leftPath, object? literal) => new()
    {
        Op = "eq",
        Left = ConditionOperand.Attr(leftPath),
        Right = ConditionOperand.Literal(literal)
    };

    public static ConditionExpression And(params ConditionExpression[] children) => new()
    {
        Op = "and",
        Children = children
    };

    public static ConditionExpression Or(params ConditionExpression[] children) => new()
    {
        Op = "or",
        Children = children
    };

    public static ConditionExpression Not(ConditionExpression child) => new()
    {
        Op = "not",
        Children = [child]
    };
}

public sealed class ConditionOperand
{
    public bool IsAttribute { get; init; }

    public string? AttributePath { get; init; }

    public object? LiteralValue { get; init; }

    public static ConditionOperand Attr(string path) => new()
    {
        IsAttribute = true,
        AttributePath = path
    };

    public static ConditionOperand Literal(object? value) => new()
    {
        IsAttribute = false,
        LiteralValue = value
    };
}
