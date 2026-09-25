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

Funding → AI balance → AI usage → cost → balance transaction

## What OSSFuel is

OSSFuel is not an AI model. It is a funding, gateway, and accounting layer that connects open-source projects with existing AI clients and providers.

Potential clients include Aider, Continue, and custom integrations. OSSFuel will keep those clients separate from the balance and usage accounting logic.

## Current status

The project is in early development. The repository currently contains:

- A React and TypeScript web application
- An ASP.NET Core API
- PostgreSQL setup through Docker Compose
- Unit and integration test projects
- Initial authentication screens and health endpoints

The payment flow, AI gateway, provider integration, balance ledger, and usage analytics are planned but are not implemented yet.

## Tech stack

- **Frontend:** React, TypeScript, Vite, TanStack Router, TanStack Query, shadcn/ui
- **Backend:** C#, ASP.NET Core, REST API
- **Database:** PostgreSQL
- **Testing:** xUnit and integration testing

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
```

## Local development

Requirements:

- Node.js 20 or newer
- pnpm 10
- .NET 10
- Docker with Docker Compose

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

## Long-term goal

Make it practical for an open-source project to say: "AI usage is funded by its sponsors, and every charge is visible."
