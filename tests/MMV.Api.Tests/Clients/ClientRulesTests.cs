using MMV.Application.Clients;
using MMV.Domain.Clients;
using MMV.Infrastructure.Clients;

namespace MMV.Api.Tests.Clients;

/// <summary>Règles D-03-01 à D-03-05 (validation serveur, recherche normalisée, cycle de vie). Données fictives.</summary>
public sealed class ClientRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    private static ClientInput Input(
        string? firstName = "Jeanne",
        string? lastName = "Martin",
        string? birthDate = null,
        string? phone = null,
        string? email = null,
        string? addressLine = null,
        string? city = null,
        string? postalCode = null,
        string? notes = null) =>
        new(firstName, lastName, birthDate, phone, email, addressLine, city, postalCode, notes);

    private static IReadOnlyDictionary<string, string[]> Errors(ClientInput input) => ClientInputValidator.Validate(input, Today).Errors;

    [Fact]
    public void OnlyFirstAndLastNameAreRequired_OptionalBlanksBecomeNull()
    {
        var result = ClientInputValidator.Validate(Input(firstName: "  Jeanne ", lastName: " Martin", phone: "  ", email: "", city: " "), Today);

        Assert.True(result.IsValid);
        Assert.Equal(new ClientDetails("Jeanne", "Martin", null, null, null, null, null, null, null), result.Details);
    }

    [Theory]
    [InlineData(null, "Martin", "firstName", "required")]
    [InlineData("   ", "Martin", "firstName", "required")]
    [InlineData("Jeanne", null, "lastName", "required")]
    [InlineData("Jeanne", "\t", "lastName", "required")]
    public void MissingNames_AreRequired(string? firstName, string? lastName, string field, string code)
    {
        Assert.Equal([code], Errors(Input(firstName: firstName, lastName: lastName))[field]);
    }

    [Fact]
    public void LengthLimits_AreEnforcedAfterTrimming()
    {
        var errors = Errors(Input(
            firstName: new string('a', 101),
            lastName: " " + new string('b', 100) + " ",
            addressLine: new string('c', 201),
            city: new string('d', 101),
            postalCode: new string('1', 11),
            notes: new string('e', 1001),
            email: new string('f', 250) + "@x.be"));

        Assert.Equal(["too_long"], errors["firstName"]);
        Assert.False(errors.ContainsKey("lastName"));
        Assert.Equal(["too_long"], errors["addressLine"]);
        Assert.Equal(["too_long"], errors["city"]);
        Assert.Equal(["too_long"], errors["postalCode"]);
        Assert.Equal(["too_long"], errors["notes"]);
        Assert.Equal(["too_long"], errors["email"]);
    }

    [Theory]
    [InlineData("0470 12 34 56", true)]
    [InlineData("+32 470.12.34.56", true)]
    [InlineData("02-123-45-67", true)]
    [InlineData("1234567", false)]
    [InlineData("1234567890123456", false)]
    [InlineData("04 70 ab 34 56", false)]
    [InlineData("32+470123456", false)]
    public void Phone_FreeFormatWith8To15Digits(string phone, bool valid)
    {
        Assert.Equal(valid, !Errors(Input(phone: phone)).ContainsKey("phone"));
    }

    [Theory]
    [InlineData("jeanne.martin@exemple.be", true)]
    [InlineData("j+tag@sous.exemple.org", true)]
    [InlineData("pas-un-email", false)]
    [InlineData("a@b", false)]
    [InlineData(".a@exemple.be", false)]
    [InlineData("a..b@exemple.be", false)]
    [InlineData("a b@exemple.be", false)]
    public void Email_FormatMatchesTheInterfaceRule(string email, bool valid)
    {
        Assert.Equal(valid, !Errors(Input(email: email)).ContainsKey("email"));
    }

    [Theory]
    [InlineData("1990-02-28", null)]
    [InlineData("1900-01-01", null)]
    [InlineData("2026-10-17", null)]
    [InlineData("1899-12-31", "out_of_range")]
    [InlineData("2026-10-18", "out_of_range")]
    [InlineData("1990-02-30", "invalid_format")]
    [InlineData("28/02/1990", "invalid_format")]
    [InlineData("1990-2-28", "invalid_format")]
    public void BirthDate_IsAPastCivilDateSince1900(string birthDate, string? code)
    {
        var errors = Errors(Input(birthDate: birthDate));

        if (code is null)
        {
            Assert.False(errors.ContainsKey("birthDate"));
        }
        else
        {
            Assert.Equal([code], errors["birthDate"]);
        }
    }

    [Theory]
    [InlineData("1000", true)]
    [InlineData("SW1A 1AA", true)]
    [InlineData("75-001", true)]
    [InlineData("-1000", false)]
    [InlineData("10_00", false)]
    public void PostalCode_LettersDigitsSpaceOrDash(string postalCode, bool valid)
    {
        Assert.Equal(valid, !Errors(Input(postalCode: postalCode)).ContainsKey("postalCode"));
    }

    [Fact]
    public void ControlCharacters_AreRefused_ExceptLineBreaksInNotes()
    {
        var errors = Errors(Input(firstName: "Jean\u0000ne", city: "Mons\u001b", notes: "Ligne 1\r\nLigne 2\tfin"));

        Assert.Equal(["invalid_format"], errors["firstName"]);
        Assert.Equal(["invalid_format"], errors["city"]);
        Assert.False(errors.ContainsKey("notes"));
    }

    [Theory]
    [InlineData("Éloïse", "eloise")]
    [InlineData("  LEFÈVRE   Dupont ", "lefevre dupont")]
    [InlineData("Ñúñez Çelik", "nunez celik")]
    [InlineData(null, "")]
    public void Fold_RemovesAccentsCaseAndExtraSpaces(string? value, string expected)
    {
        Assert.Equal(expected, ClientSearch.Fold(value));
    }

    [Fact]
    public void SearchText_ContainsFoldedNamesEmailAndPhoneDigits()
    {
        Assert.Equal(
            "eloise lefevre e.lefevre@exemple.be 32470123456",
            ClientSearch.BuildSearchText("Éloïse", "Lefèvre", "E.Lefevre@Exemple.be", "+32 470 12 34 56"));
        Assert.Equal("jeanne martin", ClientSearch.BuildSearchText("Jeanne", "Martin", null, null));
    }

    [Theory]
    [InlineData("Éloïse", new[] { "%eloise%" })]
    [InlineData("lefevre ELOISE lefevre", new[] { "%lefevre%", "%eloise%" })]
    [InlineData("0470 12", new[] { "%047012%" })]
    [InlineData("+32 470", new[] { "%32470%" })]
    [InlineData("100%_x\\", new[] { "%100\\%\\_x\\\\%" })]
    [InlineData("a1", new[] { "%a1%" })]
    [InlineData("  ", new string[0])]
    public void SearchPatterns_AllWordsOrPhoneDigits_WithEscapedWildcards(string search, string[] expected)
    {
        Assert.Equal(expected, ClientStore.SearchPatterns(search));
    }

    [Fact]
    public void Lifecycle_ArchiveReactivate_AndArchivedClientIsReadOnly()
    {
        var author = Guid.CreateVersion7();
        var created = new DateTimeOffset(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
        var client = Client.Create(Guid.CreateVersion7(), new ClientDetails("Jeanne", "Martin", null, null, null, null, null, null, null), author, created);
        Assert.False(client.IsArchived);
        Assert.Equal(created, client.UpdatedAtUtc);

        var archiver = Guid.CreateVersion7();
        client.Archive(archiver, created.AddHours(1));
        Assert.True(client.IsArchived);
        Assert.Equal(archiver, client.UpdatedByUserId);
        Assert.Throws<InvalidOperationException>(() => client.Archive(archiver, created.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => client.Update(
            new ClientDetails("Autre", "Nom", null, null, null, null, null, null, null), archiver, created.AddHours(2)));

        client.Reactivate(archiver, created.AddHours(3));
        Assert.False(client.IsArchived);
        Assert.Null(client.ArchivedAtUtc);
        Assert.Equal(author, client.CreatedByUserId);
        Assert.Throws<InvalidOperationException>(() => client.Reactivate(archiver, created.AddHours(4)));
    }
}
