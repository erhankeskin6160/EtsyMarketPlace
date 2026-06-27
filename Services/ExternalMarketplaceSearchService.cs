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
        return Sources
            .Where(source => enabledSources.Count == 0 || enabledSources.Contains(source.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(source => BuildSourceIdeas(source, query, shopType, cleanKeyword))
            .OrderByDescending(idea => idea.OpportunityScore)
            .ToList();
    }

    private static IReadOnlyList<ExternalProductIdea> BuildSourceIdeas(
        ExternalSource source,
        string query,
        string shopType,
        string keyword)
    {
        var variants = new[]
        {
            new QueryVariant(query, "Genel urun aramasi", 0),
            new QueryVariant($"{query} best seller", "Cok satan sinyali aramasi", 4),
            new QueryVariant($"{query} handmade", "El yapimi/Etsy uyumu aramasi", 3),
        };

        return variants
            .Select(variant =>
            {
                var encoded = Uri.EscapeDataString(variant.Query);
                var url = string.Format(source.SearchUrlFormat, encoded);
                return new ExternalProductIdea
                {
                    Source = source.Name,
                    Title = $"{keyword.Trim()} - {source.Name} {variant.Label}",
                    SearchUrl = url,
                    ProductUrl = url,
                    OpportunityScore = Math.Clamp(source.BaseScore + variant.Bonus + ShopTypeBonus(shopType, source.Name), 0, 100),
                    Notes = $"{variant.Label}. Kaynakta ac, uygun urunu incele, sonra dis kaynak basligi/fiyat/link alanlarini elle netlestir. Otomatik kazima yerine onayli arastirma akisi kullanilir.",
                };
            })
            .ToList();
    }

    private static int ShopTypeBonus(string shopType, string sourceName)
    {
        var text = shopType.ToLowerInvariant();
        if ((text.Contains("3d") || text.Contains("cosplay") || text.Contains("prop")) &&
            sourceName.Contains("eBay", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if ((text.Contains("yatak") || text.Contains("bed")) &&
            (sourceName.Contains("Trendyol", StringComparison.OrdinalIgnoreCase) ||
             sourceName.Contains("Hepsiburada", StringComparison.OrdinalIgnoreCase)))
        {
            return 5;
        }

        return 0;
    }

    private static string BuildQuery(string shopType, string keyword)
    {
        var parts = new[] { shopType.Trim(), keyword.Trim(), "product idea" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(' ', parts);
    }

    private sealed record ExternalSource(string Name, string SearchUrlFormat, int BaseScore);

    private sealed record QueryVariant(string Query, string Label, int Bonus);
}
