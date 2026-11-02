# AI Balance and Project Funding

Sprint 2 increment covering US-10 to US-20.

Status: draft for review.
Date: 2026-11-02.
Branch: `feature/sprint-2-ai-balance-funding`, cut from `feature/sprint-2-project-management`.

## 1. Why this exists

Today a project is a read-only snapshot of a GitHub repository. Nobody can put money behind it.
This increment gives every project a balance, lets a sponsor put money into that balance through
Stripe, and makes every credit visible to the maintainer as a transaction.

The premise that shapes everything below: **a successful payment and a successful balance credit
are two separate events, controlled by two systems**. Stripe owns the money. OSSFuel owns the
accounting. There is no distributed transaction between them, so the design leans on webhooks,
idempotency, and reconciliation instead.

The order of work follows that premise. Balance invariants and the credit operation come first.
The Stripe adapter wraps them afterwards, because the adapter is the part most likely to change.

## 2. Goals

- Every project has exactly one AI Balance, opened at zero with the project and backfilled for
  projects that already exist.
- A maintainer can read the balance and its transaction history, and nobody else can.
- A sponsor can pick a project, name an amount, pay through Stripe Checkout, and see the outcome.
- A verified `checkout.session.completed` credits the balance exactly once, writes exactly one
  balance transaction, and moves the funding to `succeeded`, all in one database transaction.
- Failed, cancelled, pending, unverified, and unmatched payments never touch the balance.
- Duplicate and concurrent webhook deliveries cannot double credit.
- A sponsor can see their own funding history.

## 3. Non-goals

- AI usage, token accounting, request cost, and deduction from the balance. Those belong to the
  AI Usage and Cost Management epic.
- Refunds and chargebacks. The events are recorded for later, but no balance is reversed. See
  section 12.
- Public balance or funding totals on the landing page or the public project page.
- Invoices, receipts, and tax documents.
- Any payment provider other than Stripe.
- Referral credit, platform fees, or a conversion rate other than 1:1.

## 4. Anti-goals

These are failure states even if the code appears to work:

- Crediting a balance because the browser came back from Stripe. The return URL is decoration.
- Trusting an amount, currency, or status that the browser sent.
- Storing a card, a Stripe secret key, or a raw provider credential in the database.
- Two credits for one payment under any timing.
- A balance row that exists without its project, or a project without its balance.
- A credit applied while the funding row still says `pending`.

## 5. Decisions made before design

| Decision | Choice |
| --- | --- |
| Payment provider | Stripe, hosted Checkout Sessions, signed webhooks |
| Increment scope | API and web in one pass |
| Currency | USD only for launch |
| Conversion | 1:1, one cent paid equals one cent of balance, no platform fee |
| Balance representation | Integer minor units (`long`), never floating point |
| Funding statuses | `pending`, `succeeded`, `failed`, `cancelled` |
| Refunds and chargebacks | Recorded, not reversed, deferred to a later increment |
| Limits | Minimum $1.00, maximum $10,000.00 per funding, both configurable |
| Visibility | Balance and transactions: maintainer only. Funding history: sponsor only |
| Base branch | The pushed project management branch, not `main` |

## 6. Money

- Everything is stored in minor units as `long`. No `decimal`, no `double`, anywhere in the
  domain, the contracts, or the database.
- `AmountMinor` on Funding is what the sponsor pays. `AmountMinor` on a transaction is signed,
  positive for a credit.
- Currency is the lowercase ISO code (`usd`) because that is what Stripe sends and expects.
  Display uppercases it.
- Limits come from configuration: `Funding:MinAmountMinor` (default `100`) and
  `Funding:MaxAmountMinor` (default `1000000`).
- The API accepts `amountMinor` as an integer. The web form accepts a string such as `25.50`,
  validates it against `^\d{1,5}(\.\d{1,2})?$`, and converts by string arithmetic. No `parseFloat`.

## 7. Data model

Four new tables. `Payment` from the specification is folded into `Funding` (see section 13).

### AIBalance

| Column | Type | Notes |
| --- | --- | --- |
| Id | uuid | primary key |
| ProjectId | uuid | unique, foreign key to Project, cascade delete |
| AmountMinor | bigint | default 0, never negative |
| Currency | varchar(3) | `usd` |
| CreatedAt, UpdatedAt | timestamptz | |

Cascade delete is safe here because a project that has any funding cannot be deleted at all
(section 18), so the only cascade ever taken is a project with a zero balance and no history.

`RowVersion` is mapped to Postgres `xmin` (`.IsRowVersion()`), which gives optimistic concurrency
without an extra column. InMemory ignores it, so concurrency is proven by the Postgres tests.

### BalanceTransaction

| Column | Type | Notes |
| --- | --- | --- |
| Id | uuid | primary key |
| AIBalanceId | uuid | foreign key, cascade delete |
| FundingId | uuid | nullable, **unique**, foreign key to Funding, restrict |
| AmountMinor | bigint | signed |
| Type | varchar(40) | `funding_credit` today |
| Status | varchar(40) | `posted` today |
| CreatedAt | timestamptz | |
| Description | varchar(200) | nullable, human text for the table |

Index on `(AIBalanceId, CreatedAt desc)`.

The unique index on `FundingId` is the hard guarantee that one funding produces at most one
credit, even if two requests race past the status check.

### Funding

| Column | Type | Notes |
| --- | --- | --- |
| Id | uuid | primary key, also the Stripe idempotency key |
| ProjectId | uuid | foreign key, restrict delete while funding exists |
| SponsorUserId | uuid | foreign key, restrict |
| AmountMinor | bigint | what the sponsor pays |
| Currency | varchar(3) | `usd` |
| Status | varchar(20) | `pending`, `succeeded`, `failed`, `cancelled` |
| Provider | varchar(20) | `stripe` |
| ProviderCheckoutSessionId | varchar(200) | nullable, unique |
| ProviderCheckoutUrl | varchar(500) | nullable, reused for a pending funding |
| ProviderPaymentIntentId | varchar(200) | nullable, unique |
| FailureReason | varchar(200) | nullable |
| CreatedAt, UpdatedAt | timestamptz | |
| CompletedAt | timestamptz | nullable |

Indexes on `ProjectId` and `(SponsorUserId, CreatedAt desc)`. A partial unique index on
`(SponsorUserId, ProjectId) WHERE Status = 'pending'` allows at most one open funding per sponsor
and project, so a double click or a second tab cannot open two Checkout Sessions.

### PaymentEvent

Audit and event-level de-duplication.

| Column | Type | Notes |
| --- | --- | --- |
| Id | uuid | primary key |
| Provider | varchar(20) | `stripe` |
| ProviderEventId | varchar(200) | unique |
| Type | varchar(100) | e.g. `checkout.session.completed` |
| ProviderObjectId | varchar(200) | checkout session or payment intent id |
| Outcome | varchar(20) | `credited`, `duplicate`, `ignored`, `rejected`, `unmatched`, `failed` |
| Detail | varchar(500) | nullable, reason for non-credited outcomes |
| ReceivedAt | timestamptz | |

The raw provider payload is deliberately not stored. The fields above are what a support request
actually needs, and they cannot leak personal data or secrets.

## 8. API

All new endpoints follow the existing minimal API style: flat paths, `Results.Problem` for errors,
the shared `LoadCurrentUserAsync` pattern from `ProjectEndpoints`, cookies for authentication.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/projects/{id}/balance` | project owner | current balance |
| GET | `/projects/{id}/balance/transactions?page&pageSize` | project owner | history, newest first |
| POST | `/fundings` | any signed-in user | create funding, open a Checkout Session |
| GET | `/fundings?page&pageSize` | signed-in user | own funding history |
| GET | `/fundings/{id}` | funding owner | one funding, used by the return page |
| POST | `/fundings/{id}/sync` | funding owner | ask Stripe for the current session state |
| POST | `/webhooks/stripe` | anonymous, signature only | provider notifications |

### Creating a funding

`POST /fundings` is the only place a Funding row is born, and it writes the row before it talks to
Stripe, because the id is both the Stripe idempotency key and part of the return URL.

1. Validate `projectId` and `amountMinor`. The project must exist.
2. If a `pending` funding already exists for this sponsor and project, return it with its stored
   `CheckoutUrl` when the amount matches (200, not 201). A different amount is a 409, so the sponsor
   cannot leave an abandoned session and start a second one by accident.
3. Insert the Funding as `pending`, `SaveChanges`.
4. Create the Checkout Session with `IdempotencyKey = funding.Id`, `amount_total = amountMinor`,
   `currency = usd`, success URL `{WebAppUrl}/fundings/{id}`, cancel URL
   `{WebAppUrl}/projects/{projectId}`.
5. Store the session id and URL, `SaveChanges`, return 201.

If step 4 fails the funding is marked `failed` with `FailureReason` set, and the response is 502
(provider unreachable) or 503 (not configured). It is not left `pending` and it is not deleted: the
failed row is the audit trail, and the next attempt starts clean because only `pending` rows are
blocked by the partial index.

### Sync

`POST /fundings/{id}/sync` retrieves the Checkout Session from Stripe by id, feeds it through the
same guard the webhook uses, and returns the funding. It exists because Stripe retries are the only
automatic recovery, and a sponsor whose payment settled while every webhook delivery failed would
otherwise sit on `pending` forever. The return page calls it once after ten seconds if the funding
is still pending. This closes the reconciliation gap without a background worker.

### Contracts

```text
BalanceResponse(Guid ProjectId, long AmountMinor, string Currency)
BalanceTransactionResponse(Guid Id, long AmountMinor, string Type, string Status,
                           string? Description, DateTimeOffset CreatedAt)
BalanceTransactionListResponse(IReadOnlyList<BalanceTransactionResponse> Items,
                               int Page, int PageSize, int TotalCount)
CreateFundingRequest(Guid ProjectId, long AmountMinor)
FundingResponse(Guid Id, Guid ProjectId, string ProjectFullName, long AmountMinor,
                string Currency, string Status, string? CheckoutUrl,
                DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt)
FundingListResponse(IReadOnlyList<FundingResponse> Items, int Page, int PageSize, int TotalCount)
```

`CheckoutUrl` is only populated on the create response.

### Status codes

| Situation | Status | Body |
| --- | --- | --- |
| Not signed in | 401 | problem |
| Signed in, not the project owner | 403 | "Only the maintainer who registered this project can read its balance." |
| Unknown project or funding | 404 | problem |
| Amount missing, not an integer, below min, above max | 400 | validation problem keyed `amountMinor` |
| Project id missing or malformed | 400 | validation problem keyed `projectId` |
| A pending funding already exists with a different amount | 409 | "Finish or wait for your pending funding." |
| Sync requested for a funding that is not pending | 200 | current state, no work |
| Stripe not configured | 503 | "Payments are not configured." |
| Stripe unreachable while creating a session | 502 | "The payment provider is unavailable." |
| Bad webhook signature or missing header | 400 | `{"error":"invalid signature"}` |
| Webhook processed, ignored, or unmatched | 200 | `{"received":true}` |
| Webhook could not be processed due to a transient failure | 500 | so Stripe retries |

## 9. The credit operation

One application service, `FundingCreditService`, is the only writer of balance changes.

```text
CreditAsync(webhookEvent, funding, now):
  transaction begin (SaveChanges already batches; use an explicit transaction)
  balance = load funding.Project.AIBalance
  funding.MarkSucceeded(paymentIntentId, now)        // throws if not pending
  balance.Credit(funding.AmountMinor, funding.Id, now) // adds amount, appends transaction
  record PaymentEvent(Outcome = credited)
  SaveChanges
  commit
```

Guards, in order, before the operation runs:

1. Event id already in `PaymentEvents` yields `duplicate`, no work.
2. Checkout session id matches no funding yields `unmatched`, no work, 200.
3. `amount_minor` or `currency` differ from the funding yields `rejected`, no work, 200, logged
   at error level.
4. Funding is not `pending` yields `duplicate` (if succeeded) or `ignored`, no work.
5. A `DbUpdateConcurrencyException` from `xmin`, or a unique violation on
   `BalanceTransaction.FundingId`, means another request won. Roll back, record `duplicate`,
   return 200 so Stripe stops retrying.

## 10. Webhook pipeline

`POST /webhooks/stripe` reads the raw body, never a model-bound object.

1. If `Stripe-Signature` is absent, 400.
2. Verify with `EventUtility.ConstructEvent(body, signature, webhookSecret)`. Failure is 400 and a
   warning log that contains no body content.
3. Persist or look up `PaymentEvent` by event id. Unknown event types are recorded as `ignored`
   and acknowledged.
4. Map:

   | Stripe event | Action |
   | --- | --- |
   | `checkout.session.completed` with `payment_status = paid` | credit |
   | `checkout.session.async_payment_succeeded` | credit |
   | `checkout.session.async_payment_failed` | funding `failed`, no credit |
   | `checkout.session.expired` | funding `cancelled`, no credit |
   | anything else, including refunds and disputes | recorded as `ignored`, no credit |

5. A credit requires the amount and currency to match the funding exactly. Stripe sends
   `amount_total` in minor units, so this is a direct comparison.
6. Return 200. Return 5xx only for a transient failure (database down, for example), which is
   exactly the signal Stripe's retry schedule needs.

Processing is synchronous inside the request. A queue is not justified at this scale, and the
retry mechanism already exists at the provider edge.

### Provider interface

`IPaymentProvider` is the only thing the rest of the API knows about Stripe.

```text
CreateCheckoutSessionAsync(Funding funding, CancellationToken) -> (SessionId, CheckoutUrl)
GetCheckoutSessionAsync(string sessionId, CancellationToken)    -> CheckoutSessionSnapshot
VerifyWebhook(string body, string signature)                    -> ProviderEvent
```

`CheckoutSessionSnapshot` carries `Status`, `PaymentStatus`, `AmountMinor`, `Currency`,
`PaymentIntentId`. `ProviderEvent` carries `EventId`, `Type`, `ObjectId`, `AmountMinor`, `Currency`,
`PaymentStatus`, and a `Succeeded` flag. The fake used in tests records the calls it received, in
the same spirit as `FakeGitHubRepositoryClient`.

## 11. Authorization

- `Project.OwnerUserId` is the only definition of a maintainer.
- Balance and transaction endpoints require that match, otherwise 403. A project that does not
  exist is 404, checked first.
- `POST /fundings` requires a signed-in user. Any user may fund any project, including their own.
  The maintainer relationship is not required.
- Funding reads require `SponsorUserId == current user`.
- The webhook is anonymous by design. The signature is the credential, and it is verified before
  any database read that could reveal whether a session id exists.

## 12. Deferred: refunds and chargebacks

`charge.refunded` and `charge.dispute.created` are recorded in `PaymentEvents` with outcome
`ignored`. The balance is not reduced. Reducing a balance after it has been spent needs the usage
epic's ledger and a policy for negative balances, so inventing it now would be wrong. The audit
row makes the gap visible instead of silent.

## 13. Calls made by the builder, not the sponsor

These are decisions inside the approved direction. Each is reversible, and each is listed here so
it can be argued with.

1. **Payment folded into Funding.** One funding has one payment in this increment. A separate
   `Payment` table would add a join and a second status field with no behaviour behind it. If
   retries with multiple payment attempts appear later, the split becomes worthwhile.
2. **Optimistic concurrency via `xmin`, backed by unique indexes.** Simpler than `SELECT FOR
   UPDATE`, and it keeps the application code provider-agnostic. The unique index on
   `BalanceTransaction.FundingId` is what actually makes double crediting impossible.
3. **Synchronous webhook processing.** No queue, no background worker. Returning 5xx for transient
   failures delegates retries to Stripe, which already does it well.
4. **No raw webhook payload stored.** Event id, type, object id, outcome, and reason only.
5. **The fund form lives on the project detail page.** No extra nested route for `/fund`. An
   inline review step satisfies "the sponsor can review the funding details before proceeding".
6. **Hosted Stripe Checkout, not Elements.** Card data never reaches OSSFuel, which removes a
   whole class of PCI obligations.
7. **Postgres concurrency tests require Docker.** The InMemory provider cannot express `xmin`,
   unique indexes, or real transactions. The suite fails with a clear message pointing at
   `docker compose up -d` instead of silently skipping. CI gets a Postgres service container.
8. **An owner-triggered sync endpoint instead of a background reconciler.** One extra endpoint
   closes the "paid but every webhook delivery failed" hole for a fraction of the cost of a worker.
9. **One pending funding per sponsor and project.** The partial unique index makes a double click
   harmless and keeps the history free of phantom rows.
10. **A failed Checkout Session creation leaves a `failed` funding row.** Deleting it would hide
    the attempt, and leaving it `pending` would block the retry.
11. **The maintainer does not see who funded.** US-12 asks for amount, type, status, and date, and
    nothing else. The funding row makes a funder name a one-line change later if you want it, and
    deciding to reveal an identity is a privacy choice rather than a technical one.

## 14. Web

### Project detail changes

- A "Fund this project" panel for any signed-in user: amount field, review step showing the
  project name and amount, then `POST /fundings` and a redirect to `checkoutUrl`. Signed-out
  visitors see a "Sign in to fund" link instead.
- For the owner, a "Balance" panel: amount in USD, then the transaction table (date, description,
  amount, type). Empty state when there are no transactions.

### New routes

| Route | Purpose |
| --- | --- |
| `/fundings` | sponsor history: project, amount, status, date, newest first, paginated |
| `/fundings/$fundingId` | return page: `pending` shows "Payment submitted, waiting for confirmation", polls every 2 seconds for 60 seconds, and calls sync once at ten seconds. `succeeded`, `failed`, and `cancelled` show the outcome and a link back to the project |

### Shared code

- `lib/funding.ts` mirrors `lib/projects.ts`: query options plus create, list, and get functions.
- Status chips reuse the existing border and eyebrow styles; no new component library.
- The header gains a "Funding" link for signed-in users.
- Every loading state uses `Loader`, per the project convention.

## 15. Configuration

| Key | Default | Notes |
| --- | --- | --- |
| `Stripe:SecretKey` | none | secret; empty means payments are not configured |
| `Stripe:WebhookSecret` | none | secret; required to verify webhooks |
| `Funding:MinAmountMinor` | `100` | $1.00 |
| `Funding:MaxAmountMinor` | `1000000` | $10,000.00 |

Redirect URLs derive from the existing `WebAppUrl` setting: success goes to
`{WebAppUrl}/fundings/{id}`, cancel goes back to `{WebAppUrl}/projects/{projectId}`.

The API starts without Stripe keys. `POST /fundings` then returns 503 and the web shows that
message. Tests replace `IPaymentProvider` with a fake, exactly as `IGitHubRepositoryClient` is
replaced today.

Local webhook testing uses `stripe listen --forward-to localhost:5050/webhooks/stripe`, and the
CLI prints the webhook secret for that session.

## 16. Testing

### Unit

- Funding status transitions: `pending` to `succeeded`, `failed`, `cancelled`; no transitions out
  of a terminal status.
- Amount validation: zero, negative, below minimum, above maximum, valid bounds.
- `AIBalance.Credit` adds the amount and appends exactly one transaction with the funding id.
- Currency and amount comparison for the webhook guard.

### Integration, InMemory, existing factory pattern

- Balance requires the owner (403), unknown project is 404, non-owner is 403.
- Transaction list is newest first and paginated.
- `POST /fundings` validates the project and amount, returns 201, and calls the fake provider with
  the exact amount, currency, and idempotency key.
- `GET /fundings` returns only the caller's fundings.
- `GET /fundings/{id}` is 403 for another user and 404 for an unknown id.
- Webhook: missing signature 400, bad signature 400, unknown session 200 with no credit, amount
  mismatch 200 with no credit, expired session marks cancelled with no credit, duplicate event id
  yields one credit, a second different event for the same session yields one credit.
- Failed and cancelled payments leave the balance at zero and write no transaction.
- A credit creates exactly one transaction and flips the funding to `succeeded`.
- A repeated `POST /fundings` for the same sponsor, project, and amount returns the same funding,
  and the fake provider is called once. A different amount while one is pending is 409.
- A provider failure during Checkout Session creation leaves the funding `failed` with a reason and
  returns 502, and a second attempt succeeds.
- `POST /fundings/{id}/sync` credits a paid session whose webhook never arrived, and is a no-op for
  a funding that already succeeded.

### Integration, Postgres, new fixture

Runs against a database named `ossfuel_tests` on the compose server, never the dev `ossfuel`
database. Compose gains a one-line init script so the database exists after `docker compose up -d`,
and CI sets `POSTGRES_DB: ossfuel_tests` on its service container. Each test class creates a unique
schema, runs the migration with that schema as `search_path`, and drops it afterwards.

These tests carry `[Trait("Category", "Postgres")]`. Without a reachable server the fixture fails
with a message naming `docker compose up -d` and the connection string it tried, rather than passing
vacuously. `dotnet test --filter "Category!=Postgres"` runs everything else without Docker.

- Two concurrent webhook POSTs for the same session produce one credit and one transaction.
- The unique index on `BalanceTransaction.FundingId` rejects a second credit row.
- A failure inside the credit transaction leaves the balance and the transaction count unchanged.
- `xmin` concurrency makes the second writer fail rather than overwrite.

### CI

`pr.yml` gains a Postgres service container and a `Database:TestConnectionString` environment
variable for the API job. The web job is unchanged.

## 17. Migration and backfill

One migration, `AddAiBalancesAndFunding`, creates the four tables and indexes. It then inserts one
zero balance for every project that already exists:

```sql
INSERT INTO "AIBalances" ("Id", "ProjectId", "AmountMinor", "Currency", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), "Id", 0, 'usd', now(), now() FROM "Projects";
```

Project registration also opens the balance in the same `SaveChanges`, so new projects never rely
on the backfill. The balance read path is defensive: if a row is somehow missing it inserts one
through a unique-index-safe path instead of returning a 500.

## 18. Deleting a project that has funding

The project management increment shipped `DELETE /projects/{id}`, and the foreign key from Funding
restricts deletion. Left alone, deleting a funded project would surface as a 500.

Three answers exist:

- **Refuse with 409.** The maintainer is told the project has funding history and cannot be deleted.
  Balance and history stay intact, and the web shows the problem message.
- **Soft delete.** Add `ArchivedAt`, hide archived projects from listings, keep everything readable.
  This touches every project query.
- **Cascade.** Delete the funding rows too. This destroys the sponsor's receipt and the balance
  ledger, and it silently empties the sponsor's funding history.

Decision: refuse with 409. The maintainer is told the project has funding history and cannot be
deleted, and the ledger stays intact. Soft delete becomes the natural answer once archiving is a
product feature; it is not needed to ship funding.

## 19. Definition of done for this increment

- [ ] All acceptance criteria in US-10 to US-20 are covered by a test.
- [ ] The full API suite passes, including the Postgres concurrency tests.
- [ ] Web lint, typecheck, and build pass.
- [ ] The end-to-end flow runs locally against Stripe test mode: create funding, pay, see the
      balance and the transaction, see the funding in history.
- [ ] Duplicate webhook delivery, verified with the Stripe CLI, credits once.
- [ ] Sync credits a paid funding whose webhook deliveries were lost.
- [ ] Deleting a project that has funding returns 409 and leaves the ledger intact.
- [ ] The design calls in section 13 are either accepted or replaced.

## Appendix A: user story coverage

The source is the Sprint 2 functional specification supplied on 2026-11-02, covering US-10 to
US-20. This table is the trace from each story to the design and to the test that proves it.

| Story | Where it is designed | Test |
| --- | --- | --- |
| US-10 balance per project | 7, 17 | balance created on registration, backfill, unique ProjectId |
| US-11 view balance | 8, 11 | owner 200, non-owner 403, unknown 404 |
| US-12 transaction history | 7, 8 | newest first, paginated, owner only, one per credit |
| US-13 choose project and amount | 6, 8 | validation bounds, 409 pending rule, created response |
| US-14 external payment | 8, 13, 15 | fake provider captures amount, currency, idempotency key, URLs |
| US-15 receive webhooks | 10 | unknown event, unmatched session, 200 protocol |
| US-16 verify webhooks | 10 | missing and bad signature 400, amount or currency mismatch |
| US-17 credit after payment | 9 | credit path, one transaction, funding succeeded |
| US-18 no duplicate processing | 9, 10, 16 | duplicate event, second event, concurrent POST, unique index |
| US-19 failed or cancelled | 10 | failed and expired leave the balance at zero, no transaction |
| US-20 sponsor history | 8, 11, 14 | own funding list, other user 403, ordering |
