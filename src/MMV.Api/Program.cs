using MMV.Api.Health;
using MMV.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddLivenessCheck();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Contrat OpenAPI exposé en développement uniquement : /openapi/v1.json
    app.MapOpenApi();
}

app.MapLiveness();

app.Run();

public partial class Program;
