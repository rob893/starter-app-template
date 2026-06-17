# Security Review — StarterApp Template

*StarterApp full-stack template — review date 2026-06-17.*

Scope: the .NET 10 API (auth, JWT, cookies, CSRF, CORS, Identity, OAuth, middleware, EF), the Azure Bicep infrastructure, the GitHub workflows, and the React SPA (token handling, XSS, CSRF, OAuth flow). Dimension: **security**. The template's stated goal is *secure by default*, so anything not secure out of the box is treated as a finding. This was a static, read-only review — nothing was exploited.

## How to read this report

Every finding is scored on three axes (integers **1–5**):

- **Impact** — severity if left unaddressed / value of fixing it (5 = critical, 1 = cosmetic).
- **Risk** — likelihood it actually bites you / how easily it is triggered or exploited (5 = very likely, 1 = rare/edge case).
- **Effort** — cost to remediate (1 = trivial, 5 = major/architectural).

**Priority Score = (Impact + Risk) ÷ Effort** (rounded to 2 dp). A *higher* score means *better to handle first* — it rewards high-impact, high-likelihood problems that are cheap to fix. **Severity** is the band of `Impact + Risk`: **Critical** (9–10), **High** (7–8), **Medium** (5–6), **Low** (< 5).

The **master table is sorted by Priority Score** (do-first at the top). Severity tells you *how bad*; Priority tells you *what to tackle first*. Detailed findings (with `file:line` citations and fix snippets) follow, grouped by area.

## Executive summary

**24 findings** — Critical: 0 · High: 3 · Medium: 9 · Low: 12.

**The cryptographic and session core is genuinely strong** — this is not a template with gaping holes. Verified-good controls include: refresh tokens hashed at rest with HMAC-SHA512 + a per-token salt, generated from a CSPRNG, rotated per device, and compared with `CryptographicOperations.FixedTimeEquals`; hardened JWT validation (issuer, audience, lifetime and signing-key all validated, `ClockSkew = 0`, algorithm pinned to HMAC-SHA512, `RequireSignedTokens`); the **access token kept in JS memory only** (never in `localStorage`/`sessionStorage`); an HttpOnly refresh-token cookie with a double-submit CSRF token; service-layer ownership/IDOR checks; React auto-escaping with **no XSS sinks** anywhere; least-privilege workflow permissions with OIDC (no long-lived cloud secrets); and enumeration-safe forgot/reset-password responses. **No Critical-severity / auth-bypass issue was found.**

Where it falls short of *secure by default*:

- **Account-takeover via OAuth email auto-linking (S-BE-02, High).** A social login is auto-linked to an existing local account purely by matching email; for Google the link is created **without checking `email_verified`**, so an attacker controlling a Google identity with the victim's email can take over the pre-existing password account. This is the single most important issue to fix.
- **No account lockout (S-BE-01, High).** Login uses `lockoutOnFailure: false` with no Identity lockout config, leaving only per-IP rate limiting against brute force / credential stuffing.
- **Infrastructure perimeter is wide open by default (S-BE-07 High, S-BE-08 Medium).** Postgres is provisioned with public networking plus an "allow all Azure services" (`0.0.0.0`) firewall rule, and Key Vault has public network access with purge protection disabled — defence is essentially the DB password and RBAC alone.
- **Reconnaissance & misconfig footguns:** OpenAPI/Scalar docs are anonymous in every environment including production (S-BE-05); a CORS fallback combines `*` origin with `AllowCredentials()` if origins are ever unset (S-BE-06); and login timing + registration errors enable user enumeration (S-BE-04).
- **Frontend defence-in-depth gaps:** there is **no Content-Security-Policy** (S-FE-01) — the most important mitigation given the in-memory-token design (S-FE-06) — and internal correlation/trace IDs are shown to end users on auth pages (S-FE-03).
- **OAuth hardening:** the SPA correctly generates and verifies an anti-CSRF `state`, but there is **no PKCE** and the server never binds/validates `state` itself (S-BE-03 / S-FE-07).

Most items are quick configuration or small code changes; the infra ones are Bicep parameter changes.

### Best to handle first (high payoff, low effort)

- **S-BE-05** (6.00, Medium, Effort 1) — OpenAPI/Scalar docs exposed publicly by default in all environments
- **S-BE-02** (4.00, High, Effort 2) — OAuth account auto-linking by email (Google `email_verified` ignored) → takeover
- **S-BE-01** (4.00, High, Effort 2) — No account lockout / brute-force protection on password login
- **S-FE-03** (4.00, Low, Effort 1) — Internal debug details (correlationId/traceId/request path) shown to end users
- **S-FE-04** (4.00, Low, Effort 1) — CSRF cookie read with unanchored regex (cookie-name confusion)
- **S-BE-06** (3.00, Medium, Effort 2) — CORS fallback `WithOrigins("*")` combined with `AllowCredentials()`

### Cross-dimension overlaps

- **OAuth `state` — reconciling the two areas.** The backend finding *S-BE-03* ("OAuth flows do not validate `state`/PKCE") and the frontend positive ("verified OAuth `state`") are **both correct and complementary**: the **SPA** generates a `crypto.randomUUID()` `state`, stores it in `sessionStorage`, and verifies it on return (client-side login-CSRF protection), but the **server** callback merely passes `state` through and the code-exchange endpoints accept a raw `code` with no server-side `state` binding and no PKCE. Net effect: client-side CSRF protection exists; server-side `state` binding and PKCE (the stronger, harder-to-bypass controls) do not. Treat **S-BE-03** + **S-FE-07** as one body of work.
- **OpenAPI exposure (S-BE-05)** pairs with the Quality finding that all CI is disabled — review default-on/default-off posture holistically when hardening the template's defaults.

## Master ranking — all findings by Priority Score

**Completed:** ✅ fixed · ◑ partial · n/a won't-fix (by design/sample) · ☐ open

| # | ID | Completed | Area | Title | Impact | Risk | Effort | Priority | Severity |
|:---:|---|:---:|---|---|:---:|:---:|:---:|:---:|:---:|
| 1 | S-BE-05 | ☐ | Backend & Infrastructure | OpenAPI/Scalar docs exposed publicly by default in all environments | 2 | 4 | 1 | 6.00 | Medium |
| 2 | S-BE-02 | ☐ | Backend & Infrastructure | OAuth account auto-linking by email (Google `email_verified` ignored) → takeover | 5 | 3 | 2 | 4.00 | High |
| 3 | S-BE-01 | ☐ | Backend & Infrastructure | No account lockout / brute-force protection on password login | 4 | 4 | 2 | 4.00 | High |
| 4 | S-FE-03 | ☐ | Frontend (React SPA) | Internal debug details (correlationId/traceId/request path) shown to end users | 2 | 2 | 1 | 4.00 | Low |
| 5 | S-FE-04 | ☐ | Frontend (React SPA) | CSRF cookie read with unanchored regex (cookie-name confusion) | 2 | 2 | 1 | 4.00 | Low |
| 6 | S-BE-06 | ☐ | Backend & Infrastructure | CORS fallback `WithOrigins("*")` combined with `AllowCredentials()` | 4 | 2 | 2 | 3.00 | Medium |
| 7 | S-BE-04 | ☐ | Backend & Infrastructure | Username/email enumeration via login timing + registration errors | 2 | 4 | 2 | 3.00 | Medium |
| 8 | S-FE-05 | ☐ | Frontend (React SPA) | `.env` committed to source control | 2 | 1 | 1 | 3.00 | Low |
| 9 | S-FE-09 | ☐ | Frontend (React SPA) | Vulnerable/outdated dev dependency (esbuild) + no `npm audit` gate | 2 | 1 | 1 | 3.00 | Low |
| 10 | S-BE-10 | ☐ | Backend & Infrastructure | OpenAPI Basic-Auth: non-constant-time compare + crash on malformed header | 1 | 2 | 1 | 3.00 | Low |
| 11 | S-BE-11 | ☐ | Backend & Infrastructure | CSRF double-submit: non-constant-time compare & non-CSPRNG (`Guid`) token | 1 | 2 | 1 | 3.00 | Low |
| 12 | S-BE-08 | ☐ | Backend & Infrastructure | Key Vault public network access enabled; purge protection disabled | 3 | 2 | 2 | 2.50 | Medium |
| 13 | S-FE-02 | ☐ | Frontend (React SPA) | OAuth `code`/`state` left in URL hash & browser history (not cleared) | 3 | 2 | 2 | 2.50 | Medium |
| 14 | S-BE-09 | ☐ | Backend & Infrastructure | PathBaseRewriter trusts arbitrary `X-Forwarded-Prefix` from any client | 2 | 3 | 2 | 2.50 | Medium |
| 15 | S-BE-07 | ☐ | Backend & Infrastructure | Postgres publicly reachable + "allow all Azure services" firewall rule | 4 | 3 | 3 | 2.33 | High |
| 16 | S-FE-06 | ☐ | Frontend (React SPA) | Access token held in JS-reachable memory — inherent XSS exfiltration exposure | 4 | 2 | 3 | 2.00 | Medium |
| 17 | S-BE-03 | ☐ | Backend & Infrastructure | OAuth flows do not validate `state` (CSRF) and use no PKCE | 3 | 3 | 3 | 2.00 | Medium |
| 18 | S-FE-01 | ☐ | Frontend (React SPA) | No Content-Security-Policy (key mitigation for in-memory token) | 3 | 3 | 3 | 2.00 | Medium |
| 19 | S-BE-12 | ☐ | Backend & Infrastructure | Workflows: third-party actions on floating tags; API deploy ungated | 3 | 1 | 2 | 2.00 | Low |
| 20 | S-BE-13 | ☐ | Backend & Infrastructure | Login does not require confirmed email; min password length 8 | 2 | 2 | 2 | 2.00 | Low |
| 21 | S-BE-14 | ☐ | Backend & Infrastructure | JWT secret length unenforced at validation; sample guidance "min 32 chars" | 2 | 2 | 2 | 2.00 | Low |
| 22 | S-FE-08 | ☐ | Frontend (React SPA) | Weak client-side password policy | 2 | 1 | 2 | 1.50 | Low |
| 23 | S-FE-10 | ☐ | Frontend (React SPA) | Non-sensitive queries fire before authentication is confirmed | 1 | 2 | 2 | 1.50 | Low |
| 24 | S-FE-07 | ☐ | Frontend (React SPA) | OAuth flow has no PKCE | 2 | 1 | 3 | 1.00 | Low |

---

## Detailed findings — Backend & Infrastructure

---

### 2) Findings

#### S-BE-05: OpenAPI/Scalar docs exposed publicly by default in all environments
- **Severity / Priority:** Medium / 6.0
- **Impact / Risk / Effort:** 2 / 4 / 1
- **Location(s):** `StarterApp.API/appsettings.json:43-49` (`OpenApi.Enabled=true`, `AuthSettings.RequireAuth=false`); `StarterApp.API/Middleware/OpenApiBasicAuthMiddleware.cs:39-43`; `StarterApp.API/ApplicationStartup/ApplicationBuilderExtensions/OpenApiApplicationBuilderExtensions.cs:25-52`
- **Finding:** The base `appsettings.json` ships with OpenAPI enabled and Basic-Auth disabled (`RequireAuth=false`). The `OpenApiBasicAuthMiddleware` short-circuits to `next()` when `RequireAuth` is false, and the Scalar/OpenAPI endpoints are mapped unconditionally whenever `Enabled` is true — there is no environment gate. Because base config is not overridden per environment (production secrets come only from Key Vault), the full machine-readable API surface (`/openapi`, `/scalar`, `/swagger`) is anonymously reachable in production.
- **Why it matters:** Publishes every route, parameter, model and auth scheme to unauthenticated attackers, dramatically lowering the cost of reconnaissance for the other findings here. A "secure by default" template should not expose live API documentation in production without auth.
- **Recommendation:** Default to `RequireAuth: true` (with no default credentials), or gate `Enabled` to non-production. Example secure default:
  ```jsonc
  "OpenApi": { "Enabled": true, "AuthSettings": { "RequireAuth": true } }
  ```
  and only enable anonymous docs when `app.Environment.IsDevelopment()`.

#### S-BE-01: No account lockout / brute-force protection on password login
- **Severity / Priority:** High / 4.0
- **Impact / Risk / Effort:** 4 / 4 / 2
- **Location(s):** `StarterApp.API/Data/Repositories/UserRepository.cs:135-140` (`CheckPasswordSignInAsync(user, password, false)`); `StarterApp.API/ApplicationStartup/ServiceCollectionExtensions/IdentityServiceCollectionExtensions.cs:15-22` (no `opt.Lockout`); `StarterApp.API/Controllers/V1/AuthController.cs:137-142`
- **Finding:** Login calls `signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false)`, so failed attempts never increment `AccessFailedCount` and accounts are never locked. No `opt.Lockout.*` options are configured. The only throttle is the IP-based rate limiter (`/api/v1/auth/` = 25 req/60 s — `RateLimiterServiceCollectionExtensions.cs:59-70`), which is per-IP and trivially bypassed with a botnet/proxy rotation, allowing offline-speed online password guessing.
- **Why it matters:** Enables credential-stuffing and brute-force account takeover against a known/enumerable user list (see S-BE-04), with no lockout backstop.
- **Recommendation:** Pass `lockoutOnFailure: true` and configure lockout:
  ```csharp
  opt.Lockout.AllowedForNewUsers = true;
  opt.Lockout.MaxFailedAccessAttempts = 5;
  opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
  ```
  Ensure `CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)` and surface lockout in the response generically.

#### S-BE-02: OAuth account auto-linking by email (Google `email_verified` ignored) → account takeover
- **Severity / Priority:** High / 4.0
- **Impact / Risk / Effort:** 5 / 3 / 2
- **Location(s):** `StarterApp.API/Controllers/V1/AuthController.cs:189-214` (Google link path); `:206` (`EmailConfirmed || validatedToken.EmailVerified`); `:308-335` (GitHub link path)
- **Finding:** When no linked account exists, the controller looks up an existing **local** user by the provider-supplied email and silently links the social account to it. For Google, the link is created **without checking `validatedToken.EmailVerified`** — any Google identity whose `email` claim matches a victim's local account email (incl. unverified/Workspace-controlled domains) is linked and immediately logged in as that victim. The same "link by email" pattern applies to a local account that registered but never confirmed its email.
- **Why it matters:** Full account takeover of a pre-existing password user via the OAuth path, bypassing the password entirely.
- **Recommendation:** Only link by email when the provider asserts the email is verified, and only when the local account's email is already confirmed (or require an explicit, authenticated "link account" step). For Google:
  ```csharp
  if (!validatedToken.EmailVerified) return this.Unauthorized("Google email not verified.");
  if (!user.EmailConfirmed) { /* do NOT auto-link; require verification */ }
  ```
  Prefer linking strictly by stable provider subject id, never by email alone.

#### S-BE-04: Username/email enumeration via login timing + registration errors
- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 2 / 4 / 2
- **Location(s):** `StarterApp.API/Controllers/V1/AuthController.cs:129-142` (early return before password check); `:84-87` (register returns Identity `DuplicateUserName`/`DuplicateEmail` descriptions)
- **Finding:** On login, when the user is not found the handler returns `Unauthorized` immediately **without performing any password hash verification**; when the user exists it runs `CheckPasswordSignInAsync`. The measurable timing delta is a username/email oracle. Separately, `RegisterAsync` returns raw Identity error descriptions, which include "Username 'x' is already taken" / "Email 'x' is already taken", directly disclosing account existence. (Note: `forgotPassword`/`resetPassword` are correctly constant-response — good.)
- **Why it matters:** Provides the attacker the valid-account list that makes S-BE-01 practical.
- **Recommendation:** Perform a dummy password hash when the user is absent to equalize timing (e.g., verify against a fixed dummy hash). For registration, return a generic message and avoid echoing duplicate-account details, or rely on the email-verification flow so existence is not confirmed synchronously.

#### S-BE-06: CORS fallback `WithOrigins("*")` combined with `AllowCredentials()`
- **Severity / Priority:** Medium / 3.0
- **Impact / Risk / Effort:** 4 / 2 / 2
- **Location(s):** `StarterApp.API/ApplicationStartup/ApplicationBuilderExtensions/CorsApplicationBuilderExtensions.cs:10-22`
- **Finding:** The policy applies `.AllowAnyMethod().AllowAnyHeader().AllowCredentials()` against origins read from config, with a hard-coded fallback `defaultOrigins = ["*"]`. If `Cors:AllowedOrigins` is ever absent/empty (a realistic prod misconfiguration, since it must be supplied via config/Key Vault), the policy degrades to a wildcard origin alongside credential support — the canonical CORS-with-credentials footgun. Coupling `AllowCredentials()` with a wildcard fallback is unsafe as a default even if the literal `"*"` is not always reflected by the framework.
- **Why it matters:** A credentialed wildcard CORS policy lets any origin read authenticated responses (cookies/refresh flow), enabling cross-site data theft.
- **Recommendation:** Remove the `"*"` fallback. Fail closed when no origins are configured (deny all cross-origin), and never combine credentials with any wildcard:
  ```csharp
  var origins = config.GetSection(Key).Get<string[]>();
  if (origins is null or { Length: 0 }) origins = Array.Empty<string>(); // deny, do not "*"
  ```

#### S-BE-10: OpenAPI Basic-Auth — non-constant-time compare + crash on malformed header
- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 1 / 2 / 1
- **Location(s):** `StarterApp.API/Middleware/OpenApiBasicAuthMiddleware.cs:54-55` (`Split(':',2)[1]`); `:77-81` (`string.Equals` comparisons)
- **Finding:** Credential checks use ordinary `string.Equals` (short-circuit, length/timing observable) rather than a fixed-time comparison. Also `decodedUsernamePassword.Split(':', 2)[1]` throws `IndexOutOfRangeException` when a `Basic` header contains no colon, producing a 500 instead of a 401.
- **Why it matters:** Minor credential timing leak and an unauthenticated DoS/error-path on the docs endpoint. Low because docs auth is a secondary control.
- **Recommendation:** Compare with `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes, and guard the split (return 401 on malformed input rather than indexing blindly).

#### S-BE-11: CSRF double-submit — non-constant-time compare and non-CSPRNG token
- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 1 / 2 / 1
- **Location(s):** `StarterApp.API/Controllers/V1/AuthController.cs:504-510` (`csrfHeader != csrfCookie`); `:563` (`Guid.NewGuid().ToString()` CSRF token)
- **Finding:** The refresh endpoint's CSRF check compares the header and cookie with `!=` (non-constant-time) and generates the CSRF token with `Guid.NewGuid()`, which is not a guaranteed cryptographic RNG. The double-submit pattern itself is otherwise reasonable (the value is also bound by the per-device refresh token).
- **Why it matters:** Defense-in-depth weakness; a `Guid` token has limited and partially structured entropy and the compare is theoretically timing-observable. Low real-world exploitability.
- **Recommendation:** Generate the CSRF token from `RandomNumberGenerator` (e.g., 32 random bytes, base64url) and compare with `CryptographicOperations.FixedTimeEquals`.

#### S-BE-08: Key Vault public network access enabled; purge protection disabled
- **Severity / Priority:** Medium / 2.5
- **Impact / Risk / Effort:** 3 / 2 / 2
- **Location(s):** `CI/Azure/modules/keyVault.bicep:32-36` (`publicNetworkAccess: 'Enabled'`, `networkAcls.defaultAction: 'Allow'`); missing `enablePurgeProtection`
- **Finding:** The vault is reachable from any network (`publicNetworkAccess: Enabled`, ACL default `Allow`); access still requires RBAC (good), but the network perimeter is open. `enablePurgeProtection` is not set, so soft-deleted secrets can be permanently purged — a compromised/over-privileged principal could destroy keys/recovery, and there is no protection against malicious purge.
- **Why it matters:** Removes network-layer defense-in-depth for the system's secret store and allows irreversible secret destruction.
- **Recommendation:** Set `publicNetworkAccess: 'Disabled'` with a private endpoint (or `networkAcls.defaultAction: 'Deny'` + allowed IPs/`bypass: 'AzureServices'`), and add `enablePurgeProtection: true` for non-dev environments.

#### S-BE-09: PathBaseRewriter trusts arbitrary `X-Forwarded-Prefix` from any client
- **Severity / Priority:** Medium / 2.5
- **Impact / Risk / Effort:** 2 / 3 / 2
- **Location(s):** `StarterApp.API/Middleware/PathBaseRewriterMiddleware.cs:26-29`; pipeline order `StarterApp.API/ApplicationStartup/Startup.cs:68-69`
- **Finding:** The middleware unconditionally sets `Request.PathBase` from the client-supplied `X-Forwarded-Prefix` header, with no trusted-proxy validation (unlike the carefully hardened `ForwardedHeaders` config that precedes it). Any client can inject this header. `PathBase` participates in URL generation (`Link`/`CreatedAtRoute`, redirects), so an attacker can influence generated absolute links/locations.
- **Why it matters:** Header-controlled path base can poison generated URLs (e.g., `Location` headers, links) and cause inconsistent routing — a request-spoofing / link-manipulation vector that should be constrained to trusted proxies like the rest of the forwarded-header handling.
- **Recommendation:** Only honor `X-Forwarded-Prefix` when the connection originates from a known proxy (reuse the `ForwardedHeaders` KnownProxies/KnownNetworks trust list), validate/whitelist the prefix value, or drop this middleware and let `UseForwardedHeaders` handle prefixing.

#### S-BE-07: Postgres publicly reachable + "allow all Azure services" firewall rule
- **Severity / Priority:** High / 2.33
- **Impact / Risk / Effort:** 4 / 3 / 3
- **Location(s):** `CI/Azure/modules/postgres.bicep:33-52` (no `network`/`publicNetworkAccess` → public), `:64-71` (firewall rule `0.0.0.0`–`0.0.0.0` = allow all Azure services)
- **Finding:** The Flexible Server is provisioned with public networking (no VNet integration / private DNS / `publicNetworkAccess: Disabled`) and a firewall rule whose `0.0.0.0/0.0.0.0` sentinel allows **any Azure service in any tenant** to reach the database. Combined with password auth (`passwordAuth: Enabled`, `activeDirectoryAuth: Disabled`), the only barrier to the data tier is the admin password.
- **Why it matters:** A leaked/guessed DB password (or an SSRF/pivot from any Azure tenant) exposes the entire user/notes/refresh-token store to the public Azure backbone. Not least-privilege networking.
- **Recommendation:** Use private access (VNet-injected Flexible Server + private DNS zone) or `publicNetworkAccess: 'Disabled'` with a private endpoint; remove the `AllowAllAzureServices` 0.0.0.0 rule and scope firewall rules to the App Service outbound/known IPs; prefer Entra (AAD) auth and enforce `SslMode=Require` end-to-end.

#### S-BE-03: OAuth flows do not validate `state` (CSRF) and use no PKCE
- **Severity / Priority:** Medium / 2.0
- **Impact / Risk / Effort:** 3 / 3 / 3
- **Location(s):** `StarterApp.API/Controllers/V1/AuthController.cs:398-415` (`GitHubCallback` passes `state` through, never validates); `:425-442` (`GoogleCallback`); `:166-258`/`:280-388` (`login/google`, `login/github` accept a raw `code` with no state binding); `StarterApp.API/Services/Auth/GitHubOAuthService.cs:44-60` (token exchange omits `redirect_uri`)
- **Finding:** The callback endpoints accept a `state` query parameter and forward it to the UI but never generate or verify it server-side, and the code-exchange login endpoints accept an attacker-suppliable `code` without binding to any `state`/PKCE verifier. There is no anti-CSRF on the OAuth login, and the GitHub token exchange does not pin `redirect_uri`. This enables login-CSRF and authorization-code injection.
- **Why it matters:** An attacker can force a victim to log into an attacker-controlled identity (login CSRF) or inject a stolen authorization code, undermining the social-login trust boundary.
- **Recommendation:** Generate a CSPRNG `state` (and PKCE `code_verifier`) server-side, store it bound to the session/cookie, and reject callbacks/logins whose `state` does not match. Always send the exact registered `redirect_uri` in token exchanges.

#### S-BE-12: Workflows — third-party actions on floating tags; API deploy job ungated
- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 3 / 1 / 2
- **Location(s):** `.github/workflows/build-and-deploy-api.yml:39,42,47,61,77,83,105`; `.github/workflows/ci.yml:18,21,26,54`; `.github/workflows/build-and-deploy-ui.yml:46,49,71,86`; deploy job `build-and-deploy-api.yml:67-108` (no `environment:`)
- **Finding:** All actions are pinned to **mutable major-version tags** (`actions/checkout@v4`, `azure/login@v2`, `azure/webapps-deploy@v3`, `actions/deploy-pages@v4`, …) rather than immutable commit SHAs, so a compromised or retagged action could execute in a workflow that holds `id-token: write` (Azure OIDC) and deployment rights. The API deploy job also has no `environment:` protection (no required reviewers), unlike the UI's `github-pages` environment. Positively: triggers are `workflow_dispatch` only, permissions are least-privilege, and there is no `pull_request_target`/untrusted-input injection.
- **Why it matters:** Supply-chain exposure of cloud-deploy credentials; ungated production deploys.
- **Recommendation:** Pin actions to full commit SHAs (with Dependabot to bump them) and add a protected `environment:` (required reviewers) to the Azure deploy job.

#### S-BE-13: Login does not require confirmed email; default password minimum length 8
- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 2 / 2 / 2
- **Location(s):** `StarterApp.API/ApplicationStartup/ServiceCollectionExtensions/IdentityServiceCollectionExtensions.cs:15-22` (no `RequireConfirmedEmail`, `RequiredLength = 8`); `StarterApp.API/Controllers/V1/AuthController.cs:122-153` (login does not check `EmailConfirmed`)
- **Finding:** Identity is configured with `SignIn.RequireConfirmedEmail` left default (false) and the login path never checks `user.EmailConfirmed`, so accounts are fully usable before email verification. Password policy minimum length is 8. These are hardening gaps rather than direct vulnerabilities, but they widen the takeover surface in S-BE-02 (unconfirmed local accounts auto-linked by email).
- **Why it matters:** Unverified accounts and shorter passwords increase abuse and brute-force feasibility.
- **Recommendation:** Set `options.SignIn.RequireConfirmedEmail = true` (and enforce in login), raise `Password.RequiredLength` to 12, and consider `Password.RequiredUniqueChars`.

#### S-BE-14: JWT signing-secret length not enforced at validation; sample guidance "min 32 chars"
- **Severity / Priority:** Low / 2.0
- **Impact / Risk / Effort:** 2 / 2 / 2
- **Location(s):** `StarterApp.API/ApplicationStartup/ServiceCollectionExtensions/AuthenticationServiceCollectionExtensions.cs:48` (signing key built with no length check); `StarterApp.API/Services/Auth/JwtTokenService.cs:127-132` (length check only on the issue path); `StarterApp.API/appsettings.Local.example.json:7` (`<your-jwt-signing-secret-min-32-chars>`)
- **Finding:** Token *generation* enforces `key.KeySize < 512` (≥64-byte secret) for HMAC-SHA512, but token *validation* constructs `IssuerSigningKey` with no length check, and the example/onboarding guidance suggests a 32-character secret (256 bits) — below the 512-bit key the algorithm warrants. An operator following the sample could deploy an under-strength HMAC key. (Positives: alg is pinned to HMAC-SHA512, `RequireSignedTokens=true`, issuer/audience/lifetime validated, `ClockSkew=Zero` — so "alg=none"/confusion is mitigated.)
- **Why it matters:** A weak shared secret undermines all JWT integrity (forgeable tokens → full auth bypass) if the misleading guidance is followed.
- **Recommendation:** Validate secret length at startup before building the validation key (throw if `< 64` bytes), and fix the sample/docs to specify a ≥64-byte (e.g., 88-char base64) secret.

---

### 3) Positive observations

- **Refresh tokens stored hashed at rest**, never plaintext: per-token random salt via `HMACSHA512`, compared with `CryptographicOperations.FixedTimeEquals`, generated from `RandomNumberGenerator` (CSPRNG, 256-bit), with rotation per device and expiry pruning (`JwtTokenService.cs:74-105,180-203`).
- **JWT validation is hardened:** explicit algorithm (HMAC-SHA512), `ValidateIssuer/Audience/Lifetime/IssuerSigningKey=true`, `RequireSignedTokens=true`, `RequireExpirationTime=true`, `ClockSkew=Zero` (`AuthenticationServiceCollectionExtensions.cs:44-56`) — no "alg=none"/confusion exposure.
- **Authorization is global** (controllers require auth; only deliberate `[AllowAnonymous]` on register/login/oauth/forgot/reset/confirm), and **resource ownership (IDOR) is enforced in the service layer** for notes and users (`NoteService.cs:80-84,124-128,150-154`; `UserService.cs:66-69,86-90,269-273,319-323`). Admin-only operations are policy-gated (`UsersController.cs:38,132,152,180`).
- **`forgotPassword`/`resetPassword` are enumeration-safe** (always `NoContent`, internal-only logging) (`UsersController.cs:283-312`, `UserService.cs:392-450`).
- **Global exception handler returns generic, correlation-id-only error bodies** and logs the full exception server-side only — no stack traces, EF/Postgres messages, or schema leaked to clients (`GlobalExceptionHandlerMiddleware.cs:88-113`). `EnableSensitiveDataLogging=false`.
- **ForwardedHeaders is explicitly hardened** with a pinned KnownProxies/KnownNetworks trust list (loopback by default), and the rate limiter intentionally reads `RemoteIpAddress` rather than raw `X-Forwarded-For` (`Startup.cs:86-120`, `RateLimiterServiceCollectionExtensions.cs:104-121`).
- **HTTPS enforced**: `UseHsts()` + `UseHttpsRedirection()`, cookies `HttpOnly`/`Secure`/`SameSite=None`, refresh-token cookie not exposed to JS (`Startup.cs:65-66`, `AuthController.cs:552-560`).
- **EF Core throughout uses parameterized LINQ**; the only `ExecuteSqlRaw` (`DatabaseSeeder.cs:75-85`) interpolates hard-coded table/column constants, not user input.
- **Infra least-privilege RBAC**: Web App MI gets only *Key Vault Secrets User* + *Monitoring Metrics Publisher*, scoped to the specific resources (`rbac.bicep`). App Service is `httpsOnly`, `minTlsVersion 1.2`, `ftpsState Disabled`, system-assigned MI, secrets via Key Vault references (`appService.bicep:49-77`). Key Vault uses RBAC mode + soft delete.
- **Secrets are not committed**: example/parameter files use placeholders; `postgresAdminPassword` is `@secure()` and supplied via GitHub secret; CI uses OIDC (no long-lived cloud creds); workflow `permissions` are least-privilege.

### 4) Out-of-scope notes
- **Refresh-token reuse detection** is absent: a rotated/stolen token is simply overwritten per device, with no breach signal (defense-in-depth, not scored).
- **PII in operational logs:** `UserService` logs raw email addresses on forgot/reset/confirm failures (`UserService.cs:401-403,432-434,443-446`); consider hashing/omitting in centralized logs.
- **Seeded admin (`admin@starter-app.local`) is created with no password** (`DatabaseSeeder.cs:138-157`) — not directly loginable, but a known default identity; ensure it is removed/renamed for real deployments.
- `GetNotesAsync` loads all of a user's notes then filters/paginates in memory (`NoteService.cs:54-66`) — a potential resource-exhaustion concern at scale (availability, not covered by this auth-focused review).
- Frontend (`starter-app-ui/**`) token storage, axios CSRF/refresh handling, and XSS were out of scope for this backend+infra pass.

---

## Detailed findings — Frontend (React SPA)

### Findings

#### S-FE-03: Internal debug details shown to end users
- **Severity / Priority:** Low / 4.0
- **Impact / Risk / Effort:** 2 / 2 / 1
- **Location(s):** starter-app-ui/src/components/ApiErrorDisplay.tsx:41-66; consumed with `showDetails={true}` in starter-app-ui/src/pages/LoginPage.tsx:63, RegisterPage.tsx:88, ForgotPasswordPage.tsx:74, ResetPasswordPage.tsx:122
- **Finding:** `ApiErrorDisplay` renders `correlationId`, `traceId`, `instance` (request path) and HTTP status whenever `showDetails` is true. Every auth page passes `showDetails={true}` unconditionally, so these internal identifiers are surfaced to unauthenticated visitors in production builds.
- **Why it matters:** Correlation/trace IDs and request paths are internal observability data. Exposing them aids reconnaissance (maps endpoints, enables log-correlation/social-engineering) and is unnecessary for end users. It is information disclosure, not a direct compromise.
- **Recommendation:** Gate debug details behind `import.meta.env.DEV` (or a deliberate prop), e.g. `showDetails={import.meta.env.DEV}`. Show users a friendly message + correlation ID only if you intend them to quote it to support; never the trace ID/instance by default.

#### S-FE-04: CSRF cookie read with an unanchored regex
- **Severity / Priority:** Low / 4.0
- **Impact / Risk / Effort:** 2 / 2 / 1
- **Location(s):** starter-app-ui/src/services/axiosConfig.ts:17-20
- **Finding:** `document.cookie.match(/csrf_token=([^;]+)/)` is not anchored to a cookie boundary. A cookie named e.g. `xcsrf_token=attacker` contains the substring `csrf_token=attacker`, so the regex can match the wrong cookie's value. An attacker able to set a cookie on the domain (subdomain/MITM-on-HTTP, or another XSS) could influence which value is submitted as the CSRF header, undermining the double-submit defense.
- **Why it matters:** The whole double-submit CSRF scheme depends on reading the *correct* `csrf_token` cookie. Cookie-prefix confusion can break or be abused to forge the header value in edge cases.
- **Recommendation:** Anchor the match to a cookie boundary: `document.cookie.match(/(?:^|;\s*)csrf_token=([^;]+)/)`. Pair with server-side `__Host-`-prefixed, `Secure`, `SameSite` cookies so the value cannot be set by subdomains.

#### S-FE-05: `.env` committed to source control
- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 2 / 1 / 1
- **Location(s):** starter-app-ui/.env (tracked by git — confirmed via `git ls-files`); starter-app-ui/.gitignore:6 only ignores `.env.local`
- **Finding:** `.env` is committed. Today it is harmless — all values are empty and the only vars are public by design (`VITE_API_BASE_URL`, `VITE_SITENAME`, `VITE_GITHUB_CLIENT_ID`, `VITE_GOOGLE_CLIENT_ID`). The risk is a footgun: every `VITE_*` var is bundled into the browser, and a tracked `.env` invites a future contributor to paste a real value (worst case an OAuth **client secret**, which must never be in a SPA) and commit it.
- **Why it matters:** Secrets in a public/template repo are leaked permanently in git history. A committed `.env` normalizes that pattern for a "secure by default" starter.
- **Recommendation:** Replace tracked `.env` with a committed `.env.example` (documented, no values) and add `.env` to `.gitignore`. Add a comment that only public, non-secret config belongs in `VITE_*`; OAuth client secrets stay server-side.

#### S-FE-09: Vulnerable/outdated dev dependency (esbuild) + no audit gate
- **Severity / Priority:** Low / 3.0
- **Impact / Risk / Effort:** 2 / 1 / 1
- **Location(s):** starter-app-ui/package.json:51 (vite → esbuild); `npm audit` reports GHSA-g7r4-m6w7-qqqr
- **Finding:** `npm audit` reports 1 low vulnerability: `esbuild` (>=0.27.3 <0.28.1) allows arbitrary file read via the dev server on Windows (CWE-22). It is a transitive dev-only dependency of Vite and does not affect production bundles, but the dev server is exposed during local development. No CI `npm audit` gate exists.
- **Why it matters:** A dev-server file-read affects developer machines (the audience of a starter template) and signals that dependency freshness is not enforced. The fix is already available.
- **Recommendation:** Run `npm audit fix` (bumps esbuild via Vite), and add `npm audit --audit-level=high` (or Dependabot) to CI so known-vuln packages are surfaced on every PR.

#### S-FE-02: OAuth `code`/`state` left in URL hash & browser history
- **Severity / Priority:** Medium / 2.5
- **Impact / Risk / Effort:** 3 / 2 / 2
- **Location(s):** starter-app-ui/src/utils/oauthUtils.ts:86-97 (reads `window.location.hash`); starter-app-ui/src/pages/OAuthCallbackPage.tsx:47-97 (processes but never clears the hash)
- **Finding:** The callback reads the authorization `code` and `state` from `window.location.hash` and never scrubs them afterward. Because routing uses `HashRouter`, the `code`/`state` remain in the address bar and in `window.history`. They are not cleared after the (successful or failed) exchange, so they persist in browser history and can leak via history APIs or shoulder-surfing on shared machines.
- **Why it matters:** Authorization codes are sensitive (single-use, short-lived). Leaving them in history widens the window for replay if the backend's single-use/expiry enforcement is weak, and is poor hygiene for an account-linking credential. (Note: `HashRouter` does keep the fragment off the `Referer`/server logs, which limits exposure — hence Risk 2 not higher.)
- **Recommendation:** Immediately after extracting the params, scrub them: `window.history.replaceState(null, '', window.location.pathname + window.location.hash.split('?')[0])`. Ensure the backend treats codes as strictly single-use and short-TTL. Avoid logging the code (currently it isn't logged — keep it that way).

#### S-FE-01: No Content-Security-Policy
- **Severity / Priority:** Medium / 2.0
- **Impact / Risk / Effort:** 3 / 3 / 3
- **Location(s):** starter-app-ui/index.html:1-15 (no CSP `<meta>`); no header-based CSP in scope
- **Finding:** There is no Content-Security-Policy (no meta tag, no header configured client-side). For a SPA whose access token lives in JS memory (see S-FE-06), CSP is the single most important defense-in-depth control: it constrains where script can load from and where data can be exfiltrated to, blunting XSS-based token theft and injection.
- **Why it matters:** Without CSP, any XSS (from a dependency, a future feature rendering API data, etc.) can read the in-memory token and POST it anywhere. A "secure by default" template should ship a baseline CSP.
- **Recommendation:** Add a starter CSP. Prefer a server/host header (App Service / GitHub Pages via `_headers` or meta) such as: `default-src 'self'; connect-src 'self' <API origin> https://github.com https://accounts.google.com; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`. Tailwind/HeroUI inject inline styles, so `style-src 'unsafe-inline'` is typically required; keep `script-src` free of `unsafe-inline`/`unsafe-eval`. Also add `frame-ancestors 'none'` (anti-clickjacking).

#### S-FE-06: Access token held in JS-reachable memory (inherent XSS exposure)
- **Severity / Priority:** Medium / 2.0
- **Impact / Risk / Effort:** 4 / 2 / 3
- **Location(s):** starter-app-ui/src/services/auth.ts:8,24-30,44-78; read in starter-app-ui/src/services/axiosConfig.ts:24-27
- **Finding:** The JWT access token is stored in a module-level variable (`let accessToken`) and attached as a `Bearer` header per request. This is the **recommended** SPA pattern — it is *not* in `localStorage`/`sessionStorage`, so it does not persist across reloads and is not trivially dumped by generic storage-scraping XSS payloads. However, any script executing in the page (XSS) can still read this variable while the tab is open, so the exposure is inherent rather than eliminated. Tokens are not logged anywhere (verified).
- **Why it matters:** If an XSS foothold exists, the in-memory token can be exfiltrated, enabling account takeover for the token's lifetime. The chosen design already minimizes the window; the residual risk is the standard SPA tradeoff.
- **Recommendation:** This is the right baseline — keep it. Strengthen the surrounding mitigations rather than changing storage: (a) ship a CSP (S-FE-01); (b) keep access-token TTL short and rely on the HttpOnly refresh cookie for longevity (already in place); (c) treat any future use of `dangerouslySetInnerHTML`/`innerHTML` or third-party script injection as high-risk; (d) consider a service-worker / BFF token-handler pattern only if your threat model warrants fully removing the token from the JS context.

#### S-FE-08: Weak client-side password policy
- **Severity / Priority:** Low / 1.5
- **Impact / Risk / Effort:** 2 / 1 / 2
- **Location(s):** starter-app-ui/src/utils/passwordValidation.ts:6-25
- **Finding:** Client policy requires only 8+ chars, ≥1 digit, ≥1 special char — no uppercase/length-12 guidance and no breached-password (HIBP) consideration. This is client-side UX only; real enforcement must be server-side.
- **Why it matters:** Weak guidance nudges users toward weaker passwords. Low impact because the server (ASP.NET Identity) is the authority; this is the UI mirror of that policy.
- **Recommendation:** Align the client hint with the server's Identity password options (ideally 12+ chars, encourage passphrases) so the two never diverge, and surface server validation errors verbatim. Optionally integrate a k-anonymity HIBP check on registration.

#### S-FE-10: Non-sensitive queries fire before authentication is confirmed
- **Severity / Priority:** Low / 1.5
- **Impact / Risk / Effort:** 1 / 2 / 2
- **Location(s):** starter-app-ui/src/hooks/api.ts:47-67 (`useHelloV1`/`useHelloV2` use `enabled: !isAuthLoading`); contrast notes hooks which use `enabled: isAuthenticated && !isAuthLoading` (api.ts:70-79,122-131)
- **Finding:** The `hello` queries fire as soon as the auth check finishes loading, regardless of whether the user is authenticated, whereas notes queries correctly require `isAuthenticated`. `ProtectedRoute` gates the page, but the hooks can still issue requests during the brief unauthenticated window before redirect.
- **Why it matters:** Minor: only non-sensitive `hello` endpoints are affected and `ProtectedRoute`/server authz remain the real gate. It is an inconsistency worth tightening so the "fetch only when authenticated" pattern is uniform and sensitive endpoints are never accidentally added with the looser predicate.
- **Recommendation:** Use `enabled: isAuthenticated && !isAuthLoading` consistently for any authenticated data fetch; reserve `!isAuthLoading`-only gating for genuinely public endpoints.

#### S-FE-07: OAuth flow has no PKCE
- **Severity / Priority:** Low / 1.0
- **Impact / Risk / Effort:** 2 / 1 / 3
- **Location(s):** starter-app-ui/src/utils/oauthUtils.ts:62-84 (authorize request builds `client_id`, `redirect_uri`, `scope`, `state` — no `code_challenge`)
- **Finding:** The authorization request omits PKCE (`code_challenge`/`code_verifier`). The mitigating factor is that the `code` is exchanged server-side by a confidential client that holds the client secret (no secret is in the SPA — confirmed), and anti-CSRF `state` is generated, stored, and verified.
- **Why it matters:** For confidential-client server-side exchange, PKCE is recommended but not load-bearing (the client secret + verified `state` already bind the code). It mainly hardens against authorization-code injection.
- **Recommendation:** Optional: add PKCE end-to-end (SPA generates verifier, sends challenge; backend forwards verifier on exchange) for defense-in-depth, especially if you ever move code exchange to the browser.

### 2) Positive observations
- **Access token in memory, never in web storage** — `services/auth.ts:8,24-30`. Best-practice SPA choice; not persisted, not in `localStorage`/`sessionStorage`.
- **Refresh token is server-set HttpOnly cookie** — the client never reads or stores it; refresh is a `POST` with `withCredentials` + `X-CSRF-Token` (`axiosConfig.ts:150-181`).
- **Double-submit CSRF** correctly implemented: token read from cookie, echoed in `X-CSRF-Token` header (`axiosConfig.ts:17-34`).
- **OAuth anti-CSRF `state`** generated with `crypto.randomUUID()`, stored in `sessionStorage`, verified and cleared on return (`oauthUtils.ts:46-60,115-121`).
- **No XSS sinks**: no `dangerouslySetInnerHTML`, `innerHTML`, `document.write`, `eval`, or `new Function` anywhere; all dynamic/user/OAuth-error content rendered as React-escaped text.
- **Refresh-loop safety**: `_retry` flag + concurrency queue, and refresh uses a bare `axios` instance (not `apiClient`) to avoid interceptor recursion (`axiosConfig.ts:45-138,150-181`).
- **No token logging** — verified; only error objects/messages are logged, never the token.
- **`withCredentials` scoped** to `apiClient` and the refresh call; `baseURL` from `VITE_API_BASE_URL`; no secrets in env (all `VITE_*` values empty/public).
- **OAuth code exchanged server-side** (confidential client) — no OAuth client secret in the SPA; only public client IDs.
- **No `target="_blank"` links** → no reverse-tabnabbing exposure.
- **Forgot-password is enumeration-safe**: always shows the generic "if an account exists" message regardless of outcome (`ForgotPasswordPage.tsx:25-30,44-46`).

### 3) Out-of-scope notes (server-side — verify but not in this scope)
- `ProtectedRoute` is client-side only and correctly used as defense-in-depth; the API must enforce authorization on every endpoint independently. (`components/ProtectedRoute.tsx:9-26`)
- Cookie flags are server-controlled: confirm the refresh cookie is `HttpOnly; Secure; SameSite=Strict/Lax` and the `csrf_token` cookie is `Secure; SameSite` (and `__Host-` prefixed) — these settings are not visible in the SPA.
- JWT signature/audience/issuer validation, token TTL, and reset-token/code single-use + expiry are backend responsibilities; the SPA only decodes the JWT for display (`utils/auth.ts`) and must not be trusted for authorization decisions.
- GitHub Pages / App Service should also send security headers (CSP, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, HSTS) at the host layer.
