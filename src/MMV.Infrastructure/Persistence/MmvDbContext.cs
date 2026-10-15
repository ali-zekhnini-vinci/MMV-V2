using Microsoft.EntityFrameworkCore;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de MMV V2, fourni par Npgsql (PostgreSQL exclusivement).
/// Aucune entité en phase 01A : aucun schéma, aucune migration, aucune donnée.
/// </summary>
public sealed class MmvDbContext(DbContextOptions<MmvDbContext> options) : DbContext(options);
