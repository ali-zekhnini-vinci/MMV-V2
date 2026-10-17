using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMV.Application.Appointments;
using MMV.Application.Identity;
using MMV.Domain.Identity;
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
//   permissions list : lecture seule (même session forcée que status) : comptes et permissions.
//   permissions grant|revoke <identifiant> <permission> (phase 03-A2, D-03-09) : attribution ou retrait d'un
//                      droit métier (clients.read | clients.write | clients.archive ; jamais system.admin), même
//                      cible stricte et mêmes trois contrôles que bootstrap-admin, confirmation interactive.
//                      Phase 03-B2 : + appointments.read | appointments.write | appointments.cancel (D-03-17).
//   responsibles list : lecture seule (même session forcée que status) : comptes, état actif, éligibilité à
//                      recevoir des rendez-vous (phase 03-B2, D-03-14).
//   responsibles enable|disable <identifiant> : éligibilité EXPLICITE d'un collaborateur à être responsable de
//                      rendez-vous, indépendante des permissions ; même cible stricte, mêmes contrôles et
//                      confirmation que permissions grant ; jamais de suppression (désactivation logique).
// Codes de sortie : 0 succès ; 1 usage ; 2 précondition non remplie ; 3 bootstrap déjà effectué ;
//                   4 saisie invalide ou annulation ; 5 cible non autorisée (refus sans modification) ;
//                   6 compte introuvable (aucune modification).
// Aucun mot de passe en argument, variable d'environnement ou flux redirigé ; rien n'est journalisé.

Console.OutputEncoding = Encoding.UTF8;

const string Usage =
    "Usage : MMV.Maintenance status | bootstrap-admin | permissions list | permissions grant|revoke <identifiant> <permission>"
    + " | responsibles list | responsibles enable|disable <identifiant>";
var command = args switch
{
    ["status"] => "status",
    ["bootstrap-admin"] => "bootstrap-admin",
    ["permissions", "list"] => "permissions list",
    ["permissions", "grant" or "revoke", _, _] => $"permissions {args[1]}",
    ["responsibles", "list"] => "responsibles list",
    ["responsibles", "enable" or "disable", _] => $"responsibles {args[1]}",
    _ => null,
};
if (command is null)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

var isWrite = command is "bootstrap-admin" or "permissions grant" or "permissions revoke" or "responsibles enable" or "responsibles disable";
var targetUserName = args.Length == 4 || command is "responsibles enable" or "responsibles disable" ? args[2] : null;
var targetPermission = args.Length == 4 ? args[3] : null;
if (targetPermission is not null && !PermissionAdministrationService.IsGrantable(targetPermission))
{
    // Refus avant toute lecture de configuration ou connexion.
    Console.Error.WriteLine(
        $"Refusé : permission non attribuable par cette commande. Admises : {string.Join(", ", Permissions.Grantable)}.");
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
if (isWrite && connectionViolations.Count > 0)
{
    return RefuseTarget(command, connectionViolations, "aucune connexion n'a été ouverte");
}

if (!isWrite)
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
Console.WriteLine($"Cible écriture : {(allViolations.Count == 0 ? "autorisée" : "NON AUTORISÉE")}");
foreach (var violation in allViolations)
{
    Console.WriteLine($"  - {violation}");
}

if (isWrite && serverViolations.Count > 0)
{
    return RefuseTarget(command, serverViolations, "aucune modification effectuée");
}

var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
Console.WriteLine($"Migrations     : {applied.Count} appliquée(s) [{string.Join(", ", applied)}] ; {pending.Count} en attente [{string.Join(", ", pending)}]");

if (pending.Count > 0)
{
    Console.WriteLine("Bootstrap      : non vérifiable (migration en attente)");
    if (isWrite)
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

if (command == "permissions list")
{
    var accounts = await scope.ServiceProvider.GetRequiredService<IIdentityStore>().ListUsersAsync(CancellationToken.None);
    foreach (var account in accounts)
    {
        var granted = account.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal).ToList();
        Console.WriteLine(
            $"  {account.UserName} ({(account.IsActive ? "actif" : "désactivé")}) : {(granted.Count == 0 ? "aucune permission" : string.Join(", ", granted))}");
    }

    return 0;
}

if (command is "permissions grant" or "permissions revoke")
{
    return await ChangePermissionAsync(command, targetUserName!, targetPermission!, target, scope.ServiceProvider, context);
}

if (command == "responsibles list")
{
    var listing = await scope.ServiceProvider.GetRequiredService<ResponsibleAdministrationService>().ListAsync(CancellationToken.None);
    foreach (var entry in listing)
    {
        var proposed = entry.IsActive && entry.IsEligible ? "proposé comme responsable" : "non proposé";
        Console.WriteLine(
            $"  {entry.UserName} ({(entry.IsActive ? "actif" : "désactivé")}) : {(entry.IsEligible ? "éligible" : "non éligible")} — {proposed}");
    }

    return 0;
}

if (command is "responsibles enable" or "responsibles disable")
{
    return await ChangeResponsibleAsync(command, targetUserName!, target, scope.ServiceProvider, context);
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
    return RefuseTarget(command, finalViolations, "aucune modification effectuée");
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

static async Task<int> ChangePermissionAsync(
    string command, string userName, string permission, ServerTarget target, IServiceProvider services, MmvDbContext context)
{
    var grant = command == "permissions grant";
    if (Console.IsInputRedirected)
    {
        Console.Error.WriteLine("Refusé : saisie interactive obligatoire (aucune entrée redirigée).");
        return 4;
    }

    Console.WriteLine();
    Console.WriteLine($"{(grant ? "Attribution" : "Retrait")} demandé : permission {permission}, compte « {userName} ».");
    Console.Write($"Confirmez la base cible en saisissant son nom ({target.Database}) : ");
    if (Console.ReadLine()?.Trim() != target.Database)
    {
        Console.Error.WriteLine("Annulé : confirmation incorrecte.");
        return 4;
    }

    // 3e contrôle, juste avant l'écriture : la cible n'a pas changé pendant la saisie interactive.
    var finalViolations = BootstrapTargetPolicy.CheckServer(await ReadTargetAsync(context));
    if (finalViolations.Count > 0)
    {
        return RefuseTarget(command, finalViolations, "aucune modification effectuée");
    }

    var administration = services.GetRequiredService<PermissionAdministrationService>();
    var result = grant
        ? await administration.GrantAsync(userName, permission, CancellationToken.None)
        : await administration.RevokeAsync(userName, permission, CancellationToken.None);
    switch (result.Status)
    {
        case PermissionChangeStatus.Granted or PermissionChangeStatus.Revoked or PermissionChangeStatus.Unchanged:
            var outcome = result.Status switch
            {
                PermissionChangeStatus.Granted => "attribuée",
                PermissionChangeStatus.Revoked => "retirée",
                _ => "inchangée (déjà dans l'état demandé)",
            };
            Console.WriteLine(
                $"Permission {permission} {outcome} (compte interne {result.UserId}). Permissions : {string.Join(", ", result.Permissions!)}.");
            return 0;
        case PermissionChangeStatus.UnknownAccount:
            Console.Error.WriteLine("Refusé : compte introuvable (aucune modification).");
            return 6;
        default:
            Console.Error.WriteLine("Refusé : permission non attribuable par cette commande.");
            return 1;
    }
}

static async Task<int> ChangeResponsibleAsync(
    string command, string userName, ServerTarget target, IServiceProvider services, MmvDbContext context)
{
    var enable = command == "responsibles enable";
    if (Console.IsInputRedirected)
    {
        Console.Error.WriteLine("Refusé : saisie interactive obligatoire (aucune entrée redirigée).");
        return 4;
    }

    Console.WriteLine();
    Console.WriteLine($"{(enable ? "Éligibilité" : "Retrait de l'éligibilité")} demandé : responsable de rendez-vous, compte « {userName} ».");
    Console.Write($"Confirmez la base cible en saisissant son nom ({target.Database}) : ");
    if (Console.ReadLine()?.Trim() != target.Database)
    {
        Console.Error.WriteLine("Annulé : confirmation incorrecte.");
        return 4;
    }

    // 3e contrôle, juste avant l'écriture : la cible n'a pas changé pendant la saisie interactive.
    var finalViolations = BootstrapTargetPolicy.CheckServer(await ReadTargetAsync(context));
    if (finalViolations.Count > 0)
    {
        return RefuseTarget(command, finalViolations, "aucune modification effectuée");
    }

    var result = await services.GetRequiredService<ResponsibleAdministrationService>()
        .SetEligibleAsync(userName, enable, CancellationToken.None);
    if (result.Status == ResponsibleChangeStatus.UnknownAccount)
    {
        Console.Error.WriteLine("Refusé : compte introuvable (aucune modification).");
        return 6;
    }

    var outcome = result.Status switch
    {
        ResponsibleChangeStatus.Enabled => "éligible",
        ResponsibleChangeStatus.Disabled => "non éligible",
        _ => "inchangé (déjà dans l'état demandé)",
    };
    Console.WriteLine($"Responsable de rendez-vous : {outcome} (compte interne {result.UserId}).");
    if (enable && !result.AccountActive)
    {
        Console.WriteLine("Attention : compte désactivé, il ne sera proposé comme responsable qu'après réactivation.");
    }

    return 0;
}

static int RefuseTarget(string command, IReadOnlyList<string> violations, string consequence)
{
    Console.Error.WriteLine(
        $"Refusé : cible non autorisée pour {command} ({consequence}). Seule cible d'écriture admise : "
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
