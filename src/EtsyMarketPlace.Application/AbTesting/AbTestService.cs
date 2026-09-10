namespace EtsyMarketPlace.Application.AbTesting;

using EtsyMarketPlace.Application.BatchQueue;

public sealed class AbTestService
{
    private readonly IAbTestRepository _repository;
    private readonly IBatchQueueRepository? _batchQueueRepository;

    public AbTestService(IAbTestRepository repository, IBatchQueueRepository? batchQueueRepository = null)
    {
        _repository = repository;
        _batchQueueRepository = batchQueueRepository;
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

    public async Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _repository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Launches a batch of A/B test experiments from optimized queue items.
    /// </summary>
    public async Task<BulkAbTestLaunchResult> BulkStartExperimentsAsync(
        IReadOnlyList<BulkAbTestItemRequest> requests,
        BulkAbTestLaunchOptions options,
        Func<long, string, string, IReadOnlyList<string>, Task>? deployVariantBAction = null,
        IProgress<BulkAbTestLaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        options ??= new BulkAbTestLaunchOptions();

        var created = new List<ListingAbTestExperiment>();
        var errors = new List<string>();
        int successCount = 0;
        int failedCount = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var req = requests[i];
            try
            {
                var startDate = DateTimeOffset.Now;
                var endDate = startDate.AddDays(options.DurationDays);
                var expName = $"{options.ExperimentPrefix} #{req.ListingId} ({options.DurationDays} Gün)";

                var save = new SaveAbTestExperiment(
                    req.ListingId,
                    string.IsNullOrWhiteSpace(req.OriginalTitle) ? $"Listing #{req.ListingId}" : req.OriginalTitle,
                    expName,
                    req.OriginalTitle,
                    req.OptimizedTitle,
                    req.OriginalTags,
                    req.OptimizedTags,
                    req.OriginalDescription,
                    req.OptimizedDescription,
                    req.CurrentViews,
                    req.CurrentFavorites,
                    req.CurrentSales,
                    endDate);

                var exp = await StartExperimentAsync(save, cancellationToken);
                created.Add(exp);

                // Auto-deploy Variant B to Etsy if requested
                if (options.AutoDeployVariantBToEtsy && deployVariantBAction != null && long.TryParse(req.ListingId, out var parsedListingId))
                {
                    try
                    {
                        await deployVariantBAction(parsedListingId, req.OptimizedTitle, req.OptimizedDescription, req.OptimizedTags);
                    }
                    catch (Exception deployEx)
                    {
                        errors.Add($"#{req.ListingId} A/B kaydedildi ancak Etsy'ye aktarılamadı: {deployEx.Message}");
                    }
                }

                // Update Batch Queue item if repository exists
                if (_batchQueueRepository != null && req.BatchQueueItemId > 0)
                {
                    await _batchQueueRepository.UpdateAbTestStatusAsync(
                        req.BatchQueueItemId,
                        exp.Id,
                        $"Active ({options.DurationDays}g)",
                        cancellationToken);
                }

                successCount++;
                progress?.Report(new BulkAbTestLaunchProgress(i + 1, requests.Count, req.OriginalTitle, true, null));
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add($"#{req.ListingId}: {ex.Message}");
                progress?.Report(new BulkAbTestLaunchProgress(i + 1, requests.Count, req.OriginalTitle, false, ex.Message));
            }

            if (i < requests.Count - 1 && options.AutoDeployVariantBToEtsy)
            {
                await Task.Delay(200, cancellationToken);
            }
        }

        return new BulkAbTestLaunchResult(requests.Count, successCount, failedCount, created, errors);
    }

    /// <summary>
    /// Checks if an active experiment is suffering an abnormal drop in views or favorites (early warning).
    /// </summary>
    public AbTestSafetyAlert? CheckSafetyGuardrail(
        ListingAbTestExperiment experiment,
        double dropThresholdPercent = 40.0)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        if (experiment.Status != AbTestStatus.Active) return null;

        // Baseline must be meaningful (>= 10 views) to avoid false positives on low-traffic items
        if (experiment.BeforeViews >= 10 && experiment.AfterViews < experiment.BeforeViews)
        {
            var dropPct = Math.Round(((double)(experiment.BeforeViews - experiment.AfterViews) / experiment.BeforeViews) * 100.0, 1);
            if (dropPct >= dropThresholdPercent)
            {
                return new AbTestSafetyAlert(
                    experiment.Id,
                    experiment.ListingId,
                    experiment.ListingTitle,
                    dropPct,
                    $"Görüntülenmeler başlangıca göre %{dropPct} oranında sert düştü ({experiment.BeforeViews} ➔ {experiment.AfterViews}). Yeni başlık veya etiketler Etsy algoritmasında gerilemeye yol açmış olabilir.",
                    RecommendationRollback: true);
            }
        }

        return null;
    }

    /// <summary>
    /// Completes the experiment and optionally deploys the chosen winning variant to Etsy.
    /// </summary>
    public async Task<ListingAbTestExperiment> ResolveExperimentAsync(
        long experimentId,
        string chosenVariant, // "VariantB" (AI) or "VariantA" (Original)
        Func<long, string, string, IReadOnlyList<string>, Task>? deployToEtsyAction = null,
        CancellationToken cancellationToken = default)
    {
        var exp = await GetByIdAsync(experimentId, cancellationToken)
            ?? throw new InvalidOperationException($"A/B testi bulunamadı: #{experimentId}");

        if (deployToEtsyAction != null && long.TryParse(exp.ListingId, out var parsedListingId))
        {
            if (chosenVariant.Equals("VariantA", StringComparison.OrdinalIgnoreCase))
            {
                // Rollback to original
                await deployToEtsyAction(parsedListingId, exp.VariantA_Title, exp.VariantA_Description, exp.VariantA_Tags);
            }
            else
            {
                // Keep / Reapply Variant B
                await deployToEtsyAction(parsedListingId, exp.VariantB_Title, exp.VariantB_Description, exp.VariantB_Tags);
            }
        }

        var updated = await _repository.UpdateStatusAsync(experimentId, AbTestStatus.Completed, cancellationToken);
        return updated ?? exp with { Status = AbTestStatus.Completed, EndDate = DateTimeOffset.Now };
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
