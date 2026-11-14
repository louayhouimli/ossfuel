using Microsoft.AspNetCore.Mvc;

namespace OSSFuel.Api.Common;

public sealed class CsrfProtectionMiddleware(RequestDelegate next, IProblemDetailsService problemDetails)
{
    public const string HeaderName = "X-OSSFuel-Client";

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsUnsafeMethod(context.Request.Method)
            && IsCookieAuthenticated(context)
            && !IsExemptPath(context.Request.Path))
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "CSRF protection check failed.",
                        Detail = $"State-mutating requests require the {HeaderName} header.",
                        Instance = $"{context.Request.Method} {context.Request.Path}",
                    }
                });

                return;
            }
        }

        await next(context);
    }

    private static bool IsUnsafeMethod(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsDelete(method)
        || HttpMethods.IsPatch(method);

    private static bool IsCookieAuthenticated(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true;

    private static bool IsExemptPath(PathString path) =>
        path.StartsWithSegments("/fundings/webhook")
        || path.StartsWithSegments("/v1")
        || path.StartsWithSegments("/api/v1");
}

public static class CsrfProtectionExtensions
{
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app) =>
        app.UseMiddleware<CsrfProtectionMiddleware>();
}
