# Authentication, Session Management, and Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the foundational monorepo, Docker PostgreSQL backing store, GitHub OAuth 2.0 authentication, encrypted token storage, cookie session persistence, and CI pipeline.

**Architecture:** Monorepo with ASP.NET Core 10 minimal APIs backend and React 19 / TanStack Router frontend. Auth feature encapsulates cookie-based session ticket issuance, GitHub OAuth code exchange, Data-Protection encrypted token storage, and session validation.

**Tech Stack:** ASP.NET Core 10 minimal APIs, EF Core 10 with Npgsql, AspNet.Security.OAuth.GitHub, xUnit 2.9.3, React 19 with TanStack Router and Query, pnpm 10 workspaces.

**Spec:** `docs/superpowers/specs/2026-10-14-authentication-and-foundation-design.md`

---

## Global Constraints

- Target framework `net10.0`; EF Core `10.0.11`; Npgsql provider `10.0.3`; xUnit `2.9.3`.
- GitHub OAuth tokens must be encrypted using `IDataProtector` before persisting to PostgreSQL.
- Session cookie named `ossfuel.session` with `HttpOnly = true`, `SameSite = Lax`, and sliding expiration.
- Minimal API endpoints return typed problem details on authentication/authorization failures.
- Web app handles loading states with `Loader` and redirects unauthenticated users to `/sign-in`.

---

### Task 1: Monorepo scaffolding and database foundation

**Files:**
- Create: `pnpm-workspace.yaml`
- Create: `package.json`
- Create: `turbo.json`
- Create: `docker-compose.yml`
- Create: `apps/api/OSSFuel.Api/Database/OSSFuelDbContext.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`

**Steps:**
- [x] Configure pnpm monorepo workspace for `apps/*` and `packages/*`.
- [x] Setup Docker Compose definition for PostgreSQL database (`ossfuel`) on port 5432.
- [x] Create `OSSFuelDbContext` with PostgreSQL Npgsql provider configuration.
- [x] Register EF Core database context in API dependency injection.

---

### Task 2: Data Protection and token encryption

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/GitHub/IGitHubTokenProtector.cs`
- Create: `apps/api/OSSFuel.Api/Features/GitHub/GitHubTokenProtector.cs`
- Create: `apps/api/OSSFuel.Api/Features/GitHub/GitHubSetup.cs`
- Test: `apps/api/OSSFuel.UnitTests/Features/GitHub/GitHubTokenProtectorTests.cs`

**Steps:**
- [x] Write unit tests for `GitHubTokenProtector` verifying encryption, decryption, and null handling.
- [x] Implement `IGitHubTokenProtector` wrapping ASP.NET Core `IDataProtectionProvider`.
- [x] Register Data Protection services in `GitHubSetup.AddGitHubFeature`.
- [x] Run unit tests and confirm pass.

---

### Task 3: User entity and database schema

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Users/User.cs`
- Create: `apps/api/OSSFuel.Api/Features/Users/UserConfiguration.cs`
- Modify: `apps/api/OSSFuel.Api/Database/OSSFuelDbContext.cs`

**Steps:**
- [x] Implement `User` domain model (`Id`, `GitHubId`, `Login`, `Name`, `Email`, `AvatarUrl`, `GitHubAccessToken`, `CreatedAt`, `LastLoginAt`).
- [x] Configure EF Core entity mapping: unique index on `GitHubId`, string length constraints.
- [x] Add `DbSet<User> Users` to `OSSFuelDbContext`.
- [x] Generate initial migration for user table.

---

### Task 4: GitHub OAuth 2.0 integration and user provisioning

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Auth/AuthSetup.cs`
- Create: `apps/api/OSSFuel.Api/Features/Auth/GitHubEvents.cs`
- Create: `apps/api/OSSFuel.Api/Features/Auth/AuthResponse.cs`

**Steps:**
- [x] Configure GitHub OAuth provider with `read:user`, `user:email`, and `public_repo` scopes.
- [x] Implement `OnCreatingTicket` handler to extract GitHub claims, encrypt the access token, and upsert the `User` entity in database.
- [x] Set principal claims (`NameIdentifier` to `User.Id`, `Name` to `User.Login`).

---

### Task 5: Cookie-based session management and authentication endpoints

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Auth/AuthEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Auth/AuthTests.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Auth/AuthenticatedAuthTests.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Auth/AuthRedirectTests.cs`

**Steps:**
- [x] Configure cookie authentication scheme (`ossfuel.session`, `SameSite = Lax`, `HttpOnly = true`).
- [x] Implement `GET /login`: triggers OAuth challenge with redirect URL resolution.
- [x] Implement `POST /logout`: calls `SignOutAsync` and clears session cookie.
- [x] Implement `GET /validate`: returns `AuthResponse` DTO for current authenticated user or 401 Unauthorized.
- [x] Write integration tests verifying unauthorized 401, authenticated 200, and redirect handling.

---

### Task 6: Frontend authentication shell and sign-in view

**Files:**
- Create: `apps/web/src/lib/auth.ts`
- Create: `apps/web/src/components/auth/login.tsx`
- Create: `apps/web/src/routes/_auth/sign-in.tsx`
- Create: `apps/web/src/routes/_site/_authenticated/route.tsx`

**Steps:**
- [x] Define `authQueryOptions()` with React Query calling `/validate`.
- [x] Create sign-in landing screen with GitHub OAuth trigger button.
- [x] Setup TanStack Router authenticated route guard redirecting unauthenticated sessions to `/sign-in`.

---

### Task 7: Continuous integration pipeline and verification

**Files:**
- Create: `.github/workflows/pr.yml`

**Steps:**
- [x] Add GitHub Actions workflow running on pull request and main push.
- [x] Configure backend job: restore, build with `-c Release`, execute xUnit tests.
- [x] Configure frontend job: pnpm install, lint, typecheck, build.
- [x] Verify full pipeline passes.
