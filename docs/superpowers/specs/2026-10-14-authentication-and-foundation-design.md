# Authentication, Session Management, and Foundation

Sprint 1 increment covering US-01, US-02, US-03, US-51, US-52, and US-61.

Status: complete / documented.  
Date: 2026-10-14.  
Branch: `main` (initial release).

---

## 1. Executive Summary

Sprint 1 established the baseline infrastructure, data tier, automated testing pipeline, and authentication subsystem for OSSFuel. 

Because OSSFuel manages funding and AI compute allocations for open-source software, identity must be rooted in GitHub repository ownership. The foundation therefore implements **GitHub OAuth 2.0 authentication**, encrypted token persistence, secure cookie-based session management, and project baseline scaffolding.

---

## 2. Goals & Scope

- **US-01 (Authenticate with GitHub):** Users authenticate with their GitHub accounts; the system retrieves profile data (login, email, avatar, GitHub user ID) and provisions or updates user records.
- **US-02 (Persistent Session):** Authenticated sessions are securely preserved across browser visits using encrypted, HTTP-only session cookies with sliding expiration.
- **US-03 (Roles & Permissions):** Access control patterns distinguishing unauthenticated guests, signed-in users, repository maintainers, and platform administrators.
- **US-51 & US-52 (Unit & Integration Tests):** Automated xUnit test suite with an in-memory `WebApplicationFactory` simulating authentication challenges, ticket issuance, and protected endpoint access.
- **US-61 (Deployment & Infrastructure):** Containerized local environment (Docker Compose for PostgreSQL), monorepo workspace setup with pnpm, and continuous integration via GitHub Actions.

---

## 3. Architecture & Authentication Flow

### GitHub OAuth 2.0 Lifecycle

```
[Browser]             [OSSFuel Web]           [OSSFuel API]          [GitHub OAuth]
   |                        |                       |                      |
   |--- Click "Sign In" --->|                       |                      |
   |                        |--- GET /login ------->|                      |
   |                        |<-- 302 to GitHub -----|-- Challenge OAuth -->|
   |<-- Redirect to GitHub ------------------------------------------------|
   |                                                                       |
   |--- Authenticate & Authorize OSSFuel on GitHub ----------------------->|
   |<-- 302 Redirect to /signin-github with code --------------------------|
   |                                                                       |
   |--- GET /signin-github?code=... --------------->|                      |
   |                                                |-- Exchange code ---->|
   |                                                |<-- Access Token -----|
   |                                                |-- Fetch profile ---->|
   |                                                |<-- User info --------|
   |                                                |                      |
   |                                                |-- Upsert User entity |
   |                                                |-- Encrypt GH token   |
   |                                                |-- Issue cookie       |
   |<-- Set-Cookie: ossfuel.session (HttpOnly) -----|                      |
   |<-- Redirect to WebAppUrl (Dashboard) ----------|                      |
```

### Endpoints

| Method | Path | Auth | Purpose |
| :--- | :--- | :--- | :--- |
| `GET` | `/login?returnUrl` | Anonymous | Issues OAuth challenge, initiating redirect to GitHub. |
| `GET` | `/signin-github` | Anonymous | GitHub OAuth callback; exchanges authorization code, provisions user, issues session cookie. |
| `GET` | `/validate` | Authenticated | Verifies active session, returns current user profile DTO. |
| `POST` | `/logout` | Authenticated | Revokes session cookie and signs out current user. |

---

## 4. Security & Data Protection

### Token Encryption (`IGitHubTokenProtector`)
GitHub access tokens grant repository read permissions and must never be stored in plaintext. 
- Tokens are encrypted prior to database persistence using the ASP.NET Core Data Protection API (`IDataProtectionProvider`).
- Decryption occurs transiently in-memory only when querying the GitHub API for repository verification.

### Cookie Configuration
- **Cookie Name**: `ossfuel.session`
- **HttpOnly**: `true` (prevents JavaScript/XSS access)
- **SameSite**: `Lax` (configurable, protects against CSRF)
- **Secure**: `SameAsRequest` in development; `Always` in production over HTTPS
- **Sliding Expiration**: Active sessions automatically refresh without requiring re-authentication.

---

## 5. Data Model

### `User` Entity

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK | Internal unique identifier |
| `GitHubId` | bigint | Unique, Indexed | GitHub numerical user ID |
| `Login` | varchar(100) | Required | GitHub username |
| `Name` | varchar(200) | Nullable | GitHub display name |
| `Email` | varchar(255) | Nullable | Primary verified email |
| `AvatarUrl` | varchar(500) | Nullable | GitHub profile avatar image |
| `GitHubAccessToken` | text | Nullable | Data-Protection encrypted OAuth token |
| `GitHubAccessTokenUpdatedAt` | timestamptz | Nullable | Timestamp of last token refresh |
| `CreatedAt` | timestamptz | Required | Account registration date |
| `LastLoginAt` | timestamptz | Required | Most recent login timestamp |

---

## 6. Monorepo & Infrastructure Baseline

- **Monorepo Architecture**:
  - `apps/api`: ASP.NET Core 10 minimal APIs, EF Core 10, Npgsql PostgreSQL provider.
  - `apps/web`: React 19, TypeScript, TanStack Router, TanStack Query, Tailwind CSS, Vite.
  - `packages/ui`: Shared design system components and primitives.
- **Docker Compose**: Containerized PostgreSQL instance configured for local development.
- **CI Pipeline**: GitHub Actions (`pr.yml`) executing:
  - Frontend linting, typechecking, and bundle verification.
  - .NET solution compilation and test execution.
