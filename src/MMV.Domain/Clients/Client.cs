namespace MMV.Domain.Clients;

/// <summary>
/// Client du magasin (phase 03-A2, G03-METIER A accepté le 17/10/2026) — données d'identification et de
/// contact strictement minimales. Aucune donnée optique, médicale, de sécurité sociale ni de mutuelle.
/// Cycle de vie : actif ou archivé, jamais supprimé physiquement (D-03-05). Concurrence : colonne système
/// <c>xmin</c> de PostgreSQL, exposée comme <see cref="Version"/> (D-03-06).
/// </summary>
public sealed class Client
{
    public const int NameMaxLength = 100;
    public const int ReferenceMaxLength = 24;
    public const int PhoneMaxLength = 32;
    public const int PhoneMinDigits = 8;
    public const int PhoneMaxDigits = 15;
    public const int EmailMaxLength = 254;
    public const int AddressLineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int PostalCodeMaxLength = 10;
    public const int NotesMaxLength = 1000;
    public const int SearchTextMaxLength = 700;

    /// <summary>Date de naissance la plus ancienne admise (contrainte <c>ck_clients_birth_date_range</c>).</summary>
    public static readonly DateOnly MinBirthDate = new(1900, 1, 1);

    private Client()
    {
        Reference = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        SearchText = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Référence lisible attribuée par PostgreSQL à l'insertion (séquence <c>clients.client_reference_seq</c>),
    /// ex. <c>CLI-000123</c> ; unique et jamais réutilisée. Vide tant que le client n'est pas enregistré.
    /// </summary>
    public string Reference { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? AddressLine { get; private set; }

    public string? City { get; private set; }

    public string? PostalCode { get; private set; }

    /// <summary>Notes internes (consigne : aucune donnée de santé).</summary>
    public string? Notes { get; private set; }

    /// <summary>Texte de recherche normalisé par l'application (<see cref="ClientSearch.BuildSearchText"/>).</summary>
    public string SearchText { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    /// <summary>Jeton de concurrence optimiste : colonne système <c>xmin</c> (aucune colonne créée).</summary>
    public uint Version { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    /// <summary>Nouveau client actif. <paramref name="details"/> doit avoir été validé (MMV.Application).</summary>
    public static Client Create(Guid id, ClientDetails details, Guid userId, DateTimeOffset now)
    {
        var client = new Client
        {
            Id = id,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
        };
        client.Apply(details, userId, now);
        return client;
    }

    /// <summary>Remplace les données saisissables. Un client archivé est en lecture seule.</summary>
    public void Update(ClientDetails details, Guid userId, DateTimeOffset now)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Un client archivé n'est pas modifiable.");
        }

        Apply(details, userId, now);
    }

    public void Archive(Guid userId, DateTimeOffset now)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Client déjà archivé.");
        }

        ArchivedAtUtc = now;
        Touch(userId, now);
    }

    public void Reactivate(Guid userId, DateTimeOffset now)
    {
        if (!IsArchived)
        {
            throw new InvalidOperationException("Client déjà actif.");
        }

        ArchivedAtUtc = null;
        Touch(userId, now);
    }

    private void Apply(ClientDetails details, Guid userId, DateTimeOffset now)
    {
        FirstName = details.FirstName;
        LastName = details.LastName;
        BirthDate = details.BirthDate;
        Phone = details.Phone;
        Email = details.Email;
        AddressLine = details.AddressLine;
        City = details.City;
        PostalCode = details.PostalCode;
        Notes = details.Notes;
        SearchText = ClientSearch.BuildSearchText(details.FirstName, details.LastName, details.Email, details.Phone);
        Touch(userId, now);
    }

    private void Touch(Guid userId, DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
    }
}

/// <summary>Données saisissables d'un client, déjà rognées et validées (champs facultatifs à <c>null</c>).</summary>
public sealed record ClientDetails(
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? PostalCode,
    string? Notes);
