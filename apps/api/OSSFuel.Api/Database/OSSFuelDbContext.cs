using Microsoft.EntityFrameworkCore;

namespace OSSFuel.Api.Database;

public class OSSFuelDbContext(DbContextOptions<OSSFuelDbContext> options) : DbContext(options);
