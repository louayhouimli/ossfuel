using Microsoft.AspNetCore.HttpOverrides;
using OSSFuel.Api.Common;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Auth;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Health;
using OSSFuel.Api.Features.Projects;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthFeature();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddAuthFeature(builder.Configuration);
builder.Services.AddGitHubFeature(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseCors(CorsSetup.Web);

app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync();

app.MapAuthEndpoints();
app.MapHealthEndpoints();
app.MapProjectEndpoints();

app.Run();

public partial class Program;