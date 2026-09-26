namespace EtsyMarketPlace.Application.Tests;

using Xunit;
using EtsyMarketPlace.Application.Shipping;

public sealed class PackageMeasurementAndQuoteTrustTests
{
    [Fact]
    public void Evaluate_ComputesDesiWith5000Divisor()
    {
        var result = PackageMeasurementCalculator.Evaluate(0.60, 15, 20, 10);

        Assert.True(result.IsValid);
        Assert.Equal(0.60, result.Desi, 2);
        Assert.Equal(0.60, result.BillableWeightKg, 2);
    }

    [Fact]
    public void Evaluate_UsesGreaterOfWeightAndDesi()
    {
        // 30 x 30 x 30 / 5000 = 5.40 desi, agirlik 2.00 -> faturalandirilacak 5.40
        var result = PackageMeasurementCalculator.Evaluate(2.00, 30, 30, 30);

        Assert.True(result.IsValid);
        Assert.Equal(5.40, result.Desi, 2);
        Assert.Equal(5.40, result.BillableWeightKg, 2);
    }

    [Fact]
    public void Evaluate_UsesWeightWhenWeightExceedsDesi()
    {
        var result = PackageMeasurementCalculator.Evaluate(7.50, 10, 10, 10);

        Assert.True(result.IsValid);
        Assert.Equal(0.20, result.Desi, 2);
        Assert.Equal(7.50, result.BillableWeightKg, 2);
    }

    [Theory]
    [InlineData(0, 15, 20, 10)]
    [InlineData(-1, 15, 20, 10)]
    public void Evaluate_RejectsNonPositiveWeight(double weight, double width, double length, double height)
    {
        var result = PackageMeasurementCalculator.Evaluate(weight, width, length, height);

        Assert.False(result.IsValid);
        Assert.True(result.HasWeightError);
        Assert.Equal(0, result.Desi, 2);
        Assert.Equal(0, result.BillableWeightKg, 2);
    }

    [Theory]
    [InlineData(0.5, 0, 20, 10)]
    [InlineData(0.5, 15, 0, 10)]
    [InlineData(0.5, 15, 20, -5)]
    public void Evaluate_RejectsNonPositiveDimension(double weight, double width, double length, double height)
    {
        var result = PackageMeasurementCalculator.Evaluate(weight, width, length, height);

        Assert.False(result.IsValid);
        Assert.True(result.HasDimensionError);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Evaluate_RejectsOversizedValues()
    {
        var tooHeavy = PackageMeasurementCalculator.Evaluate(500, 15, 20, 10);
        Assert.False(tooHeavy.IsValid);
        Assert.True(tooHeavy.HasWeightError);

        var tooBig = PackageMeasurementCalculator.Evaluate(1, 400, 20, 10);
        Assert.False(tooBig.IsValid);
        Assert.True(tooBig.HasDimensionError);
    }

    [Fact]
    public void Evaluate_DoesNotSilentlyFallBackToDefaults()
    {
        // Eski davranis: 0/negatif degerler sessizce 20/15/10/0.4'e dusuruluyordu.
        var result = PackageMeasurementCalculator.Evaluate(0, 0, 0, 0);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.BillableWeightKg, 2);
        Assert.True(result.Errors.Count >= 4);
    }

    [Fact]
    public void Classify_UsesProviderFlagWhenAvailable()
    {
        Assert.Equal(QuoteSource.Live, CarrierQuoteTrust.Classify(true, null));
        Assert.Equal(QuoteSource.Estimated, CarrierQuoteTrust.Classify(false, "Canlı Entegrasyon"));
    }

    [Fact]
    public void Classify_DetectsFallbackNotesAsEstimated()
    {
        Assert.Equal(QuoteSource.Estimated, CarrierQuoteTrust.Classify(null, "Navlungo Portalı Referans Fiyatı"));
        Assert.Equal(QuoteSource.Estimated, CarrierQuoteTrust.Classify(null, "Shiptomore Üye İndirimi (Simüle)"));
        Assert.Equal(QuoteSource.Estimated, CarrierQuoteTrust.Classify(null, "Yedek tarife"));
    }

    [Fact]
    public void Classify_DetectsLiveNotes()
    {
        Assert.Equal(QuoteSource.Live, CarrierQuoteTrust.Classify(null, "Navlungo API Canlı Teklif"));
        Assert.Equal(QuoteSource.Live, CarrierQuoteTrust.Classify(null, "Navlungo Üye İndirimli (Canlı)"));
    }

    [Fact]
    public void Classify_UnknownNoteIsNeverReportedAsLive()
    {
        Assert.Equal(QuoteSource.Estimated, CarrierQuoteTrust.Classify(null, "belirsiz saglayici notu"));
        Assert.Equal(QuoteSource.Estimated, CarrierQuoteTrust.Classify(null, null));
    }

    [Fact]
    public void BadgeText_IsTurkishAndExplicit()
    {
        Assert.Equal("Canlı", CarrierQuoteTrust.ToBadgeText(QuoteSource.Live));
        Assert.Equal("Tahmini tarife", CarrierQuoteTrust.ToBadgeText(QuoteSource.Estimated));
    }
}
