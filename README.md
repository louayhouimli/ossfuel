# OSSFuel

OSSFuel gives open-source maintainers a way to use sponsor-funded AI. Sponsors add funds to a project's AI balance, and maintainers spend that balance on the tools they need to build and maintain the project.

## The problem

AI tools help maintainers with coding, debugging, testing, documentation, and refactoring. The usage costs are recurring, though, and maintainers often pay them themselves.

General project funding does not show which AI requests were paid for or how much they cost. OSSFuel adds a transparent funding and accounting layer around the AI tools maintainers already use.

## How it works

1. A sponsor funds an open-source project.
2. An external payment provider confirms the payment.
3. The funds increase the project's AI balance.
4. A maintainer makes an AI request through the OSSFuel gateway.
5. OSSFuel checks the available balance and forwards the request to OpenRouter or another AI provider.
6. The provider returns the response and usage data.
7. OSSFuel calculates the cost, deducts the balance, and records a transaction.

`Funding → AI balance → AI usage → cost → balance transaction`

## What OSSFuel is

OSSFuel is not an AI model. It is a funding, gateway, and accounting layer that connects open-source projects with existing AI clients and providers.

Potential clients include Aider, Continue, and custom integrations. OSSFuel will keep those clients separate from the balance and usage accounting logic.

## Current status

The repository currently contains:

- GitHub OAuth authentication and sliding session cookies
- Project linking from GitHub and public project discovery
- Dedicated AI balance ledger (`AmountMinor` in USD cents) with concurrency controls
- Sponsor funding flow with checkout sessions, cancellation, and resumption
- ASP.NET Core API with vertical slice architecture and PostgreSQL database
- React and TypeScript web frontend (Vite, TanStack Router)
- Unit and integration test suites (98 automated tests)

The AI gateway (OpenRouter proxy, API keys), token usage tracking, and maintainer analytics dashboard are planned for the upcoming sprints.

## Tech stack

- **Frontend**: React, TypeScript, Vite, TanStack Router, TanStack Query, shadcn/ui, Tailwind CSS
- **Backend**: C#, ASP.NET Core, REST API, Entity Framework Core
- **Database**: PostgreSQL
- **Testing**: xUnit and WebApplicationFactory integration testing

## Planned architecture

```text
Aider / Continue / Custom client
                |
                v
          OSSFuel API
                |
          Check AI balance
                |
                v
       OpenRouter / AI provider
                |
       Response + usage data
                |
                v
    Cost calculation + ledger
```

The API also needs to receive verified payment events and expose project-level funding and usage information to the web application.

## Repository layout

```text
apps/web       React frontend
apps/api       ASP.NET Core API and .NET tests
packages/ui    Shared UI components and styles
docs/          Specifications and implementation plans
```

## Local development

### Requirements

- Node.js 20 or newer
- pnpm 10
- .NET 10 SDK
- Docker with Docker Compose

### Getting started

Install dependencies and start the local services:

```bash
pnpm install
docker compose up -d postgres
pnpm dev
```

Run the repository checks:

```bash
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

You can also run the .NET tests directly:

```bash
dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release
dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release
```

## Long-term goal

Make it practical for an open-source project to say: "AI usage is funded by its sponsors, and every charge is visible."
