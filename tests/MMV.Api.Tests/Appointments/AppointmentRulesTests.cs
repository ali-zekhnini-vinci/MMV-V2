using MMV.Application.Appointments;
using MMV.Domain.Appointments;

namespace MMV.Api.Tests.Appointments;

/// <summary>
/// Règles pures D-03-13 à D-03-18 : instants UTC, pas de cinq minutes, minuit et changements d'heure dans le fuseau
/// du magasin (Maroc, Belgique), paramètres du magasin, motifs, occupation de l'accueil. Les fuseaux sont ceux du
/// système (Windows : registre ; ICU côté interface) : les dates de transition marocaines sont aussi recherchées
/// dynamiquement, pour ne pas dépendre d'une mise à jour des données de fuseaux.
/// </summary>
public sealed class AppointmentRulesTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
    private static readonly TimeZoneInfo Casablanca = TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca");

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-07-16T08:30:00Z")]
    [InlineData("2026-07-16T08:30:00+00:00")]
    [InlineData("2026-07-16T08:30:00.000Z")]
    public void Instant_AcceptsExplicitUtcOnly(string value)
    {
        Assert.True(AppointmentSchedule.TryParseUtcInstant(value, out var instant));
        Assert.Equal(Utc(2026, 7, 16, 8, 30), instant);
    }

    [Theory]
    [InlineData("2026-07-16T10:30:00+02:00")]
    [InlineData("2026-07-16T08:30:00")]
    [InlineData("2026-07-16 08:30:00Z")]
    [InlineData("16/07/2026 08:30")]
    [InlineData("2026-07-16T08:30Z")]
    [InlineData("")]
    public void Instant_RefusesOffsetsAndLocalTimes(string value) =>
        Assert.False(AppointmentSchedule.TryParseUtcInstant(value, out _));

    [Theory]
    [InlineData(8, 0, null)]
    [InlineData(8, 5, null)]
    [InlineData(8, 55, null)]
    [InlineData(8, 7, AppointmentSchedule.InvalidStep)]
    [InlineData(8, 31, AppointmentSchedule.InvalidStep)]
    public void Slot_StartsOnAFiveMinuteStep(int hour, int minute, string? expected) =>
        Assert.Equal(expected, AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 7, 16, hour, minute), Utc(2026, 7, 16, hour, minute).AddMinutes(30)));

    [Fact]
    public void Slot_WithSecondsIsRefused() =>
        Assert.Equal(AppointmentSchedule.InvalidStep, AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 7, 16, 8, 0).AddSeconds(30), Utc(2026, 7, 16, 8, 30)));

    [Fact]
    public void Slot_NeverCrossesLocalMidnight()
    {
        // Bruxelles, été (UTC+2) : 23:30 locale = 21:30 Z.
        var lateStart = Utc(2026, 7, 16, 21, 30);
        Assert.Null(AppointmentSchedule.CheckSlot(Brussels, lateStart, lateStart.AddMinutes(30))); // 23:30–00:00 : finit à minuit
        Assert.Equal(AppointmentSchedule.OutOfRange, AppointmentSchedule.CheckSlot(Brussels, lateStart, lateStart.AddMinutes(45)));
        // 00:00 locale (22:00 Z la veille) : même jour civil local, admis.
        Assert.Null(AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 7, 16, 22, 0), Utc(2026, 7, 16, 22, 30)));
    }

    [Fact]
    public void Brussels_SpringForward_SlotOverTheMissingHourIsRefused()
    {
        // 29/03/2026 : 02:00 CET → 03:00 CEST à 01:00 Z. 01:30–02:30 n'existe pas en heure murale.
        Assert.Equal(AppointmentSchedule.TimeChange, AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 3, 29, 0, 30), Utc(2026, 3, 29, 1, 30)));
        // 03:00 CEST (01:00 Z) et après : heure normale.
        Assert.Null(AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 3, 29, 1, 0), Utc(2026, 3, 29, 1, 30)));
        // 01:00–01:30 CET : avant le changement.
        Assert.Null(AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 3, 29, 0, 0), Utc(2026, 3, 29, 0, 30)));
    }

    [Fact]
    public void Brussels_FallBack_AmbiguousLocalHourIsRefused()
    {
        // 25/10/2026 : 03:00 CEST → 02:00 CET à 01:00 Z ; 02:00–02:59 locale existe deux fois.
        Assert.Equal(AppointmentSchedule.TimeChange, AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 10, 25, 0, 15), Utc(2026, 10, 25, 0, 45))); // 1re occurrence
        Assert.Equal(AppointmentSchedule.TimeChange, AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 10, 25, 1, 15), Utc(2026, 10, 25, 1, 45))); // 2e occurrence
        Assert.Null(AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 10, 25, 2, 0), Utc(2026, 10, 25, 2, 30))); // 03:00 CET
        Assert.Null(AppointmentSchedule.CheckSlot(Brussels, Utc(2026, 10, 24, 23, 0), Utc(2026, 10, 24, 23, 45))); // 01:00 CEST
    }

    [Fact]
    public void Casablanca_ForwardChange_SlotOverTheMissingHourIsRefused()
    {
        // 22/03/2026 (données de fuseaux Windows et ICU concordantes) : 02:00 (UTC+0) → 03:00 (UTC+1) à 02:00 Z.
        Assert.Equal(TimeSpan.Zero, Casablanca.GetUtcOffset(Utc(2026, 3, 22, 1, 59)));
        Assert.Equal(TimeSpan.FromHours(1), Casablanca.GetUtcOffset(Utc(2026, 3, 22, 2, 0)));
        Assert.Equal(AppointmentSchedule.TimeChange, AppointmentSchedule.CheckSlot(Casablanca, Utc(2026, 3, 22, 1, 30), Utc(2026, 3, 22, 2, 30)));
        Assert.Null(AppointmentSchedule.CheckSlot(Casablanca, Utc(2026, 3, 22, 2, 0), Utc(2026, 3, 22, 2, 30)));
    }

    [Fact]
    public void Casablanca_EveryTransitionFoundInTheSystemDataIsGuarded()
    {
        // Indépendant d'une date figée : chaque changement d'heure trouvé du 01/01/2026 au 31/12/2027 est refusé
        // lorsqu'un créneau de 30 minutes l'enjambe ou commence dans une heure ambiguë.
        var transitions = 0;
        for (var t = Utc(2026, 1, 1, 0, 0); t < Utc(2028, 1, 1, 0, 0); t = t.AddMinutes(30))
        {
            if (Casablanca.GetUtcOffset(t) == Casablanca.GetUtcOffset(t.AddMinutes(30)))
            {
                continue;
            }

            transitions++;
            var change = t.AddMinutes(30);
            Assert.Equal(AppointmentSchedule.TimeChange, AppointmentSchedule.CheckSlot(Casablanca, change.AddMinutes(-15), change.AddMinutes(15)));
            if (Casablanca.GetUtcOffset(change) < Casablanca.GetUtcOffset(t))
            {
                // Retour en arrière : l'heure qui suit le changement est ambiguë.
                Assert.Equal(AppointmentSchedule.TimeChange, AppointmentSchedule.CheckSlot(Casablanca, change.AddMinutes(15), change.AddMinutes(45)));
            }
        }

        Assert.True(transitions >= 1, "Aucune transition marocaine dans les données de fuseaux du système (2026–2027).");
    }

    [Fact]
    public void Settings_RequireAnExplicitSupportedTimeZoneAndDisplayRange()
    {
        Assert.False(AppointmentSettings.From(null).IsConfigured);
        Assert.False(AppointmentSettings.From(new AppointmentsOptions { DisplayStartTime = "09:00", DisplayEndTime = "19:00" }).IsConfigured);
        Assert.False(AppointmentSettings.From(new AppointmentsOptions { TimeZone = "Europe/Paris", DisplayStartTime = "09:00", DisplayEndTime = "19:00" }).IsConfigured);
        Assert.False(AppointmentSettings.From(new AppointmentsOptions { TimeZone = "Europe/Brussels" }).IsConfigured);
        Assert.False(AppointmentSettings.From(new AppointmentsOptions { TimeZone = "Europe/Brussels", DisplayStartTime = "19:00", DisplayEndTime = "09:00" }).IsConfigured);

        var casablanca = AppointmentSettings.From(new AppointmentsOptions { TimeZone = "Africa/Casablanca", DisplayStartTime = "08:30", DisplayEndTime = "24:00" });
        Assert.True(casablanca.IsConfigured, string.Join(" ", casablanca.Problems));
        Assert.Equal("Africa/Casablanca", casablanca.TimeZoneId);
        // Aucun jour fermé ni capacité implicites (D-03-13, D-03-15).
        Assert.Empty(casablanca.HiddenWeekdays);
        Assert.Null(casablanca.ReceptionCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Settings_RefuseANonPositiveReceptionCapacity(int capacity) =>
        Assert.False(AppointmentSettings.From(new AppointmentsOptions
        {
            TimeZone = "Europe/Brussels", DisplayStartTime = "09:00", DisplayEndTime = "19:00", ReceptionCapacity = capacity,
        }).IsConfigured);

    [Fact]
    public void Settings_RefuseInvalidHiddenWeekdays()
    {
        static bool Ok(int[] days) => AppointmentSettings.From(new AppointmentsOptions
        {
            TimeZone = "Europe/Brussels", DisplayStartTime = "09:00", DisplayEndTime = "19:00", HiddenWeekdays = days,
        }).IsConfigured;

        Assert.True(Ok([0]));
        Assert.False(Ok([7]));
        Assert.False(Ok([1, 1]));
        Assert.False(Ok([0, 1, 2, 3, 4, 5, 6]));
    }

    [Fact]
    public void Reasons_AreTheApprovedClosedListWithTheirMode()
    {
        Assert.Equal(
            ["conseil_devis", "choix_equipement", "prise_mesures", "essayage_ajustement", "retrait_commande", "sav_reparation", "autre"],
            AppointmentReasons.All.Select(r => r.Code));
        Assert.Equal(
            ["Conseil / Devis", "Choix de monture et verres", "Prise de mesures", "Essayage / Ajustement", "Retrait de commande", "SAV / Réparation", "Autre motif"],
            AppointmentReasons.All.Select(r => r.Label));
        Assert.Equal(["retrait_commande", "sav_reparation"], AppointmentReasons.All.Where(r => !r.ResponsibleRequired).Select(r => r.Code));
        Assert.DoesNotContain(AppointmentReasons.All, r => r.Code.Contains("accueil", StringComparison.Ordinal));
        Assert.Equal([15, 30, 45, 60, 90, 120], AppointmentRules.DurationsMinutes);
    }

    [Fact]
    public void Occupancy_CountsSimultaneityNotOverlapWithTheSlot()
    {
        var ten = Utc(2026, 7, 16, 8, 0);
        // 10:00–10:30 et 10:30–11:00 recoupent tous deux 10:00–11:00 sans jamais être simultanés.
        Assert.Equal(1, ReceptionOccupancy.MaxConcurrent([(ten, ten.AddMinutes(30)), (ten.AddMinutes(30), ten.AddMinutes(60))], ten, ten.AddMinutes(60)));
        // 10:00–10:45 et 10:30–11:00 : simultanés de 10:30 à 10:45.
        Assert.Equal(2, ReceptionOccupancy.MaxConcurrent([(ten, ten.AddMinutes(45)), (ten.AddMinutes(30), ten.AddMinutes(60))], ten, ten.AddMinutes(60)));
        // Hors du créneau : ignoré.
        Assert.Equal(0, ReceptionOccupancy.MaxConcurrent([(ten.AddHours(-1), ten)], ten, ten.AddMinutes(30)));
        // Simultanéité en dehors du créneau demandé : seule la partie interne compte.
        Assert.Equal(1, ReceptionOccupancy.MaxConcurrent([(ten.AddMinutes(-30), ten.AddMinutes(15)), (ten.AddMinutes(-20), ten.AddMinutes(-5))], ten, ten.AddMinutes(30)));
        Assert.Equal(0, ReceptionOccupancy.MaxConcurrent([], ten, ten.AddMinutes(30)));
    }

    [Fact]
    public void Locks_AreTakenInAStableAscendingOrderWithoutDuplicates()
    {
        var a = AppointmentLock.Responsible(Guid.CreateVersion7());
        var b = AppointmentLock.Responsible(Guid.CreateVersion7());
        var expected = new[] { a.Key, b.Key, AppointmentLock.Reception.Key }.Distinct().Order().ToArray();

        Assert.Equal(expected, MMV.Infrastructure.Appointments.AppointmentStore.LockSequence([b, AppointmentLock.Reception, a, b]));
        Assert.Equal(expected, MMV.Infrastructure.Appointments.AppointmentStore.LockSequence([a, b, AppointmentLock.Reception]));
        // Clé stable d'un appel à l'autre (et d'un processus à l'autre : aucun hachage aléatoire).
        var id = Guid.Parse("0199b000-0000-7000-8000-000000000001");
        Assert.Equal(AppointmentLock.Responsible(id), AppointmentLock.Responsible(Guid.Parse(id.ToString())));
    }
}
