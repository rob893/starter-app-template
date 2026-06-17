# StarterApp

Batteries-included full-stack starter template. **.NET 10 API + React SPA**. Clone it and build your
app on top of a production-grade foundation: auth (JWT + social logins), EF Core + Postgres, versioned
APIs, observability, and CI/CD to Azure.

## Repo Layout

- `StarterApp.API/` — .NET 10 Web API (EF Core + Identity + Postgres)
- `StarterApp.API.Tests/` — xUnit tests (Moq), mirrored folder structure
- `starter-app-ui/` — React + Vite + TypeScript + Tailwind v4 + HeroUI v3
- `CI/Azure/` — Bicep infra (App Service, App Insights, Key Vault, Postgres)
- `.github/workflows/` — CI + deploy (API → Azure App Service via OIDC, UI → GitHub Pages)

## Commands

Run from repo root unless noted.

**API:** `dotnet build StarterApp.API/StarterApp.API.csproj` · `dotnet test` · `npm run start` (hot reload)

**UI** (from `starter-app-ui/`): `npm i` · `npm run dev` · `npm run lint` · `npm run test` (Vitest) · `npm run test:e2e` (Playwright) · `npm run build`

## API Architecture

- Extension-driven startup: `Program.cs` → `ApplicationStartup/ServiceCollectionExtensions/*` and
  `ApplicationStartup/ApplicationBuilderExtensions/*`.
- Config: `appsettings.json` → env-specific → `appsettings.Local.json` → Azure Key Vault (non-Dev).
- Global auth; use `[AllowAnonymous]` to opt out. URL-segment versioning: `/api/v1/…`, `/api/v2/…`.
- Errors: `ProblemDetailsWithErrors` + `X-Correlation-Id` on every response.
- Service-result pattern: services return `Result<T>`; controllers map failures via
  `ServiceControllerBase.HandleServiceFailureResult`.
- Cursor-based pagination via `CursorPaginatedList<T>` / `ToCursorPaginatedResponse`.
- All async methods must accept and pass `CancellationToken`.

## Auth

- ASP.NET Identity (`User`/`Role`) + JWT bearer access tokens (HMAC-SHA512).
- Social logins: **Google** and **GitHub** (OAuth code exchange → linked account → JWT).
- Refresh tokens stored in an HttpOnly cookie; rotation protected by double-submit CSRF
  (`X-CSRF-Token` header + `csrf_token` cookie). Expired access tokens signal `X-Token-Expired`.

## Data

- DbContext: `Data/DataContext.cs` (Postgres). Repositories extend `Repository<…>` / `IRepository<…>`.
- `DatabaseSeeder` supports CLI args (`seeder` + `migrate`, `seed`, `--password <…>`).

## UI ↔ API Integration

- Base URL: `VITE_API_BASE_URL`.
- `services/axiosConfig.ts`: `X-Correlation-Id`, refresh-token on 401 (`x-token-expired`),
  double-submit CSRF, `withCredentials: true`.
- API calls in `services/api.ts`, cached/managed via TanStack React Query in `hooks/api.ts`.
- Routing uses `HashRouter` (works on GitHub Pages). HeroUI v3 needs no provider; styles via `@heroui/styles`.

## Coding Style

- **C#:** Follow `.editorconfig`. `PascalCase` types/members, `I`-prefixed interfaces, `camelCase`
  fields (no `_` prefix). `this.` for instance members. XML docs on public members. Non-entity POCOs →
  `record` with `get; init;`. Repos extend `Repository<…>` / `IRepository<…>`.
- **TypeScript:** ESLint via `eslint.config.js`, format with `npm run prettier`. Prefer method syntax
  `func(): Type {}` over arrow signatures.

## Testing

- **Backend:** xUnit + Moq in `StarterApp.API.Tests/` (mirrored folders).
- **UI unit:** Vitest (`*.test.ts(x)` / `*.spec.ts(x)` under `src/`).
- **UI e2e:** Playwright in `starter-app-ui/e2e/`.

## Commits & PRs

- Conventional Commits: `feat:`, `fix:`, `chore:`, `test:`. One logical change per commit.
- Flag migration/config impacts.

## Azure

Deploys to Azure **App Service** (API) and **GitHub Pages** (UI). Infra is Bicep under `CI/Azure/`.
Use Azure best-practices tooling when working on Bicep/Key Vault/App Insights.
