namespace EtsyMarketPlace.Infrastructure.ExternalMarketplaces;

using EtsyMarketPlace.Application.ExternalMarketplaces;

public abstract class SearchUrlMarketplaceProvider : IExternalMarketplaceProvider
{
    public abstract string Name { get; }

    protected abstract string SearchUrlFormat { get; }

    protected abstract int BaseScore { get; }

    public virtual IReadOnlyList<MarketplaceProduct> Search(ExternalMarketplaceSearchContext context) =>
        Variants(context)
            .Select(variant => ToProduct(context, variant))
            .ToList();

    protected abstract IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context);

    private MarketplaceProduct ToProduct(ExternalMarketplaceSearchContext context, QueryVariant variant)
    {
        var encoded = Uri.EscapeDataString(variant.Query);
        var searchUrl = string.Format(SearchUrlFormat, encoded);
        var category = string.IsNullOrWhiteSpace(variant.CategoryHint)
            ? ExternalMarketplaceOpportunityService.InferCategory(context.ShopType, context.Keyword)
            : variant.CategoryHint;
        var tags = ExternalMarketplaceOpportunityService.BuildTags(context.ShopType, context.Keyword, variant.Query);

        return new MarketplaceProduct(
            Name,
            $"{context.Keyword} - {Name} {variant.Label}",
            searchUrl,
            searchUrl,
            variant.SellerName,
            "",
            category,
            tags,
            variant.PriceEstimate,
            BaseScore + variant.Bonus,
            Math.Clamp(BaseScore + variant.Bonus * 9, 20, 260),
            Math.Clamp(BaseScore * 12 + variant.Bonus * 280, 80, 7000),
            $"{variant.Label}. Bu kaynak provider'i gercek urun/fiyat/gorsel secimi icin arama sonucunu hazirlar");
    }

    protected sealed record QueryVariant(
        string Query,
        string Label,
        int Bonus,
        decimal PriceEstimate,
        string SellerName,
        string CategoryHint);
}

public sealed class GoogleShoppingMarketplaceProvider : SearchUrlMarketplaceProvider
{
    public override string Name => "Google Shopping";

    protected override string SearchUrlFormat => "https://www.google.com/search?tbm=shop&q={0}";

    protected override int BaseScore => 78;

    protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
    [
        new(context.Query, "Genel urun aramasi", 0, 35, "Google Shopping", ""),
        new($"{context.Query} best seller", "Cok satan sinyali aramasi", 5, 55, "Google Shopping", ""),
        new($"{context.Query} handmade custom", "El yapimi/Etsy uyumu aramasi", 7, 68, "Google Shopping", ""),
    ];
}

public sealed class EbayMarketplaceProvider : SearchUrlMarketplaceProvider
{
    private readonly IEbayApiClient? apiClient;
    private readonly Func<EbayApiSettings>? settingsProvider;

    public EbayMarketplaceProvider()
    {
    }

    public EbayMarketplaceProvider(IEbayApiClient apiClient, Func<EbayApiSettings> settingsProvider)
    {
        this.apiClient = apiClient;
        this.settingsProvider = settingsProvider;
    }

    public override string Name => "eBay";

    protected override string SearchUrlFormat => "https://www.ebay.com/sch/i.html?_nkw={0}";

    protected override int BaseScore => 76;

    public override IReadOnlyList<MarketplaceProduct> Search(ExternalMarketplaceSearchContext context)
    {
        if (apiClient is null || settingsProvider is null)
        {
            return base.Search(context);
        }

        var settings = settingsProvider();
        if (!settings.HasCredentials)
        {
            return base.Search(context);
        }

        try
        {
            var products = apiClient.SearchAsync(settings, context.Query).GetAwaiter().GetResult();
            return products.Count == 0
                ? base.Search(context)
                : products.Select(product => ToMarketplaceProduct(product, context)).ToList();
        }
        catch
        {
            return base.Search(context);
        }
    }

    protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
    [
        new(context.Query, "Pazar fiyat sinyali", 0, 42, "eBay seller", ""),
        new($"{context.Query} collectible", "Koleksiyon/nis urun sinyali", 6, 75, "eBay seller", ""),
        new($"{context.Query} custom handmade", "Etsy uyumlu varyasyon sinyali", 8, 85, "eBay seller", ""),
    ];

    private static MarketplaceProduct ToMarketplaceProduct(EbayApiProduct product, ExternalMarketplaceSearchContext context)
    {
        var category = string.IsNullOrWhiteSpace(product.Category)
            ? ExternalMarketplaceOpportunityService.InferCategory(context.ShopType, context.Keyword)
            : product.Category;
        var tags = product.Tags.Count == 0
            ? ExternalMarketplaceOpportunityService.BuildTags(context.ShopType, context.Keyword, product.Title)
            : product.Tags;

        return new MarketplaceProduct(
            "eBay",
            product.Title,
            product.Url,
            $"https://www.ebay.com/sch/i.html?_nkw={Uri.EscapeDataString(context.Query)}",
            string.IsNullOrWhiteSpace(product.SellerName) ? "eBay seller" : product.SellerName,
            product.ImageUrl,
            category,
            tags,
            product.Price,
            84,
            Math.Clamp(product.DemandSignal, 15, 260),
            Math.Clamp(product.ShopSignal, 80, 7000),
            $"eBay API gercek urun verisi. Para birimi: {(string.IsNullOrWhiteSpace(product.Currency) ? "bilinmiyor" : product.Currency)}");
    }
}

public sealed class TrendyolMarketplaceProvider : SearchUrlMarketplaceProvider
{
    public override string Name => "Trendyol";

    protected override string SearchUrlFormat => "https://www.trendyol.com/sr?q={0}";

    protected override int BaseScore => 72;

    protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
    [
        new(context.Query, "Turkiye pazar fiyat kontrolu", 0, 28, "Trendyol magaza", ""),
        new($"{context.Keyword} dekor hediye", "Hediye/dekor trend kontrolu", 4, 38, "Trendyol magaza", ""),
        new($"{context.Keyword} cok satan", "Talep sinyali kontrolu", 6, 45, "Trendyol magaza", ""),
    ];
}

public sealed class HepsiburadaMarketplaceProvider : SearchUrlMarketplaceProvider
{
    public override string Name => "Hepsiburada";

    protected override string SearchUrlFormat => "https://www.hepsiburada.com/ara?q={0}";

    protected override int BaseScore => 70;

    protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
    [
        new(context.Query, "Genel fiyat araligi kontrolu", 0, 30, "Hepsiburada satici", ""),
        new($"{context.Keyword} hediye", "Hediye pazari kontrolu", 4, 40, "Hepsiburada satici", ""),
        new($"{context.Keyword} dekor", "Ev/dekor uyumu kontrolu", 5, 48, "Hepsiburada satici", ""),
    ];
}

public sealed class GoogleWebMarketplaceProvider : SearchUrlMarketplaceProvider
{
    public override string Name => "Google Web";

    protected override string SearchUrlFormat => "https://www.google.com/search?q={0}";

    protected override int BaseScore => 68;

    protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
    [
        new($"{context.Query} review", "Review ve yorum sinyali", 2, 35, "Web kaynagi", ""),
        new($"{context.Query} site:etsy.com/listing", "Etsy benzer listing kesfi", 7, 55, "Etsy/Web", ""),
        new($"{context.Query} trend 2026", "Yeni trend sinyali", 5, 58, "Web kaynagi", ""),
    ];
}
