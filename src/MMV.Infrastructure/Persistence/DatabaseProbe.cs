using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace MMV.Infrastructure.Persistence;

/// <summary>
/// Sonde de connexion PostgreSQL bornée. Le jeton d'annulation seul ne coupe pas une ouverture Npgsql
/// face à un serveur muet (constat 01B : 30 s observées pour un timeout de check de 3 s) ; le délai de
/// connexion Npgsql (<c>Timeout</c>, en secondes) est donc plafonné pour cette sonde.
/// </summary>
public static class DatabaseProbe
{
    /// <summary>
    /// Ouvre réellement une connexion (sans requête applicative) et la referme. À n'appeler que sur un
    /// contexte dédié à la sonde : sa chaîne de connexion est modifiée pour plafonner le délai.
    /// </summary>
    public static Task<bool> CanConnectWithinAsync(
        this DatabaseFacade database, int connectTimeoutSeconds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentOutOfRangeException.ThrowIfLessThan(connectTimeoutSeconds, 1);

        var builder = new NpgsqlConnectionStringBuilder(database.GetConnectionString());
        if (builder.Timeout == 0 || builder.Timeout > connectTimeoutSeconds)
        {
            builder.Timeout = connectTimeoutSeconds;
        }

        database.SetConnectionString(builder.ConnectionString);
        return database.CanConnectAsync(cancellationToken);
    }
}
