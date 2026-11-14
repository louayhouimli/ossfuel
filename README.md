# OSSFuel

OSSFuel gives open-source maintainers a way to use sponsor-funded AI. Sponsors add funds to a project's AI balance, and maintainers spend that balance on coding tools and AI services through an OpenAI-compatible gateway.

## The problem

AI tools help maintainers code, review, debug, write tests, and maintain repositories. The usage costs are recurring, and maintainers usually pay out of pocket.

General project sponsorships do not link funds to specific AI runs or track individual token costs. OSSFuel provides a dedicated ledger and an authenticated gateway so sponsors can directly fund AI usage and verify where every dollar goes.

## How it works

1. **Sponsor funds a project**: A sponsor chooses a project and completes payment through an external provider.
2. **AI balance credited**: Verified webhooks credit the project's dedicated AI balance in USD minor units (cents).
3. **Maintainer creates an API key**: The project maintainer generates a scoped project API key (`ossf_...`).
4. **AI tool makes a request**: A coding tool (such as OpenCode, Aider, or Continue) points its OpenAI base URL to OSSFuel.
5. **Pre-check**: OSSFuel validates the API key and ensures the project balance is positive before forwarding.
6. **Upstream execution**: The gateway forwards the request to OpenRouter, supporting streaming SSE and tool calling.
7. **Ledger deduction**: OSSFuel calculates token cost based on actual input/output tokens, debits the balance, and logs the usage record.

```text
Sponsor Payment → AI Balance → Maintainer Key → AI Gateway → Provider Response → Ledger Deduction
```

## Current status

The repository contains:

- **Authentication & Sessions**: GitHub OAuth sign-in, user roles, and sliding session cookies.
- **Project Discovery & Management**: GitHub repository linking, project catalog, and maintainer settings.
- **Dedicated AI Balance Ledger**: Atomic balance debit and credit transactions (`AmountMinor` in USD cents) with concurrency controls.
- **Funding Flow**: Stripe/mock checkout sessions, cancellation handling, and webhook verification.
- **OpenAI-Compatible AI Gateway**:
  - Endpoints for `/v1/chat/completions`, `/v1/responses`, and `/v1/models` (also available under `/api/v1/*`).
  - Real-time Server-Sent Events (SSE) streaming with low Time-to-First-Token.
  - Forwarding of tool calling (`tools`, `tool_choice`, `tool_calls`) and agent parameters.
  - Upstream failover to `openrouter/free` for free model tiers on rate limits or errors.
  - In-memory cached model catalog with offline static fallback.
- **API Key Management**:
  - Scoped project API keys hashed with SHA-256 (keys are never stored in plaintext).
  - Immediate key revocation.
- **Maintainer Dashboard & Audit Logs**:
  - Web UI for generating and revoking keys.
  - Real-time token usage logs showing prompt tokens, completion tokens, and dollar deductions.
- **Test Suite**: 127 automated tests (21 unit tests and 106 integration tests) covering all authentication, funding, gateway, and ledger flows.

## Architecture

```text
Coding Agent (OpenCode / Aider / Cursor / Continue)
                       |
                       v
                 OSSFuel API
                       |
          [Bearer Key & Balance Check]
                       |
                       v
             OpenRouter / Provider
                       |
             [Streaming SSE Chunks]
                       |
                       v
       Cost Calculation & Balance Debit
```

## Tech stack

- **Frontend**: React 19, TypeScript, Vite, TanStack Router, TanStack Query, Tailwind CSS, shadcn/ui
- **Backend**: .NET 10, C#, ASP.NET Core Minimal APIs, Entity Framework Core
- **Database**: PostgreSQL
- **Testing**: xUnit, Microsoft.AspNetCore.Mvc.Testing (`WebApplicationFactory`), Testcontainers

## Screenshots

| Landing Page | Project Directory |
| :---: | :---: |
| ![OSSFuel Landing](docs/assets/home.png) | ![Project Catalog](docs/assets/projects.png) |

| Project & AI Balance | Authentication |
| :---: | :---: |
| ![Project Detail & Funding](docs/assets/project-detail.png) | ![Sign In](docs/assets/sign-in.png) |

## Repository layout

```text
apps/web       React frontend application
apps/api       ASP.NET Core API and .NET test suites
packages/ui    Shared UI components
docs/          Specifications, design docs, and implementation plans
```

## Using the gateway

Configure your AI coding tool to point to OSSFuel with a project API key:

```bash
# Production:
OPENAI_API_BASE=https://api.ossfuel.louayhouimli.dev/v1
OPENAI_API_KEY=ossf_your_api_key_here

# Local Development:
OPENAI_API_BASE=http://localhost:5050/v1
OPENAI_API_KEY=ossf_your_api_key_here
```

### Example curl request

```bash
curl https://api.ossfuel.louayhouimli.dev/v1/chat/completions \
  -H "Authorization: Bearer ossf_your_api_key_here" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "openrouter/free",
    "messages": [
      { "role": "user", "content": "Hello from OSSFuel!" }
    ]
  }'
```

## Local development

### Requirements

- Node.js 20 or newer
- pnpm 10
- .NET 10 SDK
- Docker and Docker Compose

### Setup

1. Install dependencies and start PostgreSQL:
   ```bash
   pnpm install
   docker compose up -d postgres
   ```

2. Start the development environment:
   ```bash
   pnpm dev
   ```

3. Run repository checks:
   ```bash
   pnpm lint
   pnpm typecheck
   pnpm test
   pnpm build
   ```

4. Run .NET tests directly:
   ```bash
   dotnet test apps/api/api.slnx
   ```

## Goal

Give open-source projects an auditable, sponsor-funded gateway for AI tools so maintainers never have to fund project AI usage from their own pockets.
