namespace SimilarProductsWinForms.Models;

using System;
using EtsyMarketPlace.Application.ListingOptimization;

/// <summary>
/// Yapay zeka ile denetlenen bir listingin kalıcı olarak saklanan SEO denetim kaydı.
/// Program kapatılıp açılsa bile skorlar, AI eleştirisi ve tespit edilen eksikler korunur.
/// </summary>
public sealed record SavedListingAuditData(
    long ListingId,
    string Title,
    int CurrentSeoScore,
    int OptimizedSeoScore,
    string Status,
    string ExecutedProvider,
    string ExecutedModel,
    DateTimeOffset AuditedAt,
    string SeoCritique,
    string DetailedNeeds,
    ListingOptimizationResult Result
);
