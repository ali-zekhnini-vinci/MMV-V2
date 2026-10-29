using MMV.Application.Optical;
using MMV.Domain.Optical;

namespace MMV.Api.Tests.Optical;

/// <summary>
/// Forme des saisies et critères de confirmation (orientations produit APPROUVÉES, validation professionnelle ouverte).
/// Données fictives uniquement.
/// </summary>
public sealed class OpticalRulesTests
{
    internal static PrescriptionInput Prescription(EyeInput? od = null, EyeInput? og = null, string? prescriber = "Dr Fictif", string? prescribedOn = "2026-10-01", string? notes = null) =>
        new(new PrescriptionProvenanceInput(prescriber, null, null, prescribedOn, null), od, og, notes);

    internal static MeasurementInput Measurement(Guid? measuredBy, EyeInput? od = null, EyeInput? og = null, string? measuredOn = "2026-10-02") =>
        new(new MeasurementProvenanceInput(measuredOn, measuredBy, null), od, og, null);

    private static PrescriptionContent Valid(PrescriptionInput input) => OpticalInputValidator.Validate(input).Content!;

    [Theory]
    [InlineData("+1,25")]
    [InlineData("-0.50")]
    [InlineData("−2,00")]
    [InlineData("- 3,75")]
    [InlineData("0,00")]
    [InlineData("12")]
    [InlineData("1.250")]
    public void Validate_NumberShapes_AreKeptExactly(string value)
    {
        var result = OpticalInputValidator.Validate(Prescription(new EyeInput(Sphere: value)));

        Assert.True(result.IsValid);
        Assert.Equal(value, result.Content!.Values.Od.Sphere);
    }

    [Theory]
    [InlineData("1e3")]
    [InlineData("1,2,3")]
    [InlineData("abc")]
    [InlineData("+")]
    [InlineData(".5")]
    [InlineData("1 000")]
    [InlineData("١٢")]
    public void Validate_NonNumbers_AreRefusedWithoutEchoingTheValue(string value)
    {
        var result = OpticalInputValidator.Validate(Prescription(new EyeInput(Cylinder: value)));

        Assert.False(result.IsValid);
        Assert.Equal([OpticalFieldErrors.NumberExpected], result.Errors["od.cylinder"]);
        Assert.DoesNotContain(result.Errors.SelectMany(e => e.Value), code => code.Contains(value, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EmptyIsAbsent_NeverZero_AndOtherEyeStaysEmpty()
    {
        var result = OpticalInputValidator.Validate(Prescription(new EyeInput(Sphere: "  ", Axis: ""), og: null));

        Assert.True(result.IsValid);
        Assert.True(result.Content!.Values.Od.IsEmpty);
        Assert.True(result.Content.Values.Og.IsEmpty);
        Assert.Null(result.Content.Values.Od.Sphere);
    }

    [Fact]
    public void Validate_BaseAndAcuity_AreFreeShortText()
    {
        var result = OpticalInputValidator.Validate(Prescription(new EyeInput(Prism: "1", Base: "BI", Acuity: "10/10")));

        Assert.True(result.IsValid);
        Assert.Equal("BI", result.Content!.Values.Od.Base);
        Assert.Equal("10/10", result.Content.Values.Od.Acuity);
        Assert.False(OpticalInputValidator.Validate(Prescription(new EyeInput(Acuity: new string('9', OpticalFields.ValueMaxLength + 1)))).IsValid);
    }

    [Theory]
    [InlineData("2026-13-01", OpticalFieldErrors.InvalidFormat)]
    [InlineData("01/10/2026", OpticalFieldErrors.InvalidFormat)]
    [InlineData("1899-12-31", OpticalFieldErrors.OutOfRange)]
    public void Validate_Dates_AreCivilDatesWithoutTimeZone(string value, string code)
    {
        var result = OpticalInputValidator.Validate(Prescription(prescribedOn: value));

        Assert.Equal([code], result.Errors["provenance.prescribedOn"]);
    }

    [Fact]
    public void Validate_DraftAcceptsMissingProvenance()
    {
        var result = OpticalInputValidator.Validate(Prescription(prescriber: null, prescribedOn: null));

        Assert.True(result.IsValid);
        Assert.Null(result.Content!.Provenance.PrescriberName);
    }

    [Fact]
    public void Confirm_OneEyeSuffices()
    {
        Assert.Empty(OpticalConfirmationRules.Check(Valid(Prescription(og: new EyeInput(Sphere: "-1,00")))));
        Assert.Empty(OpticalConfirmationRules.Check(Valid(Prescription(od: new EyeInput(Sphere: "+0,75")))));
    }

    [Fact]
    public void Confirm_NoValue_IsIncomplete()
    {
        var errors = OpticalConfirmationRules.Check(Valid(Prescription()));

        Assert.Equal([OpticalFieldErrors.OpticalValuesMissing], errors[OpticalFieldErrors.Correction]);
    }

    [Theory]
    [InlineData("0,00")]
    [InlineData("0")]
    [InlineData("+0.00")]
    [InlineData("-0,00")]
    public void Confirm_SingleSphereZeroAlone_IsIncomplete(string zero)
    {
        var errors = OpticalConfirmationRules.Check(Valid(Prescription(new EyeInput(Sphere: zero))));

        Assert.Equal([OpticalFieldErrors.SphereZeroOnly], errors[OpticalFieldErrors.Correction]);
    }

    [Fact]
    public void Confirm_SphereZeroWithOtherInformation_IsAccepted_AndZeroIsKept()
    {
        var content = Valid(Prescription(new EyeInput(Sphere: "0,00", Addition: "+2,00")));

        Assert.Empty(OpticalConfirmationRules.Check(content));
        Assert.Equal("0,00", content.Values.Od.Sphere);
        Assert.Empty(OpticalConfirmationRules.Check(Valid(Prescription(new EyeInput(Sphere: "0,00"), new EyeInput(Sphere: "0,00")))));
    }

    [Fact]
    public void Confirm_AcuityAlone_DependsOnTheKind()
    {
        var measurer = Guid.CreateVersion7();
        var prescription = OpticalConfirmationRules.Check(Valid(Prescription(new EyeInput(Acuity: "10/10"), new EyeInput(Acuity: "8/10"))));
        var measurement = OpticalConfirmationRules.Check(OpticalInputValidator.Validate(Measurement(measurer, new EyeInput(Acuity: "10/10"))).Content!);

        Assert.Equal([OpticalFieldErrors.AcuityOnly], prescription[OpticalFieldErrors.Correction]);
        Assert.Empty(measurement);
    }

    [Theory]
    [InlineData("-0,75", null, true)]
    [InlineData("-0,75", "0", false)]
    [InlineData("-0,75", "180", false)]
    [InlineData("0,00", null, false)]
    [InlineData(null, "90", false)]
    public void Confirm_NonZeroCylinder_RequiresAxis_AndAxisIsNeverErased(string? cylinder, string? axis, bool refused)
    {
        var content = Valid(Prescription(new EyeInput(Sphere: "-1,00", Cylinder: cylinder, Axis: axis)));
        var errors = OpticalConfirmationRules.Check(content);

        Assert.Equal(refused, errors.ContainsKey("od.axis"));
        Assert.Equal(axis, content.Values.Od.Axis);
    }

    [Theory]
    [InlineData("1,5", null, true)]
    [InlineData("1,5", "BE", false)]
    [InlineData("0", null, false)]
    public void Confirm_NonZeroPrism_RequiresBase(string prism, string? @base, bool refused)
    {
        var errors = OpticalConfirmationRules.Check(Valid(Prescription(og: new EyeInput(Sphere: "+1,00", Prism: prism, Base: @base))));

        Assert.Equal(refused, errors.TryGetValue("og.base", out var codes) && codes.SequenceEqual([OpticalFieldErrors.BaseRequired]));
    }

    [Fact]
    public void Confirm_ProvenanceRequired_PerKind()
    {
        var prescription = OpticalConfirmationRules.Check(Valid(Prescription(new EyeInput(Sphere: "-1"), prescriber: null, prescribedOn: null)));
        var measurement = OpticalConfirmationRules.Check(OpticalInputValidator.Validate(Measurement(null, new EyeInput(Sphere: "-1"), measuredOn: null)).Content!);

        Assert.Equal(["provenance.prescribedOn", "provenance.prescriberName"], prescription.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["provenance.measuredByUserId", "provenance.measuredOn"], measurement.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Decimal_IsExact_WithoutRounding()
    {
        Assert.Equal(-2.125m, OpticalFields.ToDecimal("−2,125"));
        Assert.Equal(0.000000000000001m, OpticalFields.ToDecimal("0,000000000000001"));
    }

    [Fact]
    public void Reason_OtherRequiresComment()
    {
        var errors = new Dictionary<string, string[]>();
        OpticalInputValidator.Reason("other", " ", OpticalReasons.Rectification, errors);
        Assert.Equal([OpticalFieldErrors.Required], errors["comment"]);

        errors.Clear();
        OpticalInputValidator.Reason("unknown_reason", null, OpticalReasons.Invalidation, errors);
        Assert.Equal([OpticalFieldErrors.InvalidFormat], errors["reasonCode"]);
    }
}
