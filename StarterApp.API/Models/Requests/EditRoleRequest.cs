using System.Collections.Generic;

namespace StarterApp.API.Models.Requests;

public sealed record EditRoleRequest
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];
}