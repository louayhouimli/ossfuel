using OSSFuel.Api.Common;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Auth;
using OSSFuel.Api.Features.Health;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthFeature();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddAuthFeature(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors(CorsSetup.Web);

app.UseAuthentication();
app.UseAuthorization();

await app.MigrateDatabaseAsync();

app.MapAuthEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;