using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Identity;
using MMV.Infrastructure;
using MMV.Infrastructure.Persistence;
using MMV.Maintenance;
using Npgsql;

// MMV.Maintenance — commandes LOCALES, exécutées par le propriétaire sur le poste serveur.
//   status           : lecture seule, session forcée default_transaction_read_only=on (base cible,
//                      conformité de la cible, migrations, bootstrap effectué ?, nombre de comptes)
//   bootstrap-admin  : création UNIQUE du premier administrateur (saisie interactive masquée), uniquement
//                      sur la cible autorisée (BootstrapTargetPolicy : mmv_v2_dev / mmv_v2_dev / 5433 /
//                      PostgreSQL 18 / boucle locale), contrôlée avant connexion, après connexion et
//                      juste avant l'écriture.
// Codes de sortie : 0 succès ; 1 usage ; 2 précondition non remplie ; 3 bootstrap déjà effectué ;
//                   4 saisie invalide ou annulation ; 5 cible non autorisée (refus sans modification).
// Aucun mot de passe en argument, variable d'environnement ou flux redirigé ; rien n'est journalisé.

Console.OutputEncoding = Encoding.UTF8;

var command = args.Length == 1 ? args[0] : null;
if (command is not ("status" or "bootstrap-admin"))
{
    Console.Error.WriteLine("Usage : MMV.Maintenance status | bootstrap-admin");
    return 1;
}

IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(MaintenanceMarker).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Précondition : 'ConnectionStrings:Mmv' absente (user-secrets mmv-v2-api ou variable ConnectionStrings__Mmv).");
    return 2;
}

// 1er contrôle, AVANT toute connexion : la chaîne configurée doit viser exactement la cible autorisée.
var connectionViolations = BootstrapTargetPolicy.CheckConnectionString(connectionString);
if (command == "bootstrap-admin" && connectionViolations.Count > 0)
{
    return RefuseTarget(connectionViolations, "aucune connexion n'a été ouverte");
}

if (command == "status")
{
    if (!TryForceReadOnlySession(connectionString, out var readOnlyConnectionString))
    {
        Console.Error.WriteLine("Précondition : 'ConnectionStrings:Mmv' illisible.");
        return 2;
    }

    configuration = new ConfigurationBuilder()
        .AddConfiguration(configuration)
        .AddInMemoryCollection([new($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", readOnlyConnectionString)])
        .Build();
}

var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
await using var scope = services.CreateAsyncScope();
var context = scope.ServiceProvider.GetRequiredService<MmvDbContext>();

ServerTarget target;
try
{
    target = await ReadTargetAsync(context);
}
catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or TimeoutException)
{
    Console.Error.WriteLine($"Précondition : base injoignable ({exception.GetType().Name}).");
    return 2;
}

Console.WriteLine($"Base cible     : {target.Database} (rôle {target.SessionUser}, port {target.Port}, PostgreSQL {target.ServerVersion})");

// 2e contrôle, après connexion : identité relue côté serveur, avant toute lecture applicative ou écriture.
var serverViolations = BootstrapTargetPolicy.CheckServer(target);
var allViolations = connectionViolations.Concat(serverViolations).ToList();
Console.WriteLine($"Cible bootstrap: {(allViolations.Count == 0 ? "autorisée" : "NON AUTORISÉE")}");
foreach (var violation in allViolations)
{
    Console.WriteLine($"  - {violation}");
}

if (command == "bootstrap-admin" && serverViolations.Count > 0)
{
    return RefuseTarget(serverViolations, "aucune modification effectuée");
}

var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
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

// 3e contrôle, juste avant l'écriture : la cible n'a pas changé pendant la saisie interactive.
var finalViolations = BootstrapTargetPolicy.CheckServer(await ReadTargetAsync(context));
if (finalViolations.Count > 0)
{
    return RefuseTarget(finalViolations, "aucune modification effectuée");
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

static int RefuseTarget(IReadOnlyList<string> violations, string consequence)
{
    Console.Error.WriteLine(
        $"Refusé : cible non autorisée pour bootstrap-admin ({consequence}). Seule cible admise en phase 02 : "
        + $"base {BootstrapTargetPolicy.Database}, rôle {BootstrapTargetPolicy.Role}, port {BootstrapTargetPolicy.Port}, "
        + $"PostgreSQL {BootstrapTargetPolicy.ServerMajorVersion}, connexion locale.");
    foreach (var violation in violations)
    {
        Console.Error.WriteLine($"  - {violation}");
    }

    return 5;
}

static bool TryForceReadOnlySession(string connectionString, out string readOnlyConnectionString)
{
    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Options = $"{builder.Options} -c default_transaction_read_only=on".Trim();
        readOnlyConnectionString = builder.ConnectionString;
        return true;
    }
    catch (ArgumentException)
    {
        readOnlyConnectionString = string.Empty;
        return false;
    }
}

static async Task<ServerTarget> ReadTargetAsync(MmvDbContext context)
{
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        return await BootstrapTargetPolicy.ReadServerAsync(connection, CancellationToken.None);
    }
    finally
    {
        await connection.CloseAsync();
    }
}

internal sealed class MaintenanceMarker;
