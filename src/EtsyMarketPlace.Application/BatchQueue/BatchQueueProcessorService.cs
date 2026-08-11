namespace EtsyMarketPlace.Application.BatchQueue;

using EtsyMarketPlace.Application.ListingOptimization;

public sealed class BatchQueueProcessorService
{
    private readonly IBatchQueueRepository _repository;
    private readonly IAiListingOptimizer _aiOptimizer;
    private readonly ListingDraftValidator _validator = new();
    private readonly ListingDraftRepairService _repairService = new();

    public BatchQueueProcessorService(
        IBatchQueueRepository repository,
        IAiListingOptimizer aiOptimizer)
    {
        _repository = repository;
        _aiOptimizer = aiOptimizer;
    }

    public async Task<IReadOnlyList<BatchQueueItem>> EnqueueAsync(
        IReadOnlyList<SaveBatchQueueItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return [];
        return await _repository.EnqueueBatchAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(limit, cancellationToken);
    }

    public async Task<BatchQueueSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(500, cancellationToken);
        return CalculateSummary(items, "Hazir");
    }

    public async Task ProcessQueueAsync(
        IProgress<BatchQueueSummary>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var pendingItems = await _repository.GetPendingAsync(200, cancellationToken);
        if (pendingItems.Count == 0)
        {
            var summary = await GetSummaryAsync(cancellationToken);
            progress?.Report(summary with { StatusMessage = "Kuyrukta islenecek bekleyen urun yok." });
            return;
        }

        var allItems = (await _repository.GetAllAsync(500, cancellationToken)).ToList();
        progress?.Report(CalculateSummary(allItems, $"Toplu optimizasyon baslatildi ({pendingItems.Count} urun)..."));

        foreach (var item in pendingItems)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                progress?.Report(CalculateSummary(allItems, "Toplu optimizasyon kullanici tarafindan durduruldu."));
                break;
            }

            // Mark processing
            var processingItem = item with { Status = BatchQueueItemStatus.Processing };
            await _repository.UpdateItemAsync(processingItem, cancellationToken);
            UpdateInList(allItems, processingItem);
            progress?.Report(CalculateSummary(allItems, $"Isleniyor: {item.OriginalTitle[..Math.Min(30, item.OriginalTitle.Length)]}..."));

            try
            {
                var input = new ListingOptimizationInput(
                    item.OriginalTitle,
                    item.OriginalDescription,
                    item.OriginalTags,
                    item.TargetKeyword);

                var aiResult = await _aiOptimizer.OptimizeAsync(input, cancellationToken);
                var materials = aiResult.MaterialSuggestions.Take(13).ToList();
                var title = aiResult.TitleSuggestions.FirstOrDefault() ?? item.OriginalTitle;
                var tags = aiResult.TagSuggestions.Take(13).ToList();
                var desc = !string.IsNullOrWhiteSpace(aiResult.DescriptionDraft) ? aiResult.DescriptionDraft : item.OriginalDescription;

                var validationReport = _validator.Validate(new ListingDraftValidationInput(
                    title,
                    desc,
                    tags,
                    materials,
                    item.Category,
                    item.TargetKeyword));

                var decision = _repairService.Evaluate(validationReport);

                // Auto-repair loop if score < 75
                if (decision.NeedsRepair)
                {
                    for (var attempt = 0; attempt < ListingDraftRepairService.MaxRepairIterations && decision.NeedsRepair; attempt++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var repairPrompt = ListingDraftRepairService.BuildRepairPrompt(
                            decision,
                            title,
                            desc,
                            tags,
                            materials,
                            item.TargetKeyword);

                        var repairInput = new ListingOptimizationInput(
                            title,
                            repairPrompt,
                            tags,
                            item.TargetKeyword);

                        try
                        {
                            var repairResult = await _aiOptimizer.OptimizeAsync(repairInput, cancellationToken);
                            if (repairResult.TitleSuggestions.Count > 0) title = repairResult.TitleSuggestions[0];
                            if (repairResult.TagSuggestions.Count > 0) tags = repairResult.TagSuggestions.Take(13).ToList();
                            if (!string.IsNullOrWhiteSpace(repairResult.DescriptionDraft)) desc = repairResult.DescriptionDraft;
                            if (repairResult.MaterialSuggestions.Count > 0) materials = repairResult.MaterialSuggestions.Take(13).ToList();
                        }
                        catch
                        {
                            break;
                        }

                        validationReport = _validator.Validate(new ListingDraftValidationInput(
                            title, desc, tags, materials, item.Category, item.TargetKeyword));
                        decision = _repairService.Evaluate(validationReport);
                    }
                }

                var isRisk = validationReport.Risk.RiskLevel != "Dusuk";
                var finalStatus = isRisk ? BatchQueueItemStatus.RiskWarning : BatchQueueItemStatus.Completed;

                var completedItem = item with
                {
                    Status = finalStatus,
                    OverallScore = validationReport.OverallScore,
                    OptimizedTitle = title,
                    OptimizedDescription = desc,
                    OptimizedTags = tags,
                    OptimizedMaterials = materials,
                    RiskWarnings = validationReport.Risk.DetectedTerms.Count > 0 ? validationReport.Risk.DetectedTerms : aiResult.RiskWarnings,
                    Issues = validationReport.Issues,
                    ProcessedAt = DateTimeOffset.Now,
                    ErrorMessage = null,
                };

                await _repository.UpdateItemAsync(completedItem, cancellationToken);
                UpdateInList(allItems, completedItem);
            }
            catch (Exception ex)
            {
                var failedItem = item with
                {
                    Status = BatchQueueItemStatus.Failed,
                    ProcessedAt = DateTimeOffset.Now,
                    ErrorMessage = ex.Message,
                };
                await _repository.UpdateItemAsync(failedItem, cancellationToken);
                UpdateInList(allItems, failedItem);
            }

            progress?.Report(CalculateSummary(allItems, "Toplu optimizasyon devam ediyor..."));
        }

        progress?.Report(CalculateSummary(allItems, "Toplu optimizasyon tamamlandi!"));
    }

    public async Task ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        await _repository.ClearCompletedAsync(cancellationToken);
    }

    private static BatchQueueSummary CalculateSummary(IReadOnlyList<BatchQueueItem> items, string statusMessage)
    {
        var total = items.Count;
        if (total == 0)
        {
            return new BatchQueueSummary(0, 0, 0, 0, 0, 0, 0.0, 100.0, statusMessage);
        }

        var pending = items.Count(i => i.Status == BatchQueueItemStatus.Pending);
        var processing = items.Count(i => i.Status == BatchQueueItemStatus.Processing);
        var completed = items.Count(i => i.Status == BatchQueueItemStatus.Completed);
        var risk = items.Count(i => i.Status == BatchQueueItemStatus.RiskWarning);
        var failed = items.Count(i => i.Status == BatchQueueItemStatus.Failed);

        var processedCount = completed + risk + failed;
        var pct = Math.Round(((double)processedCount / total) * 100.0, 1);

        var completedWithScore = items.Where(i => i.Status is BatchQueueItemStatus.Completed or BatchQueueItemStatus.RiskWarning).ToList();
        var avgScore = completedWithScore.Count > 0 ? Math.Round(completedWithScore.Average(i => i.OverallScore), 1) : 0.0;

        return new BatchQueueSummary(total, pending, processing, completed, risk, failed, avgScore, pct, statusMessage);
    }

    private static void UpdateInList(List<BatchQueueItem> list, BatchQueueItem updated)
    {
        var idx = list.FindIndex(i => i.Id == updated.Id);
        if (idx >= 0) list[idx] = updated;
    }
}
