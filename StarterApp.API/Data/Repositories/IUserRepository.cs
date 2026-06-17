using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using StarterApp.API.Core;
using StarterApp.API.Models.Entities;
using StarterApp.API.Models.QueryParameters;
using Microsoft.AspNetCore.Identity;

namespace StarterApp.API.Data.Repositories;

/// <summary>
/// Repository interface for user data access.
/// </summary>
public interface IUserRepository : IRepository<User, CursorPaginationQueryParameters>
{
    /// <summary>Gets the ASP.NET Identity UserManager.</summary>
    UserManager<User> UserManager { get; }

    /// <summary>Creates a user without a password (for social login flows).</summary>
    Task<IdentityResult> CreateUserWithoutPasswordAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Creates a user with the given password.</summary>
    Task<IdentityResult> CreateUserWithPasswordAsync(User user, string password, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by username using the default includes.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by username with additional includes.</summary>
    Task<User?> GetByUsernameAsync(string username, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by email with additional includes.</summary>
    Task<User?> GetByEmailAsync(string email, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by a linked OAuth account.</summary>
    Task<User?> GetByLinkedAccountAsync(string id, LinkedAccountType accountType, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>Verifies a user's password via the SignInManager.</summary>
    Task<bool> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns a cursor-paginated list of roles.</summary>
    Task<CursorPaginatedList<Role, int>> GetRolesAsync(CursorPaginationQueryParameters searchParams, CancellationToken cancellationToken = default);

    /// <summary>Returns all roles.</summary>
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all refresh tokens for a device ID.</summary>
    Task<IReadOnlyList<RefreshToken>> GetRefreshTokensForDeviceAsync(string deviceId, bool track = true, CancellationToken cancellationToken = default);
}
