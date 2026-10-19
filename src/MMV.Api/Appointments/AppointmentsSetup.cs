using MMV.Application.Appointments;

namespace MMV.Api.Appointments;

/// <summary>
/// Paramètres du magasin pour les rendez-vous (section <c>Appointments</c>, D-03-13), lus et validés une fois au
/// démarrage. Incomplets ou invalides : l'API démarre quand même (les autres modules restent disponibles), les
/// routes /api/appointments répondent 503 <c>appointments_not_configured</c> et le motif est journalisé (sans
/// donnée personnelle). Aucune valeur par défaut n'est inventée.
/// </summary>
internal static partial class AppointmentsSetup
{
    public static IServiceCollection AddAppointmentSettings(this IServiceCollection services, IConfiguration configuration)
    {
        AppointmentSettings settings;
        try
        {
            settings = AppointmentSettings.From(configuration.GetSection(AppointmentsOptions.SectionName).Get<AppointmentsOptions>());
        }
        catch (InvalidOperationException)
        {
            // Valeur non convertible (ex. capacité non numérique) : jamais d'interprétation approximative.
            settings = AppointmentSettings.NotConfigured("Section Appointments illisible (valeur non convertible).");
        }

        services.AddSingleton(settings);
        return services;
    }

    public static void LogAppointmentSettings(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<AppointmentSettings>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MMV.Api.Appointments");
        if (settings.IsConfigured)
        {
            LogConfigured(logger, settings.TimeZoneId, settings.ReceptionCapacity);
        }
        else
        {
            LogNotConfigured(logger, string.Join(" ", settings.Problems));
        }
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Information, Message = "Rendez-vous : magasin configuré (fuseau {TimeZone}, capacité d'accueil {Capacity} ; vide = aucune réservation sans responsable).")]
    private static partial void LogConfigured(ILogger logger, string timeZone, int? capacity);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Warning, Message = "Rendez-vous : service non configuré — {Problems}")]
    private static partial void LogNotConfigured(ILogger logger, string problems);
}
