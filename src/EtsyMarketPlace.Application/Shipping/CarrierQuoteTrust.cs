namespace EtsyMarketPlace.Application.Shipping;

using System;

/// <summary>
/// Kargo teklifinin kaynak güvenilirliği.
/// </summary>
public enum QuoteSource
{
    /// <summary>Sağlayıcıdan canlı olarak alınmış teklif.</summary>
    Live,

    /// <summary>Yedek/referans tarife; fiyat tahminidir.</summary>
    Estimated
}

/// <summary>
/// Teklifin canlı mı yoksa tahmini (yedek) mi olduğunu belirler ve
/// kullanıcıya gösterilecek rozet metnini üretir.
/// Kural: emin olunmayan durumda teklif asla "canlı" gösterilmez.
/// </summary>
public static class CarrierQuoteTrust
{
    public const string LiveBadgeText = "Canlı";
    public const string EstimatedBadgeText = "Tahmini tarife";

    private static readonly string[] EstimatedMarkers =
    {
        "Referans",
        "Yedek",
        "Simüle",
        "Simule",
        "Liste Fiyat",
        "Önbellek",
        "Onbellek"
    };

    private static readonly string[] LiveMarkers =
    {
        "Canlı",
        "Canli"
    };

    /// <summary>
    /// <paramref name="authoritativeIsLive"/> sağlayıcının kendi canlılık alanıdır (varsa).
    /// Yoksa <paramref name="providerNote"/> içindeki metin işaretlerine bakılır.
    /// </summary>
    public static QuoteSource Classify(bool? authoritativeIsLive, string? providerNote)
    {
        if (authoritativeIsLive.HasValue)
        {
            return authoritativeIsLive.Value ? QuoteSource.Live : QuoteSource.Estimated;
        }

        string note = providerNote ?? string.Empty;

        foreach (string marker in EstimatedMarkers)
        {
            if (note.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return QuoteSource.Estimated;
            }
        }

        foreach (string marker in LiveMarkers)
        {
            if (note.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return QuoteSource.Live;
            }
        }

        return QuoteSource.Estimated;
    }

    public static bool IsLive(QuoteSource source) => source == QuoteSource.Live;

    public static string ToBadgeText(QuoteSource source)
        => source == QuoteSource.Live ? LiveBadgeText : EstimatedBadgeText;
}
