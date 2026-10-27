using System.Globalization;
using MMV.Domain.Optical;

namespace MMV.Application.Optical;

/// <summary>Valeurs d'un œil telles que reçues (chaînes) ; absente, <c>null</c> ou vide = non renseignée.</summary>
public sealed record EyeInput(
    string? Sphere = null,
    string? Cylinder = null,
    string? Axis = null,
    string? Addition = null,
    string? Prism = null,
    string? Base = null,
    string? Acuity = null)
{
    public IReadOnlyList<string?> InOrder => [Sphere, Cylinder, Axis, Addition, Prism, Base, Acuity];
}

public sealed record PrescriptionProvenanceInput(
    string? PrescriberName = null,
    string? PrescriberQualification = null,
    string? PrescriberIdentifier = null,
    string? PrescribedOn = null,
    string? ReceivedOn = null);

public sealed record MeasurementProvenanceInput(string? MeasuredOn = null, Guid? MeasuredByUserId = null, string? Category = null);

/// <summary>Saisie brute d'une ordonnance médicale (provenance, OD, OG, notes).</summary>
public sealed record PrescriptionInput(PrescriptionProvenanceInput? Provenance, EyeInput? Od, EyeInput? Og, string? Notes);

/// <summary>Saisie brute d'une mesure magasin (provenance, OD, OG, notes).</summary>
public sealed record MeasurementInput(MeasurementProvenanceInput? Provenance, EyeInput? Od, EyeInput? Og, string? Notes);

/// <summary>Codes d'erreur de champ bornés (aucun message libre, aucune valeur saisie renvoyée ni journalisée).</summary>
public static class OpticalFieldErrors
{
    public const string Required = "required";
    public const string TooLong = "too_long";
    public const string InvalidFormat = "invalid_format";
    public const string OutOfRange = "out_of_range";
    public const string NumberExpected = "number_expected";
    public const string NotEligible = "not_eligible";

    // Critères de confirmation (422 « incomplete ») — orientations produit APPROUVÉES, validation professionnelle ouverte.
    public const string OpticalValuesMissing = "optical_values_missing";
    public const string SphereZeroOnly = "sphere_zero_only";
    public const string AcuityOnly = "acuity_only";
    public const string AxisRequired = "axis_required";
    public const string BaseRequired = "base_required";

    /// <summary>Clé des erreurs portant sur l'ensemble des valeurs optiques (pas sur un champ).</summary>
    public const string Correction = "correction";
}

/// <summary>Résultat de validation : contenu prêt, ou erreurs par chemin de champ (<c>od.sphere</c>, <c>provenance.prescribedOn</c>…).</summary>
public sealed record OpticalValidation<TContent>(TContent? Content, IReadOnlyDictionary<string, string[]> Errors)
    where TContent : OpticalContent
{
    public bool IsValid => Content is not null;
}

/// <summary>
/// Validation de FORME, identique au brouillon et à la confirmation : longueurs, dates civiles <c>AAAA-MM-JJ</c> (sans
/// fuseau ni conversion UTC), nombres décimaux pour Sphère / Cylindre / Axe / Addition / Prisme. Aucune borne clinique,
/// aucun pas, aucune dépendance entre champs (D-04-06 : avis d'un opticien requis). Les valeurs sont rognées aux
/// extrémités ; une chaîne vide devient <c>null</c>, jamais <c>0</c> ; aucune autre réécriture.
/// </summary>
public static class OpticalInputValidator
{
    public static OpticalValidation<PrescriptionContent> Validate(PrescriptionInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var provenance = input.Provenance ?? new PrescriptionProvenanceInput();
        var name = Text(provenance.PrescriberName, "provenance.prescriberName", OpticalLimits.TextMaxLength, multiline: false, errors);
        var qualification = Text(provenance.PrescriberQualification, "provenance.prescriberQualification", OpticalLimits.TextMaxLength, multiline: false, errors);
        var identifier = Text(provenance.PrescriberIdentifier, "provenance.prescriberIdentifier", OpticalLimits.TextMaxLength, multiline: false, errors);
        var prescribedOn = Date(provenance.PrescribedOn, "provenance.prescribedOn", errors);
        var receivedOn = Date(provenance.ReceivedOn, "provenance.receivedOn", errors);
        var values = Values(input.Od, input.Og, errors);
        var notes = Text(input.Notes, "notes", OpticalLimits.NotesMaxLength, multiline: true, errors);
        return errors.Count > 0
            ? new(null, errors)
            : new(new PrescriptionContent(new PrescriptionProvenance(name, qualification, identifier, prescribedOn, receivedOn), values, notes), errors);
    }

    public static OpticalValidation<MeasurementContent> Validate(MeasurementInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var provenance = input.Provenance ?? new MeasurementProvenanceInput();
        var measuredOn = Date(provenance.MeasuredOn, "provenance.measuredOn", errors);
        var category = Text(provenance.Category, "provenance.category", OpticalLimits.TextMaxLength, multiline: false, errors);
        var measuredBy = provenance.MeasuredByUserId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var values = Values(input.Od, input.Og, errors);
        var notes = Text(input.Notes, "notes", OpticalLimits.NotesMaxLength, multiline: true, errors);
        return errors.Count > 0
            ? new(null, errors)
            : new(new MeasurementContent(new MeasurementProvenance(measuredOn, measuredBy, category), values, notes), errors);
    }

    /// <summary>Motif codifié d'une liste et commentaire (exigé pour « other ») ; erreurs sous <c>reasonCode</c> / <c>comment</c>.</summary>
    public static (string? Code, string? Comment) Reason(
        string? code, string? comment, IReadOnlyList<string> allowed, Dictionary<string, string[]> errors)
    {
        var trimmedCode = code?.Trim();
        if (string.IsNullOrEmpty(trimmedCode))
        {
            errors["reasonCode"] = [OpticalFieldErrors.Required];
        }
        else if (!allowed.Contains(trimmedCode, StringComparer.Ordinal))
        {
            errors["reasonCode"] = [OpticalFieldErrors.InvalidFormat];
        }

        var text = Text(comment, "comment", OpticalLimits.CommentMaxLength, multiline: true, errors);
        if (text is null && trimmedCode == OpticalReasons.Other && !errors.ContainsKey("comment"))
        {
            errors["comment"] = [OpticalFieldErrors.Required];
        }

        return (errors.ContainsKey("reasonCode") ? null : trimmedCode, text);
    }

    private static OpticalValues Values(EyeInput? od, EyeInput? og, Dictionary<string, string[]> errors) =>
        new(Eye(EyeSide.Od, od, errors), Eye(EyeSide.Og, og, errors));

    private static EyeValues Eye(EyeSide side, EyeInput? input, Dictionary<string, string[]> errors)
    {
        var raw = (input ?? new EyeInput()).InOrder;
        var values = new string?[OpticalFields.Names.Count];
        for (var index = 0; index < values.Length; index++)
        {
            var field = OpticalFields.Names[index];
            var path = $"{OpticalFields.SideCode(side)}.{field}";
            var value = Text(raw[index], path, OpticalFields.ValueMaxLength, multiline: false, errors);
            if (value is not null && OpticalFields.Numeric.Contains(field) && !OpticalFields.IsNumber(value))
            {
                errors[path] = [OpticalFieldErrors.NumberExpected];
                value = null;
            }

            values[index] = value;
        }

        return EyeValues.FromOrdered(values);
    }

    private static string? Text(string? value, string field, int maxLength, bool multiline, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors[field] = [OpticalFieldErrors.TooLong];
            return null;
        }

        // Caractères de contrôle refusés (notes et commentaires admettent sauts de ligne et tabulations).
        if (trimmed.Any(c => char.IsControl(c) && !(multiline && (c is '\n' or '\r' or '\t'))))
        {
            errors[field] = [OpticalFieldErrors.InvalidFormat];
            return null;
        }

        return trimmed;
    }

    private static DateOnly? Date(string? value, string field, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors[field] = [OpticalFieldErrors.InvalidFormat];
            return null;
        }

        if (date < OpticalLimits.MinDate)
        {
            errors[field] = [OpticalFieldErrors.OutOfRange];
            return null;
        }

        return date;
    }
}

/// <summary>
/// Critères de CONFIRMATION (422 <c>incomplete</c>) : orientations produit APPROUVÉES par le propriétaire (mandat GO du
/// 27/10/2026), SOUMISES À VALIDATION PROFESSIONNELLE (opticien) ; recette locale sur données fictives seulement, ni
/// certification médicale, ni autorisation d'exploitation sur des données de santé réelles. Le brouillon n'y est jamais
/// soumis. Règles appliquées, sans aucune autre :
/// <list type="number">
/// <item>au moins une valeur optique, sur un œil au moins (l'autre peut rester entièrement vide) ;</item>
/// <item>une Sphère unique à zéro, sans aucune autre valeur optique, ne suffit pas (<c>0,00</c> reste une valeur explicite) ;</item>
/// <item>ordonnance : l'acuité visuelle seule ne suffit pas ; mesure magasin : elle suffit ;</item>
/// <item>Cylindre non nul sans Axe : refusé (aucune valeur d'axe déduite ; un axe conservé avec un cylindre nul est accepté, jamais effacé) ;</item>
/// <item>Prisme non nul sans Base : refusé (conventions, pas et composantes multiples du prisme : à vérifier) ;</item>
/// <item>provenance : ordonnance — date de prescription et nom du prescripteur ; mesure — date et collaborateur ayant mesuré.</item>
/// </list>
/// </summary>
public static class OpticalConfirmationRules
{
    public static IReadOnlyDictionary<string, string[]> Check(PrescriptionContent content)
    {
        var errors = Values(content.Values, acuityAloneSuffices: false);
        if (content.Provenance.PrescribedOn is null)
        {
            errors["provenance.prescribedOn"] = [OpticalFieldErrors.Required];
        }

        if (content.Provenance.PrescriberName is null)
        {
            errors["provenance.prescriberName"] = [OpticalFieldErrors.Required];
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> Check(MeasurementContent content)
    {
        var errors = Values(content.Values, acuityAloneSuffices: true);
        if (content.Provenance.MeasuredOn is null)
        {
            errors["provenance.measuredOn"] = [OpticalFieldErrors.Required];
        }

        if (content.Provenance.MeasuredByUserId is null)
        {
            errors["provenance.measuredByUserId"] = [OpticalFieldErrors.Required];
        }

        return errors;
    }

    private static Dictionary<string, string[]> Values(OpticalValues values, bool acuityAloneSuffices)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var filled = new List<(EyeSide Side, string Field, string Value)>();
        foreach (var side in new[] { EyeSide.Od, EyeSide.Og })
        {
            var eye = values.Of(side).InOrder;
            for (var index = 0; index < eye.Count; index++)
            {
                if (eye[index] is { } value)
                {
                    filled.Add((side, OpticalFields.Names[index], value));
                }
            }
        }

        if (filled.Count == 0)
        {
            errors[OpticalFieldErrors.Correction] = [OpticalFieldErrors.OpticalValuesMissing];
            return errors;
        }

        if (filled is [{ Field: OpticalFields.Sphere } single] && OpticalFields.ToDecimal(single.Value) == 0m)
        {
            errors[OpticalFieldErrors.Correction] = [OpticalFieldErrors.SphereZeroOnly];
        }
        else if (!acuityAloneSuffices && filled.All(f => f.Field == OpticalFields.Acuity))
        {
            errors[OpticalFieldErrors.Correction] = [OpticalFieldErrors.AcuityOnly];
        }

        foreach (var side in new[] { EyeSide.Od, EyeSide.Og })
        {
            var eye = values.Of(side);
            var prefix = OpticalFields.SideCode(side);
            if (NonZero(eye.Cylinder) && eye.Axis is null)
            {
                errors[$"{prefix}.{OpticalFields.Axis}"] = [OpticalFieldErrors.AxisRequired];
            }

            if (NonZero(eye.Prism) && eye.Base is null)
            {
                errors[$"{prefix}.{OpticalFields.Base}"] = [OpticalFieldErrors.BaseRequired];
            }
        }

        return errors;
    }

    private static bool NonZero(string? value) => value is not null && OpticalFields.ToDecimal(value) != 0m;
}
