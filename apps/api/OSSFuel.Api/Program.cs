using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using AspNet.Security.OAuth.GitHub;

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


var allowedOrigin = builder.Configuration["AllowedOrigin"]
    ?? throw new InvalidOperationException("AllowedOrigin is not configured");

builder.Services.AddCors(options => options.AddPolicy(MyAllowSpecificOrigins, policy => policy.WithOrigins(allowedOrigin)));

builder.Services.AddHealthChecks();

builder.Services.AddDbContext<OSSFuelDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGitHub(options =>
    {
        options.ClientId =
            builder.Configuration["Authentication:GitHub:ClientId"]!;

        options.ClientSecret =
            builder.Configuration["Authentication:GitHub:ClientSecret"]!;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", () =>
    Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = "/me"
        },
        new[] { GitHubAuthenticationDefaults.AuthenticationScheme }));

app.MapGet("/me", (System.Security.Claims.ClaimsPrincipal user) =>
    new
    {
        IsAuthenticated = user.Identity?.IsAuthenticated,
        Name = user.Identity?.Name
    })
    .RequireAuthorization();

app.MapGet("/hello", () => "Hello World!");

app.MapGet("/health/db", async (OSSFuelDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();

        return Results.Ok("Database connected");
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed",
            detail: ex.ToString());
    }
});



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.UseCors(MyAllowSpecificOrigins);

app.Run();
