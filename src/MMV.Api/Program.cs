using MMV.Api.Auth;
using MMV.Api.Cors;
using MMV.Api.Health;
using MMV.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddLivenessCheck();
builder.Services.AddReadinessCheck();
builder.Services.AddDesktopCors(builder.Configuration);
builder.Services.AddMmvAuth(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Contrat OpenAPI exposé en développement uniquement : /openapi/v1.json
    app.MapOpenApi();
}

app.MapLiveness().RequireCors(DesktopCors.PolicyName);
app.MapReadiness().RequireCors(DesktopCors.PolicyName);
app.MapAuthEndpoints();

app.Run();

public partial class Program;
