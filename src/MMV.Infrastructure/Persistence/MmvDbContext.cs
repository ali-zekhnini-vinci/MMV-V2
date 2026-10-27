using Microsoft.EntityFrameworkCore;
using MMV.Domain.Appointments;
using MMV.Domain.Audit;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Domain.Optical;
using MMV.Infrastructure.Appointments;
using MMV.Infrastructure.Audit;
using MMV.Infrastructure.Clients;
using MMV.Infrastructure.Identity;
using MMV.Infrastructure.Optical;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de MMV V2, fourni par Npgsql (PostgreSQL exclusivement).
/// Phase 02 : entités techniques d'identité (schéma <c>auth</c>). Phase 03-A2 : clients (schéma <c>clients</c>).
/// Phase 03-B2 : rendez-vous et éligibilité des collaborateurs (schéma <c>appointments</c>). Phase 03-D2 : journal
/// d'audit métier (schéma <c>audit</c>). Phase 04-B : dossier optique, ordonnances et mesures magasin (schéma
/// <c>optical</c>).
/// </summary>
public sealed class MmvDbContext(DbContextOptions<MmvDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserSession> Sessions => Set<UserSession>();

    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<AppointmentResponsible> AppointmentResponsibles => Set<AppointmentResponsible>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    public DbSet<Measurement> Measurements => Set<Measurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        IdentityModel.Configure(modelBuilder);
        ClientModel.Configure(modelBuilder);
        AppointmentModel.Configure(modelBuilder);
        AuditModel.Configure(modelBuilder);
        OpticalModel.Configure(modelBuilder);
    }
}
