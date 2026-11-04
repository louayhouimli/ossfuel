# API Hardening & Resilience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden the API by resolving dev port mismatches, adding RFC 7807 global exception handling, implementing CSRF custom header defense-in-depth, and configuring rate limiting on sensitive endpoints.

**Architecture:** Vertical slice additions and middleware in ASP.NET Core (.NET 10). Global exception handling via `AddProblemDetails()` and `UseExceptionHandler()`. CSRF custom header middleware (`X-OSSFuel-Client`) guarding state-changing mutations with cookie authentication. Rate limiting via ASP.NET Core `Microsoft.AspNetCore.RateLimiting` on sensitive endpoints (`POST /fundings`, GitHub OAuth). Frontend client sync in `apps/web/src/lib/api.ts`.

**Tech Stack:** C# / .NET 10 Minimal APIs, ASP.NET Core RateLimiter, ProblemDetails RFC 7807, TypeScript / Fetch API.

---

## Global Constraints

- Never break existing 98 unit and integration tests.
- Maintain minor unit integer currency (`long AmountMinor`).
- Maintain domain encapsulation (`AIBalance`, `Funding`).
- All error responses must conform to RFC 7807 `application/problem+json`.
- Dev port must remain consistent across web `.env` and api `launchSettings.json` (`http://localhost:5050`).

## Review Focus

1. Ensure webhook endpoints (e.g. `/fundings/webhook/mock` or future Stripe) are not blocked by the CSRF browser header check.
2. Ensure integration test factories configure default headers so existing tests continue passing without regressions.
3. Ensure rate limiter policies return HTTP 429 with standard ProblemDetails.
4. Ensure global exception handler does not leak exception stack traces in non-development environments.
5. Ensure `pnpm dev` starts the API on port 5050 using `--launch-profile http`.

---

## Tasks

### Task 1: Dev Launch Profile & Package Script Port Alignment

**Files:**
- Modify: `apps/api/package.json`
- Modify: `apps/api/OSSFuel.Api/Properties/launchSettings.json`

- [x] **Step 1**: Update `apps/api/package.json` dev script to explicitly use `--launch-profile http`.
- [x] **Step 2**: Ensure `launchSettings.json` primary profile is `http` configured to `http://localhost:5050`.
- [x] **Step 3**: Verify `dotnet run --project apps/api/OSSFuel.Api --launch-profile http --no-build` boots on `http://localhost:5050`.

---

### Task 2: Global Problem Details & Unhandled Exception Handling

**Files:**
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Create: `apps/api/OSSFuel.IntegrationTests/Features/Health/ProblemDetailsTests.cs`

- [x] **Step 1**: Write an integration test checking standard RFC 7807 problem details format on 404/500 errors.
- [x] **Step 2**: Register `builder.Services.AddProblemDetails(...)` with trace identifier enrichment in `Program.cs`.
- [x] **Step 3**: Add `app.UseExceptionHandler()` and `app.UseStatusCodePages()` to the top of the ASP.NET Core middleware pipeline.
- [x] **Step 4**: Run the test to confirm clean RFC 7807 responses.

---

### Task 3: CSRF Defense-in-Depth via Custom Header Verification

**Files:**
- Create: `apps/api/OSSFuel.Api/Common/CsrfProtection.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Modify: `apps/web/src/lib/api.ts`
- Modify: `apps/api/OSSFuel.IntegrationTests/Infrastructure/ProjectsWebApplicationFactory.cs`
- Create: `apps/api/OSSFuel.IntegrationTests/Features/Auth/CsrfProtectionTests.cs`

- [x] **Step 1**: Write failing integration tests for CSRF protection: state-mutating request without `X-OSSFuel-Client` returns 403 Forbidden; with header returns expected response; GET requests are not blocked.
- [x] **Step 2**: Implement `CsrfProtectionMiddleware` in `apps/api/OSSFuel.Api/Common/CsrfProtection.cs` checking `X-OSSFuel-Client` for state-changing HTTP methods on cookie-authenticated requests, bypassing webhooks.
- [x] **Step 3**: Register `app.UseMiddleware<CsrfProtectionMiddleware>()` in `Program.cs`.
- [x] **Step 4**: Add `headers.set("X-OSSFuel-Client", "web")` to `apps/web/src/lib/api.ts`.
- [x] **Step 5**: Update `ProjectsWebApplicationFactory.cs` to include `X-OSSFuel-Client: web` by default on test clients.
- [x] **Step 6**: Run tests and confirm CSRF protection passes.

---

### Task 4: Rate Limiting on Sensitive Endpoints

**Files:**
- Create: `apps/api/OSSFuel.Api/Common/RateLimitingSetup.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Auth/AuthEndpoints.cs`
- Create: `apps/api/OSSFuel.IntegrationTests/Features/Funding/RateLimitingTests.cs`

- [x] **Step 1**: Write a test attempting rapid bursts against a rate-limited endpoint, asserting HTTP 429 Too Many Requests when the limit is reached.
- [x] **Step 2**: Implement `RateLimitingSetup.cs` configuring token bucket / fixed window policies for `"funding-create"` and `"auth"`.
- [x] **Step 3**: Register `builder.Services.AddAppRateLimiter()` and `app.UseRateLimiter()` in `Program.cs`.
- [x] **Step 4**: Apply `.RequireRateLimiting("funding-create")` to `POST /fundings` and `.RequireRateLimiting("auth")` to `/auth/github`.
- [x] **Step 5**: Run tests and confirm rate limiting functions as expected.

---

### Task 5: Full Regression Testing & Verification

- [x] **Step 1**: Run all unit tests: `dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release`.
- [x] **Step 2**: Run all integration tests: `dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release`.
- [x] **Step 3**: Run frontend linting: `pnpm --filter web lint`.
- [x] **Step 4**: Run frontend build: `pnpm --filter web build`.
- [x] **Step 5**: Verify git status and commit clean changes.
