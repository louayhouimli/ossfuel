using Microsoft.AspNetCore.HttpOverrides;
using OSSFuel.Api.Common;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Auth;
using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Funding;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Health;
using OSSFuel.Api.Features.Projects;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthFeature();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddAppRateLimiting();
builder.Services.AddAuthFeature(builder.Configuration);
builder.Services.AddGitHubFeature(builder.Configuration);
builder.Services.AddFundingFeature(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseCors(CorsSetup.Web);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseCsrfProtection();

await app.MigrateDatabaseAsync();

app.MapAuthEndpoints();
app.MapHealthEndpoints();
app.MapProjectEndpoints();
app.MapBalanceEndpoints();
app.MapFundingEndpoints(FundingSetup.UsesMockProvider(builder.Configuration, builder.Environment));

app.Run();

public partial class Program;