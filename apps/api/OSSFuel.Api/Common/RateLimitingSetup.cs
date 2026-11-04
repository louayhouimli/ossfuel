using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace OSSFuel.Api.Common;

public static class RateLimitingSetup
{
    public const string FundingCreatePolicy = "funding-create";
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Rate limit exceeded. Please try again later.",
                        Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}",
                    }
                });
            };

            options.AddPolicy(FundingCreatePolicy, httpContext =>
            {
                var key = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "authenticated"
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(10),
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy(AuthPolicy, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    ip,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromSeconds(10),
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }
}
