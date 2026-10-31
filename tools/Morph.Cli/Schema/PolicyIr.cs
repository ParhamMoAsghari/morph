namespace Morph.Cli.Schema;

public sealed class PolicyIr
{
    public string ApiVersion { get; set; } = "morph/v1";

    public string Kind { get; set; } = "Policy";

    public string Name { get; set; } = "";

    public Guid? PolicyId { get; set; }

    public string Effect { get; set; } = "Allow";

    public List<string> Actions { get; set; } = [];

    public List<string> Resources { get; set; } = [];

    public object? Condition { get; set; }

    public int Priority { get; set; }

    public bool FieldMaskOnly { get; set; }

    public bool EmitRowFilterObligation { get; set; }

    public bool Publish { get; set; }
}

public sealed class RolesIr
{
    public string ApiVersion { get; set; } = "morph/v1";

    public string Kind { get; set; } = "Roles";

    public List<RoleIr> Roles { get; set; } = [];
}

public sealed class RoleIr
{
    public string Name { get; set; } = "";

    public string? DisplayName { get; set; }

    public List<string>? Inherits { get; set; }
}
