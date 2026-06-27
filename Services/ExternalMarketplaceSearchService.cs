namespace SimilarProductsWinForms.Services;

using SimilarProductsWinForms.Models;

internal sealed class ExternalMarketplaceSearchService
{
    private static readonly IReadOnlyList<ExternalSource> Sources =
    [
        new("Google Shopping", "https://www.google.com/search?tbm=shop&q={0}", 78),
        new("Trendyol", "https://www.trendyol.com/sr?q={0}", 72),
        new("Hepsiburada", "https://www.hepsiburada.com/ara?q={0}", 70),
        new("eBay", "https://www.ebay.com/sch/i.html?_nkw={0}", 76),
        new("Google Web", "https://www.google.com/search?q={0}", 68),
    ];

    public IReadOnlyList<string> SourceNames => Sources.Select(source => source.Name).ToList();

    public IReadOnlyList<ExternalProductIdea> BuildSearchIdeas(
        string shopType,
        string keyword,
        IReadOnlyCollection<string> enabledSources)
    {
        var cleanKeyword = string.IsNullOrWhiteSpace(keyword) ? shopType : keyword;
        if (string.IsNullOrWhiteSpace(cleanKeyword))
        {
            return [];
        }

        var query = BuildQuery(shopType, cleanKeyword);
        var encoded = Uri.EscapeDataString(query);
        return Sources
            .Where(source => enabledSources.Count == 0 || enabledSources.Contains(source.Name, StringComparer.OrdinalIgnoreCase))
            .Select(source => new ExternalProductIdea
            {
                Source = source.Name,
                Title = $"{query} icin {source.Name} aramasi",
                SearchUrl = string.Format(source.SearchUrlFormat, encoded),
                ProductUrl = string.Format(source.SearchUrlFormat, encoded),
                OpportunityScore = source.BaseScore,
                Notes = "Kaynakta ac, uygun urunu incele, sonra baslik/fiyat/link alanlarini elle netlestir. Otomatik kazima yerine onayli arastirma akisi kullanilir.",
            })
            .ToList();
    }

    private static string BuildQuery(string shopType, string keyword)
    {
        var parts = new[] { shopType.Trim(), keyword.Trim(), "product idea" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(' ', parts);
    }

    private sealed record ExternalSource(string Name, string SearchUrlFormat, int BaseScore);
}
