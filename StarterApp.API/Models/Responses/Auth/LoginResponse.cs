using StarterApp.API.Models.Dtos;

namespace StarterApp.API.Models.Responses.Auth;

public sealed record LoginResponse
{
    public required string Token { get; init; }

    public required UserDto User { get; init; }
}