using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using StarterApp.API.Constants;
using StarterApp.API.Core;
using StarterApp.API.Extensions;
using StarterApp.API.Models.Entities;
using StarterApp.API.Models.QueryParameters;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace StarterApp.API.Data.Repositories;

/// <summary>
/// Repository for user data access.
/// </summary>
public sealed class UserRepository : Repository<User, CursorPaginationQueryParameters>, IUserRepository
{
    private readonly SignInManager<User> signInManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="signInManager">The Identity sign-in manager.</param>
    public UserRepository(DataContext context, UserManager<User> userManager, SignInManager<User> signInManager)
        : base(context)
    {
        this.UserManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        this.signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
    }

    /// <inheritdoc />
    public UserManager<User> UserManager { get; init; }

    /// <inheritdoc />
    public async Task<IdentityResult> CreateUserWithoutPasswordAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        user.Created = DateTimeOffset.UtcNow;
        user.LastPasswordChange = DateTimeOffset.UtcNow;
        user.LastEmailChange = DateTimeOffset.UtcNow;
        user.LastUsernameChange = DateTimeOffset.UtcNow;
        var created = await this.UserManager.CreateAsync(user);

        if (!created.Succeeded)
        {
            return created;
        }

        await this.UserManager.AddToRoleAsync(user, UserRoleName.User);

        return created;
    }

    /// <inheritdoc />
    public async Task<IdentityResult> CreateUserWithPasswordAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        user.Created = DateTimeOffset.UtcNow;
        user.LastPasswordChange = DateTimeOffset.UtcNow;
        user.LastEmailChange = DateTimeOffset.UtcNow;
        user.LastUsernameChange = DateTimeOffset.UtcNow;
        var created = await this.UserManager.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            return created;
        }

        await this.UserManager.AddToRoleAsync(user, UserRoleName.User);

        return created;
    }

    /// <inheritdoc />
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var normalizedUsername = username.ToUpperInvariant();

        IQueryable<User> query = this.Context.Users;
        query = this.AddIncludes(query);

        return query.OrderBy(e => e.Id).FirstOrDefaultAsync(user => user.NormalizedUserName == normalizedUsername, cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> GetByUsernameAsync(string username, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var normalizedUsername = username.ToUpperInvariant();

        IQueryable<User> query = this.Context.Users;
        query = this.AddIncludes(query);
        query = includes.Aggregate(query, (current, includeProperty) => current.Include(includeProperty));

        return query.OrderBy(e => e.Id).FirstOrDefaultAsync(user => user.NormalizedUserName == normalizedUsername, cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var normalizedEmail = email.ToUpperInvariant();

        IQueryable<User> query = this.Context.Users;
        query = this.AddIncludes(query);
        query = includes.Aggregate(query, (current, includeProperty) => current.Include(includeProperty));

        return query.OrderBy(e => e.Id).FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByLinkedAccountAsync(string id, LinkedAccountType accountType, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default)
    {
        var linkedAccount = await this.Context.LinkedAccounts.FirstOrDefaultAsync(
            account => account.Id == id && account.LinkedAccountType == accountType, cancellationToken);

        if (linkedAccount == null)
        {
            return null;
        }

        IQueryable<User> query = this.Context.Users;
        query = this.AddIncludes(query);
        query = includes.Aggregate(query, (current, includeProperty) => current.Include(includeProperty));

        return await query.OrderBy(e => e.Id).FirstOrDefaultAsync(user => user.Id == linkedAccount.UserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        var result = await this.signInManager.CheckPasswordSignInAsync(user, password, false);

        return result.Succeeded;
    }

    /// <inheritdoc />
    public Task<CursorPaginatedList<Role, int>> GetRolesAsync(CursorPaginationQueryParameters searchParams, CancellationToken cancellationToken = default)
    {
        IQueryable<Role> query = this.Context.Roles;

        return query.ToCursorPaginatedListAsync(searchParams, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await this.Context.Roles.ToListAsync(cancellationToken);

        return roles;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RefreshToken>> GetRefreshTokensForDeviceAsync(string deviceId, bool track = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        IQueryable<RefreshToken> query = this.Context.Set<RefreshToken>();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        var tokens = await query
            .Where(t => t.DeviceId == deviceId)
            .Include(t => t.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(t => t.User)
            .ThenInclude(u => u.RefreshTokens)
            .ToListAsync(cancellationToken);

        return tokens;
    }

    /// <inheritdoc />
    protected override IQueryable<User> AddIncludes(IQueryable<User> query)
    {
        return query
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.LinkedAccounts);
    }
}
