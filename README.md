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
- **Test Suite**: 152 automated tests (36 unit tests and 116 integration tests) covering all authentication, funding, gateway, and ledger flows.

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

## Stripe sandbox & local webhook setup

OSSFuel supports both a zero-dependency **Mock Provider** (enabled by default in automated tests and local development when Stripe keys are unset) and the real **Stripe Checkout** provider.

### 1. Mock vs. Stripe mode

- **Mock Mode (`Payments:Provider = "mock"`)**:
  Generates an internal mock checkout URL (`/fundings/{id}/mock-checkout`) where clicking "Simulate Success" immediately completes the funding without contacting Stripe.
- **Stripe Mode (`Payments:Provider = "stripe"`)**:
  Creates a real Stripe Checkout Session and redirects the sponsor to Stripe's hosted checkout page. When payment completes, Stripe sends an asynchronous webhook event (`checkout.session.completed`) back to OSSFuel to credit the project balance.

### 2. Configure local User Secrets

Never commit real or test Stripe API keys to Git. In local development, store them in .NET User Secrets:

```bash
# Enable Stripe provider
dotnet user-secrets set "Payments:Provider" "stripe" --project apps/api/OSSFuel.Api

# Set your Stripe Test Secret Key (from Stripe Dashboard > Developers > API keys)
dotnet user-secrets set "Payments:Stripe:ApiKey" "sk_test_51..." --project apps/api/OSSFuel.Api
```

### 3. Install & log in to Stripe CLI

Stripe cannot send webhooks directly to `localhost` unless you forward them via the Stripe CLI.

1. **Install Stripe CLI**:
   - **Windows**: `winget install Stripe.StripeCLI` or `scoop install stripe` or `choco install stripe-cli`
   - **macOS**: `brew install stripe/stripe-cli/stripe`
   - **Linux**: See [Stripe CLI official installation](https://docs.stripe.com/stripe-cli)

2. **Authenticate with your Stripe account**:
   ```bash
   stripe login
   ```
   Follow the interactive browser prompt to authorize the CLI.

### 4. Listen & forward webhooks locally

Start forwarding Stripe events to the canonical OSSFuel webhook endpoint (`/api/v1/fundings/stripe-webhook`):

```bash
stripe listen --forward-to http://localhost:5050/api/v1/fundings/stripe-webhook -e checkout.session.completed -e checkout.session.expired -e payment_intent.payment_failed
```

> [!TIP]
> On Windows PowerShell, pass separate `-e` flags for each event as shown above (avoid unquoted comma-separated lists, which PowerShell interprets as arrays).

When `stripe listen` starts, it outputs your unique webhook signing secret:
```text
> Ready! You are using Stripe CLI version ...
> Ready! Your webhook signing secret is whsec_1234567890abcdef... (^C to quit)
```

Copy that `whsec_...` key and store it in your User Secrets:

```bash
dotnet user-secrets set "Payments:Stripe:WebhookSecret" "whsec_1234567890abcdef..." --project apps/api/OSSFuel.Api
```

Restart your API server (`pnpm dev` or `dotnet run --project apps/api/OSSFuel.Api`) to load the new webhook secret.

### 5. Test a payment end-to-end

1. Open the frontend at `http://localhost:5173`.
2. Select any project and click **Fund with AI Credits**.
3. Choose an amount (e.g. `$25.00`) and click **Proceed to Checkout**.
4. You will be redirected to the official Stripe Checkout page.
5. In test mode, enter:
   - **Card number**: `4242 4242 4242 4242`
   - **Expiration date**: Any valid future date (e.g. `12/34`)
   - **CVC**: Any 3 digits (e.g. `123`)
   - **Name**: Any name
6. Click **Pay**.
7. Stripe redirects back to `http://localhost:5173/fundings/{id}?session_id=...`.
8. The Stripe CLI console logs the event and forward delivery:
   ```text
   --> checkout.session.completed [evt_...]
   <-- [200] POST http://localhost:5050/api/v1/fundings/stripe-webhook
   ```
9. The OSSFuel frontend polls every 2 seconds until the status changes from `pending` to `succeeded`, and the project's AI balance increments in real time.

### 6. Production configuration (Azure Container Apps / Docker)

In production, set the following environment variables on the backend container:

| Variable | Description | Example |
| :--- | :--- | :--- |
| `Payments__Provider` | Payment provider engine | `stripe` |
| `Payments__Stripe__ApiKey` | Stripe Secret Key | `sk_live_...` or `sk_test_...` |
| `Payments__Stripe__WebhookSecret` | Webhook signing secret from Stripe Dashboard | `whsec_...` |
| `WebAppUrl` | Public URL of frontend (for redirect callbacks) | `https://ossfuel.louayhouimli.dev` |
| `AllowedOrigin` | Allowed CORS origin | `https://ossfuel.louayhouimli.dev` |

In your Stripe Dashboard (**Developers > Webhooks**):
- Add an endpoint pointing to `https://api.ossfuel.louayhouimli.dev/api/v1/fundings/stripe-webhook`.
- Subscribe to events: `checkout.session.completed`, `checkout.session.expired`, and `payment_intent.payment_failed`.
- Copy the Signing Secret (`whsec_...`) into your production environment secret `Payments__Stripe__WebhookSecret`.

## Goal

Give open-source projects an auditable, sponsor-funded gateway for AI tools so maintainers never have to fund project AI usage from their own pockets.
