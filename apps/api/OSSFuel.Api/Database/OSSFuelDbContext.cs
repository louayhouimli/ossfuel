using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Features.ApiKeys;
using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Funding;
using OSSFuel.Api.Features.Gateway;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Database;

public class OSSFuelDbContext(DbContextOptions<OSSFuelDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<AIBalance> AIBalances => Set<AIBalance>();

    public DbSet<BalanceTransaction> BalanceTransactions => Set<BalanceTransaction>();

    public DbSet<Funding> Fundings => Set<Funding>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    public DbSet<ProjectApiKey> ProjectApiKeys => Set<ProjectApiKey>();

    public DbSet<AIUsage> AIUsages => Set<AIUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OSSFuelDbContext).Assembly);
    }
}
