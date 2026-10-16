using Microsoft.EntityFrameworkCore;
using MMV.Domain.Identity;
using MMV.Infrastructure.Identity;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de MMV V2, fourni par Npgsql (PostgreSQL exclusivement).
/// Phase 02 : uniquement les entités techniques d'identité (schéma <c>auth</c>) ; aucune entité métier.
/// </summary>
public sealed class MmvDbContext(DbContextOptions<MmvDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserSession> Sessions => Set<UserSession>();

    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        IdentityModel.Configure(modelBuilder);
    }
}
