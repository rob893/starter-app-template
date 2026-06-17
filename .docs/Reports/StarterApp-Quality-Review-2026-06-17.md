# Quality Review — StarterApp Template

*StarterApp full-stack template — review date 2026-06-17.*

Scope: the .NET 10 Web API (`StarterApp.API`) and its tests, the React SPA (`starter-app-ui`), the Bicep infrastructure (`CI/Azure`), and the GitHub Actions workflows. Dimension: **code & configuration quality** — correctness, duplication, dead code, readability, organisation, best practices, test coverage, and docs-vs-reality.

## How to read this report

Every finding is scored on three axes (integers **1–5**):

- **Impact** — severity if left unaddressed / value of fixing it (5 = critical, 1 = cosmetic).
- **Risk** — likelihood it actually bites you / how easily it is triggered or exploited (5 = very likely, 1 = rare/edge case).
- **Effort** — cost to remediate (1 = trivial, 5 = major/architectural).

**Priority Score = (Impact + Risk) ÷ Effort** (rounded to 2 dp). A *higher* score means *better to handle first* — it rewards high-impact, high-likelihood problems that are cheap to fix. **Severity** is the band of `Impact + Risk`: **Critical** (9–10), **High** (7–8), **Medium** (5–6), **Low** (< 5).

The **master table is sorted by Priority Score** (do-first at the top). Severity tells you *how bad*; Priority tells you *what to tackle first*. Detailed findings (with `file:line` citations and fix snippets) follow, grouped by area.

## Executive summary

**34 findings** — Critical: 1 · High: 3 · Medium: 13 · Low: 17.

The template is, overall, a **well-organised and idiomatic foundation**: the backend builds warning-clean and all 19 backend tests pass, the extension-driven startup, repository/service-result patterns, and DTO mapping are consistent, and the React app has a clean services/hooks/components separation. The findings cluster into four themes:

1. **The out-of-the-box experience is partly broken.** CI workflow triggers are all commented out so nothing runs on PR/push despite the README advertising it (Q-CI-01); the Playwright `webServer` is disabled so `npm run test:e2e` fails on a clean checkout (Q-FE-09); the seeded `admin` user is created with **no password** and so cannot log in (Q-BE-05); and username login is **case-sensitive** (queries `UserName` instead of `NormalizedUserName`) so `Admin` vs `admin` silently fails (Q-BE-01). For a clone-and-run template these first-five-minutes failures matter most.
2. **Dead / aspirational code that cloners will trip over.** ~360 lines of unused cursor converters plus a 0-byte duplicate file (Q-BE-02), dead request DTOs including a misspelled `RegisterUserUsingGoolgleRequest` (Q-BE-06), unused exported hooks/utils/types (Q-FE-01), an unused `react-icons` dependency (Q-FE-10), and a dead error branch on the forgot-password page (Q-FE-05).
3. **Thin tests on the highest-risk paths.** The auth controller, JWT/refresh-token service, repositories, middleware, the axios refresh-interceptor, `AuthContext`, and `ProtectedRoute` are essentially untested (Q-BE-07, Q-FE-08) — exactly the parts a cloner is most likely to break.
4. **A handful of architectural inconsistencies.** `AuthController` embeds duplicated OAuth business logic instead of using the service-result pattern the other controllers follow (Q-BE-10); the Forgot/Reset pages use raw `fetch` and bypass the shared `apiClient` interceptors (Q-FE-04); `HomePage` reimplements pagination while a purpose-built hook sits unused (Q-FE-11); and refresh-token logic is duplicated in two divergent places (Q-FE-02).

### Best to handle first (high payoff, low effort)

- **Q-CI-01** (9.00, Critical, Effort 1) — All workflow triggers commented out; docs claim PR/push validation
- **Q-BE-01** (6.00, Medium, Effort 1) — Case-sensitive username lookup (inconsistent with email) breaks login
- **Q-FE-09** (6.00, Medium, Effort 1) — Playwright `webServer` disabled — `test:e2e` fails without manual server
- **Q-CI-03** (5.00, Medium, Effort 1) — NuGet cache key hashes non-existent `packages.lock.json`
- **Q-BE-03** (4.00, High, Effort 2) — `NoteService.GetNotesAsync` bypasses DB cursor pagination/filtering; repo filter is dead
- **Q-BE-02** (4.00, Low, Effort 1) — Dead `CursorConverters` methods + empty 0-byte duplicate file

### Cross-dimension overlaps

- **Q-BE-01 (case-sensitive username lookup)** is the same root cause as **P-BE-08** in the Performance report — the unindexed `UserName` column is both a correctness bug *and* a sequential-scan performance issue. Fixing it (use `NormalizedUserName`) resolves both.
- **Q-BE-03 (notes loaded into memory then paginated client-side)** is the same code as **P-BE-01** in the Performance report — it defeats the headline cursor-pagination feature (quality) and will not scale (performance).

## Resolution log (2026-06-17)

Quality fixes are being applied in batches; earlier batches are committed. The most recent work — **Q-BE-10** (extract `IExternalLoginService`) plus the Q-CI-04 / Q-BE-05 won't-fix decisions — completes the backlog: **every finding is now resolved** (fixed, partial-by-design, or won't-fix). Dispositions:

| Finding | Disposition | Detail |
|---|---|---|
| Q-BE-01 | ✅ **Fixed** | `GetByUsernameAsync` now normalizes input and queries `NormalizedUserName` (case-insensitive). Also resolves Performance finding **P-BE-08**. |
| Q-FE-09 | ✅ **Fixed** | Playwright `webServer` enabled so `npm run test:e2e` self-boots. |
| Q-CI-03 | ✅ **Fixed (revised approach)** | Cache key now hashes `**/*.csproj` only; restore stays plain `dotnet restore`. NuGet lock files (`RestorePackagesWithLockFile` + `packages.lock.json` + `--locked-mode`) were trialed, then **deliberately reverted** — every package version is exact-pinned, so hashing the `.csproj` files is sufficient and avoids the lock-file maintenance overhead (a footgun for a template). |
| Q-BE-02 | ◑ **Partially applied (by design)** | The empty 0-byte `Extensions/CursorConverters.cs` was deleted. The unused type-specific converter methods in `Utilities/CursorConverters.cs` are **intentionally retained** as reusable building blocks for apps built on the template — they are not used by the starter itself, but are expected to be useful downstream. Do not flag them as dead code. |
| Q-CI-01 | ⛔ **Won't fix (by design) — documented** | CI triggers intentionally remain commented out so the *template* repo stays idle. The README "Using this template" section now instructs users to uncomment them, and each workflow `on:` block carries an explanatory comment. |
| Q-BE-03 (and all `NoteService` / Notes findings) | ⛔ **Won't fix — sample code** | The `Notes` resource is demo/sample code slated for deletion. Rather than fix it, the README now instructs template users to remove the Notes (and Hello) samples. No Notes code was modified. |
| Q-CI-05 | ✅ **Fixed** | Workflow-level `id-token: write` removed; least-privilege job permissions — `build` → `contents: read`, `deploy` → `id-token: write` + `contents: read`. |
| Q-BE-04 | ✅ **Fixed** | `AuthController` injects `ILogger`; both bare `catch` blocks now log the exception and exclude `OperationCanceledException` (no more swallowed errors or cancellation-as-500). |
| Q-FE-02 | ✅ **Fixed** | Single `refreshAccessToken()` in `axiosConfig.ts`; `authApi.refreshToken()` delegates to it. Interceptor still uses bare axios (no recursion); duplicate removed. |
| Q-FE-04 | ✅ **Fixed (also fixed a latent bug)** | Forgot/Reset now use `apiClient` via new `authApi.forgotPassword`/`resetPassword`. Validation found the old raw `fetch` hit **non-existent routes** (`/auth/forgot-password`); corrected to the real `POST /api/v1/users/forgotPassword` / `resetPassword`. Enumeration-safe UX preserved. |
| Q-FE-06 | ✅ **Fixed** | Added a class-based `ErrorBoundary` (`components/ErrorBoundary.tsx`) wrapping `<App />` in `main.tsx`, with a friendly fallback + reload. |
| Q-INFRA-05 | ✅ **Fixed** | Key Vault name is now `take('${namePrefix}-kv-${environment}', 24)` with a trailing-hyphen guard → guaranteed ≤24 chars and a valid KV name. `az bicep build` compiles clean. |
| Q-BE-06 | ✅ **Fixed** | Deleted the dead `RegisterUserUsingGoolgleRequest.cs` (typo) and `GoogleLoginRequest.cs` — confirmed zero references solution-wide (real endpoints use `OAuthCodeLoginRequest`). |
| Q-FE-05 | ✅ **Fixed** | Removed the unreachable `error` state + `ApiErrorDisplay` from `ForgotPasswordPage` (it always swallows errors for enumeration-safety); generic-success UX preserved. |
| Q-FE-10 | ✅ **Fixed** | `npm uninstall react-icons` (unused; zero imports). Also resolves Performance **P-FE-03**. |
| Q-INFRA-02 | ✅ **Fixed** | Removed the `//` JSONC comments from `main.parameters.dev.json` (guidance already in `CI/README.md`); the file now parses as strict JSON. |
| Q-FE-03 | ✅ **Fixed** | Extracted `components/SocialLoginButtons.tsx` (shared Google/GitHub buttons + handlers, used by Login & Register) and `components/oauthIcons.tsx` (shared icons, reused by `OAuthCallbackPage`); removed the duplication and converged an `aria-hidden` drift. |
| Q-BE-08 | ✅ **Fixed** | Replaced all 12 `occured` → `occurred` in the `<response code="500">` XML doc comments (`AuthController`, `UsersController`). |
| Q-BE-07 | ✅ **Fixed** | Added **51 unit tests** (xUnit + Moq): `JwtTokenService` (refresh-hash round-trip, JWT validation, key-size guard, expiry/pruning, revokes), `UserService` (authz/validation/anti-enumeration branches), `AuthController` (register/login/refresh happy + error paths), and middleware. Suite now **70/70**. |
| Q-FE-01 | ◑ **Mostly fixed (by design)** | Removed the dead auth hooks (`useLogin/Register/Logout/RefreshToken`), notes hooks (`useNotes/NotesPage/UpdateNote/NotesCursorPaginated`), the 4 oauth-callback aliases, and `OAuthLoginRequest`. **Kept/restored** `hasAdminRole` and `isTokenExpired` as useful self-contained role/token helpers (now unit-tested) per the keep-useful-scaffolding preference; also kept `decodeJwtToken` and the used oauth helpers. |
| Q-FE-07 | ✅ **Fixed** | Enabled `@typescript-eslint/no-explicit-any` as **`error`** and typed away the `any`s (refresh queue → `string`/`null` + `unknown`; vitest.setup cast). One unavoidable vite-plugin cast kept with a targeted, commented disable. |
| Q-FE-08 | ✅ **Fixed** | Added **36 Vitest tests**: `decodeJwtToken`, OAuth-callback util, an end-to-end **401 → refresh → retry** interceptor test (single refresh under concurrent 401s), `AuthContext` transitions, and `ProtectedRoute`. Suite now **46/46**, fully typed (no `any`). |
| Q-FE-11 | ✅ **Fixed** | HomePage now uses a `useInfiniteNotes` (`useInfiniteQuery`) hook — cached + invalidation-aware; removed the bespoke `pages`/`loadingMore` state and the unused `useNotesCursorPaginated`. |
| Q-FE-12 | ✅ **Fixed** | Added `src/constants/routes.ts` (`routePaths` + `publicRoutePaths` + `isPublicRoute`); `AuthContext.checkAuth` and `App.tsx` route defs now share it, so all public routes (incl. forgot/reset-password) are covered consistently. |
| Q-BE-09 | ✅ **Fixed** | Renamed `GetGitHubUser` → `GetGitHubUserAsync` (impl, interface, AuthController caller). `GoogleJsonWebSignature.ValidateAsync` has no `CancellationToken` overload, so the param is kept (interface consistency) with a comment noting it can't be honored. |
| Q-BE-11 | ✅ **Fixed** | Bound a `CancellationToken` parameter on every async controller action (Auth/Users/Notes — ASP.NET Core auto-binds it to `RequestAborted`), threaded it through the private helpers, and reverted the AGENTS.md carve-out to the clean convention. Sync redirect/ping actions left as-is. Tests updated; build 0 warnings, 70/70. |
| Q-FE-13 | ✅ **Fixed** | Aligned on `userName`: backend `LoginRequest.Username` → `UserName` (wire now `userName`, matching register/User/JWT) + `AuthController` + `AuthControllerTests`; frontend `types/auth.ts`, `LoginPage`, and the AuthContext test updated. |
| Q-CI-02 | ✅ **Fixed** | Extracted composite actions `.github/actions/build-test-api` and `build-test-ui`; CI and both deploy workflows now call them (versions/cache/steps in one place). Deploy reuses setup/restore via a `run-build-test: false` input. |
| Q-INFRA-04 | ✅ **Fixed** | Surfaced Postgres/observability sizing as `main.bicep` parameters (SKU/tier/storage/backup/HA, log + App Insights retention) with `@description`/`@allowed` and defaults = current literals; threaded into the modules. |
| Q-INFRA-03 | ✅ **Fixed** | Removed 6 unused/placeholder outputs (`connectionStringShape`, `serverName`, `appInsightsId`, `instrumentationKey`, `workspaceName`, `keyVaultId`); kept the outputs `main.bicep` consumes. |
| Q-INFRA-06 | ✅ **Fixed** | Aligned resource `apiVersion`s to recent stable releases (Postgres `2024-08-01`, Web `2024-11-01`, Key Vault `2024-11-01`, Log Analytics `2025-02-01`); the bump also cleared the prior BCP081 warnings. |
| Q-CI-04 | ⛔ **Won't fix** | Leaving the API deploy job without an `environment:` gate is an accepted decision. |
| Q-BE-05 | ⛔ **Won't fix** | By design the seeded admin uses **social login**, so there is deliberately no password in the code/seeder — the "can't log in with a password" behavior is intended. |
| Q-BE-10 | ✅ **Fixed** | Extracted `IExternalLoginService` (+ `ExternalLoginIdentity` record) owning the shared find-or-link-or-create flow; both OAuth actions now build a normalized identity and delegate via `Result<T>` / `HandleServiceFailureResult` (controller −117/+35 lines). Behavior preserved exactly (status codes, per-provider email-verified semantics); +7 service tests → 77/77. |

**All 34 findings are now resolved** — 28 fixed, 2 partial (by design), 4 won't-fix, 0 open. Final verification: API Release build **0 warnings** + **77/77** tests; UI **lint + build + 54/54** tests; Bicep `az bicep build` **0 warnings** (the prior BCP081 notices were cleared by the apiVersion bump).

## Master ranking — all findings by Priority Score

**Completed:** ✅ fixed · ◑ partial · n/a won't-fix (by design/sample) · ☐ open

| # | ID | Completed | Area | Title | Impact | Risk | Effort | Priority | Severity |
|:---:|---|:---:|---|---|:---:|:---:|:---:|:---:|:---:|
| 1 | Q-CI-01 | n/a | Frontend UI & Infrastructure / CI | All workflow triggers commented out; docs claim PR/push validation | 4 | 5 | 1 | 9.00 | Critical |
| 2 | Q-BE-01 | ✅ | Backend (.NET API & tests) | Case-sensitive username lookup (inconsistent with email) breaks login | 3 | 3 | 1 | 6.00 | Medium |
| 3 | Q-FE-09 | ✅ | Frontend UI & Infrastructure / CI | Playwright `webServer` disabled — `test:e2e` fails without manual server | 2 | 4 | 1 | 6.00 | Medium |
| 4 | Q-CI-03 | ✅ | Frontend UI & Infrastructure / CI | NuGet cache key hashes non-existent `packages.lock.json` | 2 | 3 | 1 | 5.00 | Medium |
| 5 | Q-BE-03 | n/a | Backend (.NET API & tests) | `NoteService.GetNotesAsync` bypasses DB cursor pagination/filtering; repo filter is dead | 4 | 4 | 2 | 4.00 | High |
| 6 | Q-BE-02 | ◑ | Backend (.NET API & tests) | Dead `CursorConverters` methods + empty 0-byte duplicate file | 2 | 2 | 1 | 4.00 | Low |
| 7 | Q-CI-04 | n/a | Frontend UI & Infrastructure / CI | API deploy job lacks `environment:` (UI deploy has it) | 2 | 2 | 1 | 4.00 | Low |
| 8 | Q-CI-05 | ✅ | Frontend UI & Infrastructure / CI | `id-token: write` granted to build job that doesn't need it | 2 | 2 | 1 | 4.00 | Low |
| 9 | Q-BE-04 | ✅ | Backend (.NET API & tests) | `AuthController` swallows exceptions into 500 with no logging | 3 | 3 | 2 | 3.00 | Medium |
| 10 | Q-BE-05 | n/a | Backend (.NET API & tests) | Seeded admin user has no password and cannot log in | 3 | 3 | 2 | 3.00 | Medium |
| 11 | Q-FE-02 | ✅ | Frontend UI & Infrastructure / CI | Duplicated, divergent refresh-token implementations | 3 | 3 | 2 | 3.00 | Medium |
| 12 | Q-FE-04 | ✅ | Frontend UI & Infrastructure / CI | Forgot/Reset pages use raw `fetch`, bypassing apiClient interceptors | 3 | 3 | 2 | 3.00 | Medium |
| 13 | Q-FE-06 | ✅ | Frontend UI & Infrastructure / CI | No React error boundary anywhere | 3 | 3 | 2 | 3.00 | Medium |
| 14 | Q-INFRA-05 | ✅ | Frontend UI & Infrastructure / CI | Key Vault name can exceed 24-char limit; not enforced | 3 | 3 | 2 | 3.00 | Medium |
| 15 | Q-BE-06 | ✅ | Backend (.NET API & tests) | Dead/unused request DTOs (incl. misspelled `Goolgle`) | 2 | 1 | 1 | 3.00 | Low |
| 16 | Q-FE-05 | ✅ | Frontend UI & Infrastructure / CI | ForgotPasswordPage renders error UI that can never trigger | 1 | 2 | 1 | 3.00 | Low |
| 17 | Q-FE-10 | ✅ | Frontend UI & Infrastructure / CI | Unused dependency `react-icons` | 1 | 2 | 1 | 3.00 | Low |
| 18 | Q-INFRA-02 | ✅ | Frontend UI & Infrastructure / CI | Bicep parameters file uses JSONC comments in a `.json` | 1 | 2 | 1 | 3.00 | Low |
| 19 | Q-FE-03 | ✅ | Frontend UI & Infrastructure / CI | Duplicated OAuth button UI + handlers across 3 files | 2 | 3 | 2 | 2.50 | Medium |
| 20 | Q-FE-01 | ◑ | Frontend UI & Infrastructure / CI | Dead/unused exports: hooks, utils, types | 2 | 3 | 2 | 2.50 | Medium |
| 21 | Q-FE-07 | ✅ | Frontend UI & Infrastructure / CI | `no-explicit-any` disabled; `any` casts in core code | 2 | 3 | 2 | 2.50 | Medium |
| 22 | Q-BE-07 | ✅ | Backend (.NET API & tests) | Critical test-coverage gaps (auth, JWT, users, repos, middleware) | 4 | 4 | 4 | 2.00 | High |
| 23 | Q-FE-08 | ✅ | Frontend UI & Infrastructure / CI | Thin test coverage of critical auth/interceptor/route paths | 4 | 4 | 4 | 2.00 | High |
| 24 | Q-FE-11 | ✅ | Frontend UI & Infrastructure / CI | HomePage reimplements pagination; dedicated hook unused | 2 | 2 | 2 | 2.00 | Low |
| 25 | Q-FE-12 | ✅ | Frontend UI & Infrastructure / CI | `checkAuth` route detection via fragile string matching | 2 | 2 | 2 | 2.00 | Low |
| 26 | Q-BE-08 | ✅ | Backend (.NET API & tests) | Misspelling "occured" repeated across XML docs | 1 | 1 | 1 | 2.00 | Low |
| 27 | Q-BE-09 | ✅ | Backend (.NET API & tests) | Async naming + ignored `CancellationToken` in OAuth services | 1 | 1 | 1 | 2.00 | Low |
| 28 | Q-BE-10 | ✅ | Backend (.NET API & tests) | `AuthController` holds business logic + ~duplicated OAuth flows | 3 | 3 | 4 | 1.50 | Medium |
| 29 | Q-FE-13 | ✅ | Frontend UI & Infrastructure / CI | Inconsistent `username` vs `userName` field casing in types | 1 | 2 | 2 | 1.50 | Low |
| 30 | Q-CI-02 | ✅ | Frontend UI & Infrastructure / CI | Build/test steps duplicated across 3 workflows (not DRY) | 2 | 2 | 3 | 1.33 | Low |
| 31 | Q-INFRA-04 | ✅ | Frontend UI & Infrastructure / CI | Postgres/infra sizing hardcoded, not parameterized for prod | 2 | 2 | 3 | 1.33 | Low |
| 32 | Q-INFRA-03 | ✅ | Frontend UI & Infrastructure / CI | Unused/placeholder Bicep outputs (`connectionStringShape`, etc.) | 1 | 1 | 2 | 1.00 | Low |
| 33 | Q-INFRA-06 | ✅ | Frontend UI & Infrastructure / CI | Inconsistent resource API versions across modules | 1 | 1 | 2 | 1.00 | Low |
| 34 | Q-BE-11 | ✅ | Backend (.NET API & tests) | Controllers don't accept `CancellationToken` (deviates from AGENTS.md) | 1 | 1 | 3 | 0.67 | Low |

---

## Detailed findings — Backend (.NET API & tests)

### Findings

#### Q-BE-01: Case-sensitive username lookup (inconsistent with email) breaks login

> **Status (2026-06-17): ✅ Fixed** in `cf2e866` — both `GetByUsernameAsync` overloads now normalize the input and query `NormalizedUserName`. Also resolves Performance finding P-BE-08.

- **Severity / Priority:** Medium / 6.0
- **Impact / Risk / Effort:** 3 / 3 / 1
- **Location(s):** StarterApp.API/Data/Repositories/UserRepository.cs:88, StarterApp.API/Data/Repositories/UserRepository.cs:100, StarterApp.API/Data/Repositories/UserRepository.cs:104-113, StarterApp.API/Controllers/V1/AuthController.cs:129, StarterApp.API/Data/DatabaseSeeder.cs:141
- **Finding:** `GetByUsernameAsync` filters with a raw `user.UserName == username` comparison, whereas `GetByEmailAsync` correctly normalizes (`user.NormalizedEmail == email.ToUpperInvariant()`). ASP.NET Identity stores `NormalizedUserName` precisely so lookups can be case-insensitive. Login (`AuthController.LoginAsync`) calls `GetByUsernameAsync` first, so a user who registered `Admin` (or the seeded `admin`) cannot log in by typing `ADMIN`/`Admin` with different casing. On PostgreSQL the `UserName` column comparison is case-sensitive, so this is a genuine correctness bug, not just a style inconsistency.
- **Why it matters:** Username login silently fails for legitimate users depending on casing — a confusing, hard-to-diagnose authentication failure in a template whose headline feature is auth.
- **Recommendation:** Compare against the normalized column, mirroring the email path:
  ```csharp
  var normalized = this.UserManager.NormalizeName(username); // or username.ToUpperInvariant()
  return query.OrderBy(e => e.Id)
      .FirstOrDefaultAsync(user => user.NormalizedUserName == normalized, cancellationToken);
  ```
  Apply to both `GetByUsernameAsync` overloads (lines 83-101).

#### Q-BE-02: Dead `CursorConverters` methods + empty 0-byte duplicate file

> **Status (2026-06-17): ◑ Partially applied (by design)** — the empty 0-byte `Extensions/CursorConverters.cs` was deleted (`cf2e866`). The unused type-specific converter methods in `Utilities/CursorConverters.cs` are **intentionally kept** as reusable building blocks for apps built on the template (unused by the starter itself, useful downstream). Note: the original "24 dead methods" claim was inaccurate — the two generic factories dispatch to 20 of them; only 4 three-tuple variants were ever truly unreferenced.

- **Severity / Priority:** Low / 4.0
- **Impact / Risk / Effort:** 2 / 2 / 1
- **Location(s):** StarterApp.API/Utilities/CursorConverters.cs:16-358, StarterApp.API/Extensions/CursorConverters.cs (0 bytes)
- **Finding:** `Utilities/CursorConverters.cs` declares 26 public methods, but only the two generic ones — `CreateCompositeKeyConverter<TOrderKey,TEntityKey>()` (line 388) and `CreateCompositeCursorConverter<TOrderKey,TEntityKey>()` (line 428) — are referenced (from `EntityFrameworkExtensions.cs:367-368` and `CollectionExtensions.cs:512-513`). The 24 type-specific variants (`...IntStringInt`, `...DateTimeOffsetLong`, etc., ~360 lines) have zero call sites anywhere in the solution. Separately, `Extensions/CursorConverters.cs` is an empty 0-byte file (the real implementation lives under `Utilities/`), almost certainly an accidental leftover.
- **Why it matters:** ~360 lines of unreachable code plus a stray empty file in a template that people clone and read top-to-bottom; it inflates surface area, invites confusion about which file/method to use, and rots silently.
- **Recommendation:** Delete the empty `Extensions/CursorConverters.cs`. Remove the unused type-specific converter methods (keep only the two generic methods actually used), or, if they are intended as a public convenience API, add tests/usages demonstrating them so they are not dead.

#### Q-BE-03: `NoteService.GetNotesAsync` bypasses DB cursor pagination/filtering; repo filter is dead

> **Status (2026-06-17): ⛔ Won't fix — sample code** — `Notes` is demo/sample code slated for deletion. The README now instructs template users to remove the Notes (and Hello) samples instead of fixing them. No Notes code was modified. Applies to all `NoteService`/Notes-related findings.

- **Severity / Priority:** High / 4.0
- **Impact / Risk / Effort:** 4 / 4 / 2
- **Location(s):** StarterApp.API/Services/Domain/NoteService.cs:45-67, StarterApp.API/Data/Repositories/NoteRepository.cs:22-30, StarterApp.API/Data/Repositories/Repository.cs:134-159
- **Finding:** `GetNotesAsync` calls the predicate overload `SearchAsync(n => n.UserId == userId, track:false)` (Repository.cs:107-132), which materializes **every** note owned by the user into memory, then filters `Title` in memory and paginates in memory via `mapped.ToCursorPaginatedList(queryParameters)`. The DB-side path — `Repository.SearchAsync(TSearchParams)` (Repository.cs:134-159) combined with `NoteRepository.AddWhereClauses` (NoteRepository.cs:22-30), which already implements proper `EF.Functions.ILike` title filtering and cursor pagination — is **never invoked**, making that override dead code. Additionally, `var originalTitle = queryParameters.Title;` (NoteService.cs:51) is assigned and never used.
- **Why it matters:** This is the canonical demonstration of the template's headline cursor-pagination feature, yet it is wired to load the full result set and paginate client-side — performance/memory cost grows linearly with each user's note count, and the carefully written DB filter/pagination code is exercised by nothing. Cloners will copy this anti-pattern.
- **Recommendation:** Route through the paginated repository overload so filtering and cursor pagination happen in SQL:
  ```csharp
  queryParameters.UserId = this.currentUserService.UserId; // scope via AddWhereClauses
  var pagedList = await this.noteRepository.SearchAsync(queryParameters, track: false, cancellationToken);
  return new CursorPaginatedList<NoteDto, int>(
      pagedList.Select(NoteDto.FromEntity).ToList(),
      pagedList.HasNextPage, pagedList.HasPreviousPage,
      pagedList.StartCursor, pagedList.EndCursor, pagedList.TotalCount);
  ```
  Extend `NoteRepository.AddWhereClauses` to also apply the user-scope filter, and delete the unused `originalTitle` local.

#### Q-BE-04: `AuthController` swallows exceptions into 500 with no logging

> **Status (2026-06-17): ✅ Fixed** — `AuthController` now injects `ILogger`; both bare `catch` blocks log the exception and exclude `OperationCanceledException` (no more silently-swallowed errors or cancellation-as-500). Mirrors the build-clean logging pattern in `GlobalExceptionHandlerMiddleware` (CA1848 is `none` in `.editorconfig`); no tokens/codes are logged.

- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** StarterApp.API/Controllers/V1/AuthController.cs:264-267, StarterApp.API/Controllers/V1/AuthController.cs:384-387
- **Finding:** The Google and GitHub login actions wrap their bodies in `try { … } catch (HttpRequestException) { … } catch { return this.InternalServerError(...); }`. The bare `catch` swallows the exception entirely and returns a generic 500. `AuthController` injects no `ILogger`, so the original exception (stack trace, inner exception) is never recorded anywhere — and because it is caught here, the `GlobalExceptionHandlerMiddleware` (which would otherwise log it) never sees it. The bare `catch` also captures `OperationCanceledException` from `RequestAborted`, turning client cancellations into spurious 500s.
- **Why it matters:** Production OAuth failures become undiagnosable — operators get a 500 with a correlation id but no corresponding error log to correlate it to. This defeats the very observability story the rest of the codebase invests in.
- **Recommendation:** Either remove the bare `catch` and let `GlobalExceptionHandlerMiddleware` handle/log it, or inject `ILogger<AuthController>` and `logger.LogError(ex, ...)` before returning 500. Filter out cancellation: `catch (Exception ex) when (ex is not OperationCanceledException)`.

#### Q-BE-05: Seeded admin user has no password and cannot log in

> **Status (2026-06-17): ⛔ Won't fix** — by design the seeded admin uses social login, so no password is stored in code/the seeder; the "can't log in with a password" behavior is intended.

- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** StarterApp.API/Data/DatabaseSeeder.cs:138-160, StarterApp.API/Program.cs:58-74
- **Finding:** `SeedAdminUserAsync` creates the admin via `this.userManager.CreateAsync(adminUser)` with no password argument, so no password hash is set. The `--password`/`SeederPassword` value in `Program.cs` gates *running the seeder*; it is never applied to the admin account. Identity's `CheckPasswordSignInAsync` returns failure for users without a password hash, so the seeded admin can never authenticate through `/auth/login`.
- **Why it matters:** A clone-and-run starter template should yield a usable admin out of the box. As written, the first thing a new user tries (log in as admin) fails with "Invalid username or password", with no obvious cause.
- **Recommendation:** Seed the admin with a password (reuse the validated `SeederPassword`, or a dedicated `--adminPassword`): `await this.userManager.CreateAsync(adminUser, adminPassword);`. If passwordless admin is intentional, document it and provide a clear first-login path.

#### Q-BE-06: Dead/unused request DTOs (incl. misspelled `Goolgle`)

> **Status (2026-06-17): ✅ Fixed** — deleted both dead DTOs (`RegisterUserUsingGoolgleRequest.cs`, `GoogleLoginRequest.cs`) after confirming zero references solution-wide. Build clean, 19/19 tests.

- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 2 / 1 / 1
- **Location(s):** StarterApp.API/Models/Requests/Auth/RegisterUserUsingGoolgleRequest.cs:5, StarterApp.API/Models/Requests/Auth/GoogleLoginRequest.cs:5
- **Finding:** Neither `RegisterUserUsingGoolgleRequest` nor `GoogleLoginRequest` is referenced anywhere in the solution (the actual Google/GitHub endpoints use `OAuthCodeLoginRequest`). Both are dead types; `RegisterUserUsingGoolgleRequest` additionally has a typo (`Goolgle`) in both the file name and the type identifier.
- **Why it matters:** Dead, misspelled public types are visible to everyone cloning the template and erode confidence in the foundation's polish.
- **Recommendation:** Delete both files. If a Google-specific registration request is genuinely planned, reintroduce it with the correct spelling (`RegisterUserUsingGoogleRequest`) and an actual endpoint that consumes it.

#### Q-BE-07: Critical test-coverage gaps (auth, JWT, users, repos, middleware)

> **Status (2026-06-17): ✅ Fixed** — added 51 xUnit/Moq tests (JwtTokenService, UserService, AuthController, middleware); the suite is now **70/70** green. No production code changed and no bugs surfaced.

- **Severity / Priority:** High / 2.0
- **Impact / Risk / Effort:** 4 / 4 / 4
- **Location(s):** StarterApp.API.Tests/** (only 4 test files / 19 tests), StarterApp.API/Controllers/V1/AuthController.cs, StarterApp.API/Services/Auth/JwtTokenService.cs, StarterApp.API/Services/Domain/UserService.cs
- **Finding:** Tests exist only for `NoteService` (partial), `ServiceControllerBase`, `HelloController` (v1), and the in-memory `ToCursorPaginatedList`. For ~7,500 lines of backend, the highest-risk paths are entirely untested: `AuthController` (register/login/OAuth/refresh/CSRF/logout), `JwtTokenService` (JWT generation, refresh-token hashing/verification, expiry/eligibility), the whole `UserService` (authorization checks, username/password/email flows, forgot/reset/confirm), `UserRepository`/`NoteRepository`, the middleware (`GlobalExceptionHandlerMiddleware`, `CorrelationIdMiddleware`), both OAuth services, and `EmailTemplateService`. Even within the tested `NoteService`, `GetNotesAsync` (the buggy method in Q-BE-03) and `UpdateNoteAsync` success path are untested.
- **Why it matters:** A "production-grade foundation" advertises correctness it does not verify. Security-sensitive logic (token verification, ownership/authorization gating) can regress undetected as users extend the template.
- **Recommendation:** Prioritize unit tests for `JwtTokenService` (hash round-trip via `CreateTokenHash`/`VerifyTokenHash`, expiry pruning, refresh eligibility) and `UserService` authorization branches (forbidden/not-found/validation), then `AuthController` happy/error paths with mocked services, and at least one integration-style test per middleware. Add the missing `NoteService.GetNotesAsync` test once Q-BE-03 is fixed.

#### Q-BE-08: Misspelling "occured" repeated across XML docs

> **Status (2026-06-17): ✅ Fixed** — all 12 `occured` → `occurred` in the XML doc comments (`AuthController`, `UsersController`).

- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 1 / 1 / 1
- **Location(s):** StarterApp.API/Controllers/V1/AuthController.cs (6×), StarterApp.API/Controllers/V1/UsersController.cs (6×)
- **Finding:** The `<response code="500">If an unexpected server error occured.</response>` doc-comments consistently misspell "occurred" as "occured" (12 occurrences). These surface in generated OpenAPI/Swagger documentation.
- **Why it matters:** User-facing API documentation typos undermine the polished impression a starter template should give.
- **Recommendation:** Find/replace `occured` → `occurred` across the doc comments.

#### Q-BE-09: Async naming + ignored `CancellationToken` in OAuth services

> **Status (2026-06-17): ✅ Fixed** — `GetGitHubUser` → `GetGitHubUserAsync` (impl + interface + caller). Google's `ValidateAsync` has no `CancellationToken` overload, so the parameter is kept with a comment explaining it can't be honored.

- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 1 / 1 / 1
- **Location(s):** StarterApp.API/Services/Auth/GitHubOAuthService.cs:62, StarterApp.API/Services/Auth/IGitHubOAuthService.cs, StarterApp.API/Services/Auth/GoogleOAuthService.cs:68-76
- **Finding:** `GitHubOAuthService.GetGitHubUser` is an `async Task<…>` method but lacks the `Async` suffix used everywhere else in the codebase (and on its sibling `GetGitHubEmailsAsync`). Separately, `GoogleOAuthService.ValidateIdTokenAsync` accepts a `cancellationToken` parameter but never passes it to `GoogleJsonWebSignature.ValidateAsync`, so the token is silently ignored (misleading to callers who assume cancellation is honored).
- **Why it matters:** Inconsistent async naming and dropped cancellation tokens are exactly the small inconsistencies that proliferate when copied from a template.
- **Recommendation:** Rename to `GetGitHubUserAsync` (and the interface member). For `ValidateIdTokenAsync`, either thread the token through where the underlying API supports it or document why it is intentionally unused.

#### Q-BE-10: `AuthController` holds business logic + ~duplicated OAuth flows

> **Status (2026-06-17): ✅ Fixed** — extracted `IExternalLoginService`/`ExternalLoginIdentity`; the Google/GitHub actions now build a normalized identity and delegate the find-or-link-or-create flow via `Result<T>` + `HandleServiceFailureResult` (controller −117/+35 lines). Behavior preserved exactly (status codes + per-provider email-verified semantics); +7 service tests (suite 77/77). Follow-up: the confirmation-email send was consolidated into a single `UserService.SendEmailConfirmationAsync(User)` — removing the duplicated `SendConfirmEmailLink` from both `AuthController` and `ExternalLoginService` (the core now lives in exactly one place).

- **Severity / Priority:** Medium / 1.5
- **Impact / Risk / Effort:** 3 / 3 / 4
- **Location(s):** StarterApp.API/Controllers/V1/AuthController.cs:169-268 (Google), StarterApp.API/Controllers/V1/AuthController.cs:283-388 (GitHub)
- **Finding:** Unlike `UsersController`/`NotesController` (which delegate to services returning `Result<T>` and map failures via `HandleServiceFailureResult`), `AuthController` performs substantial domain logic directly: provider token exchange, user lookup, account linking, user creation, `SaveChangesAsync`, and cookie issuance. The Google (169-268) and GitHub (283-388) actions are near-duplicate ~100-line blocks differing only in provider specifics (find-or-link-or-create user, then issue tokens). This violates the service-result architecture documented in `AGENTS.md` and demonstrated by the other controllers.
- **Why it matters:** Inconsistent architecture in the most security-critical controller; the duplication means a fix to the find/link/create logic must be made in two places, and the inline persistence is untestable without an integration host (compounding Q-BE-07).
- **Recommendation:** Extract an `IAuthService`/`IExternalLoginService` that returns `Result<LoginResponse>` and encapsulates the shared "resolve-or-provision user from a verified external identity" flow; have both OAuth actions call it with provider-specific adapters. Controllers then become thin and consistent with the rest of the codebase.

#### Q-BE-11: Controllers don't accept `CancellationToken` (deviates from AGENTS.md)

> **Status (2026-06-17): ✅ Fixed** — every async controller action (Auth/Users/Notes) now takes a `CancellationToken` parameter, which ASP.NET Core's model binder auto-binds to `RequestAborted`; the token is threaded through the private helpers, and the AGENTS.md carve-out was reverted to the clean convention. Synchronous redirect/ping actions are left as-is. Build 0 warnings, 70/70 tests.

- **Severity / Priority:** Low / 0.67
- **Impact / Risk / Effort:** 1 / 1 / 3
- **Location(s):** StarterApp.API/Controllers/V1/UsersController.cs:42, StarterApp.API/Controllers/V1/NotesController.cs:46, StarterApp.API/Controllers/V1/AuthController.cs:82, AGENTS.md:33
- **Finding:** `AGENTS.md` states "All async methods must accept and pass `CancellationToken`." Controller actions instead omit the parameter and pull `this.HttpContext.RequestAborted` internally. This is a defensible, consistent choice (and `RequestAborted` is the right token), but it does technically deviate from the stated convention, and binding a `CancellationToken` action parameter is the idiomatic ASP.NET Core approach that also makes actions easier to unit-test.
- **Why it matters:** Minor convention drift; mainly noted because the convention is explicitly written down and uniformly not followed at the controller layer.
- **Recommendation:** Either add `CancellationToken cancellationToken` parameters to controller actions (model binding supplies `RequestAborted`) for literal convention adherence and testability, or amend `AGENTS.md` to carve out controllers as using `HttpContext.RequestAborted`.

### Positive observations
- **Consistent Result pattern.** `UsersController`, `NotesController`, `UserService`, and `NoteService` cleanly apply `Result<T>` + `ServiceControllerBase.HandleServiceFailureResult` with a tidy `DomainErrorType → HTTP status` mapping (ServiceControllerBase.cs:91-99), and it is unit-tested (ServiceControllerBaseTests.cs).
- **Solid refresh-token hygiene.** `JwtTokenService` hashes refresh tokens with `HMACSHA512` + per-token salt, compares with constant-time `CryptographicOperations.FixedTimeEquals`, prunes expired tokens, and validates key size before signing (JwtTokenService.cs:127-203).
- **Careful global error handling.** `GlobalExceptionHandlerMiddleware` guards `Response.HasStarted`, re-establishes the logging scope, logs full exceptions server-side, and returns generic, leakage-free `problem+json` bodies with a correlation id (GlobalExceptionHandlerMiddleware.cs).
- **Convention adherence is enforced and clean.** `.editorconfig` mandates `this.` qualification at `error` severity and `using` placement; the build is warning-clean. File-scoped namespaces, `sealed` classes, and `record … { get; init; }` for DTOs/requests/settings are used consistently (e.g., NoteDto.cs, RegisterUserRequest.cs, AuthenticationSettings.cs).
- **Justified versioning duplication.** The V1/V2 `HelloController` pair is an intentional, minimal API-versioning demonstration — appropriate duplication, not a smell.
- **Correct DI lifetimes & options usage.** Repositories and domain/auth/core services are registered `Scoped`; settings are bound via `IOptions<T>` and consumed by value in constructors (AuthenticationServiceCollectionExtensions.cs, *ServiceCollectionExtensions.cs).
- **Sensible EF model config.** Composite keys for `UserRole`/`RefreshToken`/`LinkedAccount`, enum-to-string conversion, DB default timestamps, and cascade delete for notes are configured explicitly in `DataContext.OnModelCreating`.

### Out-of-scope notes
These are primarily security/performance concerns (owned by other reviewers); listed only where they intersect quality/correctness:
- **No brute-force lockout.** `UserRepository.CheckPasswordAsync` calls `CheckPasswordSignInAsync(user, password, lockoutOnFailure: false)` (UserRepository.cs:137) — password login never triggers Identity lockout. (Security.)
- **Cross-user device-token scan.** `GetRefreshTokensForDeviceAsync` filters only by `DeviceId` (UserRepository.cs:159-180), returning tokens across all users for a client-chosen id before hash matching. Functionally safe due to salted-hash verification, but worth a security look. (Security.)
- **Seeder `ClearAllDataAsync` loads all rows to delete** via `DbSet.RemoveRange(dbSet)` (EntityFrameworkExtensions.cs:35-40) rather than a bulk `ExecuteDelete`. Acceptable for a dev seeder; noted for completeness. (Performance.)
- **Cookie `SameSite=None`** on refresh/CSRF cookies (AuthController.cs:552-572) is required for cross-origin but broadens exposure; verify against the deployment topology. (Security.)

---

## Detailed findings — Frontend UI & Infrastructure / CI

### Findings

#### Q-CI-01: All workflow triggers commented out; docs claim automatic PR/push validation

> **Status (2026-06-17): ⛔ Won't fix (by design) — documented** in `cf2e866` — triggers intentionally remain commented so this *template* repo stays idle. The README "Using this template" section now tells users to uncomment them, and each workflow `on:` block has an explanatory comment.

- **Severity / Priority:** Critical / 9.0
- **Impact / Risk / Effort:** 4 / 5 / 1
- **Location(s):** `.github/workflows/ci.yml:3-9`, `.github/workflows/build-and-deploy-api.yml:19-26`, `.github/workflows/build-and-deploy-ui.yml:18-25`; docs: `CI/README.md:51-60`, `README.md:21,33`
- **Finding:** Every workflow's real trigger is commented out — only `workflow_dispatch` remains. `ci.yml` has `pull_request`/`push` commented (lines 5-9); both deploy workflows have their `push` blocks commented. Yet `CI/README.md:53` states "Triggers on `pull_request` and `push` to non-main/non-master branches", `README.md:21` advertises "CI/CD: GitHub Actions (PR validation, …)" and `README.md:33` lists the test stack. Out of the box, nothing runs automatically — no PR validation, no auto-deploy.
- **Why it matters:** This is a "batteries-included, production-grade" template people clone expecting working CI. The advertised safety net is silently disabled, and the documentation actively misrepresents behavior. A cloner gets green-looking docs but zero enforcement until they discover the commented triggers.
- **Recommendation:** Enable the intended triggers by default (uncomment), keeping `workflow_dispatch` as well. For `ci.yml`:
  ```yaml
  on:
    pull_request:
    push:
      branches-ignore: [main, master]
    workflow_dispatch:
  ```
  If intentionally disabled for first-clone safety, say so explicitly in `CI/README.md`/`README.md` ("triggers are commented out — enable them after configuring secrets") instead of claiming they're active.

#### Q-FE-09: Playwright `webServer` disabled — `npm run test:e2e` fails without a manually-started server

> **Status (2026-06-17): ✅ Fixed** in `cf2e866` — the `webServer` block is enabled so Playwright boots the app automatically (`reuseExistingServer` outside CI).

- **Severity / Priority:** Medium / 6.0
- **Impact / Risk / Effort:** 2 / 4 / 1
- **Location(s):** `starter-app-ui/playwright.config.ts:20-24`; `README.md:136`
- **Finding:** The `webServer` block is commented out, but `use.baseURL` is `http://localhost:5173` and `e2e/smoke.spec.ts` does `page.goto('/')`. Running `npm run test:e2e` with no dev server running fails immediately with connection-refused. `README.md:136` only parenthetically notes "(needs the app running)". E2E is also not run by any workflow.
- **Why it matters:** A documented command that fails on a clean checkout erodes trust in the template and the "Playwright (UI e2e)" claim.
- **Recommendation:** Uncomment and complete the `webServer` config so Playwright boots the app automatically:
  ```ts
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI
  }
  ```

#### Q-CI-03: NuGet cache key hashes `packages.lock.json` files that don't exist

> **Status (2026-06-17): ✅ Fixed (revised approach)** in `cf2e866` — the cache key now hashes `**/*.csproj` only and restore stays as plain `dotnet restore`. NuGet lock files were trialed then deliberately reverted: all versions are exact-pinned, so hashing the `.csproj` files is sufficient and avoids lock-file maintenance overhead in a template.

- **Severity / Priority:** Medium / 5.0
- **Impact / Risk / Effort:** 2 / 3 / 1
- **Location(s):** `.github/workflows/ci.yml:29`, `.github/workflows/build-and-deploy-api.yml:50`
- **Finding:** Cache key is `${{ runner.os }}-nuget-${{ hashFiles('**/packages.lock.json', '**/*.csproj') }}`, but the repo has no `packages.lock.json` anywhere (verified — no lock files, no `RestorePackagesWithLockFile`). The key therefore depends only on `*.csproj`, so transitive-dependency changes never invalidate the cache, risking stale restores. Restore is also non-deterministic without lock files.
- **Why it matters:** Stale or incorrect NuGet caches cause confusing, hard-to-reproduce build differences — exactly the kind of footgun a starter should avoid.
- **Recommendation:** Either enable lock files (`<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` and commit `packages.lock.json`, then `dotnet restore --locked-mode`), or drop the non-existent glob from the key and rely on `*.csproj` + `Directory.Packages.props` if used.

#### Q-CI-04: API deploy job lacks an `environment:` while UI deploy has one

> **Status (2026-06-17): ⛔ Won't fix** — leaving the API deploy job without an `environment:` gate is an accepted decision.

- **Severity / Priority:** Low / 4.0
- **Impact / Risk / Effort:** 2 / 2 / 1
- **Location(s):** `.github/workflows/build-and-deploy-api.yml:67-71` vs `.github/workflows/build-and-deploy-ui.yml:75-81`
- **Finding:** The UI `deploy` job declares `environment: { name: github-pages, url: … }`, enabling deployment tracking/protection rules. The API `deploy` job (which provisions infra and pushes to App Service — higher blast radius) has none.
- **Why it matters:** Inconsistent deployment governance; the riskier deploy has no environment gate, deployment history, or required-reviewer hook.
- **Recommendation:** Add an `environment: { name: production, url: https://${{ vars.AZURE_WEBAPP_NAME }}.azurewebsites.net }` to the API deploy job for parity and to enable branch/approval protections.

#### Q-CI-05: `id-token: write` granted workflow-wide, including the build job that doesn't use OIDC

> **Status (2026-06-17): ✅ Fixed** — the workflow-level `permissions` block was removed in favor of least-privilege job-level permissions: `build` gets `contents: read`, and `deploy` gets `id-token: write` + `contents: read`.

- **Severity / Priority:** Low / 4.0
- **Impact / Risk / Effort:** 2 / 2 / 1
- **Location(s):** `.github/workflows/build-and-deploy-api.yml:28-30`
- **Finding:** `permissions: id-token: write` is set at workflow scope, so the `build` job (pure `dotnet publish` + artifact upload, no Azure login) also receives token-minting permission. Only the `deploy` job calls `azure/login@v2`.
- **Why it matters:** Least-privilege/clarity maintainability issue — broader-than-needed permissions on a template are copied forward by every cloner.
- **Recommendation:** Move `permissions: { id-token: write }` to the `deploy` job and give `build` only `contents: read`.

#### Q-FE-02: Duplicated, divergent refresh-token implementations

> **Status (2026-06-17): ✅ Fixed** — consolidated into a single `refreshAccessToken()` in `axiosConfig.ts`; `authApi.refreshToken()` delegates to it. The 401 interceptor still uses the bare-axios routine (no interceptor recursion); the divergent duplicate is gone.

- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** `starter-app-ui/src/services/axiosConfig.ts:150-181` and `starter-app-ui/src/services/auth.ts:91-99`
- **Finding:** Two refresh flows exist. `axiosConfig.refreshToken()` calls bare `axios.post(.../refreshToken)`, manually reads the CSRF cookie, bails if absent, and reads `response.data.token`. `authApi.refreshToken()` posts via `apiClient` (so the request interceptor injects CSRF) and is what `AuthContext.checkAuth` (`contexts/AuthContext.tsx:146`) and `useRefreshToken` use. The two paths have different CSRF handling and error semantics for the same endpoint.
- **Why it matters:** Behavior drift — fixing/altering refresh logic in one place silently leaves the other stale; the interceptor's manual CSRF read duplicates the request interceptor that already adds `X-CSRF-Token`.
- **Recommendation:** Have the response interceptor reuse a single refresh routine. Since the interceptor can't import its own `apiClient` recursively for the refresh, factor a small `postRefresh()` that uses a bare axios call *or* a flagged `apiClient` request, and call it from both `authApi.refreshToken` and the interceptor. Eliminate one implementation.

#### Q-FE-04: Forgot/Reset password pages use raw `fetch`, bypassing the shared apiClient

> **Status (2026-06-17): ✅ Fixed (also fixed a latent bug)** — added `authApi.forgotPassword`/`resetPassword` (via `apiClient`) plus request types, and switched both pages to them. Validation revealed the old raw `fetch` was calling **non-existent routes** (`/api/v1/auth/forgot-password`); corrected to the real `POST /api/v1/users/forgotPassword` and `/resetPassword`. The enumeration-safe "always success" UX on Forgot Password is preserved.

- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** `starter-app-ui/src/pages/ForgotPasswordPage.tsx:20-24`, `starter-app-ui/src/pages/ResetPasswordPage.tsx:57-63`
- **Finding:** Both pages call `fetch(\`${import.meta.env.VITE_API_BASE_URL}/api/v1/auth/...\`)` directly instead of going through `services/auth.ts` + `apiClient`. They miss the `X-Correlation-Id`, CSRF header, `withCredentials`, and the `ApiError`/`ProblemDetails` normalization that every other call gets. There are also no `authApi.forgotPassword`/`resetPassword` methods, so the service layer is inconsistently complete.
- **Why it matters:** Two parallel HTTP styles in the same app is a maintainability hazard and produces inconsistent error UX (these pages can't surface `ProblemDetails` errors). Correlation IDs are lost for these flows.
- **Recommendation:** Add `forgotPassword(email)` and `resetPassword({email, token, password})` to `authApi` using `apiClient`, and call those from the pages. Map failures through `ApiError` like the rest of the app.

#### Q-FE-06: No React error boundary anywhere in the tree

> **Status (2026-06-17): ✅ Fixed** — added a class-based `ErrorBoundary` (`components/ErrorBoundary.tsx`, `getDerivedStateFromError`/`componentDidCatch`) wrapping `<App />` in `main.tsx`, rendering a friendly fallback with a reload action.

- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** `starter-app-ui/src/main.tsx:28-37`, `starter-app-ui/src/App.tsx:12-43`
- **Finding:** Neither `main.tsx` nor `App.tsx` wraps the app in an error boundary. A render-time throw (e.g., `decodeJwtToken` edge case, a HeroUI render error) unmounts the whole SPA to a blank page with no recovery UI.
- **Why it matters:** A production-grade template should ship a top-level boundary; cloners inherit a fragile root. There's also no fallback for chunk-load failures.
- **Recommendation:** Add an `ErrorBoundary` component (class component with `componentDidCatch`/`getDerivedStateFromError`, or `react-error-boundary`) wrapping `<App />` in `main.tsx`, rendering a friendly fallback + reload action.

#### Q-INFRA-05: Key Vault name can exceed the 24-character Azure limit; nothing enforces it

> **Status (2026-06-17): ✅ Fixed** — vault name is now `take('${namePrefix}-kv-${environment}', 24)` with a trailing-hyphen guard, so it is always a valid ≤24-char Key Vault name. Verified with `az bicep build`.

- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 3 / 3 / 2
- **Location(s):** `CI/Azure/modules/keyVault.bicep:1,13`, `CI/Azure/main.bicep:4-5`
- **Finding:** `namePrefix` is capped at `@maxLength(16)`, and `vaultName = '${namePrefix}-kv-${environment}'`. Key Vault names must be ≤24 chars. With `environment='staging'`, a 16-char prefix yields `…-kv-staging` = 27 chars → deployment fails. The module comment even says "must be 3–24 chars total" but no constraint enforces it.
- **Why it matters:** A latent deploy-time failure that only appears for longer env names — surprising and hard to diagnose for a cloner who picks `environment: staging`.
- **Recommendation:** Either tighten `@maxLength` on `namePrefix` accounting for the longest supported env, or derive a guaranteed-valid name, e.g. `var vaultName = take('${namePrefix}kv${environment}', 24)` (drop hyphens) or `take(toLower(replace('${namePrefix}-kv-${environment}','-','')), 24)`, and add an `@maxLength(24)`-validated assertion/comment.

#### Q-FE-05: ForgotPasswordPage renders an error component that can never display

> **Status (2026-06-17): ✅ Fixed** — removed the dead `error` state, the unreachable `<ApiErrorDisplay>`, and its now-unused import; the enumeration-safe generic-success behavior is preserved.

- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 1 / 2 / 1
- **Location(s):** `starter-app-ui/src/pages/ForgotPasswordPage.tsx:9,25-30,74`
- **Finding:** `error` state is declared and `{error && <ApiErrorDisplay … />}` is rendered (line 74), but the only `catch` block is empty (lines 25-27, intentionally swallowing for the "always show success" pattern) and `setError` is never called. The error branch is dead.
- **Why it matters:** Dead UI + unused state mislead future maintainers into thinking errors surface here; `ApiErrorDisplay`/`setError` import looks load-bearing but isn't.
- **Recommendation:** Remove the unused `error` state and `ApiErrorDisplay` from this page, or genuinely surface non-security validation errors (e.g., malformed email) if desired.

#### Q-FE-10: Unused dependency `react-icons`

> **Status (2026-06-17): ✅ Fixed** — `react-icons` removed via `npm uninstall` (zero imports). Also resolves Performance finding P-FE-03.

- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 1 / 2 / 1
- **Location(s):** `starter-app-ui/package.json:26`
- **Finding:** `react-icons` is a declared dependency but has zero imports anywhere in `src/` (verified). All icons are inlined SVGs.
- **Why it matters:** Dead dependencies bloat install size and confuse cloners about the intended icon strategy.
- **Recommendation:** Remove `react-icons` from `dependencies` (or actually use it to replace the duplicated inline SVGs — see Q-FE-03).

#### Q-INFRA-02: Bicep parameters file uses JSONC comments in a `.json` file

> **Status (2026-06-17): ✅ Fixed** — the `//` comments were removed so the parameters file is strict, valid JSON (verified via `ConvertFrom-Json`); the override guidance remains in `CI/README.md`.

- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 1 / 2 / 1
- **Location(s):** `CI/Azure/parameters/main.parameters.dev.json:15-16`
- **Finding:** The `.json` parameters file contains `//` comments. `az deployment` tolerates JSONC, but strict JSON tooling (linters, `JSON.parse`, some editors/CI validators) will reject it.
- **Why it matters:** A template that "looks like JSON but isn't" can break unrelated tooling and is an easy paper-cut for cloners.
- **Recommendation:** Move the guidance into `CI/README.md` (already documented there) and keep the parameters file strict JSON, or rename to `.jsonc` if comments must stay.

#### Q-FE-03: Duplicated OAuth button UI + handlers across Login, Register, and OAuthCallback

> **Status (2026-06-17): ✅ Fixed** — extracted `components/SocialLoginButtons.tsx` (shared buttons + OAuth-start handlers, used by Login & Register) and `components/oauthIcons.tsx` (shared `GitHubIcon`/`GoogleIcon`, reused by the callback page); removed the triplicated markup and converged the `aria-hidden` inconsistency.

- **Severity / Priority:** Medium / 2.5
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `starter-app-ui/src/pages/LoginPage.tsx:36-50,107-122`, `starter-app-ui/src/pages/RegisterPage.tsx:58-72,157-172`, `starter-app-ui/src/pages/OAuthCallbackPage.tsx:17-33`
- **Finding:** `handleGitHubLogin`/`handleGoogleLogin` are copy-pasted verbatim into both LoginPage and RegisterPage, and the multi-`<path>` Google + GitHub SVG markup is duplicated three times (Login, Register, and again inside `providerConfigs` in OAuthCallbackPage).
- **Why it matters:** ~60+ lines of duplicated JSX/handlers; a change to OAuth wiring or branding must be made in three places, and they can drift (the Google SVG already differs slightly: LoginPage omits `aria-hidden` that RegisterPage includes).
- **Recommendation:** Extract a `SocialLoginButtons` component (rendering both providers, owning the `redirectToOAuth` try/catch and error callback) used by Login + Register, and centralize the provider SVGs in one `oauthIcons.tsx` reused by the callback page too.

#### Q-FE-01: Dead/unused exported hooks, utilities, and types

> **Status (2026-06-17): ◑ Mostly fixed (by design)** — removed the dead auth hooks, notes hooks, oauth-callback aliases, and `OAuthLoginRequest`. The two utility helpers `hasAdminRole` and `isTokenExpired` were **kept (restored)** as useful self-contained scaffolding (no competing pattern, unlike the auth hooks) and are now covered by unit tests; `decodeJwtToken` and the used oauth helpers also remain.

- **Severity / Priority:** Medium / 2.5
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `starter-app-ui/src/hooks/api.ts:22-44,97-107,122-141`; `starter-app-ui/src/utils/auth.ts:38-50`; `starter-app-ui/src/utils/oauthUtils.ts:133-138`; `starter-app-ui/src/types/auth.ts:22-25`
- **Finding:** Several exports have no consumers (verified by grep): hooks `useLogin`, `useRegister`, `useLogout`, `useRefreshToken` (AuthContext calls `authApi.*` directly, not these), `useUpdateNote`, and `useNotesCursorPaginated`; utils `hasAdminRole`, `isTokenExpired`; oauth wrappers `handleGitHubCallbackFromUrl`, `handleGoogleCallbackFromUrl`, `handleGitHubCallback`, `handleGoogleCallback`; type `OAuthLoginRequest`. `noUnusedLocals` can't catch these because they're exported.
- **Why it matters:** In a clone-and-build template, dead scaffolding is actively harmful — cloners can't tell what's wired vs. aspirational, and `useRefreshToken` etc. duplicate AuthContext responsibilities (state-management confusion).
- **Recommendation:** Remove the unused exports, or if they're meant as "ready to use" examples, consolidate them and document that intent. At minimum delete the OAuth wrapper aliases that nothing calls and `OAuthLoginRequest`.

#### Q-FE-07: `@typescript-eslint/no-explicit-any` disabled; `any` used in core code

> **Status (2026-06-17): ✅ Fixed** — the rule is now `error`; the refresh queue is strongly typed (token `string`/`null`, errors `unknown`) and the `globalThis` cast was narrowed. One genuine vite-plugin type mismatch keeps a single commented `eslint-disable`.

- **Severity / Priority:** Medium / 2.5
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `starter-app-ui/eslint.config.js:23`; usages: `starter-app-ui/src/services/axiosConfig.ts:46-51,68`, `starter-app-ui/vitest.config.ts:5`, `starter-app-ui/vitest.setup.ts:14`
- **Finding:** ESLint turns `no-explicit-any` fully off. The refresh queue (`failedQueue: Array<{ resolve(value?: any); reject(error?: any) }>`, `processQueue(error: any, …)`) and `react() as any` rely on it. For a "production-grade" foundation this weakens the otherwise-strict TS posture (`strict`, `noUnusedLocals`, etc.).
- **Why it matters:** Blanket `any` tolerance lets unsafe types spread as the app grows; the queue could be strongly typed (`string | null` token, `unknown` error).
- **Recommendation:** Set the rule to `'warn'` (or `'error'` with targeted `// eslint-disable-next-line` where unavoidable) and type the queue: `Array<{ resolve: (token: string | null) => void; reject: (err: unknown) => void }>`, `processQueue(error: unknown, token: string | null = null)`.

#### Q-FE-08: Thin test coverage of critical auth / interceptor / routing paths

> **Status (2026-06-17): ✅ Fixed** — added 36 Vitest tests covering `decodeJwtToken`, the OAuth callback, an end-to-end 401→refresh→retry interceptor test, `AuthContext`, and `ProtectedRoute`; suite now **46/46**, fully typed (no `any`).

- **Severity / Priority:** High / 2.0
- **Impact / Risk / Effort:** 4 / 4 / 4
- **Location(s):** `starter-app-ui/src/utils/__tests__/utils.test.ts`, `errors.test.ts`; missing coverage for `services/axiosConfig.ts`, `contexts/AuthContext.tsx`, `components/ProtectedRoute.tsx`, `utils/auth.ts` (`decodeJwtToken`), `services/auth.ts`, OAuth callback flow
- **Finding:** Only two unit files exist — `validatePassword`/`handleOAuthCallback` (partial: missing the happy-path `{code}` and `missing_code` branches) and `ApiError`. The riskiest logic is untested: the 401→refresh→retry interceptor with its `isRefreshing`/`failedQueue` concurrency (`axiosConfig.ts:63-148`), `decodeJwtToken` role normalization (`utils/auth.ts:17-36`), `AuthContext` login/refresh/checkAuth reducer flows, and `ProtectedRoute` redirect/spinner behavior. The single e2e is a smoke check.
- **Why it matters:** The template's headline value is auth + interceptors; these are precisely the parts most likely to regress when cloners modify them, and they have no safety net. `README.md:33` advertises a full test stack the coverage doesn't back up.
- **Recommendation:** Add focused tests: (1) interceptor — mock a 401 + `x-token-expired`, assert single refresh and queued requests replay with the new token; (2) `decodeJwtToken` — string vs. array `role`, malformed token → `null`; (3) `AuthContext` via `@testing-library/react` — login success/failure transitions; (4) `ProtectedRoute` — loading spinner, redirect when unauthenticated. Add the missing `handleOAuthCallback` happy-path/`missing_code` cases.

#### Q-FE-11: HomePage reimplements cursor pagination; the dedicated hook is unused

> **Status (2026-06-17): ✅ Fixed** — HomePage now uses a `useInfiniteNotes` (`useInfiniteQuery`) hook (cached + invalidation-aware); the bespoke pagination state and the unused `useNotesCursorPaginated` are gone. Create/delete invalidation refreshes the list.

- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 2 / 2 / 2
- **Location(s):** `starter-app-ui/src/pages/HomePage.tsx:14-42`, `starter-app-ui/src/hooks/api.ts:122-141`
- **Finding:** `HomePage` manages `pages`/`loadingMore` local state and calls `notesApi.getNotes(...)` directly for "load more" (`HomePage.tsx:31-42`), bypassing React Query's cache. Meanwhile `useNotesCursorPaginated` (with its own `loadMore`) was written for exactly this and is never imported (Q-FE-01).
- **Why it matters:** Duplicated pagination logic, an unused hook, and a manual fetch that escapes the query cache (inconsistent with the rest of the app's React Query usage).
- **Recommendation:** Either delete `useNotesCursorPaginated` or adopt it (or `useInfiniteQuery`) in `HomePage`, removing the bespoke `pages`/`loadingMore` state.

#### Q-FE-12: `checkAuth` route detection relies on fragile manual hash string matching

> **Status (2026-06-17): ✅ Fixed** — replaced the hash substring-matching with `src/constants/routes.ts` (`isPublicRoute` + a shared `routePaths` list used by the router); all public routes (incl. forgot/reset-password) are now covered.

- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 2 / 2 / 2
- **Location(s):** `starter-app-ui/src/contexts/AuthContext.tsx:130-137`
- **Finding:** `checkAuth` reads `window.location.hash.replace('#','')` and tests `includes('/auth/') || includes('/login') || includes('/register')` to skip the auth bootstrap on auth pages. This hand-rolled matching lives outside the router, is easy to break when routes are renamed/added (e.g., `/forgot-password` isn't excluded), and bypasses React Router's location APIs.
- **Why it matters:** Brittle coupling between auth logic and URL string shapes; a route rename silently changes auth bootstrap behavior.
- **Recommendation:** Drive this from router state (e.g., pass a flag from route definitions, or use `useLocation` in a wrapper) rather than substring-matching the raw hash, and centralize the list of "public" routes.

#### Q-FE-13: Inconsistent `username` vs `userName` field casing in auth types

> **Status (2026-06-17): ✅ Fixed** — aligned on `userName` end-to-end: backend `LoginRequest.Username` → `UserName` (+ AuthController + tests), and the frontend `LoginRequest` type, `LoginPage`, and AuthContext test updated to match.

- **Severity / Priority:** Low / 1.5
- **Impact / Risk / Effort:** 1 / 2 / 2
- **Location(s):** `starter-app-ui/src/types/auth.ts:9-20`; usage `pages/LoginPage.tsx:27`, `pages/RegisterPage.tsx:49`
- **Finding:** `LoginRequest` uses `username` (all-lower) while `RegisterRequest`, `User`, and JWT decoding use `userName` (camel). Both are sent to the API.
- **Why it matters:** Easy to introduce bugs when copying request shapes; readers can't tell which casing the backend actually expects. If it mirrors backend DTOs intentionally, that's undocumented.
- **Recommendation:** Align on one casing (likely `userName`) unless the backend genuinely requires `username` for login — in which case add a comment noting the deliberate mismatch.

#### Q-CI-02: Build/test steps duplicated across three workflows (not DRY)

> **Status (2026-06-17): ✅ Fixed** — extracted `.github/actions/build-test-api` and `build-test-ui` composite actions; CI and both deploy workflows call them, so Node/.NET versions and the build/test steps live in one place. Deploy reuses setup/restore via a `run-build-test: false` input.

- **Severity / Priority:** Low / 1.33
- **Impact / Risk / Effort:** 2 / 2 / 3
- **Location(s):** `.github/workflows/ci.yml:12-70`, `build-and-deploy-api.yml:32-65`, `build-and-deploy-ui.yml:36-67`
- **Finding:** The .NET setup/restore/cache block is repeated in `ci.yml` (api job) and `build-and-deploy-api.yml`; the Node setup/install/lint/test/build block is repeated in `ci.yml` (ui job) and `build-and-deploy-ui.yml`. Any change (e.g., Node version bump from `'24'`) must be made in two places.
- **Why it matters:** Drift risk and maintenance overhead in the canonical CI a template ships.
- **Recommendation:** Extract reusable workflows (`workflow_call`) or composite actions for "build-test-api" and "build-test-ui", and call them from both CI and deploy pipelines.

#### Q-INFRA-04: Postgres/infra sizing hardcoded; not parameterized for non-dev environments

> **Status (2026-06-17): ✅ Fixed** — Postgres + observability sizing is now parameterized in `main.bicep` (SKU/tier/storage/backup/HA, log + App Insights retention) with defaults equal to the previous literals, threaded into the modules.

- **Severity / Priority:** Low / 1.33
- **Impact / Risk / Effort:** 2 / 2 / 3
- **Location(s):** `CI/Azure/modules/postgres.bicep:29-51`, `CI/Azure/modules/appInsights.bicep:26`, `CI/Azure/modules/logAnalytics.bicep:23`
- **Finding:** Postgres SKU (`Standard_B1ms`/Burstable), `storageSizeGB: 32`, `backupRetentionDays: 7`, `highAvailability: Disabled`, and App Insights `RetentionInDays: 90` / Log Analytics `retentionInDays: 30` are all literals. `main.bicep` exposes only `appServiceSku`. A prod deployment can't scale DB tier/storage/HA or retention without editing modules.
- **Why it matters:** A multi-environment template (`dev/staging/prod` is the stated model) forces module edits for prod, undermining reuse and idempotent environment promotion.
- **Recommendation:** Surface the key knobs as parameters with dev-friendly defaults (e.g., `param postgresSkuName`, `postgresTier`, `storageSizeGB`, `highAvailabilityMode`, `logRetentionInDays`) threaded from `main.bicep`, so prod overrides via a `main.parameters.prod.json`.

#### Q-INFRA-03: Unused / placeholder Bicep outputs

> **Status (2026-06-17): ✅ Fixed** — removed 6 unused/placeholder outputs (`connectionStringShape`, `serverName`, `appInsightsId`, `instrumentationKey`, `workspaceName`, `keyVaultId`); kept the ones `main.bicep` consumes.

- **Severity / Priority:** Low / 1.0
- **Impact / Risk / Effort:** 1 / 1 / 2
- **Location(s):** `CI/Azure/modules/postgres.bicep:76` (`connectionStringShape` with literal `<replace>`), `appInsights.bicep:36` (`instrumentationKey`), `logAnalytics.bicep:33` (`workspaceName`), `keyVault.bicep:40` (`keyVaultId`)
- **Finding:** Several module outputs are not consumed by `main.bicep` or wired downstream. `connectionStringShape` emits a string containing the literal `<replace>` placeholder — an odd, easily-misused "output".
- **Why it matters:** Outputs imply a contract; unused/placeholder ones are noise and the `<replace>` connection string invites copy-paste mistakes.
- **Recommendation:** Drop outputs nothing consumes, or document where they're meant to be used. Replace `connectionStringShape` with a documented note in `CI/README.md` rather than a deployable output, or build it from a `@secure()` value at runtime.

#### Q-INFRA-06: Inconsistent resource API versions across modules

> **Status (2026-06-17): ✅ Fixed** — aligned resource `apiVersion`s to recent stable releases; `az bicep build` is now warning-free (the prior BCP081 notices are gone).

- **Severity / Priority:** Low / 1.0
- **Impact / Risk / Effort:** 1 / 1 / 2
- **Location(s):** `CI/Azure/modules/*.bicep` — e.g. `serverfarms@2023-12-01`, `vaults@2023-07-01`, `components@2020-02-02` (appInsights.bicep:18, rbac.bicep:26), `roleAssignments@2022-04-01` (rbac.bicep:31,42), `workspaces@2023-09-01`
- **Finding:** API versions vary widely between modules; App Insights uses a 2020 version. Not wrong, but inconsistent and some are notably old.
- **Why it matters:** Mixed/aging API versions make the IaC harder to audit and may miss newer properties; a template sets the baseline cloners inherit.
- **Recommendation:** Standardize on recent, consistent API versions per resource type and review periodically (Azure Bicep linter / `bicep lint` can flag preferred versions).

### Positive observations
- Clean separation of concerns overall: `services/` (HTTP) → `hooks/` (React Query) → `components/`/`pages/`, with `contexts/AuthContext` as the single auth state owner. `useAuth` correctly throws outside its provider.
- `AuthContext` value is memoized and callbacks are `useCallback`-wrapped with correct dependency arrays; reducer-based state is clear.
- The 401→refresh→retry interceptor implements the request-queue pattern (`isRefreshing`/`failedQueue`) and an `_retry` guard against infinite loops — the right approach.
- `ApiError`/`ProblemDetailsError` modeling mirrors the backend `ProblemDetailsWithErrors` and is unit-tested; correlation-id propagation is wired end-to-end.
- OAuth `state` CSRF check (`oauthUtils.ts:115-121`) and per-provider config map are well-structured and extensible.
- TypeScript is otherwise strict (`strict`, `noUnusedLocals`, `noUnusedParameters`, `verbatimModuleSyntax`); Prettier/ESLint flat config present and consistent.
- Bicep is cleanly modularized with `@description` on every param, `@secure()` on the DB password, RBAC-based Key Vault (no access policies), workspace-based App Insights, system-assigned MI, and `resolvedTags` via `union` for consistent tagging.
- Workflows use OIDC (no long-lived cloud secrets), pin actions to major versions, and the UI Pages deploy correctly sets `concurrency` and an `environment`.

### Out-of-scope notes (flagged for the security/perf agents)
- **Security:** `postgres.bicep:64-71` opens the `AllowAllAzureServices` (0.0.0.0) firewall rule and `keyVault.bicep:32` / `appInsights.bicep:28-29` / `logAnalytics.bicep:27-28` set `publicNetworkAccess: Enabled` — review against the intended network posture. `.env` (`starter-app-ui/.env`) is committed (empty values only). These are security-owned.
- **Performance:** React Query `staleTime`/retry tuning, bundle size from inline SVGs, and App Insights sampling are perf-owned and intentionally excluded here.
