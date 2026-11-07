using System.Globalization;
using System.Text.RegularExpressions;
using MMV.Domain.Catalog;
using MMV.Domain.Pricing;
using MMV.Domain.Suppliers;

namespace MMV.Application.Catalog;

/// <summary>Saisie brute d'un modèle (chaînes), avant validation.</summary>
public sealed record ModelInput(
    string? FamilyCode, string? Brand, string? Name, string? Description, Guid? LensRangeId, IReadOnlyDictionary<string, string?>? Attributes);

/// <summary>Saisie brute d'une variante. <see cref="StockPolicy"/> : création seulement (changement par action dédiée).</summary>
public sealed record VariantInput(
    string? Sku,
    string? ManufacturerReference,
    string? Barcode,
    string? Label,
    IReadOnlyDictionary<string, string?>? Attributes,
    Guid? LensIndexId,
    IReadOnlyList<Guid>? TreatmentIds,
    string? StockPolicy);

/// <summary>Saisie brute d'un fournisseur.</summary>
public sealed record SupplierInput(
    string? Code,
    string? Name,
    string? CountryCode,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine,
    string? PostalCode,
    string? City,
    IReadOnlyDictionary<string, string?>? LegalIdentifiers,
    string? Notes);

/// <summary>
/// Rattachement variante ↔ fournisseur. <see cref="PriceSpecified"/> : le corps contient un champ de prix (même vide) ;
/// sans <c>costs.write</c> c'est un refus 403, et le prix existant n'est jamais effacé par omission.
/// </summary>
public sealed record VariantSupplierInput(
    string? SupplierReference, bool PriceSpecified, string? SupplierPriceHt, string? SupplierCurrency, int? LeadTimeDays, bool IsPrimary);

/// <summary>Validation SERVEUR des fiches de la Phase 05 (le poste n'est qu'une aide de saisie). Valeurs rognées ; vide → absent.</summary>
public static partial class CatalogInputValidator
{
    [GeneratedRegex("^[^\\s].{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryPattern();

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[+0-9][0-9 ().-]{5,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();

    public static ProductModelDetails? Model(ModelInput input, IDictionary<string, string[]> errors, string prefix = "")
    {
        var family = Families.Find(input.FamilyCode);
        if (family is null)
        {
            errors[prefix + "familyCode"] = [input.FamilyCode is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown];
        }

        var brand = CatalogText.Clean(input.Brand, prefix + "brand", CatalogLimits.BrandMaxLength, required: false, errors);
        var name = CatalogText.Clean(input.Name, prefix + "name", CatalogLimits.NameMaxLength, required: true, errors);
        var description = CatalogText.Clean(input.Description, prefix + "description", CatalogLimits.DescriptionMaxLength, required: false, errors, multiline: true);
        if (family is { IsOphthalmicLens: true } && input.LensRangeId is null)
        {
            errors[prefix + "lensRangeId"] = [CatalogFieldErrors.Required];
        }
        else if (family is { IsOphthalmicLens: false } && input.LensRangeId is not null)
        {
            errors[prefix + "lensRangeId"] = [CatalogFieldErrors.NotAllowed];
        }

        var attributes = family is null ? null : Attributes(input.Attributes, family.ModelAttributes, prefix + "attributes", errors);
        return family is null || name is null || attributes is null || errors.Count > 0
            ? null
            : new ProductModelDetails(family.Code, brand, name, description, input.LensRangeId, attributes);
    }

    /// <summary>Variante ; <paramref name="requirePolicy"/> à la création seulement.</summary>
    public static (ProductVariantDetails? Details, StockPolicy? Policy) Variant(
        VariantInput input, FamilyDefinition family, bool requirePolicy, IDictionary<string, string[]> errors, string prefix = "")
    {
        var before = errors.Count;
        var sku = CatalogText.Clean(input.Sku, prefix + "sku", CatalogLimits.SkuMaxLength, required: true, errors);
        if (sku is not null && !SkuPattern().IsMatch(sku))
        {
            errors[prefix + "sku"] = [CatalogFieldErrors.InvalidFormat];
            sku = null;
        }

        var manufacturerReference = CatalogText.Clean(input.ManufacturerReference, prefix + "manufacturerReference", CatalogLimits.ReferenceMaxLength, required: false, errors);
        var barcode = CatalogText.Clean(input.Barcode, prefix + "barcode", CatalogLimits.BarcodeMaxLength, required: false, errors);
        if (barcode is not null && !Gtin.IsValid(barcode))
        {
            errors[prefix + "barcode"] = [CatalogFieldErrors.InvalidFormat];
        }

        var label = CatalogText.Clean(input.Label, prefix + "label", CatalogLimits.LabelMaxLength, required: true, errors);
        var attributes = Attributes(input.Attributes, family.VariantAttributes, prefix + "attributes", errors);

        var treatments = (input.TreatmentIds ?? []).Distinct().ToList();
        if (family.IsOphthalmicLens)
        {
            if (input.LensIndexId is null)
            {
                errors[prefix + "lensIndexId"] = [CatalogFieldErrors.Required];
            }

            if (treatments.Count > CatalogLimits.MaxTreatmentsPerVariant)
            {
                errors[prefix + "treatmentIds"] = [CatalogFieldErrors.OutOfRange];
            }
        }
        else
        {
            if (input.LensIndexId is not null)
            {
                errors[prefix + "lensIndexId"] = [CatalogFieldErrors.NotAllowed];
            }

            if (treatments.Count > 0)
            {
                errors[prefix + "treatmentIds"] = [CatalogFieldErrors.NotAllowed];
            }
        }

        StockPolicy? policy = null;
        if (requirePolicy)
        {
            policy = StockPolicies.TryParse(input.StockPolicy);
            if (policy is null)
            {
                errors[prefix + "stockPolicy"] = [input.StockPolicy is null ? CatalogFieldErrors.Required : CatalogFieldErrors.Unknown];
            }
            else if (!family.AllowedPolicies.Contains(policy.Value))
            {
                errors[prefix + "stockPolicy"] = [CatalogFieldErrors.NotAllowed];
            }
        }
        else if (input.StockPolicy is not null)
        {
            // La politique se change par l'action dédiée (contrôles de stock et d'inventaire), jamais par la modification.
            errors[prefix + "stockPolicy"] = [CatalogFieldErrors.NotAllowed];
        }

        return errors.Count > before || sku is null || label is null || attributes is null
            ? (null, null)
            : (new ProductVariantDetails(sku, manufacturerReference, barcode, label, attributes, input.LensIndexId, treatments), policy);
    }

    public static SupplierDetails? Supplier(SupplierInput input, IDictionary<string, string[]> errors)
    {
        var code = CatalogText.Clean(input.Code, "code", Domain.Suppliers.Supplier.CodeMaxLength, required: true, errors);
        var name = CatalogText.Clean(input.Name, "name", Domain.Suppliers.Supplier.NameMaxLength, required: true, errors);
        var country = CatalogText.Clean(input.CountryCode, "countryCode", 2, required: true, errors)?.ToUpperInvariant();
        if (country is not null && !CountryPattern().IsMatch(country))
        {
            errors["countryCode"] = [CatalogFieldErrors.InvalidFormat];
        }

        var contact = CatalogText.Clean(input.ContactName, "contactName", Domain.Suppliers.Supplier.ContactMaxLength, required: false, errors);
        var email = CatalogText.Clean(input.Email, "email", Domain.Suppliers.Supplier.EmailMaxLength, required: false, errors);
        if (email is not null && !EmailPattern().IsMatch(email))
        {
            errors["email"] = [CatalogFieldErrors.InvalidFormat];
        }

        var phone = CatalogText.Clean(input.Phone, "phone", Domain.Suppliers.Supplier.PhoneMaxLength, required: false, errors);
        if (phone is not null && !PhonePattern().IsMatch(phone))
        {
            errors["phone"] = [CatalogFieldErrors.InvalidFormat];
        }

        var address = CatalogText.Clean(input.AddressLine, "addressLine", Domain.Suppliers.Supplier.AddressLineMaxLength, required: false, errors);
        var postal = CatalogText.Clean(input.PostalCode, "postalCode", Domain.Suppliers.Supplier.PostalCodeMaxLength, required: false, errors);
        var city = CatalogText.Clean(input.City, "city", Domain.Suppliers.Supplier.CityMaxLength, required: false, errors);
        var notes = CatalogText.Clean(input.Notes, "notes", Domain.Suppliers.Supplier.NotesMaxLength, required: false, errors, multiline: true);

        var legal = new Dictionary<string, string>(StringComparer.Ordinal);
        var formats = country is null ? null : LegalIdentifierFormats.ByCountry.GetValueOrDefault(country);
        foreach (var (key, raw) in input.LegalIdentifiers ?? new Dictionary<string, string?>())
        {
            var value = raw?.Replace(".", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (formats is null || !formats.TryGetValue(key, out var pattern))
            {
                errors[$"legalIdentifiers.{key}"] = [CatalogFieldErrors.NotAllowed];
            }
            else if (!Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                errors[$"legalIdentifiers.{key}"] = [CatalogFieldErrors.InvalidFormat];
            }
            else
            {
                legal[key] = value;
            }
        }

        return errors.Count > 0 || code is null || name is null || country is null
            ? null
            : new SupplierDetails(code, name, country, contact, email, phone, address, postal, city, legal, notes);
    }

    public static VariantSupplierDetails? Link(VariantSupplierInput input, VariantSupplierDetails? current, IDictionary<string, string[]> errors)
    {
        var reference = CatalogText.Clean(input.SupplierReference, "supplierReference", CatalogLimits.ReferenceMaxLength, required: false, errors);
        decimal? price = current?.SupplierPriceHt;
        var currency = current?.SupplierCurrency;
        if (input.PriceSpecified)
        {
            price = null;
            currency = null;
            if (!string.IsNullOrWhiteSpace(input.SupplierPriceHt))
            {
                price = MoneyText.TryParse(input.SupplierPriceHt.Trim(), MoneyText.PurchaseScale);
                if (price is null or > MoneyText.MaxPurchasePrice)
                {
                    errors["supplierPriceHt"] = [CatalogFieldErrors.InvalidFormat];
                }

                currency = input.SupplierCurrency?.Trim().ToUpperInvariant();
                if (currency is null || !CurrencyPattern().IsMatch(currency))
                {
                    errors["supplierCurrency"] = [currency is null ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat];
                }
            }
        }

        if (input.LeadTimeDays is < 0 or > VariantSupplier.LeadTimeMaxDays)
        {
            errors["leadTimeDays"] = [CatalogFieldErrors.OutOfRange];
        }

        return errors.Count > 0 ? null : new VariantSupplierDetails(reference, price, currency, input.LeadTimeDays, input.IsPrimary);
    }

    /// <summary>Attributs : clés du jeu de la famille seulement, valeurs normalisées ; vide = absent.</summary>
    private static Dictionary<string, string>? Attributes(
        IReadOnlyDictionary<string, string?>? input, IReadOnlyList<AttributeDefinition> definitions, string field, IDictionary<string, string[]> errors)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var valid = true;
        foreach (var (key, raw) in input ?? new Dictionary<string, string?>())
        {
            var definition = definitions.FirstOrDefault(d => d.Key == key);
            if (definition is null)
            {
                errors[$"{field}.{key}"] = [CatalogFieldErrors.NotAllowed];
                valid = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var value = AttributeValues.Normalize(definition, raw);
            if (value is null)
            {
                errors[$"{field}.{key}"] = [definition.Kind == AttributeKind.Text ? CatalogFieldErrors.TooLong : CatalogFieldErrors.InvalidFormat];
                valid = false;
            }
            else
            {
                result[key] = value;
            }
        }

        return valid ? result : null;
    }

    /// <summary>Indice de verre : décimal à 3 décimales au plus, strictement entre 1 et 2,5.</summary>
    public static decimal? LensIndex(string? text, IDictionary<string, string[]> errors)
    {
        var value = text is null ? null : MoneyText.TryParse(text.Trim(), 3);
        if (value is not > 1m or not < 2.5m)
        {
            errors["indexValue"] = [text is null ? CatalogFieldErrors.Required : CatalogFieldErrors.InvalidFormat];
            return null;
        }

        return value;
    }

    public static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
