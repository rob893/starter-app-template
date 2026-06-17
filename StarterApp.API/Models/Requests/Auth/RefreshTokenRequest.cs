using System.ComponentModel.DataAnnotations;

namespace StarterApp.API.Models.Requests.Auth;

public sealed record RefreshTokenRequest
{
    [Required]
    public string DeviceId { get; init; } = default!;
}