using System.Collections.Generic;

namespace StarterApp.API.Models.Settings;

public sealed record OpenApiSettings
{
    public OpenApiAuthSettings AuthSettings { get; init; } = default!;

    public bool Enabled { get; init; }

    public IReadOnlyList<string> SupportedApiVersions { get; init; } = [];
}