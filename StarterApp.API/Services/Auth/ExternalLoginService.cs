using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarterApp.API.Core;
using StarterApp.API.Data.Repositories;
using StarterApp.API.Models.Auth;
using StarterApp.API.Models.Entities;
using StarterApp.API.Services.Domain;

namespace StarterApp.API.Services.Auth;

/// <summary>
/// Default implementation of <see cref="IExternalLoginService"/> that owns the shared
/// resolve-or-provision flow for verified external identities.
/// </summary>
public sealed class ExternalLoginService : IExternalLoginService
{
    private readonly IUserRepository userRepository;

    private readonly IUserService userService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalLoginService"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="userService">The user service.</param>
    public ExternalLoginService(IUserRepository userRepository, IUserService userService)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    /// <inheritdoc />
    public async Task<Result<User>> ResolveOrProvisionUserAsync(ExternalLoginIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var user = await this.userRepository.GetByLinkedAccountAsync(identity.ProviderSubjectId, identity.ProviderType, [user => user.RefreshTokens], cancellationToken);

        if (user != null)
        {
            return Result<User>.Success(user);
        }

        // Try to find user by email if no linked account exists
        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            user = await this.userRepository.GetByEmailAsync(identity.Email, [user => user.RefreshTokens], cancellationToken);
        }

        if (user != null)
        {
            // Link the external account to the existing user
            user.LinkedAccounts.Add(new LinkedAccount
            {
                Id = identity.ProviderSubjectId,
                LinkedAccountType = identity.ProviderType
            });

            user.EmailConfirmed = user.EmailConfirmed || identity.EmailVerified;

            var updated = await this.userRepository.SaveChangesAsync(cancellationToken);

            if (updated == 0)
            {
                return Result<User>.Failure(DomainErrorType.Unknown, $"Unable to link {identity.ProviderType} account to existing user.");
            }

            return Result<User>.Success(user);
        }

        // Create a new user with the external account
        var newUser = new User
        {
            UserName = identity.SuggestedUserName,
            Email = identity.Email,
            EmailConfirmed = identity.EmailVerified,
            LinkedAccounts =
            [
                new LinkedAccount
                {
                    Id = identity.ProviderSubjectId,
                    LinkedAccountType = identity.ProviderType
                }
            ]
        };

        var createResult = await this.userRepository.CreateUserWithoutPasswordAsync(newUser, cancellationToken);

        if (!createResult.Succeeded)
        {
            return Result<User>.Failure(DomainErrorType.Validation, string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        // Optionally send confirmation email if email is not confirmed
        if (!newUser.EmailConfirmed && !string.IsNullOrWhiteSpace(newUser.Email))
        {
            await this.userService.SendEmailConfirmationAsync(newUser, cancellationToken);
        }

        return Result<User>.Success(newUser);
    }
}
