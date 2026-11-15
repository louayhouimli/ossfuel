using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Auth;

public class MobileAuthCode
{
    public Guid Id { get; set; }

    public required string CodeHash { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public User? User { get; set; }
}
