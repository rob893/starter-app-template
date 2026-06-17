using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StarterApp.API.Constants;
using StarterApp.API.Controllers.V1;
using StarterApp.API.Data.Repositories;
using StarterApp.API.Models.Entities;
using StarterApp.API.Models.Requests.Auth;
using StarterApp.API.Models.Settings;
using StarterApp.API.Services.Auth;
using StarterApp.API.Services.Core;
using StarterApp.API.Services.Domain;

namespace StarterApp.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="AuthController"/> (v1).
/// </summary>
public sealed class AuthControllerTests
{
    private const string DeviceId = "device-1";

    private readonly Mock<IUserRepository> userRepositoryMock;
    private readonly Mock<IJwtTokenService> jwtTokenServiceMock;
    private readonly Mock<IUserService> userServiceMock;
    private readonly Mock<IGitHubOAuthService> gitHubOAuthServiceMock;
    private readonly Mock<IGoogleOAuthService> googleOAuthServiceMock;
    private readonly Mock<IExternalLoginService> externalLoginServiceMock;
    private readonly Mock<ICorrelationIdService> correlationIdServiceMock;
    private readonly AuthController sut;

    public AuthControllerTests()
    {
        this.userRepositoryMock = new Mock<IUserRepository>();
        this.jwtTokenServiceMock = new Mock<IJwtTokenService>();
        this.userServiceMock = new Mock<IUserService>();
        this.gitHubOAuthServiceMock = new Mock<IGitHubOAuthService>();
        this.googleOAuthServiceMock = new Mock<IGoogleOAuthService>();
        this.externalLoginServiceMock = new Mock<IExternalLoginService>();
        this.correlationIdServiceMock = new Mock<ICorrelationIdService>();
        this.correlationIdServiceMock.Setup(s => s.CorrelationId).Returns("corr-id");

        this.jwtTokenServiceMock
            .Setup(s => s.GenerateJwtTokenForUser(It.IsAny<User>()))
            .Returns("access-token");
        this.jwtTokenServiceMock
            .Setup(s => s.GenerateAndSaveRefreshTokenForUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var settings = Options.Create(new AuthenticationSettings
        {
            APISecret = "this-is-a-sufficiently-long-api-secret-of-at-least-64-characters!!",
            TokenIssuer = "https://issuer",
            TokenAudience = "https://audience",
            RefreshTokenExpirationTimeInMinutes = 60
        });

        this.sut = new AuthController(
            this.userRepositoryMock.Object,
            this.jwtTokenServiceMock.Object,
            this.userServiceMock.Object,
            this.gitHubOAuthServiceMock.Object,
            this.googleOAuthServiceMock.Object,
            this.externalLoginServiceMock.Object,
            settings,
            this.correlationIdServiceMock.Object,
            NullLogger<AuthController>.Instance);

        this.sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task RegisterAsync_Success_Returns201()
    {
        var userManagerMock = BuildUserManagerMock();
        userManagerMock
            .Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
            .ReturnsAsync("email-token");
        this.userRepositoryMock.Setup(r => r.UserManager).Returns(userManagerMock.Object);
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "newuser@example.com",
            Password = "Password1!",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await this.sut.RegisterAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterAsync_CreateFails_ReturnsBadRequest()
    {
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email taken" }));

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "taken@example.com",
            Password = "Password1!",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginAsync_Success_Returns200()
    {
        var user = BuildUser();
        this.userRepositoryMock
            .Setup(r => r.GetByUsernameAsync("jane", It.IsAny<Expression<System.Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        this.userRepositoryMock
            .Setup(r => r.CheckPasswordAsync(user, "Password1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "jane", Password = "Password1!", DeviceId = DeviceId }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_Returns401()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<Expression<System.Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<Expression<System.Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "ghost", Password = "x", DeviceId = DeviceId }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Returns401()
    {
        var user = BuildUser();
        this.userRepositoryMock
            .Setup(r => r.GetByUsernameAsync("jane", It.IsAny<Expression<System.Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        this.userRepositoryMock
            .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "jane", Password = "wrong", DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await this.sut.LoginAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RefreshTokenAsync_CsrfMismatch_Returns401()
    {
        this.sut.ControllerContext.HttpContext.Request.Headers[AppHeaderNames.CsrfToken] = "header-value";
        this.sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{CookieKeys.CsrfToken}=different-value";

        var result = await this.sut.RefreshTokenAsync(new RefreshTokenRequest { DeviceId = DeviceId }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_MissingRefreshCookie_Returns401()
    {
        this.sut.ControllerContext.HttpContext.Request.Headers[AppHeaderNames.CsrfToken] = "csrf-value";
        this.sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{CookieKeys.CsrfToken}=csrf-value";

        var result = await this.sut.RefreshTokenAsync(new RefreshTokenRequest { DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidCsrfAndCookieButIneligibleToken_Returns401()
    {
        this.sut.ControllerContext.HttpContext.Request.Headers[AppHeaderNames.CsrfToken] = "csrf-value";
        this.sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{CookieKeys.CsrfToken}=csrf-value; {CookieKeys.RefreshToken}=some-refresh-token";

        this.jwtTokenServiceMock
            .Setup(s => s.IsTokenEligibleForRefreshAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (User?)null));

        var result = await this.sut.RefreshTokenAsync(new RefreshTokenRequest { DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await this.sut.RefreshTokenAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static Mock<UserManager<User>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static User BuildUser() => new()
    {
        Id = 7,
        UserName = "jane",
        Email = "jane@example.com",
        RefreshTokens = []
    };
}
