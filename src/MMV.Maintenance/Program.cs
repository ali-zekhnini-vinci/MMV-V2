using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Identity;
using MMV.Infrastructure;
using MMV.Infrastructure.Persistence;

// MMV.Maintenance — commandes LOCALES, exécutées par le propriétaire sur le poste serveur.
//   status           : lecture seule (base cible, migrations, bootstrap effectué ?, nombre de comptes)
//   bootstrap-admin  : création UNIQUE du premier administrateur (saisie interactive masquée)
// Codes de sortie : 0 succès ; 1 usage ; 2 précondition non remplie ; 3 bootstrap déjà effectué ;
//                   4 saisie invalide ou annulation.
// Aucun mot de passe en argument, variable d'environnement ou flux redirigé ; rien n'est journalisé.

Console.OutputEncoding = Encoding.UTF8;

var command = args.Length == 1 ? args[0] : null;
if (command is not ("status" or "bootstrap-admin"))
{
    Console.Error.WriteLine("Usage : MMV.Maintenance status | bootstrap-admin");
    return 1;
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(MaintenanceMarker).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(DependencyInjection.ConnectionStringName)))
{
    Console.Error.WriteLine("Précondition : 'ConnectionStrings:Mmv' absente (user-secrets mmv-v2-api ou variable ConnectionStrings__Mmv).");
    return 2;
}

var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
await using var scope = services.CreateAsyncScope();
var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();

TargetInfo target;
try
{
    target = await TargetInfo.ReadAsync(context);
}
catch (Exception exception) when (exception is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
{
    Console.Error.WriteLine($"Précondition : base injoignable ({exception.GetType().Name}).");
    return 2;
}

var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
Console.WriteLine($"Base cible     : {target.Database} (port {target.Port}, PostgreSQL {target.ServerVersion})");
Console.WriteLine($"Migrations     : {applied.Count} appliquée(s) [{string.Join(", ", applied)}] ; {pending.Count} en attente [{string.Join(", ", pending)}]");

if (pending.Count > 0)
{
    Console.WriteLine("Bootstrap      : non vérifiable (migration en attente)");
    if (command == "bootstrap-admin")
    {
        Console.Error.WriteLine("Précondition : migrations en attente. Appliquer d'abord le script SQL approuvé (aucune migration automatique).");
        return 2;
    }

    return 0;
}

var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapService>();
var completed = await bootstrap.IsCompletedAsync(CancellationToken.None);
Console.WriteLine($"Bootstrap      : {(completed ? "déjà effectué" : "non effectué")}");
Console.WriteLine($"Comptes        : {await context.Users.CountAsync()}");

if (command == "status")
{
    return 0;
}

if (completed)
{
    Console.Error.WriteLine("Refusé : le bootstrap administrateur a déjà été effectué sur cette base (une seule fois).");
    return 3;
}

if (Console.IsInputRedirected)
{
    Console.Error.WriteLine("Refusé : saisie interactive obligatoire (aucune entrée redirigée).");
    return 4;
}

Console.WriteLine();
Console.Write($"Confirmez la base cible en saisissant son nom ({target.Database}) : ");
if (Console.ReadLine()?.Trim() != target.Database)
{
    Console.Error.WriteLine("Annulé : confirmation incorrecte.");
    return 4;
}

var userName = Prompt("Identifiant de l'administrateur : ");
var displayName = Prompt("Nom affiché : ");
var password = PromptSecret($"Mot de passe ({BootstrapService.PasswordMinLength} caractères minimum) : ");
if (password != PromptSecret("Confirmez le mot de passe : "))
{
    Console.Error.WriteLine("Annulé : les deux mots de passe diffèrent.");
    return 4;
}

var result = await bootstrap.CreateInitialAdministratorAsync(userName, displayName, password, CancellationToken.None);
switch (result.Status)
{
    case BootstrapStatus.Created:
        Console.WriteLine($"Administrateur créé (identifiant interne {result.AdministratorId}). Permission : system.admin.");
        return 0;
    case BootstrapStatus.AlreadyCompleted:
        Console.Error.WriteLine("Refusé : le bootstrap a été effectué entre-temps.");
        return 3;
    default:
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"Saisie invalide : {error}");
        }

        return 4;
}

static string Prompt(string label)
{
    Console.Write(label);
    return Console.ReadLine() ?? string.Empty;
}

static string PromptSecret(string label)
{
    Console.Write(label);
    var buffer = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return buffer.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
            {
                buffer.Length--;
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            buffer.Append(key.KeyChar);
        }
    }
}

internal sealed record TargetInfo(string Database, int Port, string ServerVersion)
{
    public static async Task<TargetInfo> ReadAsync(MmvDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_database(), inet_server_port(), current_setting('server_version')";
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return new TargetInfo(
                reader.GetString(0),
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                reader.GetString(2));
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}

internal sealed class MaintenanceMarker;
