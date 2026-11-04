# OSSFuel

OSSFuel connects open-source software maintainers with sponsor-funded AI compute. Sponsors contribute funding to a project's dedicated AI balance, and maintainers spend that balance on AI-assisted development tools to build, test, and maintain their repositories.

---

## The Problem

AI coding tools dramatically accelerate software development, but their subscription and token usage fees create a recurring financial burden for open-source maintainers. Traditional open-source sponsorship lacks a transparent accounting link between donated funds and the actual AI resources consumed.

OSSFuel introduces a dedicated, transparent funding and accounting ledger that connects:

$$\text{Sponsor Funding} \longrightarrow \text{Dedicated AI Balance} \longrightarrow \text{AI Usage} \longrightarrow \text{Transparent Ledger}$$

---

## Current Status & Implemented Sprints

### ✅ Sprint 1: Foundation & Authentication
- **GitHub OAuth 2.0:** Single sign-on with GitHub, user provisioning, and encrypted token storage using ASP.NET Core Data Protection.
- **Session Management:** Secure HTTP-only sliding session cookies (`ossfuel.session`).
- **Core Infrastructure:** Modular monolith with vertical slices, PostgreSQL backing store, Docker Compose environment, and GitHub Actions CI.

### ✅ Sprint 2: Project Management, AI Balance & Funding
- **Repository Linking:** Maintainers import and verify public GitHub repositories; metadata (stars, forks, open issues, language, topics) auto-syncs.
- **Public Discovery:** Public project catalog and detail screens with search, pagination, and real-time status.
- **Dedicated AI Balance:** Every project opens with a minor-unit integer ledger (`long AmountMinor` in USD cents, avoiding floating-point rounding errors).
- **Project Funding Flow:** Sponsors fund projects through hosted checkout sessions with configurable limits ($1.00 – $10,000.00).
- **Ledger Invariants & Idempotency:** Atomic database transactions credit balances and append immutable `BalanceTransaction` records. Unique index constraints and optimistic concurrency tokens (`xmin`) eliminate race conditions.
- **Sponsor History & Management:** Sponsors track past funding contributions, view live statuses (`pending`, `succeeded`, `failed`, `cancelled`), and can resume or cancel pending checkouts.

### ⏳ Upcoming Sprints
- **Sprint 3 (AI Gateway & API Keys):** OpenAI-compatible proxy, project API key generation and revocation, request routing to OpenRouter, and balance checks before request execution.
- **Sprint 4 (AI Usage & Cost Management):** Token recording (input, output, total), cost calculation, and balance deductions.
- **Sprint 5 (Dashboards & Mobile):** Maintainer analytics dashboard, sponsor overview, and companion mobile application.

---

## Tech Stack

- **Backend:** C# / .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core 10, PostgreSQL (Npgsql).
- **Frontend:** React 19, TypeScript, Vite, TanStack Router (type-safe routing), TanStack Query, Tailwind CSS, shadcn/ui.
- **Architecture:** Vertical Slice Architecture (Features: `Auth`, `Balances`, `Funding`, `GitHub`, `Projects`, `Users`).
- **Testing:** xUnit, custom `WebApplicationFactory` integration testing harness (98 automated tests).
- **Monorepo Tooling:** pnpm workspaces, Turborepo.

---

## Repository Layout

```text
apps/
  api/                    # ASP.NET Core Minimal API and test projects
    OSSFuel.Api/          # Domain, features, and endpoints
    OSSFuel.UnitTests/    # Unit tests
    OSSFuel.IntegrationTests/ # End-to-end integration tests
  web/                    # React 19 TanStack Start / Router frontend
packages/
  ui/                     # Shared design system components
docs/
  superpowers/
    specs/                # Architecture specifications (Sprint 1, Sprint 2)
    plans/                # Step-by-step TDD implementation plans
```

---

## Local Development

### Prerequisites
- [Node.js](https://nodejs.org/) (v20 or newer)
- [pnpm](https://pnpm.io/) (v10 or newer)
- [.NET SDK](https://dotnet.microsoft.com/) (10.0 or newer)
- [Docker](https://www.docker.com/) with Docker Compose

### Getting Started

1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-org/ossfuel.git
   cd ossfuel
   ```

2. **Install frontend dependencies:**
   ```bash
   pnpm install
   ```

3. **Start PostgreSQL database:**
   ```bash
   docker compose up -d postgres
   ```

4. **Run the API & Web servers:**
   ```bash
   # In terminal 1 (Backend API):
   dotnet run --project apps/api/OSSFuel.Api

   # In terminal 2 (Frontend Web):
   pnpm --filter web dev
   ```

5. **Run tests:**
   ```bash
   # Run all unit and integration tests:
   dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release
   dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release
   ```
