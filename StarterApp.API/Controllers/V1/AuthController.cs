using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using StarterApp.API.Constants;
using StarterApp.API.Data.Repositories;
using StarterApp.API.Extensions;
using StarterApp.API.Models.Auth;
using StarterApp.API.Models.Dtos;
using StarterApp.API.Models.Entities;
using StarterApp.API.Models.Requests.Auth;
using StarterApp.API.Models.Responses.Auth;
using StarterApp.API.Models.Settings;
using StarterApp.API.Services.Auth;
using StarterApp.API.Services.Core;
using StarterApp.API.Services.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static StarterApp.API.Utilities.UtilityFunctions;

namespace StarterApp.API.Controllers.V1;

[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1")]
[ApiController]
public sealed class AuthController : ServiceControllerBase
{
    private readonly IUserRepository userRepository;

    private readonly IJwtTokenService jwtTokenService;

    private readonly IUserService userService;

    private readonly IGitHubOAuthService gitHubOAuthService;

    private readonly IGoogleOAuthService googleOAuthService;

    private readonly IExternalLoginService externalLoginService;

    private readonly AuthenticationSettings authSettings;

    private readonly ILogger<AuthController> logger;

    public AuthController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IUserService userService,
        IGitHubOAuthService gitHubOAuthService,
        IGoogleOAuthService googleOAuthService,
        IExternalLoginService externalLoginService,
        IOptions<AuthenticationSettings> authSettings,
        ICorrelationIdService correlationIdService,
        ILogger<AuthController> logger)
            : base(correlationIdService)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        this.jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
        this.gitHubOAuthService = gitHubOAuthService ?? throw new ArgumentNullException(nameof(gitHubOAuthService));
        this.googleOAuthService = googleOAuthService ?? throw new ArgumentNullException(nameof(googleOAuthService));
        this.externalLoginService = externalLoginService ?? throw new ArgumentNullException(nameof(externalLoginService));
        this.authSettings = authSettings?.Value ?? throw new ArgumentNullException(nameof(authSettings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="registerUserRequest">The register request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="201">The user object and tokens.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("register", Name = nameof(RegisterAsync))]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<LoginResponse>> RegisterAsync([FromBody] RegisterUserRequest registerUserRequest, CancellationToken cancellationToken)
    {
        if (registerUserRequest == null)
        {
            return this.BadRequest();
        }

        var user = registerUserRequest.ToEntity();

        var result = await this.userRepository.CreateUserWithPasswordAsync(user, registerUserRequest.Password, cancellationToken);

        if (!result.Succeeded)
        {
            return this.BadRequest([.. result.Errors.Select(e => e.Description)]);
        }

        var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, registerUserRequest.DeviceId, cancellationToken);

        await this.userService.SendEmailConfirmationAsync(user, cancellationToken);

        var userToReturn = UserDto.FromEntity(user);

        return this.CreatedAtRoute(
            nameof(UsersController.GetUserAsync),
            new
            {
                controller = GetControllerName<UsersController>(),
                id = user.Id
            },
            new LoginResponse
            {
                Token = token,
                User = userToReturn
            });
    }

    /// <summary>
    /// Logs the user in.
    /// </summary>
    /// <param name="loginRequest">The login request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="200">The user object and tokens.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("login", Name = nameof(LoginAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest loginRequest, CancellationToken cancellationToken)
    {
        if (loginRequest == null)
        {
            return this.BadRequest();
        }

        var user = await this.userRepository.GetByUsernameAsync(loginRequest.UserName, [user => user.RefreshTokens], cancellationToken)
            ?? await this.userRepository.GetByEmailAsync(loginRequest.UserName, [user => user.RefreshTokens], cancellationToken);

        if (user == null)
        {
            return this.Unauthorized("Invalid username or password.");
        }

        var result = await this.userRepository.CheckPasswordAsync(user, loginRequest.Password, cancellationToken);

        if (!result)
        {
            return this.Unauthorized("Invalid username or password.");
        }

        var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);

        var userToReturn = UserDto.FromEntity(user);

        return this.Ok(
            new LoginResponse
            {
                Token = token,
                User = userToReturn
            });
    }

    /// <summary>
    /// Logs the user in using Google OAuth authorization code (similar to GitHub flow).
    /// </summary>
    /// <param name="loginRequest">The login request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="200">If the user was logged in.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("login/google", Name = nameof(LoginGoogleAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> LoginGoogleAsync([FromBody] OAuthCodeLoginRequest loginRequest, CancellationToken cancellationToken)
    {
        try
        {
            if (loginRequest == null)
            {
                return this.BadRequest("Request body cannot be null.");
            }

            var idToken = await this.googleOAuthService.ExchangeCodeForGoogleIdTokenAsync(loginRequest.Code, cancellationToken);

            if (string.IsNullOrWhiteSpace(idToken))
            {
                return this.Unauthorized("Invalid Google authorization code.");
            }

            var validatedToken = await this.googleOAuthService.ValidateIdTokenAsync(idToken, cancellationToken);

            var identity = new ExternalLoginIdentity
            {
                ProviderSubjectId = validatedToken.Subject,
                ProviderType = LinkedAccountType.Google,
                Email = validatedToken.Email,
                EmailVerified = validatedToken.EmailVerified,
                SuggestedUserName = validatedToken.Email?.Split('@').FirstOrDefault() ?? validatedToken.Subject
            };

            var result = await this.externalLoginService.ResolveOrProvisionUserAsync(identity, cancellationToken);

            if (!result.IsSuccess)
            {
                return this.HandleServiceFailureResult(result);
            }

            var user = result.ValueOrThrow;

            var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);
            var userToReturn = UserDto.FromEntity(user);

            return this.Ok(
                new LoginResponse
                {
                    Token = token,
                    User = userToReturn
                });
        }
        catch (HttpRequestException)
        {
            return this.Unauthorized("Unable to validate Google authorization code.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "Unexpected error during Google login.");
            return this.InternalServerError("Unable to login with Google.");
        }
    }

    /// <summary>
    /// Logs the user in using GitHub OAuth credentials.
    /// </summary>
    /// <param name="loginRequest">The login request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="200">If the user was logged in.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("login/github", Name = nameof(LoginGitHubAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> LoginGitHubAsync([FromBody] OAuthCodeLoginRequest loginRequest, CancellationToken cancellationToken)
    {
        try
        {
            if (loginRequest == null)
            {
                return this.BadRequest("Request body cannot be null.");
            }

            var githubToken = await this.gitHubOAuthService.ExchangeCodeForGithubAccessTokenAsync(loginRequest.Code, cancellationToken);

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                return this.Unauthorized("Invalid GitHub code.");
            }

            var gitHubUserInfo = await this.gitHubOAuthService.GetGitHubUserAsync(githubToken, cancellationToken);

            if (gitHubUserInfo == null || string.IsNullOrWhiteSpace(gitHubUserInfo.Login))
            {
                return this.Unauthorized("Unable to retrieve GitHub user information.");
            }

            var gitHubEmail = string.IsNullOrWhiteSpace(gitHubUserInfo.Email)
                ? (await this.gitHubOAuthService.GetGitHubEmailsAsync(githubToken, cancellationToken)).FirstOrDefault(e => e.Primary && e.Verified)?.Email
                : gitHubUserInfo.Email;

            var identity = new ExternalLoginIdentity
            {
                ProviderSubjectId = gitHubUserInfo.Id.ToString(CultureInfo.InvariantCulture),
                ProviderType = LinkedAccountType.GitHub,
                Email = gitHubEmail,
                EmailVerified = !string.IsNullOrEmpty(gitHubEmail),
                SuggestedUserName = gitHubUserInfo.Login
            };

            var result = await this.externalLoginService.ResolveOrProvisionUserAsync(identity, cancellationToken);

            if (!result.IsSuccess)
            {
                return this.HandleServiceFailureResult(result);
            }

            var user = result.ValueOrThrow;

            var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);
            var userToReturn = UserDto.FromEntity(user);

            return this.Ok(
                new LoginResponse
                {
                    Token = token,
                    User = userToReturn
                });
        }
        catch (HttpRequestException)
        {
            return this.Unauthorized("Unable to validate GitHub token.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "Unexpected error during GitHub login.");
            return this.InternalServerError("Unable to login with GitHub.");
        }
    }

    /// <summary>
    /// Handles the GitHub OAuth callback by redirecting to the UI with the authorization code.
    /// </summary>
    /// <param name="code">The authorization code from GitHub.</param>
    /// <param name="state">The optional state parameter for CSRF protection.</param>
    /// <returns>A redirect to the UI with the authorization code.</returns>
    /// <response code="302">Redirects to the UI with the authorization code or error.</response>
    [AllowAnonymous]
    [HttpGet("github/callback", Name = nameof(GitHubCallback))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GitHubCallback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return this.Redirect($"{this.authSettings.UIBaseUrl}#/auth/github/callback?error=missing_code");
        }

        var redirectUrl = $"{this.authSettings.UIBaseUrl}#/auth/github/callback?code={Uri.EscapeDataString(code)}";

        if (!string.IsNullOrWhiteSpace(state))
        {
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";
        }

        return this.Redirect(redirectUrl);
    }

    /// <summary>
    /// Handles the Google OAuth callback by redirecting to the UI with the authorization code.
    /// </summary>
    /// <param name="code">The authorization code from Google.</param>
    /// <param name="state">The optional state parameter for CSRF protection.</param>
    /// <returns>A redirect to the UI with the authorization code.</returns>
    /// <response code="302">Redirects to the UI with the authorization code or error.</response>
    [AllowAnonymous]
    [HttpGet("google/callback", Name = nameof(GoogleCallback))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleCallback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return this.Redirect($"{this.authSettings.UIBaseUrl}#/auth/google/callback?error=missing_code");
        }

        var redirectUrl = $"{this.authSettings.UIBaseUrl}#/auth/google/callback?code={Uri.EscapeDataString(code)}";

        if (!string.IsNullOrWhiteSpace(state))
        {
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";
        }

        return this.Redirect(redirectUrl);
    }

    /// <summary>
    /// Logs the user out by revoking all refresh tokens and clearing the refresh token cookie.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>No content.</returns>
    /// <response code="204">No content.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [HttpPost("logout", Name = nameof(LogoutAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (!this.User.TryGetUserId(out var userId) || userId == null)
        {
            return this.Unauthorized("You must be logged in to log out.");
        }

        var user = await this.userRepository.GetByIdAsync(userId.Value, [u => u.RefreshTokens], track: true, cancellationToken);

        if (user == null)
        {
            return this.NotFound("User not found.");
        }

        await this.jwtTokenService.RevokeAllRefreshTokensForUserAsync(user, cancellationToken);
        this.Response.Cookies.Delete(CookieKeys.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Domain = this.authSettings.CookieDomain
        });

        return this.NoContent();
    }

    /// <summary>
    /// Refreshes a user's access token. Refresh token is stored in a cookie.
    /// </summary>
    /// <param name="refreshTokenRequest">The refresh token request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A new set of tokens.</returns>
    /// <response code="200">If the token was refreshed.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided token pair was invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("refreshToken", Name = nameof(RefreshTokenAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest refreshTokenRequest, CancellationToken cancellationToken)
    {
        if (refreshTokenRequest == null)
        {
            return this.BadRequest();
        }

        // CSRF protection using Double Submit Cookie pattern. This is the only endpoint that requires CSRF token as it is the only one that uses cookies.
        // This isn't really needed as the device id is effectively a CSRF token, but it's a good practice to have it.
        var csrfHeader = this.Request.Headers[AppHeaderNames.CsrfToken].ToString();
        var csrfCookie = this.Request.Cookies[CookieKeys.CsrfToken];

        if (string.IsNullOrEmpty(csrfHeader) || csrfHeader != csrfCookie)
        {
            return this.Unauthorized("CSRF token mismatch.");
        }

        if (!this.Request.Cookies.TryGetValue(CookieKeys.RefreshToken, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return this.Unauthorized("Missing refresh token cookie");
        }

        var (isTokenEligibleForRefresh, user) = await this.jwtTokenService.IsTokenEligibleForRefreshAsync(
            refreshToken,
            refreshTokenRequest.DeviceId,
            cancellationToken);

        if (!isTokenEligibleForRefresh || user == null)
        {
            return this.Unauthorized("Invalid token.");
        }

        var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, refreshTokenRequest.DeviceId, cancellationToken, updateLastLogin: false);

        return this.Ok(
            new RefreshTokenResponse
            {
                Token = token
            });
    }



    private async Task<string> GenerateAndSaveAccessAndRefreshTokensAsync(User user, string deviceId, CancellationToken cancellationToken, bool updateLastLogin = true)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (updateLastLogin)
        {
            user.LastLogin = DateTimeOffset.UtcNow;
        }

        var token = this.jwtTokenService.GenerateJwtTokenForUser(user);
        var refreshToken = await this.jwtTokenService.GenerateAndSaveRefreshTokenForUserAsync(user, deviceId, cancellationToken);

        // Set the refresh token cookie
        this.Response.Cookies.Append(CookieKeys.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Always require HTTPS
            SameSite = SameSiteMode.None, // Required for cross-origin requests
            Expires = DateTimeOffset.UtcNow.AddMinutes(this.authSettings.RefreshTokenExpirationTimeInMinutes),
            IsEssential = true, // Mark as essential for GDPR compliance
            Domain = this.authSettings.CookieDomain // Don't set domain to restrict to exact host
        });

        // Generate and set CSRF token cookie for Double Submit Cookie pattern
        var csrfToken = Guid.NewGuid().ToString();
        this.Response.Cookies.Append(CookieKeys.CsrfToken, csrfToken, new CookieOptions
        {
            HttpOnly = false, // Must be false so JavaScript can read it
            Secure = true, // Always require HTTPS
            SameSite = SameSiteMode.None, // Required for cross-origin requests
            Expires = DateTimeOffset.UtcNow.AddMinutes(this.authSettings.RefreshTokenExpirationTimeInMinutes),
            IsEssential = true, // Mark as essential for GDPR compliance
            Domain = this.authSettings.CookieDomain // Set domain to allow cross-origin requests
        });

        return token;
    }
}