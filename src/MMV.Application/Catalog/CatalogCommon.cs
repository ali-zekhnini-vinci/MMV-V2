using System.Text.RegularExpressions;
using MMV.Application.Audit;
using MMV.Domain.Audit;

namespace MMV.Application.Catalog;

/// <summary>Famille de réponse HTTP d'une écriture de la Phase 05 (le code borné est porté par <see cref="Outcome{T}.Code"/>).</summary>
public enum OutcomeKind
{
    Ok,
    Created,
    NotFound,

    /// <summary>422 <c>validation_failed</c> avec erreurs par champ.</summary>
    Invalid,

    /// <summary>422 avec code métier (<c>stock_insufficient</c>, <c>archive_refused</c>…).</summary>
    Refused,

    /// <summary>409 (<c>version_conflict</c>, <c>request_id_reused</c>, <c>stock_changed</c>…).</summary>
    Conflict,

    /// <summary>403 avec code (<c>costs_forbidden</c>, <c>manual_price_forbidden</c>).</summary>
    Forbidden,
}

/// <summary>
/// Résultat d'une écriture : état, valeur (relue), code borné, erreurs par champ, détails non sensibles.
/// <see cref="Replayed"/> : même identifiant de requête déjà appliqué — état courant renvoyé, rien n'est réécrit.
/// </summary>
public sealed record Outcome<T>(
    OutcomeKind Kind, T? Value = default, string? Code = null, IReadOnlyDictionary<string, string[]>? Errors = null, bool Replayed = false, object? Details = null)
{
    public bool IsSuccess => Kind is OutcomeKind.Ok or OutcomeKind.Created;

    /// <summary>Même refus, autre type de valeur.</summary>
    public Outcome<TOther> As<TOther>() => new(Kind, default, Code, Errors, Replayed, Details);
}

/// <summary>Fabriques de résultats (classe non générique : CA1000).</summary>
public static class Outcome
{
    public static Outcome<T> Ok<T>(T value, bool replayed = false) => new Outcome<T>(OutcomeKind.Ok, value, Replayed: replayed);

    public static Outcome<T> Created<T>(T value) => new Outcome<T>(OutcomeKind.Created, value);

    public static Outcome<T> Missing<T>(string code = "not_found") => new Outcome<T>(OutcomeKind.NotFound, Code: code);

    public static Outcome<T> Invalid<T>(IReadOnlyDictionary<string, string[]> errors) => new Outcome<T>(OutcomeKind.Invalid, Code: "validation_failed", Errors: errors);

    public static Outcome<T> Invalid<T>(string field, string error) =>
        Invalid<T>(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [error] });

    public static Outcome<T> Refuse<T>(string code, object? details = null) => new Outcome<T>(OutcomeKind.Refused, Code: code, Details: details);

    public static Outcome<T> Conflict<T>(string code = "version_conflict", object? details = null) => new Outcome<T>(OutcomeKind.Conflict, Code: code, Details: details);

    public static Outcome<T> Forbidden<T>(string code) => new Outcome<T>(OutcomeKind.Forbidden, Code: code);
}

/// <summary>Codes d'erreur de champ bornés (aucun message libre, aucune valeur saisie renvoyée).</summary>
public static class CatalogFieldErrors
{
    public const string Required = "required";
    public const string TooLong = "too_long";
    public const string InvalidFormat = "invalid_format";
    public const string OutOfRange = "out_of_range";
    public const string Duplicate = "duplicate";
    public const string Unknown = "unknown";
    public const string NotAllowed = "not_allowed";
    public const string Archived = "archived";
}

/// <summary>Droits de la session utiles aux règles de filtrage (coûts) et aux écritures multi-permissions.</summary>
public sealed record CatalogActor(Guid UserId, IReadOnlySet<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission);
}

/// <summary>Issue technique d'un enregistrement.</summary>
public enum CommitStatus
{
    Saved,
    VersionConflict,
    UniqueViolation,

    /// <summary>Ligne figée par déclencheur (journal en ajout seul, session close…) : écriture concurrente, rien n'est écrit.</summary>
    Frozen,

    /// <summary>Contrainte de vérification (stock négatif…) violée : garde de base, rien n'est écrit.</summary>
    CheckViolation,
    Deadlock,
    LockTimeout,
}

public sealed record CommitResult(CommitStatus Status, string? Constraint = null)
{
    public bool Saved => Status == CommitStatus.Saved;
}

/// <summary>Transaction explicite (READ COMMITTED) : non validée à la libération = annulée, verrous relâchés.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>Force d'un verrou de ligne.</summary>
public enum RowLock
{
    /// <summary><c>FOR SHARE</c> : lecture stable (statut, politique) ; plusieurs écritures de stock se partagent ce verrou.</summary>
    Share,

    /// <summary><c>FOR NO KEY UPDATE</c> : archivage, changement de politique (attend les écritures de stock en cours).</summary>
    NoKeyUpdate,

    /// <summary><c>FOR UPDATE</c> : soldes, tarifs, sessions.</summary>
    Update,
}

public static partial class CatalogText
{
    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharacters();

    /// <summary>Texte rogné ; vide → <c>null</c> ; erreurs <c>required</c>, <c>too_long</c>, <c>invalid_format</c> (caractères de contrôle, saut de ligne hors texte multiligne).</summary>
    public static string? Clean(string? value, string field, int maxLength, bool required, IDictionary<string, string[]> errors, bool multiline = false)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (required)
            {
                errors[field] = [CatalogFieldErrors.Required];
            }

            return null;
        }

        if (text.Length > maxLength)
        {
            errors[field] = [CatalogFieldErrors.TooLong];
            return null;
        }

        if (ControlCharacters().IsMatch(text) || (!multiline && (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal))))
        {
            errors[field] = [CatalogFieldErrors.InvalidFormat];
            return null;
        }

        return text;
    }
}

/// <summary>Enregistrement des événements d'audit des modules de la Phase 05 (aucun dossier client, aucun coût).</summary>
public static class CatalogAudit
{
    public static void Record(IAuditRecorder audit, Guid userId, DateTimeOffset now, string module, string action, string entityType, Guid entityId, IReadOnlyList<AuditChange> changes) =>
        audit.Record(new AuditRecord(userId, now, null, module, action, entityType, entityId, changes));

    /// <summary>Champ cité par son nom (valeur conservée seulement s'il figure dans la liste blanche de <see cref="AuditFieldPolicy"/>).</summary>
    public static AuditChange Field(string field, string? oldValue = null, string? newValue = null) => AuditFieldPolicy.Change(field, oldValue, newValue);

    public static IReadOnlyList<AuditChange> Fields(IEnumerable<string> names) => [.. names.Select(n => Field(n))];
}

/// <summary>Horloge à la précision PostgreSQL (microseconde) : la valeur renvoyée est identique à celle relue.</summary>
public static class CatalogClock
{
    public static DateTimeOffset Now(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }
}
