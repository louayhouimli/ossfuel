using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Database;

public class OSSFuelDbContext(DbContextOptions<OSSFuelDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OSSFuelDbContext).Assembly);
    }
}
