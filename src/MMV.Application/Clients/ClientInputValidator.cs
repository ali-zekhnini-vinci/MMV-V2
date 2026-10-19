using System.Globalization;
using System.Text.RegularExpressions;
using MMV.Domain.Clients;

namespace MMV.Application.Clients;

/// <summary>
/// Saisie brute d'un client telle que reçue (chaînes, date civile <c>AAAA-MM-JJ</c>), avant validation.
/// Champs facultatifs : <c>null</c> ou vides. Aucun champ de sécurité sociale, mutuelle ni donnée de santé.
/// </summary>
public sealed record ClientInput(
    string? FirstName,
    string? LastName,
    string? BirthDate,
    string? Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? PostalCode,
    string? Notes);

/// <summary>Codes d'erreur de champ bornés (aucun message libre, aucune valeur saisie renvoyée).</summary>
public static class ClientFieldErrors
{
    public const string Required = "required";
    public const string TooLong = "too_long";
    public const string InvalidFormat = "invalid_format";
    public const string OutOfRange = "out_of_range";
}

/// <summary>
/// Validation SERVEUR des règles D-03-02 (identiques à <c>apps/desktop/src/clients/clientForm.ts</c>, qui
/// n'est qu'une aide de saisie) : prénom et nom obligatoires, tout le reste facultatif, aucune unicité
/// d'email ni de téléphone. Les valeurs sont rognées ; une chaîne vide devient <c>null</c>.
/// </summary>
public static partial class ClientInputValidator
{
    public sealed record Result(ClientDetails? Details, IReadOnlyDictionary<string, string[]> Errors)
    {
        public bool IsValid => Details is not null;
    }

    public static Result Validate(ClientInput input, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var firstName = Text(input.FirstName, "firstName", Client.NameMaxLength, required: true, multiline: false, errors);
        var lastName = Text(input.LastName, "lastName", Client.NameMaxLength, required: true, multiline: false, errors);
        var birthDate = BirthDate(input.BirthDate, today, errors);
        var phone = Text(input.Phone, "phone", Client.PhoneMaxLength, required: false, multiline: false, errors);
        if (phone is not null && !errors.ContainsKey("phone") && !IsValidPhone(phone))
        {
            errors["phone"] = [ClientFieldErrors.InvalidFormat];
        }

        var email = Text(input.Email, "email", Client.EmailMaxLength, required: false, multiline: false, errors);
        if (email is not null && !errors.ContainsKey("email") && !EmailPattern().IsMatch(email))
        {
            errors["email"] = [ClientFieldErrors.InvalidFormat];
        }

        var addressLine = Text(input.AddressLine, "addressLine", Client.AddressLineMaxLength, required: false, multiline: false, errors);
        var city = Text(input.City, "city", Client.CityMaxLength, required: false, multiline: false, errors);
        var postalCode = Text(input.PostalCode, "postalCode", Client.PostalCodeMaxLength, required: false, multiline: false, errors);
        if (postalCode is not null && !errors.ContainsKey("postalCode") && !PostalCodePattern().IsMatch(postalCode))
        {
            errors["postalCode"] = [ClientFieldErrors.InvalidFormat];
        }

        var notes = Text(input.Notes, "notes", Client.NotesMaxLength, required: false, multiline: true, errors);

        return errors.Count > 0
            ? new Result(null, errors)
            : new Result(new ClientDetails(firstName!, lastName!, birthDate, phone, email, addressLine, city, postalCode, notes), errors);
    }

    /// <summary>Format libre : chiffres, espaces, points, tirets, « + » initial ; 8 à 15 chiffres.</summary>
    public static bool IsValidPhone(string phone)
    {
        if (!PhonePattern().IsMatch(phone))
        {
            return false;
        }

        var digits = ClientSearch.Digits(phone).Length;
        return digits is >= Client.PhoneMinDigits and <= Client.PhoneMaxDigits;
    }

    private static string? Text(
        string? value, string field, int maxLength, bool required, bool multiline, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (required)
            {
                errors[field] = [ClientFieldErrors.Required];
            }

            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors[field] = [ClientFieldErrors.TooLong];
            return null;
        }

        // Caractères de contrôle refusés (les notes admettent sauts de ligne et tabulations).
        if (trimmed.Any(c => char.IsControl(c) && !(multiline && c is '\n' or '\r' or '\t')))
        {
            errors[field] = [ClientFieldErrors.InvalidFormat];
            return null;
        }

        return trimmed;
    }

    private static DateOnly? BirthDate(string? value, DateOnly today, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors["birthDate"] = [ClientFieldErrors.InvalidFormat];
            return null;
        }

        if (date < Client.MinBirthDate || date > today)
        {
            errors["birthDate"] = [ClientFieldErrors.OutOfRange];
            return null;
        }

        return date;
    }

    [GeneratedRegex(@"^\+?[0-9 .\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    // Même motif que la validation d'email de Zod 4 utilisée par l'interface (z.email()).
    [GeneratedRegex(@"^(?!\.)(?!.*\.\.)([A-Za-z0-9_'+\-\.]*)[A-Za-z0-9_+\-]@([A-Za-z0-9][A-Za-z0-9\-]*\.)+[A-Za-z]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9 -]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PostalCodePattern();
}
