namespace Morph.EntityEngine.Domain;

/// <summary>
/// How a parent includes a child definition.
/// Embed = value-object semantics owned by parent; Reference = independent lifecycle/identity.
/// </summary>
public enum ComponentIncludeMode
{
    Embed = 0,
    Reference = 1
}
