# Stripe Sandbox Checkout Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the local mock payment provider with a real Stripe Sandbox checkout workflow using Stripe Hosted Checkout Sessions and cryptographic Stripe webhook event processing.

**Architecture:** Implement `StripePaymentProvider` conforming to `IPaymentProvider` using the official `Stripe.net` SDK. Map `CreateFundingAsync` to Stripe Checkout Sessions with project metadata and line items in minor units (cents). Expose an unauthenticated, CSRF-exempt webhook endpoint `POST /fundings/stripe-webhook` that verifies `Stripe-Signature` headers via `EventUtility.ConstructEvent` and feeds `FundingCreditService.ApplyAsync` to credit `AIBalance`. Provide real-time UI polling on `/fundings/$fundingId` while in `pending` status.

**Tech Stack:** ASP.NET Core (.NET 10 Minimal APIs), `Stripe.net`, Entity Framework Core / PostgreSQL, TanStack Router & Query, React / TypeScript.

**Spec:** The payment architecture defined in `IPaymentProvider.cs` and `FundingCreditService.cs`, switching provider from `"mock"` to `"stripe"`, supporting Stripe Sandbox test mode (`sk_test_...` and `whsec_...`).

---

## Global Constraints

- Preserve minor unit integer currency (`long AmountMinor` in USD cents).
- Maintain existing domain encapsulation (`Funding`, `AIBalance`, `PaymentEvent`).
- Never leak Stripe secret API keys or webhook signing secrets to clients or version control.
- Ensure all 105 existing unit and integration tests continue to pass without regressions by keeping `FakePaymentProvider` in test factory defaults while adding dedicated Stripe integration tests.
- Stripe webhook endpoint must be exempt from browser cookie CSRF checks (`X-OSSFuel-Client`).
- Webhook signature failures must return HTTP 400 Bad Request, while unhandled event types must return HTTP 200 OK to prevent Stripe retry storms.

---

## Review Focus

1. **Webhook Signature Tampering:** An attacker sending fake `checkout.session.completed` events without a valid `Stripe-Signature` signed by the `whsec_...` secret must be rejected with 400 Bad Request without touching the database.
2. **Webhook Replay / Idempotency:** Duplicate webhook events from Stripe must return HTTP 200 OK and be recorded as `PaymentOutcome.Duplicate` without double-crediting the project balance.
3. **Mismatched Amounts / Currencies:** If a checkout session amount or currency differs from the recorded `Funding` row, it must be recorded as `PaymentOutcome.Rejected` and left uncredited.
4. **Session Expiration & Failure Handling:** When a checkout session expires (`checkout.session.expired`) or payment fails (`payment_intent.payment_failed`), the corresponding `Funding` must transition to `Cancelled` or `Failed`.
5. **UI Return Polling:** When a sponsor completes payment on Stripe and is redirected back to `/fundings/{fundingId}?session_id=...`, the UI must automatically poll every 2 seconds until the webhook completes and status updates to `succeeded`.

---

## Tasks

### Task 1: Add `Stripe.net` Package & Configure Strongly-Typed Options

**Files:**
- Modify: `apps/api/OSSFuel.Api/OSSFuel.Api.csproj`
- Create: `apps/api/OSSFuel.Api/Features/Funding/StripeOptions.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/Funding.cs`
- Modify: `apps/api/OSSFuel.Api/appsettings.json`
- Modify: `apps/api/OSSFuel.Api/appsettings.Development.json`

**Interfaces:**
- Produces: `StripeOptions` with `ApiKey`, `WebhookSecret`, `SuccessUrl`, and `CancelUrl`.
- Produces: `Funding.StripeProvider = "stripe"`.

- [ ] **Step 1: Add `Stripe.net` package to `OSSFuel.Api.csproj`**
  Add `<PackageReference Include="Stripe.net" Version="47.4.0" />` (or latest 47.x stable).

- [ ] **Step 2: Create `StripeOptions.cs`**
  Define `StripeOptions` class:
  ```csharp
  namespace OSSFuel.Api.Features.Funding;

  public sealed class StripeOptions
  {
      public const string SectionName = "Payments:Stripe";

      public string? ApiKey { get; set; }
      public string? WebhookSecret { get; set; }
      public string? SuccessUrl { get; set; }
      public string? CancelUrl { get; set; }
  }
  ```

- [ ] **Step 3: Add `StripeProvider` constant to `Funding.cs`**
  Add `public const string StripeProvider = "stripe";` in `Funding.cs`.

- [ ] **Step 4: Update `appsettings.json` and `appsettings.Development.json`**
  Add configuration template under `"Payments"`:
  ```json
  "Payments": {
    "Provider": "stripe",
    "Stripe": {
      "ApiKey": "",
      "WebhookSecret": "",
      "SuccessUrl": "",
      "CancelUrl": ""
    }
  }
  ```

- [ ] **Step 5: Verify build**
  Run: `dotnet build apps/api/OSSFuel.Api/OSSFuel.Api.csproj -c Release`
  Expected: Build succeeded with 0 errors.

---

### Task 2: Implement `StripePaymentProvider`

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/StripePaymentProvider.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingSetup.cs`
- Create: `apps/api/OSSFuel.UnitTests/Features/Funding/StripePaymentProviderTests.cs`

**Interfaces:**
- Consumes: `IPaymentProvider`, `IOptions<StripeOptions>`, `Funding`, `OSSFuelDbContext`.
- Produces: `StripePaymentProvider` creating Stripe `Session` with line items, metadata, success/cancel URLs.

- [ ] **Step 1: Write unit test for `StripePaymentProvider` configuration validation**
  Verify `IsConfigured` returns `false` when `ApiKey` is empty, and `true` when set.

- [ ] **Step 2: Implement `StripePaymentProvider.cs`**
  Create class implementing `IPaymentProvider`:
  ```csharp
  public sealed class StripePaymentProvider(
      IOptions<StripeOptions> stripeOptions,
      IConfiguration configuration,
      OSSFuelDbContext db) : IPaymentProvider
  ```
  - In `CreateCheckoutSessionAsync`:
    - Fetch project name from `db.Projects.AsNoTracking().Where(p => p.Id == funding.ProjectId)`.
    - Build `SessionCreateOptions`:
      - `Mode = "payment"`
      - `PaymentMethodTypes = ["card"]`
      - `LineItems`:
        - Name: $"OSSFuel AI Balance — {projectName}"
        - UnitAmount: `funding.AmountMinor`
        - Currency: `funding.Currency.ToLowerInvariant()`
        - Quantity: 1
      - `ClientReferenceId = funding.Id.ToString()`
      - `Metadata`: `funding_id`, `project_id`, `sponsor_user_id`
      - `SuccessUrl`: `{webAppUrl}/fundings/{funding.Id}?session_id={CHECKOUT_SESSION_ID}`
      - `CancelUrl`: `{webAppUrl}/fundings/{funding.Id}?cancelled=true`
    - Execute via `SessionService(new StripeClient(apiKey))`.
    - Return `ProviderSession(session.Id, session.Url)`.

- [ ] **Step 3: Register `StripePaymentProvider` in `FundingSetup.cs`**
  In `AddFundingFeature`:
  - Bind `services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));`.
  - Check if `configuration["Payments:Provider"] == "stripe"`. If so, register `services.AddScoped<IPaymentProvider, StripePaymentProvider>();`.

- [ ] **Step 4: Run unit tests**
  Run: `dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release`
  Expected: PASS.

---

### Task 3: Implement Stripe Webhook Endpoint & Event Parser

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/StripeWebhookEndpoint.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Common/CsrfProtection.cs`
- Create: `apps/api/OSSFuel.IntegrationTests/Features/Funding/StripeWebhookTests.cs`

**Interfaces:**
- Consumes: Raw HTTP Request body, `Stripe-Signature` header, `IOptions<StripeOptions>`, `FundingCreditService`.
- Produces: `POST /fundings/stripe-webhook` handling `checkout.session.completed`, `checkout.session.expired`, `payment_intent.payment_failed`.

- [ ] **Step 1: Write integration tests for Stripe Webhook**
  In `StripeWebhookTests.cs`:
  - Test 1: Missing or invalid `Stripe-Signature` returns HTTP 400 Bad Request.
  - Test 2: Valid `checkout.session.completed` event triggers `FundingCreditService`, transitions `Funding` to `Succeeded`, and adds `AIBalance` credit.
  - Test 3: Duplicate webhook delivery returns HTTP 200 OK without crediting twice.
  - Test 4: Webhook request without `X-OSSFuel-Client` CSRF header is allowed through without 403 Forbidden.

- [ ] **Step 2: Ensure CSRF exemption in `CsrfProtection.cs`**
  Verify `IsExemptPath` includes `path.StartsWithSegments("/fundings/stripe-webhook")`.

- [ ] **Step 3: Implement `StripeWebhookEndpoint.cs`**
  ```csharp
  public static class StripeWebhookEndpoint
  {
      public static async Task<IResult> HandleWebhookAsync(
          HttpRequest request,
          IOptions<StripeOptions> options,
          FundingCreditService credits,
          ILoggerFactory loggerFactory,
          CancellationToken cancellationToken)
  ```
  - Read raw body via `StreamReader(request.Body).ReadToEndAsync()`.
  - Validate `Stripe-Signature` header using `EventUtility.ConstructEvent(json, signature, webhookSecret, throwOnApiVersionMismatch: false)`.
  - Handle event types:
    - `checkout.session.completed`:
      - Extract `session.Id`, `session.AmountTotal`, `session.Currency`, `session.PaymentIntentId`.
      - Call `credits.ApplyAsync(new PaymentNotification(..., Action: PaymentAction.Credit, Provider: Funding.StripeProvider))`.
    - `checkout.session.expired`:
      - Call `credits.ApplyAsync(..., Action: PaymentAction.Cancel)`.
    - `payment_intent.payment_failed`:
      - Call `credits.ApplyAsync(..., Action: PaymentAction.Fail)`.
    - Any other event: return `Results.Ok(new { received = true })`.

- [ ] **Step 4: Map route in `FundingEndpoints.cs`**
  Map `group.MapPost("/fundings/stripe-webhook", StripeWebhookEndpoint.HandleWebhookAsync).AllowAnonymous();`.

- [ ] **Step 5: Run integration tests**
  Run: `dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release --filter FullyQualifiedName~StripeWebhookTests`
  Expected: All tests PASS.

---

### Task 4: Frontend Payment Polling & Checkout Callback Experience

**Files:**
- Modify: `apps/web/src/routes/_site/_authenticated/fundings/$fundingId.tsx`
- Modify: `apps/web/src/components/fund-project-form.tsx`

**Interfaces:**
- Consumes: URL query params `?session_id=...` and `?cancelled=true`, TanStack Query `fundingQueryOptions`.
- Produces: Dynamic status polling while `funding.status === "pending"`, friendly status alerts for Stripe returns.

- [ ] **Step 1: Add dynamic refetch polling to `$fundingId.tsx`**
  Update `useQuery(fundingQueryOptions(fundingId))` to poll every 2000ms while `funding.status === "pending"`:
  ```ts
  const { data: funding, isLoading, isError, error } = useQuery({
    ...fundingQueryOptions(fundingId),
    refetchInterval: (query) =>
      query.state.data?.status === "pending" ? 2000 : false,
  })
  ```

- [ ] **Step 2: Add session / cancellation banner on return from Stripe**
  Read search params in `$fundingId.tsx`:
  - If `session_id` is present and status is `pending`: show an alert banner *"Payment received by Stripe. Synchronizing your project's balance..."* with a subtle spinner.
  - If `cancelled` is present: show an alert banner *"Payment checkout was cancelled. You can try again at any time."*.

- [ ] **Step 3: Validate checkout URL in `fund-project-form.tsx`**
  Before redirecting with `window.location.assign(funding.checkoutUrl)`, verify:
  - URL starts with `https://checkout.stripe.com/` (or current origin if mock provider in dev).

- [ ] **Step 4: Build and typecheck web app**
  Run: `pnpm --filter web typecheck` and `pnpm --filter web build`
  Expected: Clean build with 0 errors.

---

### Task 5: End-to-End Sandbox Verification & Developer Documentation

**Files:**
- Create: `docs/stripe-sandbox-setup.md`
- Modify: `README.md`

- [ ] **Step 1: Create Stripe Sandbox developer guide (`docs/stripe-sandbox-setup.md`)**
  Document step-by-step instructions:
  1. Getting Stripe Test API Keys from Stripe Dashboard.
  2. Setting secret keys via `dotnet user-secrets`:
     ```bash
     cd apps/api/OSSFuel.Api
     dotnet user-secrets set "Payments:Provider" "stripe"
     dotnet user-secrets set "Payments:Stripe:ApiKey" "sk_test_..."
     dotnet user-secrets set "Payments:Stripe:WebhookSecret" "whsec_..."
     ```
  3. Running the Stripe CLI listener to forward webhooks to localhost:
     ```bash
     stripe listen --forward-to http://localhost:5050/fundings/stripe-webhook
     ```
  4. Testing with Stripe test cards (e.g., `4242 4242 4242 4242`).

- [ ] **Step 2: Run full regression test suite**
  Run: `dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release`
  Run: `dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release`
  Expected: All 105+ tests passing.

---

## Execution Handoff

Please review this plan. Which execution approach would you prefer?

- **Subagent-driven** - A fresh subagent implements each task and a fresh reviewer checks it before the next task begins, followed by a final whole-branch review.
- **Native** - I implement every task directly in this session, verify using the test suite, and run full end-to-end checks.
