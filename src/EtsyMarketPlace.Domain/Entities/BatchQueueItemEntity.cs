namespace EtsyMarketPlace.Domain.Entities;

using System;

public class BatchQueueItemEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ListingId { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public string OriginalDescription { get; set; } = string.Empty;
    public string OriginalTags { get; set; } = "[]";
    public string TargetKeyword { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int OverallScore { get; set; }
    public string OptimizedTitle { get; set; } = string.Empty;
    public string OptimizedDescription { get; set; } = string.Empty;
    public string OptimizedTags { get; set; } = "[]";
    public string OptimizedMaterials { get; set; } = "[]";
    public string RiskWarnings { get; set; } = "[]";
    public string Issues { get; set; } = "[]";
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSyncedToEtsy { get; set; }
    public DateTime? SyncedAt { get; set; }
    public long? AbTestExperimentId { get; set; }
    public string? AbTestStatus { get; set; }
}
