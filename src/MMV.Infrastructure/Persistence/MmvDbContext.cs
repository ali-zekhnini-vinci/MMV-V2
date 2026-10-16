using Microsoft.EntityFrameworkCore;
using MMV.Domain.Clients;
using MMV.Domain.Identity;
using MMV.Infrastructure.Clients;
using MMV.Infrastructure.Identity;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de MMV V2, fourni par Npgsql (PostgreSQL exclusivement).
/// Phase 02 : entités techniques d'identité (schéma <c>auth</c>). Phase 03-A2 : clients (schéma <c>clients</c>).
/// </summary>
public sealed class MmvDbContext(DbContextOptions<MmvDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserSession> Sessions => Set<UserSession>();

    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();

    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        IdentityModel.Configure(modelBuilder);
        ClientModel.Configure(modelBuilder);
    }
}
