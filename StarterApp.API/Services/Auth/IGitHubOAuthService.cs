using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarterApp.API.Models.GitHub;

namespace StarterApp.API.Services.Auth;

/// <summary>
/// Service for validating GitHub OAuth tokens and retrieving user information
/// </summary>
public interface IGitHubOAuthService
{
    Task<string> ExchangeCodeForGithubAccessTokenAsync(string code, CancellationToken cancellationToken);

    Task<GitHubUser> GetGitHubUserAsync(string accessToken, CancellationToken cancellationToken);

    Task<List<GitHubEmail>> GetGitHubEmailsAsync(string accessToken, CancellationToken cancellationToken);
}