using System.Text.Json;

namespace Morph.Client.Models;

public sealed class RoleDto
{
    public Guid RoleId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
}

public sealed record CreateRoleRequest(string Name, string? DisplayName, IReadOnlyList<string>? Inherits);

public sealed record BindRoleRequest(string PrincipalId, string RoleName, Guid? EnvironmentId = null);

public sealed class PolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "Allow";
    public List<string> Actions { get; set; } = [];
    public List<string> Resources { get; set; } = [];
    public string Status { get; set; } = "Draft";
}

public sealed record CreatePolicyRequest(
    string Name,
    string Effect,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Resources,
    JsonElement? Condition = null,
    int Priority = 0,
    Guid? EnvironmentId = null,
    bool FieldMaskOnly = false,
    bool EmitRowFilterObligation = false);
