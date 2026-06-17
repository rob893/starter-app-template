# Performance Review — StarterApp Template

*StarterApp full-stack template — review date 2026-06-17.*

Scope: the .NET 10 API (EF Core access patterns, async, pagination, startup, connection pooling) and the React SPA (bundle, rendering, React Query, build config). Dimension: **performance**. Per the review brief, **performance is the lowest-priority dimension** — none of these should be pursued at the expense of quality or security, and every recommendation below notes any tradeoff (most are purely additive).

## How to read this report

Every finding is scored on three axes (integers **1–5**):

- **Impact** — severity if left unaddressed / value of fixing it (5 = critical, 1 = cosmetic).
- **Risk** — likelihood it actually bites you / how easily it is triggered or exploited (5 = very likely, 1 = rare/edge case).
- **Effort** — cost to remediate (1 = trivial, 5 = major/architectural).

**Priority Score = (Impact + Risk) ÷ Effort** (rounded to 2 dp). A *higher* score means *better to handle first* — it rewards high-impact, high-likelihood problems that are cheap to fix. **Severity** is the band of `Impact + Risk`: **Critical** (9–10), **High** (7–8), **Medium** (5–6), **Low** (< 5).

The **master table is sorted by Priority Score** (do-first at the top). Severity tells you *how bad*; Priority tells you *what to tackle first*. Detailed findings (with `file:line` citations and fix snippets) follow, grouped by area.

## Executive summary

**19 findings** — Critical: 3 · High: 6 · Medium: 6 · Low: 4.

The performance-sensitive **auth crypto and request pipeline are fine**; the real issues are **database access patterns** and a few **front-end build-hygiene** items — and most of the highest-value fixes are one-liners with no quality or security tradeoff.

Backend themes:

1. **Hot auth queries miss their indexes (cheap, high value).** Token refresh filters `RefreshTokens` by `DeviceId`, which is the *trailing* column of the composite PK and so triggers a **sequential scan** (P-BE-02); username login filters the **unindexed, case-sensitive `UserName`** column instead of the indexed `NormalizedUserName` (P-BE-08 — also a correctness bug). Both are trivial fixes.
2. **An access pattern that will not scale.** `NoteService.GetNotesAsync` loads **all** of a user's notes into memory and then filters/paginates in .NET, bypassing the DB-side `ILike` filter and cursor pagination that already exist (P-BE-01).
3. **Routine over-fetching.** The user repository **always** eager-loads roles + linked accounts via split queries (3 round-trips per fetch) even when unused (P-BE-05); refresh-eligibility loads every token across all devices plus the role graph (P-BE-04); per-request `JsonSerializerOptions` discard EF's reflection cache (P-BE-03); and `AddDbContext` could be `AddDbContextPool` (P-BE-06).

Frontend themes:

1. **Shipping dev-only weight to production.** `ReactQueryDevtools` is statically imported into the production bundle (P-FE-02), and an unused `react-icons` dependency invites accidental barrel-import bloat (P-FE-03).
2. **Gratuitous latency.** ~**1.3 s of artificial `setTimeout` delays** sit in the OAuth callback on every social login (P-FE-08).
3. **Caching & splitting hygiene.** No global React Query `staleTime` default (P-FE-06); "load more" bypasses the query cache (P-FE-05); no route-level code splitting (P-FE-01); and no vendor-chunk strategy (P-FE-04).

The two genuinely larger refactors (the auth-context split P-FE-09 and infinite-query pagination P-FE-05) are correctly ranked **low priority** and explicitly carry quality tradeoffs — defer them unless profiling justifies them.

### Best to handle first (high payoff, low effort)

- **P-BE-02** (9.00, Critical, Effort 1) — Missing index on `RefreshTokens.DeviceId`
- **P-BE-08** (8.00, High, Effort 1) — `GetByUsernameAsync` filters on non-indexed `UserName` column
- **P-FE-02** (8.00, High, Effort 1) — `ReactQueryDevtools` statically imported — bundled in production
- **P-FE-08** (7.00, High, Effort 1) — Artificial 1.3 s delays in OAuth callback flow
- **P-BE-03** (6.00, Medium, Effort 1) — `JsonSerializerOptions` instances created per request in OAuth services
- **P-BE-01** (5.00, Critical, Effort 2) — `NoteService.GetNotesAsync` — all user notes loaded to memory; client-side title filter and pagination

### Cross-dimension overlaps

- **P-BE-08** (username lookup on the unindexed `UserName` column) and **P-BE-01** (in-memory notes pagination) are the performance facets of Quality findings **Q-BE-01** and **Q-BE-03** respectively — fixing them once satisfies both reports.
- **P-FE-03** (unused `react-icons`) is also reported as a Quality finding (**Q-FE-10**); a single removal closes both.

## Master ranking — all findings by Priority Score

**Completed:** ✅ fixed · ◑ partial · n/a won't-fix (by design/sample) · ☐ open

| # | ID | Completed | Area | Title | Impact | Risk | Effort | Priority | Severity |
|:---:|---|:---:|---|---|:---:|:---:|:---:|:---:|:---:|
| 1 | P-BE-02 | ☐ | Backend (.NET API) | Missing index on `RefreshTokens.DeviceId` | 4 | 5 | 1 | 9.00 | Critical |
| 2 | P-BE-08 | ✅ | Backend (.NET API) | `GetByUsernameAsync` filters on non-indexed `UserName` column | 3 | 5 | 1 | 8.00 | High |
| 3 | P-FE-02 | ☐ | Frontend (React SPA) | `ReactQueryDevtools` statically imported — bundled in production | 3 | 5 | 1 | 8.00 | High |
| 4 | P-FE-08 | ☐ | Frontend (React SPA) | Artificial 1.3 s delays in OAuth callback flow | 3 | 4 | 1 | 7.00 | High |
| 5 | P-BE-03 | ☐ | Backend (.NET API) | `JsonSerializerOptions` instances created per request in OAuth services | 2 | 4 | 1 | 6.00 | Medium |
| 6 | P-BE-01 | n/a | Backend (.NET API) | `NoteService.GetNotesAsync` — all user notes loaded to memory; client-side title filter and pagination | 5 | 5 | 2 | 5.00 | Critical |
| 7 | P-FE-06 | ☐ | Frontend (React SPA) | No global `staleTime` default — new queries silently refetch on every mount | 2 | 3 | 1 | 5.00 | Medium |
| 8 | P-FE-01 | ☐ | Frontend (React SPA) | No route-level code splitting — all pages in the initial bundle | 4 | 5 | 2 | 4.50 | Critical |
| 9 | P-BE-04 | ☐ | Backend (.NET API) | `GetRefreshTokensForDeviceAsync` over-fetches: loads all user tokens plus full role hierarchy | 3 | 5 | 2 | 4.00 | High |
| 10 | P-FE-07 | ☐ | Frontend (React SPA) | `allNotes` derived array recomputed on every render | 2 | 2 | 1 | 4.00 | Low |
| 11 | P-FE-03 | ✅ | Frontend (React SPA) | `react-icons` in `dependencies` but never imported | 1 | 3 | 1 | 4.00 | Low |
| 12 | P-BE-05 | ☐ | Backend (.NET API) | `UserRepository.AddIncludes` always eagerly loads `UserRoles → Role` and `LinkedAccounts` | 3 | 4 | 2 | 3.50 | High |
| 13 | P-BE-06 | ☐ | Backend (.NET API) | `AddDbContext` instead of `AddDbContextPool` | 3 | 4 | 2 | 3.50 | High |
| 14 | P-FE-05 | ☐ | Frontend (React SPA) | `handleLoadMore` bypasses React Query — no caching on paginated pages | 3 | 3 | 2 | 3.00 | Medium |
| 15 | P-BE-07 | ☐ | Backend (.NET API) | `AuthController.LoginAsync` — sequential username-then-email DB lookups | 2 | 4 | 2 | 3.00 | Medium |
| 16 | P-FE-04 | ☐ | Frontend (React SPA) | Vite build config has no `manualChunks` / vendor-chunk strategy | 2 | 3 | 2 | 2.50 | Medium |
| 17 | P-BE-09 | ☐ | Backend (.NET API) | `GetByLinkedAccountAsync` — two round-trips where a single JOIN suffices | 2 | 3 | 2 | 2.50 | Low |
| 18 | P-BE-10 | ☐ | Backend (.NET API) | `JwtTokenService` — `SymmetricSecurityKey` and `SigningCredentials` recreated on every token generation | 2 | 3 | 2 | 2.50 | Low |
| 19 | P-FE-09 | ☐ | Frontend (React SPA) | Auth context over-subscription — `isLoading` changes re-render all consumers | 2 | 3 | 3 | 1.67 | Medium |

---

## Detailed findings — Backend (.NET API)

---

### Findings

#### P-BE-02: Missing index on `RefreshTokens.DeviceId`

- **Severity / Priority:** Critical / 9.00
- **Impact / Risk / Effort:** 4 / 5 / 1
- **Location(s):** `StarterApp.API/Data/DataContext.cs:53-55`, `StarterApp.API/Migrations/20260617051126_InitialCreate.cs:211-231`, `StarterApp.API/Data/Repositories/UserRepository.cs:159-178`
- **Finding:** The `RefreshTokens` table has a composite PK of `(UserId, DeviceId)`. PostgreSQL B-tree indexes on compound keys are only usable when the query includes a leading-column predicate. `GetRefreshTokensForDeviceAsync` filters with `WHERE DeviceId = @deviceId` and no `UserId` constraint — the PK index **cannot** be used for this predicate, so PostgreSQL falls back to a sequential scan of the entire table.
- **Why it matters:** This query runs on **every token refresh** (and during login validation). As the token table grows with active users across multiple devices, the seq scan cost grows linearly. No index at all currently covers `DeviceId` alone.
- **Recommendation:** Add a dedicated index on `DeviceId` in a new migration and in `DataContext.OnModelCreating`:

  ```csharp
  // DataContext.cs — OnModelCreating
  builder.Entity<RefreshToken>(rToken =>
  {
      rToken.HasKey(k => new { k.UserId, k.DeviceId });
      rToken.HasIndex(rt => rt.DeviceId); // add this
  });
  ```

  No quality/security tradeoff — purely additive.

---

#### P-BE-08: `GetByUsernameAsync` queries the non-indexed `UserName` column

> **Status (2026-06-17): ✅ Resolved via Quality fix Q-BE-01** (`cf2e866`) — `GetByUsernameAsync` now queries the indexed `NormalizedUserName` instead of the unindexed, case-sensitive `UserName`, eliminating both the sequential scan and the casing bug.

- **Severity / Priority:** High / 8.00
- **Impact / Risk / Effort:** 3 / 5 / 1
- **Location(s):** `StarterApp.API/Data/Repositories/UserRepository.cs:83-88`, `StarterApp.API/Migrations/20260617051126_InitialCreate.cs:264-268`
- **Finding:** The migration creates a unique index on `NormalizedUserName`, not `UserName`. `GetByUsernameAsync` generates `WHERE "UserName" = @username` (case-sensitive, unindexed column), so EF/Postgres cannot use the `UserNameIndex`. By contrast, `GetByEmailAsync` correctly normalises the input and filters on `NormalizedEmail`. This also means the username lookup is **case-sensitive**, which is inconsistent with ASP.NET Identity's intent.

  ```csharp
  // Current (line 88) — unindexed, case-sensitive
  return query.OrderBy(e => e.Id).FirstOrDefaultAsync(user => user.UserName == username, cancellationToken);

  // Should be — uses the existing unique index
  var normalized = username.ToUpperInvariant();
  return query.OrderBy(e => e.Id).FirstOrDefaultAsync(user => user.NormalizedUserName == normalized, cancellationToken);
  ```

- **Why it matters:** `LoginAsync` calls this on **every login attempt** where the caller enters a username. Sequential scan on `AspNetUsers` grows O(users). The same applies to the overload that takes `Expression<Func<User, object>>[]` includes (line 96–100).
- **Recommendation:** Normalise the input with `username.ToUpperInvariant()` and filter on `NormalizedUserName` in both `GetByUsernameAsync` overloads (lines 88 and 100). No quality/security tradeoff — this is strictly more correct and faster.

---

#### P-BE-03: `JsonSerializerOptions` instances created per request in OAuth services

- **Severity / Priority:** Medium / 6.00
- **Impact / Risk / Effort:** 2 / 4 / 1
- **Location(s):** `StarterApp.API/Services/Auth/GoogleOAuthService.cs:19-22`, `StarterApp.API/Services/Auth/GitHubOAuthService.cs:22-25`
- **Finding:** Both OAuth services declare `JsonSerializerOptions` as instance fields initialised in the field initialiser:

  ```csharp
  private readonly JsonSerializerOptions jsonSerializerOptions = new()
  {
      PropertyNameCaseInsensitive = true
  };
  ```

  Because both services are registered as `Scoped`, a new `JsonSerializerOptions` is allocated on every request. `JsonSerializerOptions` builds and caches internal reflection metadata lazily on first use — each new instance starts with an empty cache, discarding the warm-up work done on the previous request. Under load this is repeated GC pressure and redundant reflection work.

- **Why it matters:** Every authenticated OAuth login creates fresh instances. The reflection cache warm-up cost is small per call but accumulates under concurrent load.
- **Recommendation:** Promote the options to `private static readonly` fields in each class. `JsonSerializerOptions` is thread-safe for read-only use after construction. No quality/security tradeoff — the options contain no request-specific state.

  ```csharp
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
  ```

---

#### P-BE-01: `NoteService.GetNotesAsync` — entire user note set loaded, title filter and pagination done in-memory

> **Status (2026-06-17): ⛔ Won't fix — sample code** — the `Notes` resource is demo code slated for removal (the template README now instructs deleting it), so this is moot for the starter. If you keep or adapt a notes-like resource, apply the DB-side filter + cursor pagination described below.

- **Severity / Priority:** Critical / 5.00
- **Impact / Risk / Effort:** 5 / 5 / 2
- **Location(s):** `StarterApp.API/Services/Domain/NoteService.cs:54-66`, `StarterApp.API/Data/Repositories/NoteRepository.cs:22-30`
- **Finding:** `GetNotesAsync` calls the wrong `SearchAsync` overload — the one that materialises all matching rows:

  ```csharp
  // NoteService.cs:54–58 — loads ALL notes for the user into memory
  var pagedList = await this.noteRepository.SearchAsync(
      n => n.UserId == userId,
      track: false,
      cancellationToken);

  // Title filtering then happens in .NET (line 60-62)
  var filtered = string.IsNullOrWhiteSpace(queryParameters.Title)
      ? pagedList
      : pagedList.Where(n => n.Title.Contains(queryParameters.Title, StringComparison.OrdinalIgnoreCase)).ToList();

  // Pagination also happens in .NET (line 66)
  return mapped.ToCursorPaginatedList(queryParameters);
  ```

  The `NoteRepository` already has an `AddWhereClauses` override that pushes the title filter to the database via `EF.Functions.ILike`. The `ToCursorPaginatedListAsync` EF extension exists for IQueryable-based DB-level cursor pagination. Both are entirely bypassed. The `NoteQueryParameters` is already designed to carry the `Title` filter to the repository.

  The comment `// Inject the user filter via a custom query` explains the intent but chose the wrong mechanism: full materialisation + in-memory work instead of composing IQueryable predicates.

- **Why it matters:** A user with 10 000 notes requesting page 1 of 20 loads all 10 000 notes and their content strings into memory, transfers them from Postgres, filters them in-app, then discards 9 980. Title search (`ILike`) that could be a DB-side indexed scan becomes a full .NET LINQ scan. This won't scale.
- **Recommendation:** Add `UserId` as a filter in `NoteRepository.AddWhereClauses` or `NoteQueryParameters`, then call the paginating overload:

  ```csharp
  // Option A — simplest: add UserId to NoteQueryParameters
  // NoteQueryParameters.cs
  public int? UserId { get; set; }

  // NoteRepository.cs — AddWhereClauses
  if (searchParams.UserId.HasValue)
      query = query.Where(n => n.UserId == searchParams.UserId.Value);
  if (!string.IsNullOrWhiteSpace(searchParams.Title))
      query = query.Where(n => EF.Functions.ILike(n.Title, $"%{searchParams.Title}%"));

  // NoteService.cs
  queryParameters.UserId = this.currentUserService.UserId;
  return await this.noteRepository.SearchAsync(queryParameters, track: false, cancellationToken);
  ```

  No quality/security tradeoff. The service-layer ownership check (`userId` scoping) moves from an in-memory lambda to a DB predicate, which is both faster and equivalent in correctness.

---

#### P-BE-04: `GetRefreshTokensForDeviceAsync` over-fetches all user tokens plus full role hierarchy

- **Severity / Priority:** High / 4.00
- **Impact / Risk / Effort:** 3 / 5 / 2
- **Location(s):** `StarterApp.API/Data/Repositories/UserRepository.cs:159-178`, `StarterApp.API/Services/Auth/JwtTokenService.cs:38-63`
- **Finding:** The method loads every `RefreshToken` for the given device's user (all devices, not just the requested one), and eagerly loads the full `User → UserRoles → Role` graph and `User → RefreshTokens` list:

  ```csharp
  var tokens = await query
      .Where(t => t.DeviceId == deviceId)
      .Include(t => t.User)
      .ThenInclude(u => u.UserRoles)
      .ThenInclude(ur => ur.Role)    // unnecessary for token validation
      .Include(t => t.User)
      .ThenInclude(u => u.RefreshTokens)  // loads ALL device tokens
      .ToListAsync(cancellationToken);
  ```

  `JwtTokenService.IsTokenEligibleForRefreshAsync` uses this to:
  1. Find the token matching the incoming hash (HMAC comparison in .NET, not the DB).
  2. Delete expired tokens in-memory and save.

  The first use (finding the right token) requires at most the tokens for this device. The expired-token cleanup forces loading all tokens for all devices. The `UserRoles → Role` join is only needed later for JWT generation, not for the refresh-eligibility check.

- **Why it matters:** Every token refresh loads the full navigation graph: all refresh tokens across all devices for the user plus the role hierarchy. A user with 10 registered devices and multiple roles causes unnecessary JOINs and data transfer on every refresh — which can happen on every authenticated page load.
- **Recommendation:** Split the concern. Load only the minimal token for eligibility check; load roles only when actually generating the JWT. Consider replacing the in-memory expired-token cleanup with a targeted `ExecuteDeleteAsync`:

  ```csharp
  // Batch-delete expired tokens instead of loading all to remove them
  await this.context.RefreshTokens
      .Where(t => t.UserId == userId && t.Expiration <= DateTimeOffset.UtcNow)
      .ExecuteDeleteAsync(cancellationToken);
  ```

  No quality/security tradeoff — the HMAC verification logic remains unchanged.

---

#### P-BE-05: `UserRepository.AddIncludes` unconditionally eager-loads `UserRoles → Role` and `LinkedAccounts`

- **Severity / Priority:** High / 3.50
- **Impact / Risk / Effort:** 3 / 4 / 2
- **Location(s):** `StarterApp.API/Data/Repositories/UserRepository.cs:183-189`
- **Finding:** Every user lookup through the repository — `GetByIdAsync`, `GetByUsernameAsync`, `GetByEmailAsync`, `SearchAsync`, `GetByLinkedAccountAsync` — always joins `UserRoles`, `AspNetRoles`, and `LinkedAccounts` via the base `AddIncludes` override:

  ```csharp
  protected override IQueryable<User> AddIncludes(IQueryable<User> query)
  {
      return query
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .Include(u => u.LinkedAccounts);
  }
  ```

  With `QuerySplittingBehavior.SplitQuery` enabled (DatabaseServiceCollectionExtensions.cs:40), this means **3 SQL queries** (Users + UserRoles+Roles + LinkedAccounts) are issued for every single user fetch, including those that only need the user's basic profile. Most read paths (`GetUserByIdAsync`, `GetUsersAsync` for listing) never touch roles or linked accounts.

- **Why it matters:** Under any real load, listing users or fetching a user for display issues 3× the necessary queries. With SplitQuery, each of those is a separate round-trip to Postgres.
- **Recommendation:** Remove the default includes from `AddIncludes`. Instead, pass required includes explicitly at the call site (the infrastructure already supports `Expression<Func<User, object>>[] includes` parameters). For paths that truly need roles (JWT generation, role management), pass `[u => u.UserRoles, u => u.UserRoles.Select(ur => ur.Role)]` explicitly. This may require small call-site updates in `AuthController` and `UserService`, but improves all read-only paths. No security tradeoff.

---

#### P-BE-06: `AddDbContext` instead of `AddDbContextPool`

- **Severity / Priority:** High / 3.50
- **Impact / Risk / Effort:** 3 / 4 / 2
- **Location(s):** `StarterApp.API/ApplicationStartup/ServiceCollectionExtensions/DatabaseServiceCollectionExtensions.cs:33`
- **Finding:** The application uses `AddDbContext<DataContext>`, which allocates a new `DataContext` instance (and its internal EF state) on every HTTP request. `AddDbContextPool` maintains a pool of reused instances, resetting their state between scopes, avoiding repeated allocation and GC of the large `DataContext` object and its internal change tracker.

  `DataContext` is safe for pooling: its constructor takes only `DbContextOptions<DataContext>` (no per-request scoped services). Npgsql connection pooling already happens at the driver level (via the `NpgsqlDataSource`); `DbContextPool` adds GC and allocation savings on top.

- **Why it matters:** Under concurrent load, each in-flight request allocates a full `DataContext` with EF's internal `InternalServiceProvider`, entity type maps, and change tracker. `AddDbContextPool` eliminates these allocations (Microsoft recommends a pool size of 128 by default).
- **Recommendation:**

  ```csharp
  // DatabaseServiceCollectionExtensions.cs
  services.AddDbContextPool<DataContext>(
      dbContextOptions => { /* same config */ },
      poolSize: 128);
  ```

  **Tradeoff to verify:** Pooled contexts are reset via `PooledDbContextResetter`. EF Core resets tracked entities and config state. The `EnableDetailedErrors` / `EnableSensitiveDataLogging` calls in the factory delegate run per-pool-entry creation, not per request — this is fine. Ensure no code retains a `DataContext` reference beyond the request scope (none found in review). This is safe to apply.

---

#### P-BE-07: `AuthController.LoginAsync` — sequential username-then-email DB lookups

- **Severity / Priority:** Medium / 3.00
- **Impact / Risk / Effort:** 2 / 4 / 2
- **Location(s):** `StarterApp.API/Controllers/V1/AuthController.cs:129-130`
- **Finding:** Login always issues the username query first; only if it returns null does it issue the email query. Users authenticating with their email address (the common pattern in many apps) always pay for **two DB round-trips** where one suffices:

  ```csharp
  var user = await this.userRepository.GetByUsernameAsync(loginRequest.Username, ...)
      ?? await this.userRepository.GetByEmailAsync(loginRequest.Username, ...);
  ```

  Each query already includes eager loads for `UserRoles → Role`, `LinkedAccounts`, and `RefreshTokens` (3 split-queries each). Email-based logins issue up to **6 SQL statements**.

- **Why it matters:** On every email-based login, an extra unnecessary DB query runs. Latency is additive (two sequential round-trips). In a high-login-rate app this is noticeable.
- **Recommendation:** Add a single combined query in `UserRepository`:

  ```csharp
  public Task<User?> GetByUsernameOrEmailAsync(string value, ...)
  {
      var normalizedValue = value.ToUpperInvariant();
      return query.FirstOrDefaultAsync(
          u => u.NormalizedUserName == normalizedValue || u.NormalizedEmail == normalizedValue,
          cancellationToken);
  }
  ```

  This also benefits from P-BE-08's fix (using normalized columns with existing indexes). No security tradeoff.

---

#### P-BE-09: `GetByLinkedAccountAsync` — two DB round-trips where a single JOIN query suffices

- **Severity / Priority:** Low / 2.50
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `StarterApp.API/Data/Repositories/UserRepository.cs:117-131`
- **Finding:** The method issues two sequential queries: first to fetch the `LinkedAccount` row, then a second to fetch the `User` by the `UserId` extracted from it:

  ```csharp
  var linkedAccount = await this.Context.LinkedAccounts
      .FirstOrDefaultAsync(account => account.Id == id && account.LinkedAccountType == accountType, ...);
  ...
  return await query.FirstOrDefaultAsync(user => user.Id == linkedAccount.UserId, ...);
  ```

  A single navigation-based query would achieve this in one round-trip.

- **Why it matters:** Two sequential DB round-trips on every social login (Google/GitHub). Minor at low scale, measurable at higher concurrency.
- **Recommendation:**

  ```csharp
  return await this.Context.Users
      .Where(u => u.LinkedAccounts.Any(la => la.Id == id && la.LinkedAccountType == accountType))
      ... // add includes, ordering
      .FirstOrDefaultAsync(cancellationToken);
  ```

  No quality/security tradeoff.

---

#### P-BE-10: `JwtTokenService.GenerateJwtTokenForUser` — `SymmetricSecurityKey` and `SigningCredentials` recreated on every token

- **Severity / Priority:** Low / 2.50
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `StarterApp.API/Services/Auth/JwtTokenService.cs:127-134`
- **Finding:** Each call to `GenerateJwtTokenForUser` allocates a new `SymmetricSecurityKey` (which copies the secret's byte array) and a new `SigningCredentials` object from a config value that never changes at runtime:

  ```csharp
  var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.authSettings.APISecret));
  // ...
  var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
  ```

  Similarly, `new JwtSecurityTokenHandler()` is created inline (line 146). `JwtSecurityTokenHandler` is documented as thread-safe and expensive to construct.

- **Why it matters:** Minor allocation pressure on every login and token refresh, but `JwtSecurityTokenHandler` construction is non-trivial (reflection/config work). At scale this is measurable GC and CPU overhead.
- **Recommendation:** Cache `SymmetricSecurityKey`, `SigningCredentials`, and `JwtSecurityTokenHandler` as private readonly fields (or `static readonly` for the handler) in `JwtTokenService`, initialised in the constructor from `authSettings`. The key size check can also move to the constructor as a one-time validation. No quality/security tradeoff — none of these contain per-request state.

---

### Positive Observations

- **`IHttpClientFactory` used correctly:** Both `GoogleOAuthService` and `GitHubOAuthService` inject and use `IHttpClientFactory.CreateClient()` rather than `new HttpClient(...)`. Socket exhaustion / DNS staleness is not an issue.
- **`CancellationToken` propagation is thorough:** All async EF and HTTP calls correctly thread `CancellationToken` through. `HttpContext.RequestAborted` is used consistently at controller boundaries.
- **`AsNoTracking()` respected:** Read-only service paths consistently pass `track: false`, and the repository correctly applies `AsNoTracking()` when requested.
- **Cursor pagination is DB-side for all correctly-routed paths:** `ToCursorPaginatedListAsync` issues efficient `TAKE n+1` queries with cursor predicates pushed to SQL. The infrastructure is well-designed; P-BE-01 is a call-site defect, not an infrastructure defect.
- **`QuerySplittingBehavior.SplitQuery` enabled:** Prevents cartesian product explosion from multi-level `Include()` chains — a good global default.
- **Npgsql data source configured at startup:** The `NpgsqlDataSource` is built once and shared, giving Npgsql's connection pool full lifecycle control. Retry-on-failure is also enabled.
- **Indexes cover the common query patterns:** `IX_Notes_UserId`, `IX_LinkedAccounts_UserId`, `EmailIndex` on `NormalizedEmail`, `UserNameIndex` on `NormalizedUserName` are all present. The gap (P-BE-02, P-BE-08) is in `RefreshTokens.DeviceId` and the mismatch in `GetByUsernameAsync`.

---

### Out-of-Scope Notes

- **`DatabaseSeeder.ClearAllDataAsync`** loads full DbSet contents into EF's change tracker to delete them (`dbSet.RemoveRange(dbSet)`). `ExecuteDeleteAsync()` (EF Core 7+) would be more efficient, but this method is a one-time maintenance operation, not a runtime hot path — fixing it is purely cosmetic at this scale.
- **No meaningful caching opportunities** exist for this domain (per-user notes are user-specific; roles are tiny and already loaded via split queries). Adding a cache layer would introduce complexity with little gain.
- **`new JwtSecurityTokenHandler` per token** (P-BE-10) is low severity but is the only place where a trivially-shareable, construction-heavy object is not reused. If auth throughput matters, it is the easiest win after the index and column fixes.

---

## Detailed findings — Frontend (React SPA)

> **Severity key:** Critical = 9–10 · High = 7–8 · Medium = 5–6 · Low < 5 (Impact + Risk).  
> **Priority** = round((Impact + Risk) / Effort, 2). Higher = handle first.

---

### Findings

#### P-FE-02: `ReactQueryDevtools` statically imported — bundled in production

- **Severity / Priority:** High / 8.00
- **Impact / Risk / Effort:** 3 / 5 / 1
- **Location(s):** `starter-app-ui/src/main.tsx:6,34`
- **Finding:** `ReactQueryDevtools` is imported statically and rendered unconditionally. Although the package is listed in `devDependencies`, Vite bundles anything that is transitively imported at build time — the `npm run build` script (`tsc -b && vite build`) does not prune devDeps from the graph, it only follows import statements. The devtools bundle is ~80–130 KB parsed JS.

  ```tsx
  // main.tsx:6
  import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
  // main.tsx:34
  <ReactQueryDevtools initialIsOpen={false} />
  ```

- **Why it matters:** Every production user pays the parse + execute cost of the devtools panel on cold load. On mid-range mobile devices (4× CPU throttle), this adds measurable startup time.
- **Recommendation:** Guard behind `import.meta.env.DEV` via a dynamic import so Vite's build dead-code elimination removes it from production:

  ```tsx
  // main.tsx
  const ReactQueryDevtools = import.meta.env.DEV
    ? (await import('@tanstack/react-query-devtools')).ReactQueryDevtools
    : () => null;
  ```

  Or, more idiomatically, move it to a separate `DevTools.tsx` component that is only rendered in dev:

  ```tsx
  // DevTools.tsx — only imported in main.tsx inside `if (import.meta.env.DEV)`
  import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
  export function DevTools() {
    return <ReactQueryDevtools initialIsOpen={false} />;
  }
  ```

  **No quality or security tradeoff** — this is purely build-pipeline hygiene.

---

#### P-FE-08: Artificial 1.3 s delays in OAuth callback flow

- **Severity / Priority:** High / 7.00
- **Impact / Risk / Effort:** 3 / 4 / 1
- **Location(s):** `starter-app-ui/src/pages/OAuthCallbackPage.tsx:76,86`
- **Finding:** Two synthetic `setTimeout` calls add 500 ms + 800 ms = **1.3 seconds** of unconditional wait time before the user is redirected to the home page after every social login:

  ```ts
  setStep('exchanging');
  await new Promise(resolve => setTimeout(resolve, 500));   // line 76
  setStep('authenticating');

  if (provider === 'github') {
    await loginWithGitHub(result.code);
  } else {
    await loginWithGoogle(result.code);
  }

  setStep('success');
  await new Promise(resolve => setTimeout(resolve, 800));   // line 86
  navigate('/', { replace: true });
  ```

- **Why it matters:** Social login is a primary authentication path. Every user who chooses GitHub or Google login incurs 1.3 s of idle browser time on top of the actual network round-trips. This is particularly noticeable on fast networks where the real auth call resolves quickly.
- **Recommendation:** The step animation is a nice UX touch, but the delays are artificially long. Remove the `await new Promise(...)` pauses entirely. The visual steps will still progress naturally as each async operation completes. If a short "success" pause is desired before redirect, 200–300 ms is sufficient:

  ```ts
  setStep('success');
  await new Promise(resolve => setTimeout(resolve, 200)); // reduced from 800 ms
  navigate('/', { replace: true });
  ```

  **No quality/security tradeoff** — the step labels still update correctly because state transitions happen before each `await`.

---

#### P-FE-06: No global `staleTime` default — new queries silently refetch on every mount

- **Severity / Priority:** Medium / 5.00
- **Impact / Risk / Effort:** 2 / 3 / 1
- **Location(s):** `starter-app-ui/src/main.tsx:11–26`
- **Finding:** The `QueryClient` default options do not set `staleTime`:

  ```ts
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: ...,
        refetchOnWindowFocus: false   // ← good
        // staleTime missing → defaults to 0
      }
    }
  });
  ```

  React Query's default `staleTime` is `0`, meaning every query is considered instantly stale. On any component mount, React Query will trigger a background refetch for any cached data. The three current data queries (`useHelloV1`, `useHelloV2`, `useNotes`) each set explicit `staleTime` values and are therefore unaffected. However, any future query added without a `staleTime` will silently refetch on every navigation and component mount.

- **Why it matters:** As the app grows, engineers naturally copy patterns. A pattern without an explicit `staleTime` will cause unnecessary server load and perceived "flashing" as stale data is replaced. One-liner fix now prevents a recurring future issue.
- **Recommendation:** Add a sensible global default:

  ```ts
  defaultOptions: {
    queries: {
      staleTime: 60 * 1000,  // 1 minute — overridable per-query
      refetchOnWindowFocus: false,
      retry: ...
    }
  }
  ```

  **No quality/security tradeoff.** Individual queries can still override with a lower value for freshness-critical data.

---

#### P-FE-01: No route-level code splitting — all pages in the initial bundle

- **Severity / Priority:** Critical / 4.50
- **Impact / Risk / Effort:** 4 / 5 / 2
- **Location(s):** `starter-app-ui/src/App.tsx:1–10`
- **Finding:** All six page components plus their dependencies are eagerly imported at the module level:

  ```tsx
  import { HomePage } from './pages/HomePage';
  import { LoginPage } from './pages/LoginPage';
  import { RegisterPage } from './pages/RegisterPage';
  import { OAuthCallbackPage } from './pages/OAuthCallbackPage';
  import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
  import { ResetPasswordPage } from './pages/ResetPasswordPage';
  ```

  Because none of these use `React.lazy()`, Vite produces a single app chunk containing every page's code, all HeroUI components those pages import, and all their utilities. A user arriving at `/login` downloads and parses `HomePage` (including notes list, `notesApi`, pagination logic) and vice versa. As the application grows with more routes, this compounds: each new route adds to the monolithic initial chunk.

- **Why it matters:** Larger initial JS payload = longer parse/execute time on low-end devices. A real user hitting the login page has no reason to pay for the home page's logic before authenticating.
- **Recommendation:** Wrap each page in `React.lazy` + `Suspense`. The auth pages can share a fallback; protected routes can share another:

  ```tsx
  // App.tsx
  import { lazy, Suspense } from 'react';
  import { Spinner } from '@heroui/react';

  const HomePage = lazy(() => import('./pages/HomePage').then(m => ({ default: m.HomePage })));
  const LoginPage = lazy(() => import('./pages/LoginPage').then(m => ({ default: m.LoginPage })));
  const RegisterPage = lazy(() => import('./pages/RegisterPage').then(m => ({ default: m.RegisterPage })));
  const OAuthCallbackPage = lazy(() => import('./pages/OAuthCallbackPage').then(m => ({ default: m.OAuthCallbackPage })));
  const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage').then(m => ({ default: m.ForgotPasswordPage })));
  const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage').then(m => ({ default: m.ResetPasswordPage })));

  const PageFallback = () => (
    <div className="flex justify-center items-center min-h-screen">
      <Spinner size="lg" color="accent" />
    </div>
  );

  function App() {
    return (
      <AuthProvider>
        <div className="app min-h-screen bg-background text-foreground">
          <Suspense fallback={<PageFallback />}>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              {/* …rest of routes unchanged… */}
            </Routes>
          </Suspense>
        </div>
      </AuthProvider>
    );
  }
  ```

  **No quality/security tradeoff.** React.lazy + Suspense is production-grade and the recommended React pattern. Error boundaries can be added alongside Suspense if desired.

---

#### P-FE-07: `allNotes` derived array recomputed on every render

- **Severity / Priority:** Low / 4.00
- **Impact / Risk / Effort:** 2 / 2 / 1
- **Location(s):** `starter-app-ui/src/pages/HomePage.tsx:22–25`
- **Finding:** The merged notes array is recomputed unconditionally on every render:

  ```tsx
  const allNotes: Note[] = [
    ...(firstPage?.nodes ?? firstPage?.edges?.map(e => e.node) ?? []),
    ...pages.flatMap(p => p.nodes ?? p.edges?.map(e => e.node) ?? [])
  ];
  ```

  In addition to the spread, `flatMap` + conditional `edges?.map` executes on every render regardless of whether `firstPage` or `pages` have changed (e.g., it re-runs when `noteTitle`/`noteContent` state updates on every keystroke).

- **Why it matters:** At a handful of notes this is negligible. As `pages` grows (many "Load More" calls) and the form's controlled inputs trigger frequent re-renders, the repeated `flatMap` over all accumulated pages adds up. More importantly, this establishes a pattern that worsens as the app grows.
- **Recommendation:**

  ```tsx
  const allNotes = useMemo<Note[]>(
    () => [
      ...(firstPage?.nodes ?? firstPage?.edges?.map(e => e.node) ?? []),
      ...pages.flatMap(p => p.nodes ?? p.edges?.map(e => e.node) ?? [])
    ],
    [firstPage, pages]
  );
  ```

  **No quality tradeoff.** `useMemo` here is straightforward and the dependency array accurately captures what the computation depends on.

---

#### P-FE-03: `react-icons` in `dependencies` but never imported

> **Status (2026-06-17): ✅ Resolved via Quality fix Q-FE-10** — `react-icons` was removed from `package.json`, eliminating the unused-dependency / accidental-barrel-import bundle risk.

- **Severity / Priority:** Low / 4.00
- **Impact / Risk / Effort:** 1 / 3 / 1
- **Location(s):** `starter-app-ui/package.json:27`
- **Finding:** `react-icons` v5.6.0 is listed as a production dependency but is imported nowhere in the source tree. All OAuth icons are correctly implemented as inline SVGs in `LoginPage`, `RegisterPage`, and `OAuthCallbackPage`. The `react-icons` package exposes ~45 icon families via barrel exports; if any future developer imports from the top-level barrel (e.g., `import { FaGithub } from 'react-icons'` instead of `import { FaGithub } from 'react-icons/fa6'`), the entire icon set (~800 KB+ unpacked) enters the bundle with no tree-shaking.

- **Why it matters:** Currently has zero bundle impact (not imported). The risk is a future trap — the dependency signals to contributors that `react-icons` is available and expected, but without a sub-path convention enforced by lint, barrel imports are one `import { ... } from 'react-icons'` away from large bundle bloat.
- **Recommendation:** Either remove the dependency entirely (inline SVGs already cover the needed icons) or, if you want to keep `react-icons` available for extensibility, add an ESLint rule to enforce sub-path imports (`react-icons/fa6`, `react-icons/hi2`, etc.) and document the convention.

  ```bash
  npm uninstall react-icons
  ```

  **No quality tradeoff.** The inline SVGs are already in place and accessible.

---

#### P-FE-05: `handleLoadMore` bypasses React Query — no caching on subsequent pages

- **Severity / Priority:** Medium / 3.00
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** `starter-app-ui/src/pages/HomePage.tsx:31–41`, `starter-app-ui/src/hooks/api.ts:122–141`
- **Finding:** "Load more" pages are fetched by calling `notesApi.getNotes` directly, storing the result in local React state (`pages`):

  ```tsx
  // HomePage.tsx:35
  const next = await notesApi.getNotes({ first: 5, after: endCursor });
  setPages(prev => [...prev, next]);
  ```

  This bypasses React Query entirely: the fetched pages are not cached, not deduplicated, and not invalidated when notes are created/deleted. A mutation (`useCreateNote` / `useDeleteNote`) calls `queryClient.invalidateQueries({ queryKey: ['notes'] })`, which refreshes only the first page — subsequent pages become stale with no mechanism to refresh them. Additionally, `useNotesCursorPaginated` in `hooks/api.ts` (lines 122–141) was written to address exactly this scenario but is never used, creating dead code and architectural confusion.

- **Why it matters:** After a note deletion, the UI may show a note in a "load more" page that no longer exists on the server. After note creation, the total count is off. Users loading many pages will re-fetch them from scratch on every component mount (navigating away and back).
- **Recommendation:** Replace the manual pagination with React Query's `useInfiniteQuery` (TanStack Query v5). This keeps all pages in the query cache, participates in invalidation, and eliminates the `pages` local state:

  ```ts
  // hooks/api.ts
  export function useInfiniteNotes(pageSize = 5) {
    const { isLoading: isAuthLoading, isAuthenticated } = useAuth();

    return useInfiniteQuery({
      queryKey: ['notes', 'infinite', pageSize],
      queryFn: ({ pageParam }) => notesApi.getNotes({ first: pageSize, after: pageParam }),
      initialPageParam: undefined as string | undefined,
      getNextPageParam: last => last.pageInfo.hasNextPage ? last.pageInfo.endCursor : undefined,
      enabled: isAuthenticated && !isAuthLoading,
      staleTime: 5 * 60 * 1000
    });
  }
  ```

  Remove the unused `useNotesCursorPaginated` hook to eliminate dead code.

  **Tradeoff:** Moderate refactor of `HomePage` pagination state; recommend doing alongside P-FE-07 (memoize `allNotes`) since the shapes will change.

---

#### P-FE-04: Vite build config has no `manualChunks` / vendor-chunk strategy

- **Severity / Priority:** Medium / 2.50
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `starter-app-ui/vite.config.ts:1–11`
- **Finding:** `vite.config.ts` contains no `build` configuration at all:

  ```ts
  export default defineConfig({
    plugins: [react(), tailwindcss()],
    base: process.env.VITE_BASE_PATH || '/'
  });
  ```

  Vite 5+ does automatic vendor chunking via Rollup's built-in heuristics, but without explicit `manualChunks` the heavy stable vendor libraries (React runtime, React Router, TanStack Query, HeroUI, Axios) may end up co-located in chunks with application code. When any app file changes, the shared chunk's content hash changes, invalidating CDN/browser caches for all users even though the vendor libraries themselves haven't changed.

- **Why it matters:** Poor chunk granularity forces returning users to re-download vendor code that didn't change. On a CDN-deployed GitHub Pages app, this affects cache hit rates for every deployment.
- **Recommendation:** Add explicit vendor chunk splitting:

  ```ts
  // vite.config.ts
  import { defineConfig } from 'vite';
  import react from '@vitejs/plugin-react';
  import tailwindcss from '@tailwindcss/vite';

  export default defineConfig({
    plugins: [react(), tailwindcss()],
    base: process.env.VITE_BASE_PATH || '/',
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            'vendor-react': ['react', 'react-dom', 'react-router'],
            'vendor-query': ['@tanstack/react-query'],
            'vendor-ui': ['@heroui/react', '@heroui/styles'],
            'vendor-http': ['axios', 'jwt-decode']
          }
        }
      }
    }
  });
  ```

  **No quality tradeoff.** This is purely additive and does not affect behaviour. Note: if P-FE-01 (lazy routes) is implemented first, Rollup will do more of this automatically via dynamic import boundaries.

---

#### P-FE-09: Auth context over-subscription — all consumers re-render on `isLoading` changes

- **Severity / Priority:** Medium / 1.67
- **Impact / Risk / Effort:** 2 / 3 / 3
- **Location(s):** `starter-app-ui/src/contexts/AuthContext.tsx:173–184`
- **Finding:** The single context value contains the full `AuthState` spread (`user`, `isAuthenticated`, `isLoading`) plus all action callbacks. The `useMemo` correctly prevents re-creation of the value object when callbacks are stable, but since `state` is a dependency, any `AuthState` change — including `isLoading` toggling `true → false` during the initial `checkAuth()` — causes every context consumer to re-render. Components like `AppHeader` (only needs `isAuthenticated`, `user`, `logout`) and data query hooks (only check `isLoading`) both re-render on every transition.

  At the current scale (3–4 consumers, small component tree), this is inconsequential. The concern grows as more components call `useAuth()`.

- **Why it matters:** Each `checkAuth()` cycle dispatches `SET_LOADING(true)` then `SET_USER` / `CLEAR_USER`, causing two full fan-out re-renders across all consumers. In a larger app with many `useAuth()` call sites, this creates noticeable jank on page load.
- **Recommendation (low urgency):** Split the context into a stable "actions" context and a reactive "state" context. This is a meaningful readability/complexity tradeoff:

  ```tsx
  // Stable — never changes after mount
  const AuthActionsContext = createContext<AuthActions | undefined>(undefined);
  // Reactive — changes with auth state
  const AuthStateContext = createContext<AuthState | undefined>(undefined);
  ```

  Components that only need `logout` subscribe to `AuthActionsContext` only and never re-render on state changes.

  **Tradeoff:** Doubles the context/provider complexity. **Recommendation: defer until the app has materially more `useAuth()` consumers.** This is the lowest-priority finding for a good reason — the current implementation is clean and correct. Only apply if profiling shows measurable render cost.

---

### Positive observations

- **Auth context is properly memoised.** `AuthProvider` wraps the value object in `useMemo` with all stable callbacks in `useCallback`. This is done right (line 173, `AuthContext.tsx`).
- **`refetchOnWindowFocus: false` set globally** — avoids the most common React Query refetch spam in tab-switching UIs.
- **Token refresh queue prevents parallel refresh storms.** The `failedQueue` / `isRefreshing` pattern in `axiosConfig.ts` correctly serialises concurrent 401s behind a single refresh attempt.
- **`isCheckingAuth` ref** prevents duplicate auth checks on double-render (important under `React.StrictMode`).
- **Inline SVGs for OAuth icons** — avoids pulling in `react-icons` at all for the few icons needed. Good judgement.
- **Query keys centralised** in `queryKeys` object with typed factories — makes invalidation predictable and maintainable.
- **Queries gated on `isAuthenticated && !isAuthLoading`** — ensures no authenticated requests fire before auth state is resolved, preventing a class of 401 waterfalls.
- **Retry disabled for 404s** — prevents hammering the server for genuinely missing resources.
- **No sourcemaps in production** (Vite default) — correct; not an oversight.

---

### Out-of-scope notes

- **`@heroui/styles` CSS import** (`index.css:2`): HeroUI v3 uses a single CSS file rather than per-component CSS-in-JS. This is loaded synchronously but is a single predictable payload. No issue identified.
- **Inter font** declared in `index.css` root but not loaded via a `<link rel="preconnect">` or `@font-face`. If Inter is served as a web font by a CDN, a `<link rel="preconnect">` in `index.html` would help TTFB. Currently falls back to `system-ui` — no real-world problem unless Inter is explicitly served.
- **No virtualization** on the notes list: appropriate at current scale (< 100 notes in a typical session). If notes could grow into thousands, `@tanstack/react-virtual` would be warranted. Not a concern now.
- **`ReactQueryDevtools` in `devDependencies`** is correct placement. The issue is the static import in `main.tsx`, not where the package is listed.
