using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;

namespace OSSFuel.Api.Features.Users;

internal static class CurrentUser
{
    public static async Task<User?> LoadAsync(
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            return null;
        }

        return await db.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
