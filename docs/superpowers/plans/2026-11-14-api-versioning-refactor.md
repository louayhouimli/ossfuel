# API Versioning Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the OSSFuel API to be versioned under `/api/v1`, namespace auth endpoints under `/api/v1/auth/...` (`/api/v1/auth/sign-in`, `/api/v1/auth/sign-out`, `/api/v1/auth/validate`, `/api/v1/auth/callback`), update the web frontend client, and align existing integration tests without adding extra version testing suites.

**Architecture:**
- ASP.NET Core (.NET 10) Minimal APIs route grouping via `app.MapGroup("/api/v1")`.
- All domain feature endpoints (`Projects`, `Balances`, `Funding`, `ApiKeys`, `AIUsage`) mount onto `/api/v1`.
- Authentication feature mounts under `/api/v1/auth` (`/sign-in`, `/sign-out`, `/validate`), with GitHub OAuth callback configured as `/api/v1/auth/callback`.
- External AI client wire-compatibility endpoints (`/v1/chat/completions`, `/v1/responses`, `/v1/models`) are preserved at root for drop-in proxy compatibility, while also being available under `/api/v1/...`.
- Unversioned infrastructure endpoints (`/health`, `/health/db`, `/health/info`) remain at root.
- Web frontend (`apps/web`) updates `apiFetch` in `api.ts`, sign-in in `auth-shell.tsx`, sign-out in `site-header.tsx`, and session validate in `auth.ts`.
- All existing 21 unit tests and 106 integration tests pass against the new versioned routes.

---

## Global Constraints

- Do NOT add new version testing suites or version header assertion tests (per user request).
- Preserve wire compatibility for external AI clients calling `/v1/chat/completions` and `/v1/models`.
- All existing tests (21 unit tests + 106 integration tests) must pass green upon completion.
- TypeScript builds in `apps/web` must succeed with zero type errors.

---

## Tasks

### Task 1: API Route Grouping & Feature Endpoints Refactoring

**Files:**
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Auth/AuthSetup.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Auth/AuthEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Projects/ProjectEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Balances/BalanceEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/ApiKeys/ApiKeyEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Gateway/GatewayEndpoints.cs`

- [x] **Step 1**: In `AuthSetup.cs`, change `options.CallbackPath` to `"/api/v1/auth/callback"`.
- [x] **Step 2**: In `AuthEndpoints.cs`, map:
  - `app.MapGet("/auth/sign-in", ...)` (and alias `/auth/login`)
  - `app.MapPost("/auth/sign-out", ...)` (and alias `/auth/logout`)
  - `app.MapGet("/auth/validate", ...)`
- [x] **Step 3**: In `GatewayEndpoints.cs`, map `/chat/completions`, `/responses`, `/models`, and `/projects/{projectId:guid}/ai-usage` on the versioned group, while preserving `/v1/chat/completions`, `/v1/responses`, `/v1/models` at root for external clients.
- [x] **Step 4**: In `Program.cs`, create `var v1 = app.MapGroup("/api/v1");` and mount:
  - `v1.MapAuthEndpoints();`
  - `v1.MapProjectEndpoints();`
  - `v1.MapBalanceEndpoints();`
  - `v1.MapFundingEndpoints(FundingSetup.UsesMockProvider(builder.Configuration, builder.Environment));`
  - `v1.MapApiKeyEndpoints();`
  - `v1.MapGatewayEndpoints();`
  - Retain root `/health` and `/v1/...` gateway routes.
- [x] **Step 5**: Run `dotnet build apps/api/OSSFuel.Api/OSSFuel.Api.csproj` to confirm compilation.

---

### Task 2: Frontend Web Client Updates

**Files:**
- Modify: `apps/web/src/lib/api.ts`
- Modify: `apps/web/src/lib/auth.ts`
- Modify: `apps/web/src/components/auth/auth-shell.tsx`
- Modify: `apps/web/src/components/site-header.tsx`

- [x] **Step 1**: In `apps/web/src/lib/api.ts`, update `apiFetch` base URL to `${API_URL}/api/v1`.
- [x] **Step 2**: In `apps/web/src/lib/auth.ts`, update `apiFetch("/validate")` to `apiFetch("/auth/validate")`.
- [x] **Step 3**: In `apps/web/src/components/auth/auth-shell.tsx`, update sign-in redirect to `${API_URL}/api/v1/auth/sign-in?returnUrl=${returnUrl}`.
- [x] **Step 4**: In `apps/web/src/components/site-header.tsx`, update sign-out to `await apiFetch("/auth/sign-out", { method: "POST" })`.
- [x] **Step 5**: Run TypeScript check: `pnpm --filter web typecheck` (or `pnpm --filter web build`).

---

### Task 3: Update Integration Tests to Target `/api/v1`

**Files:**
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Auth/AuthRedirectTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Auth/AuthTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Auth/AuthenticatedAuthTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Auth/CsrfProtectionTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Projects/ProjectBrowseTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Projects/ProjectEndpointsTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Projects/ProjectImportTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Projects/ProjectManagementTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Balances/BalanceCreationTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Balances/BalanceEndpointsTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingCreationTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingCreditServiceTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingHistoryTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Funding/MockCompletionTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Funding/RateLimitingTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/ApiKeys/ApiKeyEndpointsTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Gateway/GatewayEndpointsTests.cs`
- Modify: `apps/api/OSSFuel.IntegrationTests/Features/Health/ProblemDetailsTests.cs`

- [x] **Step 1**: Update Auth tests to use `/api/v1/auth/sign-in`, `/api/v1/auth/callback`, `/api/v1/auth/sign-out`, `/api/v1/auth/validate`.
- [x] **Step 2**: Update Project tests to use `/api/v1/projects` and `/api/v1/repos/available`.
- [x] **Step 3**: Update Balance and Funding tests to use `/api/v1/projects/.../balance` and `/api/v1/fundings`.
- [x] **Step 4**: Update API Key tests to use `/api/v1/projects/.../api-keys`.
- [x] **Step 5**: Update AI Gateway `/projects/.../ai-usage` tests to `/api/v1/projects/.../ai-usage`.
- [x] **Step 6**: Run `dotnet test apps/api/api.slnx` and ensure all 127 tests pass.

---

### Task 4: Final Verification

- [x] **Step 1**: Run `dotnet test apps/api/api.slnx`.
- [x] **Step 2**: Run `pnpm --filter web build`.
- [x] **Step 3**: Inspect git diff.
