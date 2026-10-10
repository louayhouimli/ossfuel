using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Auth;

public record AuthResponse(Guid Id, string Login, string? Name, string? Email, string? AvatarUrl)
{
    public static AuthResponse From(User user) => new(
        user.Id,
        user.Login,
        user.Name,
        user.Email,
        user.AvatarUrl);
}