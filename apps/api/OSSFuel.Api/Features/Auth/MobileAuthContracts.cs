namespace OSSFuel.Api.Features.Auth;

public sealed record MobileExchangeRequest(string? Code);

public sealed record MobileAuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthResponse User);
