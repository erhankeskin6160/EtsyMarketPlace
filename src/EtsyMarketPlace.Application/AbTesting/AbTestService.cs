namespace EtsyMarketPlace.Application.AbTesting;

public sealed class AbTestService
{
    private readonly IAbTestRepository _repository;

    public AbTestService(IAbTestRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListingAbTestExperiment> StartExperimentAsync(
        SaveAbTestExperiment experiment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        if (string.IsNullOrWhiteSpace(experiment.ListingTitle))
        {
            throw new ArgumentException("Listing basligi bos olamaz.", nameof(experiment));
        }

        return await _repository.SaveAsync(experiment, cancellationToken);
    }

    public async Task<ListingAbTestExperiment?> UpdateMetricsAsync(
        UpdateAbTestMetrics update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return await _repository.UpdateMetricsAsync(update, cancellationToken);
    }

    public async Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetRecentAsync(limit, cancellationToken);
    }

    public async Task<ListingAbTestExperiment?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Generates a performance impact report evaluating metric changes and determining the winning variant.
    /// </summary>
    public AbTestImpactReport CalculateImpact(ListingAbTestExperiment experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);

        var viewsDiff = experiment.AfterViews - experiment.BeforeViews;
        var viewsPct = CalculatePercentChange(experiment.BeforeViews, experiment.AfterViews);

        var favsDiff = experiment.AfterFavorites - experiment.BeforeFavorites;
        var favsPct = CalculatePercentChange(experiment.BeforeFavorites, experiment.AfterFavorites);

        var salesDiff = experiment.AfterSales - experiment.BeforeSales;
        var salesPct = CalculatePercentChange(experiment.BeforeSales, experiment.AfterSales);

        var (winner, summary, takeaways) = DetermineWinner(
            viewsDiff, viewsPct,
            favsDiff, favsPct,
            salesDiff, salesPct,
            experiment.Status);

        return new AbTestImpactReport(
            experiment.Id,
            experiment.ExperimentName,
            experiment.ListingTitle,
            experiment.Status,
            experiment.BeforeViews,
            experiment.AfterViews,
            viewsPct,
            experiment.BeforeFavorites,
            experiment.AfterFavorites,
            favsPct,
            experiment.BeforeSales,
            experiment.AfterSales,
            salesPct,
            winner,
            summary,
            takeaways);
    }

    private static double CalculatePercentChange(int before, int after)
    {
        if (before == 0)
        {
            return after > 0 ? 100.0 : 0.0;
        }

        return Math.Round(((double)(after - before) / before) * 100.0, 1);
    }

    private static (string Winner, string Summary, List<string> Takeaways) DetermineWinner(
        int viewsDiff, double viewsPct,
        int favsDiff, double favsPct,
        int salesDiff, double salesPct,
        AbTestStatus status)
    {
        var takeaways = new List<string>();

        if (salesDiff > 0)
        {
            takeaways.Add($"Satislar +{salesDiff} adet (%{salesPct:+#;-#;0}) artti.");
        }
        else if (salesDiff < 0)
        {
            takeaways.Add($"Satislar {salesDiff} adet (%{salesPct:+#;-#;0}) dustu.");
        }

        if (favsDiff > 0)
        {
            takeaways.Add($"Favoriler +{favsDiff} adet (%{favsPct:+#;-#;0}) artti.");
        }
        else if (favsDiff < 0)
        {
            takeaways.Add($"Favoriler {favsDiff} adet (%{favsPct:+#;-#;0}) dustu.");
        }

        if (viewsDiff > 0)
        {
            takeaways.Add($"Goruntulenme +{viewsDiff} adet (%{viewsPct:+#;-#;0}) artti.");
        }
        else if (viewsDiff < 0)
        {
            takeaways.Add($"Goruntulenme {viewsDiff} adet (%{viewsPct:+#;-#;0}) dustu.");
        }

        // Weighted score calculation to decide winner
        // Sales weight: 5, Favs weight: 2, Views weight: 1
        var score = (salesDiff * 5.0) + (favsDiff * 2.0) + (viewsDiff * 0.5);

        if (score >= 3.0)
        {
            var winner = "Varyant B (Yeni AI Optimizasyonu)";
            var summary = "Varyant B belirgin sekilde daha yüksek performans gosterdi. Yeni baslik ve tag stratejisi kalici olarak uygulanmali.";
            if (takeaways.Count == 0) takeaways.Add("Metrikler olumlu yonlu ilerliyor.");
            return (winner, summary, takeaways);
        }

        if (score <= -3.0)
        {
            var winner = "Varyant A (Orijinal Listing)";
            var summary = "Orijinal Varyant A daha iyi performans gostermisti. Yapilan degisiklikler geri alinmali veya yeni bir optimizasyon denenmeli.";
            if (takeaways.Count == 0) takeaways.Add("Metriklerde dusus gozlemlendi.");
            return (winner, summary, takeaways);
        }

        var inconclusiveWinner = "Karsilastirilabilir (Nötr)";
        var inconclusiveSummary = "Henüz belirgin bir kazanan yok. Testi devam ettirerek daha fazla veri toplanmasi önerilir.";
        if (takeaways.Count == 0) takeaways.Add("Henüz yeterli metrik degisimi kaydedilmedi.");
        return (inconclusiveWinner, inconclusiveSummary, takeaways);
    }
}
