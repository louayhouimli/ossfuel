# AI Balance and Project Funding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every project a Stripe-funded AI Balance, credited exactly once per verified payment, and visible to the maintainer as a transaction history.

**Architecture:** Two new API features. `Balances` owns the balance row and its immutable transaction ledger. `Funding` owns the sponsor's funding record, the payment-provider adapter, and one application service that is the only writer of balance changes. The web reads both features through two new route files and two panels on the existing project page.

**Tech Stack:** ASP.NET Core 10 minimal APIs, EF Core 10 with Npgsql, Stripe.net, xUnit 2.9.3, React 19 with TanStack Router and Query, pnpm 10.

**Spec:** `docs/superpowers/specs/2026-11-02-ai-balance-and-funding-design.md`

## Global Constraints

- Target framework `net10.0`; EF Core `10.0.11`; Npgsql provider `10.0.3`; xUnit `2.9.3`.
- Money is `long` minor units everywhere. No `decimal`, no `double`, no `parseFloat`.
- Currency is the lowercase string `usd` in the database and in JSON.
- Funding status strings stored lowercase: `pending`, `succeeded`, `failed`, `cancelled`.
- Amount bounds come from configuration: `Funding:MinAmountMinor` = `100`, `Funding:MaxAmountMinor` = `1000000`.
- Run every API test with `-c Release`, because the running `dotnet watch` holds the Debug output.
- Web files must pass `pnpm exec prettier --write` and `pnpm --filter web lint`.
- Every loading state in the web uses the existing `Loader` component.
- User-facing copy stays plain: no em dashes, no exclamation marks, no invented numbers.
- Do not add a web test runner. The amount parser is verified by hand in Task 12.
- Do not commit without running the task's own test command and reading its output.

## Review Focus

The spec does not enumerate these, and a reasonable person would expect them to work. Each one is pinned to a test in the task named beside it.

1. A sponsor pays, Stripe's webhooks all fail, and the funding sits `pending` forever → recovery test in Task 7.
2. A valid amount like `0.10` or `25.50` is rejected or turned into 24 or 26 cents → validation tests in Task 4 and the hand check in Task 12.
3. Two webhook deliveries for one payment arrive at the same moment and credit twice → concurrency test in Task 11.
4. A cancelled or expired event arrives after the funding already succeeded and drags it backwards → status guard test in Task 5.
5. One maintainer deep-links to another project's balance and reads it → authorization test in Task 2.
6. A maintainer deletes a project that received money and the API answers 500 → delete guard test in Task 3.

---

### Task 1: A balance opens with every project

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Balances/AIBalance.cs`
- Create: `apps/api/OSSFuel.Api/Features/Balances/Money.cs`
- Create: `apps/api/OSSFuel.Api/Features/Balances/BalanceTransaction.cs`
- Create: `apps/api/OSSFuel.Api/Features/Balances/AIBalanceConfiguration.cs`
- Create: `apps/api/OSSFuel.Api/Features/Balances/BalanceTransactionConfiguration.cs`
- Modify: `apps/api/OSSFuel.Api/Database/OSSFuelDbContext.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Projects/ProjectEndpoints.cs:301-314`
- Test: `apps/api/OSSFuel.UnitTests/Features/Balances/AIBalanceTests.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Balances/BalanceCreationTests.cs`

**Interfaces:**
- Consumes: `Project.Id` from `Project.Register(GitHubRepository, Guid, DateTimeOffset)`.
- Produces: `AIBalance.Open(Guid projectId, DateTimeOffset now) -> AIBalance`; `AIBalance.Credit(long amountMinor, Guid fundingId, string? description, DateTimeOffset now) -> BalanceTransaction`; `Money.Currency == "usd"`; `FundingCreditType == "funding_credit"`.

- [ ] **Step 1: Write the failing unit test**

```csharp
// apps/api/OSSFuel.UnitTests/Features/Balances/AIBalanceTests.cs
public class AIBalanceTests
{
    [Fact]
    public void Open_starts_at_zero_in_usd()
    {
        var balance = AIBalance.Open(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(0, balance.AmountMinor);
        Assert.Equal("usd", balance.Currency);
    }

    [Fact]
    public void Credit_adds_the_amount_and_appends_one_transaction()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = AIBalance.Open(Guid.NewGuid(), now);
        var fundingId = Guid.NewGuid();

        var transaction = balance.Credit(2500, fundingId, "Funding received", now);

        Assert.Equal(2500, balance.AmountMinor);
        Assert.Single(balance.Transactions);
        Assert.Equal(2500, transaction.AmountMinor);
        Assert.Equal(fundingId, transaction.FundingId);
        Assert.Equal("funding_credit", transaction.Type);
        Assert.Equal("posted", transaction.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Credit_rejects_non_positive_amounts(long amount)
    {
        var balance = AIBalance.Open(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => balance.Credit(amount, Guid.NewGuid(), null, DateTimeOffset.UtcNow));
    }
}
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release --filter "FullyQualifiedName~AIBalanceTests"`
Expected: build error, `AIBalance` does not exist.

- [ ] **Step 3: Implement the entities**

`Money.cs`: `public static class Money { public const string Currency = "usd"; }`.

`AIBalance.cs`: the properties from spec section 7, `RowVersion` as `uint`, the navigation `IReadOnlyCollection<BalanceTransaction> Transactions` backed by a `List`, plus `Open` and `Credit` above. `Credit` throws `ArgumentOutOfRangeException` for `amountMinor <= 0`, adds to `AmountMinor`, sets `UpdatedAt`, builds a `BalanceTransaction` and appends it.

`BalanceTransaction.cs` in the same folder: `Id`, `AIBalanceId`, `Guid? FundingId`, `long AmountMinor`, `string Type`, `string Status`, `string? Description`, `DateTimeOffset CreatedAt`, constants `FundingCreditType = "funding_credit"` and `PostedStatus = "posted"`.

`AIBalanceConfiguration.cs`: key on `Id`; unique index on `ProjectId`; `Currency` max length 3 and required; `RowVersion` via `builder.Property(balance => balance.RowVersion).IsRowVersion()`; the project link with `builder.HasOne<Project>().WithOne().HasForeignKey<AIBalance>(balance => balance.ProjectId).OnDelete(DeleteBehavior.Cascade);`. `BalanceTransactionConfiguration.cs`: key; `builder.HasOne<AIBalance>().WithMany(balance => balance.Transactions).HasForeignKey(t => t.AIBalanceId).OnDelete(DeleteBehavior.Cascade);`; unique index on `FundingId`; index on `(AIBalanceId, CreatedAt)`. Do not add a navigation to `Project`; the projects feature stays untouched.

`OSSFuelDbContext.cs`: add `public DbSet<AIBalance> AIBalances => Set<AIBalance>();`.

- [ ] **Step 4: Run the unit test and confirm it passes**

Run the Step 2 command. Expected: 4 passed.

- [ ] **Step 5: Write the failing integration test**

```csharp
// apps/api/OSSFuel.IntegrationTests/Features/Balances/BalanceCreationTests.cs
public class BalanceCreationTests(ProjectsWebApplicationFactory factory)
    : IClassFixture<ProjectsWebApplicationFactory>
{
    [Fact]
    public async Task Registering_a_project_opens_one_zero_balance()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClientFor(userId);
        factory.GitHub.Repository = TestRepository.Create(id: 4242, name: "new-project");

        var response = await client.PostAsJsonAsync("/projects", new { fullName = "octocat/new-project" });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectPayload>();

        var balances = factory.ReadDb(db => db.AIBalances.Where(b => b.ProjectId == project!.Id).ToList());
        var balance = Assert.Single(balances);
        Assert.Equal(0, balance.AmountMinor);
    }

    [Fact]
    public async Task Registering_the_same_repository_twice_does_not_add_a_second_balance()
    {
        // Register, call POST /projects again with the same repo, assert the 409
        // and that the balance count for the project is still 1.
    }

    private sealed record ProjectPayload(Guid Id, string FullName);
}
```

Existing payload records live as private nested records inside each test file, so follow that pattern.

Add two helpers to `ProjectsWebApplicationFactory`:

```csharp
public T ReadDb<T>(Func<OSSFuelDbContext, T> read)
{
    using var scope = Services.CreateScope();
    return read(scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>());
}

public Guid SeedCredit(Guid projectId, long amountMinor, string? description = "Funding received")
{
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
    var balance = db.AIBalances.Single(b => b.ProjectId == projectId);
    balance.Credit(amountMinor, Guid.NewGuid(), description, DateTimeOffset.UtcNow);
    db.SaveChanges();

    return balance.Id;
}
```

- [ ] **Step 6: Run it and confirm it fails**

Run: `dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~BalanceCreationTests"`
Expected: one balance found is 0, because registration does not create one yet.

- [ ] **Step 7: Create the balance during registration**

In `CreateProjectAsync`, after `var project = Project.Register(...)`, add:

```csharp
var now = DateTimeOffset.UtcNow;
db.Projects.Add(project);
db.AIBalances.Add(AIBalance.Open(project.Id, now));

try { await db.SaveChangesAsync(cancellationToken); }
```

- [ ] **Step 8: Run both test files and confirm they pass**

Run the Step 6 command, then the Step 2 command. Expected: all green.

- [ ] **Step 9: Commit**

```bash
git add apps/api/OSSFuel.Api/Features/Balances apps/api/OSSFuel.Api/Database/OSSFuelDbContext.cs apps/api/OSSFuel.Api/Features/Projects/ProjectEndpoints.cs apps/api/OSSFuel.UnitTests/Features/Balances apps/api/OSSFuel.IntegrationTests/Features/Balances apps/api/OSSFuel.IntegrationTests/Infrastructure
git commit -m "feat(api): open an AI balance with every project"
```

---

### Task 2: Read the balance and its history

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Balances/BalanceContracts.cs`
- Create: `apps/api/OSSFuel.Api/Features/Balances/BalanceEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Balances/BalanceAccessTests.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Balances/BalanceTransactionTests.cs`

**Interfaces:**
- Consumes: `AIBalance`, `BalanceTransaction`.
- Produces: `BalanceResponse(Guid ProjectId, long AmountMinor, string Currency)`, `BalanceTransactionResponse(Guid Id, long AmountMinor, string Type, string Status, string? Description, DateTimeOffset CreatedAt)`, `BalanceTransactionListResponse(IReadOnlyList<BalanceTransactionResponse> Items, int Page, int PageSize, int TotalCount)`; `MapBalanceEndpoints(this IEndpointRouteBuilder)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// BalanceAccessTests.cs
[Fact]
public async Task Owner_reads_the_balance()
{
    var owner = factory.SeedUser("maintainer");
    var projectId = factory.SeedProject(owner);
    factory.SeedCredit(projectId, 1500, "Funding received");

    var response = await factory.CreateClientFor(owner).GetAsync($"/projects/{projectId}/balance");

    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadFromJsonAsync<BalancePayload>();
    Assert.Equal(1500, body!.AmountMinor);
    Assert.Equal("usd", body.Currency);
}

[Fact]
public async Task Another_user_is_forbidden()
{
    var owner = factory.SeedUser("maintainer");
    var stranger = factory.SeedUser("stranger");
    var projectId = factory.SeedProject(owner);

    var response = await factory.CreateClientFor(stranger).GetAsync($"/projects/{projectId}/balance");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task Unknown_project_is_not_found()
{
    var userId = factory.SeedUser();

    var response = await factory.CreateClientFor(userId).GetAsync($"/projects/{Guid.NewGuid()}/balance");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}

[Fact]
public async Task Anonymous_is_unauthorized() { /* GET without the test header -> 401 */ }

[Fact]
public async Task A_missing_balance_is_created_instead_of_returning_500()
{
    var owner = factory.SeedUser("maintainer");
    var projectId = factory.SeedProject(owner);
    factory.ReadDb(db =>
    {
        db.AIBalances.RemoveRange(db.AIBalances.Where(b => b.ProjectId == projectId));
        return db.SaveChanges();
    });

    var response = await factory.CreateClientFor(owner).GetAsync($"/projects/{projectId}/balance");

    response.EnsureSuccessStatusCode();
    Assert.Equal(0, (await response.Content.ReadFromJsonAsync<BalancePayload>())!.AmountMinor);
    Assert.Single(factory.ReadDb(db => db.AIBalances.Where(b => b.ProjectId == projectId).ToList()));
}
```

```csharp
// BalanceTransactionTests.cs
[Fact]
public async Task History_is_newest_first_and_paginated()
{
    // Seed three credits at now-2h, now-1h, now, read page 1 size 2,
    // assert the newest two in descending order and TotalCount 3.
}

[Fact]
public async Task Another_user_cannot_read_history() { /* 403 */ }
```

Add `private sealed record BalancePayload(Guid ProjectId, long AmountMinor, string Currency);` inside each file.
`factory.SeedCredit` was added in Task 1.

- [ ] **Step 2: Run them and confirm they fail**

Run: `dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~BalanceAccessTests|FullyQualifiedName~BalanceTransactionTests"`
Expected: 404, because no route exists.

- [ ] **Step 3: Implement the endpoints**

`BalanceEndpoints.GetBalanceAsync`: first extract `ProjectEndpoints.LoadCurrentUserAsync` into `apps/api/OSSFuel.Api/Features/Users/UserLookup.cs` as

```csharp
internal static class UserLookup
{
    public static Task<User?> LoadAsync(ClaimsPrincipal principal, OSSFuelDbContext db, CancellationToken ct)
}
```

with the existing body, and point `ProjectEndpoints` at it. Then load the project by id, and load the balance separately with `db.AIBalances.FirstOrDefaultAsync(b => b.ProjectId == id)` (no navigation on `Project`, so the projects feature stays untouched); 404 when the project is missing; 403 when `OwnerUserId` differs; if the balance is missing, insert `AIBalance.Open(projectId, now)` and save, catching `DbUpdateException` and reloading, so a missing row cannot become a 500; return `BalanceResponse`.

`GetTransactionsAsync`: same authorization, then query `BalanceTransactions` by `AIBalanceId`, `OrderByDescending(t => t.CreatedAt)`, page and page size clamped to 1..50 with default 20, project to the response.

Register in `Program.cs`: `app.MapBalanceEndpoints();` after `app.MapProjectEndpoints();`.

- [ ] **Step 4: Run them and confirm they pass**

Run the Step 2 command. Expected: all green.

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(api): expose the project balance and transaction history"
```

---

### Task 3: Funding records and the delete guard

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/FundingStatus.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/Funding.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/FundingConfiguration.cs`
- Modify: `apps/api/OSSFuel.Api/Database/OSSFuelDbContext.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Projects/ProjectEndpoints.cs:93-96`
- Test: `apps/api/OSSFuel.UnitTests/Features/Funding/FundingTests.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Projects/ProjectDeleteGuardTests.cs`

**Interfaces:**
- Produces: `enum FundingStatus { Pending, Succeeded, Failed, Cancelled }`; `Funding.Create(Guid projectId, Guid sponsorUserId, long amountMinor, DateTimeOffset now) -> Funding`; `MarkCheckoutCreated(string sessionId, string checkoutUrl, DateTimeOffset now)`; `MarkSucceeded(string? paymentIntentId, DateTimeOffset now)`; `MarkFailed(string reason, DateTimeOffset now)`; `MarkCancelled(string reason, DateTimeOffset now)`; `bool CanTransitionTo(FundingStatus next)`.

- [ ] **Step 1: Write the failing unit test**

```csharp
[Fact]
public void New_funding_is_pending_and_cannot_skip_to_succeeded_twice()
{
    var funding = Funding.Create(Guid.NewGuid(), Guid.NewGuid(), 2500, DateTimeOffset.UtcNow);

    Assert.Equal(FundingStatus.Pending, funding.Status);
    Assert.True(funding.CanTransitionTo(FundingStatus.Succeeded));

    funding.MarkSucceeded("pi_1", DateTimeOffset.UtcNow);

    Assert.Equal(FundingStatus.Succeeded, funding.Status);
    Assert.NotNull(funding.CompletedAt);
    Assert.Throws<InvalidOperationException>(
        () => funding.MarkSucceeded("pi_1", DateTimeOffset.UtcNow));
}

[Theory]
[InlineData(FundingStatus.Succeeded)]
[InlineData(FundingStatus.Failed)]
[InlineData(FundingStatus.Cancelled)]
public void Terminal_statuses_never_move_again(FundingStatus terminal) { /* MarkCancelled on it throws */ }
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `dotnet test apps/api/OSSFuel.UnitTests/OSSFuel.UnitTests.csproj -c Release --filter "FullyQualifiedName~FundingTests"`

- [ ] **Step 3: Implement the entity**

Properties from spec section 7, navigations `Project` and `Sponsor`, constant `StripeProvider = "stripe"`. `MarkSucceeded` throws `InvalidOperationException("Funding is not pending.")` unless `Pending`. `CanTransitionTo` returns true only from `Pending` to any other status. `MarkCheckoutCreated` sets the session id and URL and `UpdatedAt`. `MarkFailed` and `MarkCancelled` set `FailureReason`, `CompletedAt`, `UpdatedAt`.

`FundingConfiguration`: key, indexes from the spec, `Status` stored lowercase via a value converter, unique indexes on the two provider ids, and:

```csharp
builder.HasIndex(funding => new { funding.SponsorUserId, funding.ProjectId })
    .IsUnique()
    .HasFilter("\"Status\" = 'pending'");
```

`OSSFuelDbContext.cs`: add `DbSet<Funding>`.

- [ ] **Step 4: Run the unit test and confirm it passes**

- [ ] **Step 5: Write the failing delete-guard test**

```csharp
[Fact]
public async Task Deleting_a_funded_project_returns_conflict_and_keeps_the_ledger()
{
    var owner = factory.SeedUser();
    var projectId = factory.SeedProject(owner);
    factory.SeedFunding(projectId, owner, 2500);

    var response = await factory.CreateClientFor(owner).DeleteAsync($"/projects/{projectId}");

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.NotNull(factory.ReadDb(db => db.Projects.Find(projectId)));
    Assert.Single(factory.ReadDb(db => db.Fundings.Where(f => f.ProjectId == projectId).ToList()));
}

[Fact]
public async Task Deleting_an_unfunded_project_still_works() { /* 204 and balance gone */ }
```

`SeedFunding` and its variants live on `ProjectsWebApplicationFactory` (Task 11 moves them to the shared base):

```csharp
public Guid SeedFunding(Guid projectId, Guid sponsorUserId, long amountMinor, string? checkoutSessionId = null)
public (Guid ProjectId, Guid FundingId) SeedPendingFunding(long amountMinor)
public (Guid ProjectId, Guid FundingId, Guid SponsorId) SeedPendingFundingWithSponsor(long amountMinor)
```

`SeedFunding` inserts a `pending` `Funding` and, when `checkoutSessionId` is given, calls `MarkCheckoutCreated(checkoutSessionId, "https://checkout.test/x", now)`. `SeedPendingFunding` creates a maintainer, a project, and a sponsor, then calls `SeedFunding` with `checkoutSessionId = $"cs_{id:N}"` and returns the ids.

- [ ] **Step 6: Run it and confirm it fails**

Expected: 500 or 204, not 409.

- [ ] **Step 7: Add the guard**

In `DeleteProjectAsync`, after the ownership check:

```csharp
if (await db.Fundings.AnyAsync(funding => funding.ProjectId == id, cancellationToken))
{
    return Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Project has funding",
        detail: "This project has funding history. Projects with funding cannot be deleted.");
}
```

- [ ] **Step 8: Run the task's tests and confirm they pass**

- [ ] **Step 9: Commit**

```bash
git commit -am "feat(api): add funding records and protect funded projects from deletion"
```

---

### Task 4: Create a funding through the payment provider

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/FundingOptions.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/IPaymentProvider.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/FundingContracts.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/FundingEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Infrastructure/FakePaymentProvider.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingCreateTests.cs`

**Interfaces:**
- Produces: `enum PaymentAction { Credit, Fail, Cancel, Ignore }`; `sealed record PaymentNotification(string EventId, string Type, string ObjectId, long AmountMinor, string Currency, string? PaymentIntentId, PaymentAction Action)`; `sealed record ProviderSession(string SessionId, string CheckoutUrl)`; `sealed record CheckoutSessionSnapshot(string Status, string PaymentStatus, long AmountMinor, string Currency, string? PaymentIntentId)`; `interface IPaymentProvider { bool IsConfigured { get; } Task<ProviderSession> CreateCheckoutSessionAsync(Funding funding, CancellationToken ct); Task<CheckoutSessionSnapshot> GetCheckoutSessionAsync(string sessionId, CancellationToken ct); PaymentNotification? VerifyWebhook(string body, string signature); }`; `CreateFundingRequest(Guid ProjectId, long AmountMinor)`; `FundingResponse(...)`; `MapFundingEndpoints(this IEndpointRouteBuilder)`.

- [ ] **Step 1: Define the provider types, then write the fake**

The fake cannot compile before the interface exists, so define the types from the Interfaces block first (interface plus the three records and the enum, no implementations), then write the fake below.

```csharp
public sealed class FakePaymentProvider : IPaymentProvider
{
    public bool IsConfigured { get; set; } = true;
    public Exception? CreateFailure { get; set; }
    public List<Funding> Created { get; } = [];
    public string? LastSignature { get; private set; }
    public PaymentNotification? NextNotification { get; set; }
    public CheckoutSessionSnapshot SessionSnapshot { get; set; } =
        new("open", "unpaid", 0, "usd", null);

    public Task<ProviderSession> CreateCheckoutSessionAsync(Funding funding, CancellationToken ct)
    {
        if (CreateFailure is not null) throw CreateFailure;
        Created.Add(funding);
        return Task.FromResult(new ProviderSession($"cs_{funding.Id:N}", $"https://checkout.test/{funding.Id:N}"));
    }

    public Task<CheckoutSessionSnapshot> GetCheckoutSessionAsync(string sessionId, CancellationToken ct) =>
        Task.FromResult(SessionSnapshot);

    public PaymentNotification? VerifyWebhook(string body, string signature)
    {
        LastSignature = signature;
        return signature == "bad" ? null : NextNotification;
    }
}
```

- [ ] **Step 2: Write the failing create tests**

```csharp
[Fact]
public async Task Creating_a_funding_returns_a_checkout_url_and_pending_status()
{
    var sponsor = factory.SeedUser("sponsor");
    var projectId = factory.SeedProject(factory.SeedUser("maintainer"));

    var response = await factory.CreateClientFor(sponsor)
        .PostAsJsonAsync("/fundings", new { projectId, amountMinor = 2500 });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<FundingPayload>();
    Assert.Equal("pending", body!.Status);
    Assert.StartsWith("https://checkout.test/", body.CheckoutUrl);
    Assert.Single(factory.Payments.Created);
    Assert.Equal(2500, factory.Payments.Created[0].AmountMinor);
}

[Theory]
[InlineData(0)]
[InlineData(99)]
[InlineData(1_000_001)]
public async Task Amounts_outside_the_bounds_are_rejected(long amountMinor)
{
    // 400 with errors["amountMinor"]
}

[Fact]
public async Task A_repeat_with_the_same_amount_returns_the_same_funding()
{
    // POST twice, assert 201 then 200, same id, factory.Payments.Created has one entry.
}

[Fact]
public async Task A_repeat_with_a_different_amount_is_conflict() { /* 409 */ }

[Fact]
public async Task A_provider_failure_marks_the_funding_failed_and_returns_502()
{
    factory.Payments.CreateFailure = new HttpRequestException("down");
    // assert 502, then ReadDb the funding: status failed, FailureReason not null,
    // then clear the failure, POST again, assert 201.
}

[Fact]
public async Task An_unconfigured_provider_returns_503()
{
    factory.Payments.IsConfigured = false;
    // 503
}

[Fact]
public async Task An_unknown_project_is_not_found() { /* 404 */ }
```

`ProjectsWebApplicationFactory` gains `public FakePaymentProvider Payments { get; } = new();` and, inside its existing `ConfigureTestServices`, replaces `IPaymentProvider` with it. Every integration test then shares one factory, InMemory by default.

- [ ] **Step 3: Run them and confirm they fail**

- [ ] **Step 4: Implement**

`FundingOptions { long MinAmountMinor = 100; long MaxAmountMinor = 1_000_000; }`, bound from section `Funding` and registered in `Program.cs`.

`CreateFundingAsync`, in the order of spec section 8 "Creating a funding":

1. Resolve the user, 401 when absent.
2. Validate `ProjectId` is not empty and the project exists, else 404.
3. `amountMinor` below min or above max, or `<= 0`, returns `Results.ValidationProblem` keyed `amountMinor` with the bounds in the message.
4. Load the existing `pending` funding for `(sponsor, project)`; when its amount matches return `Results.Ok(ToResponse(existing, ...))`, otherwise return the 409 problem from the spec.
5. Insert `Funding.Create(...)` and save.
6. `if (!payments.IsConfigured) return 503`.
7. `try { var session = await payments.CreateCheckoutSessionAsync(funding, ct); funding.MarkCheckoutCreated(session.SessionId, session.CheckoutUrl, now); await db.SaveChangesAsync(ct); } catch (HttpRequestException) { funding.MarkFailed("Checkout session could not be created.", now); await db.SaveChangesAsync(ct); return 502; }`
8. Return `Results.Created($"/fundings/{funding.Id}", ToResponse(funding, project.FullName, includeCheckoutUrl: true))`, where

```csharp
private static FundingResponse ToResponse(Funding funding, string projectFullName, bool includeCheckoutUrl = false)
```

maps the entity and supplies `checkoutUrl` only when asked. The client-facing `status` is `funding.Status.ToString().ToLowerInvariant()`. Define `private sealed record FundingPayload(...)` inside the test file.

`MapFundingEndpoints` registers only `POST /fundings` in this task.

- [ ] **Step 5: Run and confirm they pass**

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(api): create sponsor funding and open a checkout session"
```

---

### Task 5: The credit operation

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/PaymentEvent.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/PaymentOutcome.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/PaymentEventConfiguration.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/FundingCreditService.cs`
- Modify: `apps/api/OSSFuel.Api/Database/OSSFuelDbContext.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Test: `apps/api/OSSFuel.UnitTests/Features/Funding/PaymentActionMappingTests.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingCreditServiceTests.cs`

**Interfaces:**
- Produces: `enum PaymentOutcome { Credited, Duplicate, Ignored, Rejected, Unmatched, Failed, Cancelled }`; `PaymentEvent` entity; `Task<PaymentOutcome> FundingCreditService.ApplyAsync(PaymentNotification notification, CancellationToken ct)`; `static PaymentAction PaymentActionMapping.Decide(string type, string? paymentStatus)`.

- [ ] **Step 1: Write the failing mapping test**

```csharp
[Theory]
[InlineData("checkout.session.completed", "paid", PaymentAction.Credit)]
[InlineData("checkout.session.completed", "unpaid", PaymentAction.Ignore)]
[InlineData("checkout.session.async_payment_succeeded", null, PaymentAction.Credit)]
[InlineData("checkout.session.async_payment_failed", null, PaymentAction.Fail)]
[InlineData("checkout.session.expired", null, PaymentAction.Cancel)]
[InlineData("charge.refunded", null, PaymentAction.Ignore)]
public void Maps_stripe_events(string type, string? paymentStatus, PaymentAction expected)
{
    Assert.Equal(expected, PaymentActionMapping.Decide(type, paymentStatus));
}
```

- [ ] **Step 2: Run it and confirm it fails**

- [ ] **Step 3: Implement the mapping and the entity**

`PaymentActionMapping.Decide` is a pure switch, no service, no dependencies. `PaymentEvent` holds the fields from spec section 7. `PaymentEventConfiguration`: key, unique index on `ProviderEventId`, `Outcome` stored as its lowercase name, `Type` max 100, `Detail` max 500.

- [ ] **Step 4: Write the failing service tests**

```csharp
[Fact]
public async Task A_paid_session_credits_once_and_records_the_event()
{
    var (projectId, fundingId) = factory.SeedPendingFunding(amountMinor: 2500);

    var outcome = await factory.ApplyPaymentAsync(Notification.Paid(fundingId, 2500));

    Assert.Equal(PaymentOutcome.Credited, outcome);
    Assert.Equal(2500, factory.ReadDb(db => db.AIBalances.Single(b => b.ProjectId == projectId).AmountMinor));
    Assert.Single(factory.ReadDb(db => db.BalanceTransactions.Where(t => t.FundingId == fundingId).ToList()));
    Assert.Equal("succeeded", factory.ReadDb(db => db.Fundings.Single(f => f.Id == fundingId)).Status.ToString().ToLowerInvariant());
    Assert.Equal(PaymentOutcome.Duplicate, await factory.ApplyPaymentAsync(Notification.Paid(fundingId, 2500)));
}

[Fact]
public async Task An_amount_mismatch_is_rejected_and_changes_nothing() { /* 2499 -> Rejected, balance 0, no transaction */ }

[Fact]
public async Task An_unknown_session_is_unmatched() { /* random object id -> Unmatched */ }

[Fact]
public async Task A_cancel_after_success_is_ignored_and_leaves_succeeded()
{
    // credit, then Notification.Cancel(fundingId) -> Ignored, status still succeeded, one transaction
}
```

Add these helpers to `ProjectsWebApplicationFactory`:

```csharp
public Task<PaymentOutcome> ApplyPaymentAsync(PaymentNotification notification)
{
    using var scope = Services.CreateScope();
    return scope.ServiceProvider
        .GetRequiredService<FundingCreditService>()
        .ApplyAsync(notification, CancellationToken.None);
}

public long BalanceOf(Guid projectId) =>
    ReadDb(db => db.AIBalances.Single(balance => balance.ProjectId == projectId).AmountMinor);

public List<BalanceTransaction> TransactionsOf(Guid fundingId) =>
    ReadDb(db => db.BalanceTransactions.Where(t => t.FundingId == fundingId).ToList());
```

And a `Notification` helper in the test project, matching the `cs_{fundingId:N}` convention from `SeedFunding`:

```csharp
internal static class Notification
{
    public static PaymentNotification Paid(Guid fundingId, long amountMinor, string? eventId = null) =>
        new(eventId ?? $"evt_{Guid.NewGuid():N}", "checkout.session.completed",
            $"cs_{fundingId:N}", amountMinor, "usd", "pi_1", PaymentAction.Credit);

    public static PaymentNotification Cancel(Guid fundingId) =>
        new($"evt_{Guid.NewGuid():N}", "checkout.session.expired",
            $"cs_{fundingId:N}", 0, "usd", null, PaymentAction.Cancel);

    public static PaymentNotification Fail(Guid fundingId) =>
        new($"evt_{Guid.NewGuid():N}", "checkout.session.async_payment_failed",
            $"cs_{fundingId:N}", 0, "usd", null, PaymentAction.Fail);
}
```

Payload records stay private inside each test file, as the existing tests do.

- [ ] **Step 5: Run them and confirm they fail**

- [ ] **Step 6: Implement `FundingCreditService`**

```csharp
public async Task<PaymentOutcome> ApplyAsync(PaymentNotification notification, CancellationToken ct)
```

1. If `PaymentEvents.AnyAsync(e => e.ProviderEventId == notification.EventId)` return `Duplicate`.
2. Load the funding by `ProviderCheckoutSessionId == notification.ObjectId` including `Project` and `Sponsor`, then load its balance with `db.AIBalances.FirstAsync(b => b.ProjectId == funding.ProjectId)` inside the credit step. Missing funding means record `Unmatched` and return it.
3. `notification.Action == Ignore` records `Ignored` and returns it.
4. Amount or currency mismatch records `Rejected` with a detail naming both amounts and returns it.
5. `Action == Fail` calls `MarkFailed` when pending, records `Failed`, saves, returns `Failed`.
6. `Action == Cancel` calls `MarkCancelled` when pending, records `Cancelled`, saves, returns `Cancelled`.
7. `Action == Credit`:
   - Already `Succeeded` records `Duplicate` and returns it.
   - Not `Pending` records `Ignored` and returns it.
   - Otherwise: `await using var transaction = await db.Database.BeginTransactionAsync(ct);` then `funding.MarkSucceeded(notification.PaymentIntentId, now)`, `balance.Credit(notification.AmountMinor, funding.Id, "Funding received", now)`, add a `PaymentEvent` with `Credited`, `await db.SaveChangesAsync(ct)`, `await transaction.CommitAsync(ct)`, return `Credited`.
   - Catch `DbUpdateConcurrencyException` and `DbUpdateException` around that block. On either, roll back, then record a `Duplicate` event in a new save and return `Duplicate`.
8. Every record-and-return path writes the `PaymentEvent` with `ProviderEventId = notification.EventId`.

Register `FundingCreditService` as scoped in `Program.cs`.

- [ ] **Step 7: Run and confirm they pass**

- [ ] **Step 8: Commit**

```bash
git commit -am "feat(api): credit a balance exactly once per verified payment"
```

---

### Task 6: The Stripe webhook endpoint

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/PaymentWebhookEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingWebhookTests.cs`

**Interfaces:**
- Consumes: `IPaymentProvider.VerifyWebhook`, `FundingCreditService.ApplyAsync`.
- Produces: `MapWebhookEndpoints(this IEndpointRouteBuilder)`; route `POST /webhooks/stripe`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task A_missing_signature_is_bad_request()
{
    var response = await factory.CreateClient().PostAsync("/webhooks/stripe", JsonContent("{}"));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

[Fact]
public async Task A_bad_signature_is_bad_request_and_credits_nothing()
{
    var (projectId, fundingId) = factory.SeedPendingFunding(2500);
    factory.Payments.NextNotification = Notification.Paid(fundingId, 2500);

    var response = await factory.CreateClient().PostAsync("/webhooks/stripe", JsonContent("{}", "bad"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(0, factory.BalanceOf(projectId));
}

[Fact]
public async Task A_verified_paid_event_credits_and_answers_ok()
{
    var (projectId, fundingId) = factory.SeedPendingFunding(2500);
    factory.Payments.NextNotification = Notification.Paid(fundingId, 2500);

    var response = await factory.CreateClient().PostAsync("/webhooks/stripe", JsonContent("{}", "ok"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2500, factory.BalanceOf(projectId));
}

[Fact]
public async Task The_same_event_twice_credits_once() { /* replay NextNotification, balance stays 2500 */ }

[Fact]
public async Task A_failed_event_leaves_the_funding_failed_and_credits_nothing() { /* Fail action */ }

[Fact]
public async Task An_expired_event_cancels_without_crediting() { /* Cancel action */ }

[Fact]
public async Task An_unconfigured_provider_returns_503() { /* IsConfigured = false */ }
```

- [ ] **Step 2: Run them and confirm they fail**

- [ ] **Step 3: Implement the endpoint**

```csharp
private static async Task<IResult> HandleStripeWebhookAsync(
    HttpRequest request,
    IPaymentProvider payments,
    FundingCreditService credits,
    CancellationToken cancellationToken)
{
    if (!payments.IsConfigured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    var signature = request.Headers["Stripe-Signature"].ToString();
    if (string.IsNullOrEmpty(signature)) return Results.BadRequest(new { error = "invalid signature" });

    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync(cancellationToken);

    var notification = payments.VerifyWebhook(body, signature);
    if (notification is null) return Results.BadRequest(new { error = "invalid signature" });

    await credits.ApplyAsync(notification, cancellationToken);
    return Results.Ok(new { received = true });
}
```

Register `app.MapWebhookEndpoints();` in `Program.cs`. Exceptions that reach the handler bubble to a 500, which is the retry signal the spec wants. Log one warning per rejected signature with the request path only, and one information line per applied event with its id, type, and outcome. Never log the body or the signature header.

In the test file, add `private static StringContent JsonContent(string body, string? signature = null)` that sets `Stripe-Signature` when a value is given.

- [ ] **Step 4: Run and confirm they pass**

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(api): receive and verify payment provider webhooks"
```

---

### Task 7: Recover a payment whose webhooks were lost

**Files:**
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingEndpoints.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingSyncTests.cs`

**Interfaces:**
- Consumes: `IPaymentProvider.GetCheckoutSessionAsync`, `FundingCreditService.ApplyAsync`.
- Produces: route `POST /fundings/{id}/sync`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Sync_credits_a_paid_session_whose_webhook_never_arrived()
{
    var (projectId, fundingId, sponsor) = factory.SeedPendingFundingWithSponsor(2500);
    factory.Payments.SessionSnapshot = new("complete", "paid", 2500, "usd", "pi_1");

    var response = await factory.CreateClientFor(sponsor).PostAsync($"/fundings/{fundingId}/sync", null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2500, factory.BalanceOf(projectId));
}

[Fact]
public async Task Sync_is_a_no_op_for_a_succeeded_funding() { /* second call, still one transaction */ }

[Fact]
public async Task Sync_for_another_user_is_forbidden() { /* 403 */ }

[Fact]
public async Task Sync_for_an_unpaid_session_changes_nothing() { /* status open -> still pending, balance 0 */ }
```

- [ ] **Step 2: Run them and confirm they fail**

- [ ] **Step 3: Implement**

Load the user, then the funding with `Project`. 404 when missing, 403 when `SponsorUserId != user.Id`, return the current funding when not `pending`. Otherwise call `GetCheckoutSessionAsync(funding.ProviderCheckoutSessionId!)`, build a `PaymentNotification` with `EventId = $"sync:{funding.Id}:{now:O}"`, `Type = "checkout.session.sync"`, `ObjectId = sessionId`, the snapshot's amount, currency and payment intent, and `Action = PaymentActionMapping.Decide(snapshotStatus == "complete" ? "checkout.session.completed" : "", snapshot.PaymentStatus)`; then `ApplyAsync` and return the reloaded funding.

- [ ] **Step 4: Run and confirm they pass**

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(api): allow a sponsor to resynchronise a pending funding"
```

---

### Task 8: Sponsor funding history

**Files:**
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingEndpoints.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingContracts.cs`
- Modify: `apps/api/OSSFuel.Api/Features/Funding/FundingConfiguration.cs`
- Test: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingHistoryTests.cs`

**Interfaces:**
- Produces: `GET /fundings?page&pageSize`, `GET /fundings/{id}`, `FundingListResponse`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task History_returns_only_the_callers_fundings_newest_first()
{
    // sponsor A: two fundings (older, newer), sponsor B: one.
    // GET /fundings as A -> 2 items, newest first, no B rows.
}

[Fact]
public async Task Reading_someone_elses_funding_is_forbidden() { /* 403 */ }

[Fact]
public async Task An_unknown_funding_is_not_found() { /* 404 */ }

[Fact]
public async Task Every_status_is_represented()
{
    // Seed four fundings for one sponsor. Mark them succeeded, failed and cancelled
    // through the domain methods inside ReadDb, then GET /fundings and assert the four
    // status strings in order.
}
```

The index on `(SponsorUserId, CreatedAt)` is configured in Task 3.

- [ ] **Step 2: Run them and confirm they fail**

- [ ] **Step 3: Implement the two GET routes**

List: filter by `SponsorUserId`, `OrderByDescending(f => f.CreatedAt)`, `Include(f => f.Project)`, page and size clamped as in Task 2, and map through `FundingResponse` with `CheckoutUrl = null`. Single: owner-only, 403 for anyone else.

- [ ] **Step 4: Run and confirm they pass**

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(api): expose a sponsor's funding history"
```

---

### Task 9: The Stripe adapter

**Files:**
- Create: `apps/api/OSSFuel.Api/Features/Funding/StripePaymentProvider.cs`
- Create: `apps/api/OSSFuel.Api/Features/Funding/StripeSetup.cs`
- Modify: `apps/api/OSSFuel.Api/Program.cs`
- Modify: `apps/api/OSSFuel.Api/OSSFuel.Api.csproj`
- Modify: `apps/api/OSSFuel.Api/appsettings.json`
- Test: `apps/api/OSSFuel.UnitTests/Features/Funding/PaymentActionMappingTests.cs` (extended)

**Interfaces:**
- Consumes: `IPaymentProvider`, `PaymentActionMapping`.
- Produces: `AddPaymentFeature(this IServiceCollection, IConfiguration)`.

- [ ] **Step 1: Add the package**

Run: `dotnet add apps/api/OSSFuel.Api/OSSFuel.Api.csproj package Stripe.net`
Expected: the latest stable version is added to the csproj. Record the resolved version in the commit message.

- [ ] **Step 2: Implement `StripePaymentProvider`**

Hold a nullable `StripeClient` built only when `Stripe:SecretKey` is non-empty, and the webhook secret string. `IsConfigured` is true when both exist. `CreateCheckoutSessionAsync` builds a session with `Mode = "payment"`, `ClientReferenceId = funding.Id.ToString()`, `SuccessUrl = $"{webAppUrl}/fundings/{funding.Id}"`, `CancelUrl = $"{webAppUrl}/projects/{funding.ProjectId}"`, one line item in `usd` with `UnitAmount = funding.AmountMinor`, and `RequestOptions { IdempotencyKey = funding.Id.ToString() }`. `GetCheckoutSessionAsync` retrieves the session and maps to `CheckoutSessionSnapshot`. `VerifyWebhook` calls `EventUtility.ConstructEvent(body, signature, webhookSecret)` and maps to `PaymentNotification` using `PaymentActionMapping.Decide(eventType, session.PaymentStatus)`.

- [ ] **Step 3: Write the failing test for the extra mapping case**

```csharp
[InlineData("checkout.session.sync", "paid", PaymentAction.Ignore)]
```

Sync sets its own action, so this pins that an unknown type defaults to `Ignore` rather than `Credit`.

- [ ] **Step 4: Implement `StripeSetup` and wire it**

Read `Stripe:SecretKey`, `Stripe:WebhookSecret`, and the existing `WebAppUrl`. Register `IPaymentProvider` as a singleton with the resolved values. Replace the Task 4 placeholder registration. Add empty `Stripe` keys and the `Funding` bounds to `appsettings.json` with no secret values.

- [ ] **Step 5: Run the full API suite and confirm nothing regressed**

Run: `dotnet test apps/api/api.slnx -c Release`
Expected: all tests pass, including the fake-provider tests, because the test factories replace `IPaymentProvider`.

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(api): add the Stripe payment adapter"
```

---

### Task 10: The schema migration and backfill

**Files:**
- Create: `apps/api/OSSFuel.Api/Migrations/<timestamp>_AddAiBalancesAndFunding.cs` (generated)
- Create: `apps/api/OSSFuel.Api/Migrations/<timestamp>_AddAiBalancesAndFunding.Designer.cs` (generated)
- Modify: `apps/api/OSSFuel.Api/Migrations/OSSFuelDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Produces: tables `AIBalances`, `BalanceTransactions`, `Fundings`, `PaymentEvents`, all indexes, and one zero balance per existing project.

- [ ] **Step 1: Generate the migration**

If the tool is missing, run `dotnet tool install --global dotnet-ef` first.

Run from the repo root: `dotnet ef migrations add AddAiBalancesAndFunding --project apps/api/OSSFuel.Api --startup-project apps/api/OSSFuel.Api`
Expected: three files appear. No database connection is needed.

- [ ] **Step 2: Inspect the generated file**

Check that `AIBalances` has no `RowVersion` column (it maps to the `xmin` system column), that the unique indexes exist, and that the partial index filter reads `"Status" = 'pending'`. If the model produced a `RowVersion` column, switch the property to Npgsql's `UseXminAsConcurrencyToken` and regenerate.

- [ ] **Step 3: Append the backfill**

At the end of `Up`, before the closing brace:

```csharp
migrationBuilder.Sql("""
    INSERT INTO "AIBalances" ("Id", "ProjectId", "AmountMinor", "Currency", "CreatedAt", "UpdatedAt")
    SELECT gen_random_uuid(), "Id", 0, 'usd', now(), now() FROM "Projects";
    """);
```

- [ ] **Step 4: Apply it to the dev database and confirm the data**

Run: `$env:Database__MigrateOnStartup="true"; dotnet run --project apps/api/OSSFuel.Api --configuration Release` against the compose database, or `dotnet ef database update --project apps/api/OSSFuel.Api`.
Then: `docker exec ossfuel-postgres psql -U ossfuel -d ossfuel -c "select count(*) from \"AIBalances\"; select count(*) from \"Projects\";"` and confirm the counts match.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test apps/api/api.slnx -c Release`
Expected: all pass. The InMemory tests ignore the migration.

- [ ] **Step 6: Commit**

```bash
git add apps/api/OSSFuel.Api/Migrations
git commit -m "feat(api): migrate balances, funding and payment events"
```

---

### Task 11: Prove the guarantees on real Postgres

**Files:**
- Create: `apps/api/OSSFuel.IntegrationTests/Infrastructure/PostgresDatabase.cs`
- Create: `apps/api/OSSFuel.IntegrationTests/Infrastructure/PostgresFundingWebApplicationFactory.cs`
- Create: `apps/api/OSSFuel.IntegrationTests/Features/Funding/FundingConcurrencyTests.cs`
- Create: `docker/init/01-test-database.sql`
- Modify: `docker-compose.yml`
- Modify: `.github/workflows/pr.yml`
- Modify: `apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj` (add `Microsoft.EntityFrameworkCore` explicit reference if the transitive one is not enough)

**Interfaces:**
- Produces: `PostgresDatabase.CreateAsync() -> Task<PostgresDatabase>` exposing `ConnectionString`; `PostgresFundingWebApplicationFactory` with the same seeding helpers as the InMemory factory.

- [ ] **Step 1: Add the init script and compose mount**

```sql
-- docker/init/01-test-database.sql
CREATE DATABASE ossfuel_tests;
```

```yaml
# docker-compose.yml, under services.postgres
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./docker/init:/docker-entrypoint-initdb.d
```

Note in the commit that an existing volume does not re-run init scripts; `docker compose down -v` is the reset.

- [ ] **Step 2: Write the failing concurrency test**

```csharp
[Trait("Category", "Postgres")]
public sealed class FundingConcurrencyTests : IAsyncLifetime
{
    [Fact]
    public async Task Two_identical_webhooks_at_once_credit_once()
    {
        var (projectId, fundingId) = SeedPendingFunding(2500);
        var notification = Notification.Paid(fundingId, 2500);

        var results = await Task.WhenAll(
            Task.Run(() => ApplyAsync(notification)),
            Task.Run(() => ApplyAsync(notification)));

        Assert.Contains(PaymentOutcome.Credited, results);
        Assert.Equal(2500, BalanceOf(projectId));
        Assert.Single(TransactionsOf(fundingId));
        Assert.Single(PaymentEventsFor(notification.EventId));
    }
```

`ApplyPaymentAsync` opens its own scope per call, so the two tasks really do race on separate `DbContext` instances. Use the existing `ReadDb` helper for `PaymentEventsFor`, or inline `ReadDb(db => db.PaymentEvents.Where(e => e.ProviderEventId == id).ToList())`.

    [Fact]
    public async Task The_unique_index_refuses_a_second_credit_row() { /* insert two BalanceTransactions with the same FundingId -> DbUpdateException */ }
}
```

- [ ] **Step 3: Run it and confirm it fails**

Run: `docker compose up -d`, then `dotnet test apps/api/OSSFuel.IntegrationTests/OSSFuel.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~FundingConcurrencyTests"`
Expected: fails because the fixture does not exist.

- [ ] **Step 4: Implement `PostgresDatabase`**

Read `Database:TestConnectionString`, defaulting to `Host=localhost;Port=5432;Database=ossfuel_tests;Username=ossfuel;Password=ossfuel_dev`. On first use, open a connection and run `CREATE SCHEMA IF NOT EXISTS "<unique>"`; store the connection string with `Search Path=<unique>`; dispose with `DROP SCHEMA "<unique>" CASCADE`. When the connection fails, throw with the message `Postgres is not reachable at <connection string>. Start it with 'docker compose up -d'.`

- [ ] **Step 5: Implement the Postgres factory**

Extract the shared half of `ProjectsWebApplicationFactory` into `public abstract class OssFuelWebApplicationFactory : WebApplicationFactory<Program>`. It owns the settings, the test auth scheme, `GitHub`, `Payments`, the seeding helpers, and `ReadDb`, and declares one hook:

```csharp
protected abstract void ConfigureDatabase(IServiceCollection services);
```

`ProjectsWebApplicationFactory` becomes `public class ProjectsWebApplicationFactory : OssFuelWebApplicationFactory` whose override keeps the InMemory registration. This name and constructor shape must not change, because the existing suites use it as `IClassFixture`.

`PostgresFundingWebApplicationFactory : OssFuelWebApplicationFactory` overrides `ConfigureDatabase` with `options.UseNpgsql(database.ConnectionString)` and, after `builder.Build()` in the test class's `InitializeAsync`, calls `db.Database.MigrateAsync()` once and drops the schema in `DisposeAsync`.

After the extraction, rerun the entire API suite before adding the concurrency tests.

- [ ] **Step 6: Run and confirm it passes**

Run the Step 3 command, then `dotnet test apps/api/api.slnx -c Release`.

- [ ] **Step 7: Add the CI service**

In `.github/workflows/pr.yml`, under the `api` job:

```yaml
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: ossfuel
          POSTGRES_PASSWORD: ossfuel_dev
          POSTGRES_DB: ossfuel_tests
        ports: ["5432:5432"]
        options: >-
          --health-cmd pg_isready --health-interval 5s --health-timeout 5s --health-retries 10
    env:
      Database__TestConnectionString: Host=localhost;Port=5432;Database=ossfuel_tests;Username=ossfuel;Password=ossfuel_dev
```

- [ ] **Step 8: Commit**

```bash
git add apps/api/OSSFuel.IntegrationTests docker docker-compose.yml .github/workflows/pr.yml
git commit -m "test(api): prove the credit guarantees on real postgres"
```

---

### Task 12: Fund a project and read its balance in the web app

**Files:**
- Create: `apps/web/src/lib/funding.ts`
- Modify: `apps/web/src/routes/_site/projects/$projectId.tsx`
- Modify: `apps/web/src/lib/styles.ts` (status chip helper only if reused three times)

**Interfaces:**
- Produces: `type Funding`, `parseAmountMinor(value: string): number | null`, `formatMinor(amountMinor: number, currency: string): string`, `createFunding(projectId: string, amountMinor: number): Promise<Funding>`, `fundingQueryOptions(id: string)`, `fundingsQueryOptions(page: number)`.

- [ ] **Step 1: Write the lib**

```ts
export function parseAmountMinor(value: string): number | null {
  const match = value.trim().match(/^(\d{1,5})(?:\.(\d{1,2}))?$/)
  if (!match) return null
  return Number(match[1]) * 100 + Number((match[2] ?? "").padEnd(2, "0"))
}

export function formatMinor(amountMinor: number, currency: string): string {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: currency.toUpperCase(),
  }).format(amountMinor / 100)
}
```

`/ 100` at the display edge only. Add the fetch functions with `apiFetch`.

- [ ] **Step 2: Add the balance panel**

For `canManage`, under the maintainer section: the amount from `GET /projects/{id}/balance` with `formatMinor`, then the transaction list from `GET /projects/{id}/balance/transactions` with `LiveTime` for the date and a signed amount. Empty state text: "No transactions yet." Loading uses `Loader`.

- [ ] **Step 3: Add the fund panel**

Signed-in: an amount input, a review line showing the project name and the parsed amount, and a button that calls `createFunding` and sets `window.location.assign(checkoutUrl)`. Invalid input disables the button and shows "Enter an amount between $1 and $10,000." A 409 shows the API message. Signed out: a "Sign in to fund" link to `/sign-in`.

- [ ] **Step 4: Verify by hand**

Run the dev servers, open a project, and check: `25.50` produces `checkout.test` redirect, `0.10` is accepted, `0.001` and `abc` are rejected, the review line matches, and the console has no errors. Screenshot desktop and mobile.

- [ ] **Step 5: Format, lint, typecheck, build**

Run: `pnpm exec prettier --write apps/web/src/lib/funding.ts "apps/web/src/routes/_site/projects/$projectId.tsx"; pnpm --filter web lint; pnpm --filter web typecheck; pnpm --filter web build`
Expected: all exit 0.

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(web): fund a project and show its balance"
```

---

### Task 13: Funding history and the return page

**Files:**
- Create: `apps/web/src/routes/_site/fundings/index.tsx`
- Create: `apps/web/src/routes/_site/fundings/$fundingId.tsx`
- Modify: `apps/web/src/components/site-header.tsx`
- Modify: `apps/web/src/routeTree.gen.ts` (generated)

**Interfaces:**
- Consumes: `fundingsQueryOptions`, `fundingQueryOptions`, `parseAmountMinor` is not needed here.

- [ ] **Step 1: Build the history page**

A table with project, amount, status, and created date, newest first, paginated with the same controls as the projects list. Status shown as text with the same border chip styling used by topics. Empty state: "You have not funded a project yet." with a link to `/projects`.

- [ ] **Step 2: Build the return page**

Poll `GET /fundings/{id}` every 2 seconds while `pending`, stop after 60 seconds or on unmount. At ten seconds call `POST /fundings/{id}/sync` once. `pending` shows "Payment submitted, waiting for confirmation". The other statuses show a plain sentence and a link back to the project. A 404 shows "Funding not found".

- [ ] **Step 3: Add the header link**

In `site-header.tsx`, add "Funding" to the signed-in navigation, next to the existing project links.

- [ ] **Step 4: Verify by hand**

With the fake provider or Stripe test mode: create a funding, land on the return page, watch it poll, run the webhook manually with the Stripe CLI, and confirm the page flips to succeeded and the balance and history both update. Screenshot the pending and succeeded states.

- [ ] **Step 5: Format, lint, typecheck, build**

Expected: all exit 0, and `routeTree.gen.ts` now contains the two new routes.

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(web): add funding history and the payment return page"
```

---

### Task 14: End-to-end verification against Stripe test mode

**Files:** none.

- [ ] **Step 1: Configure test keys**

Set `Stripe:SecretKey` and `Stripe:WebhookSecret` through user secrets, start `stripe listen --forward-to localhost:5050/webhooks/stripe`, and copy the `whsec_` value it prints.

- [ ] **Step 2: Run the flow**

Start the API and web. Sign in, open a project, fund $5.00, pay with `4242 4242 4242 4242`, and land on the return page. Confirm the page reaches `succeeded`, the maintainer sees $5.00 and one transaction, and `/fundings` lists it.

- [ ] **Step 3: Prove the failure paths**

Pay with `4000 0000 0000 0002` and confirm the funding lands `failed`, the balance stays put, and no transaction exists. Use `stripe trigger checkout.session.expired` and confirm a pending funding becomes `cancelled`.

- [ ] **Step 4: Prove idempotency**

In the Stripe CLI, re-send the last successful event with `stripe events resend <id>` and confirm the balance and transaction count do not change.

- [ ] **Step 5: Record the result**

Append the commands you ran and the outcomes to the PR description. Do not claim the flow works without this evidence.
