using System.Text.Json.Serialization;
using MMV.Api.Appointments;
using MMV.Api.Audit;
using MMV.Api.Auth;
using MMV.Api.Cors;
using MMV.Api.Health;
using MMV.Api.Installation;
using MMV.Api.Realtime;
using MMV.Application.Audit;
using MMV.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// Nombres JSON stricts (jamais lus depuis une chaîne) : le contrat OpenAPI décrit des entiers, pas « integer | string ».
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddLivenessCheck();
builder.Services.AddReadinessCheck();
builder.Services.AddDesktopCors(builder.Configuration);
builder.Services.AddMmvAuth(builder.Configuration);
builder.Services.AddAppointmentSettings(builder.Configuration);
// Un serveur local = un magasin (COR-05-01) : Installation:StoreCode, résolu à la première requête magasin.
builder.Services.AddInstallation(builder.Configuration);
builder.Services.AddImportAssistant();
// CSV-IMPORT-AI-01 : interpréteur de catalogue (qwen3.5:4b), file d'analyse, assistant en cinq étapes (désactivé par défaut).
builder.Services.AddCatalogInterpreter();
// Temps réel (COR-05-02) : hub SignalR /hubs/changes, notifications après écriture confirmée.
builder.Services.AddRealtime();
// Journal d'audit (03-D2) : session et corrélation de la requête, enregistrés avant MMV.Infrastructure.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContext, HttpAuditContext>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.LogAppointmentSettings();

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
app.MapRealtime();

app.Run();

public partial class Program;
