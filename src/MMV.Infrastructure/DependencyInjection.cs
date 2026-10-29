using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MMV.Application.Appointments;
using MMV.Application.Audit;
using MMV.Application.Clients;
using MMV.Application.Identity;
using MMV.Application.Optical;
using MMV.Infrastructure.Appointments;
using MMV.Infrastructure.Audit;
using MMV.Infrastructure.Clients;
using MMV.Infrastructure.Identity;
using MMV.Infrastructure.Optical;
using MMV.Infrastructure.Persistence;
using MMV.Domain.Optical;

namespace MMV.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Nom de la chaîne de connexion (variable d'environnement <c>ConnectionStrings__Mmv</c>).</summary>
    public const string ConnectionStringName = "Mmv";

    /// <summary>
    /// Enregistre la persistance PostgreSQL, les services d'identité et les modules Clients et Rendez-vous. La chaîne de connexion est lue
    /// à la première résolution du contexte, jamais au démarrage : l'API démarre sans base configurée.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MmvDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Chaîne de connexion 'ConnectionStrings:{ConnectionStringName}' absente. "
                    + "Elle doit être fournie hors Git (variable d'environnement ou user-secrets).");
            options.UseNpgsql(connectionString);
        });

        // Identité (phase 02). Les options peuvent être enregistrées avant cet appel (MMV.Api les lit
        // dans la section « Auth ») ; à défaut, valeurs par défaut de AuthOptions.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new AuthOptions());
        services.TryAddSingleton<IPasswordHashing, IdentityPasswordHashing>();
        services.TryAddScoped<IIdentityStore, IdentityStore>();
        services.TryAddScoped<AuthService>();
        services.TryAddScoped<BootstrapService>();
        services.TryAddScoped<PermissionAdministrationService>();

        // Journal d'audit (phase 03-D2) : même contexte EF que les modules métier (même transaction). Contexte de
        // requête : MMV.Api l'enregistre avant cet appel (session, corrélation) ; à défaut, aucun identifiant technique.
        services.TryAddScoped<IAuditContext, NoAuditContext>();
        services.TryAddScoped<IAuditRecorder, AuditRecorder>();
        services.TryAddScoped<IAuditStore, AuditStore>();
        services.TryAddScoped<AuditService>();

        // Clients (phase 03-A2).
        services.TryAddScoped<IClientStore, ClientStore>();
        services.TryAddScoped<ClientService>();

        // Rendez-vous (phase 03-B2). Paramètres du magasin : MMV.Api les lit dans la section « Appointments » et
        // les enregistre avant cet appel ; à défaut, service non configuré (aucune valeur inventée).
        services.TryAddSingleton(AppointmentSettings.NotConfigured("Paramètres Appointments non fournis à MMV.Infrastructure."));
        services.TryAddScoped<IAppointmentStore, AppointmentStore>();
        services.TryAddScoped<IAppointmentResponsibleStore, AppointmentResponsibleStore>();
        services.TryAddScoped<AppointmentService>();
        services.TryAddScoped<ResponsibleAdministrationService>();

        // Dossier optique (phase 04-B) : deux types distincts, même contexte EF (même transaction que l'audit).
        services.TryAddScoped<IOpticalRecordStore<Prescription>, OpticalRecordStore<Prescription, PrescriptionRevision, PrescriptionContent>>();
        services.TryAddScoped<IOpticalRecordStore<Measurement>, OpticalRecordStore<Measurement, MeasurementRevision, MeasurementContent>>();
        services.TryAddScoped<IOpticalReadStore, OpticalReadStore>();
        services.TryAddScoped<IOpticalDirectory, OpticalDirectory>();
        services.TryAddScoped<PrescriptionService>();
        services.TryAddScoped<MeasurementService>();
        services.TryAddScoped<OpticalQueryService>();

        return services;
    }
}
