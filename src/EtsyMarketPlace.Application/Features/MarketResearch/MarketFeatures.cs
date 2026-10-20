namespace EtsyMarketPlace.Application.Features.MarketResearch;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.MarketResearch;
using MediatR;

// 1. Search Market
public sealed record SearchMarketQuery(MarketSearchQuery Query) : IRequest<ServiceResponse>;

public sealed class SearchMarketHandler(IMarketResearchService service)
    : IRequestHandler<SearchMarketQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SearchMarketQuery request, CancellationToken cancellationToken)
    {
        var result = await service.SearchMarketAsync(request.Query, cancellationToken);
        return ServiceResponse.CreateSuccess("Pazar araması başarıyla tamamlandı.", result);
    }
}

// 2. Track Keyword
public sealed record TrackMarketKeywordCommand(string Keyword) : IRequest<ServiceResponse>;

public sealed class TrackMarketKeywordHandler(IMarketKeywordTrackingService service)
    : IRequestHandler<TrackMarketKeywordCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(TrackMarketKeywordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
            return ServiceResponse.CreateError("Takip için keyword gerekli.", HttpStatusCode.BadRequest);

        var history = await service.TrackKeywordAsync(request.Keyword, cancellationToken);
        return ServiceResponse.CreateSuccess("Kelime başarıyla takibe alındı.", history);
    }
}

// 3. Get Tracked Keywords
public sealed record GetTrackedKeywordsQuery : IRequest<ServiceResponse>;

public sealed class GetTrackedKeywordsHandler(IMarketKeywordTrackingService service)
    : IRequestHandler<GetTrackedKeywordsQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetTrackedKeywordsQuery request, CancellationToken cancellationToken)
    {
        var list = await service.GetTrackedKeywordsAsync(cancellationToken);
        return ServiceResponse.CreateSuccess("Takip edilen kelimeler getirildi.", list);
    }
}

// 4. Delete Tracked Keyword
public sealed record DeleteTrackedKeywordCommand(string Keyword) : IRequest<ServiceResponse>;

public sealed class DeleteTrackedKeywordHandler(IMarketKeywordTrackingService service)
    : IRequestHandler<DeleteTrackedKeywordCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(DeleteTrackedKeywordCommand request, CancellationToken cancellationToken)
    {
        var removed = await service.DeleteKeywordAsync(request.Keyword, cancellationToken);
        return ServiceResponse.CreateSuccess("Kelime takipten çıkarıldı.", new { success = removed });
    }
}

// 5. Generate AI Market Report
public sealed record GenerateAiMarketReportCommand(MarketAiReportCommand Command) : IRequest<ServiceResponse>;

public sealed class GenerateAiMarketReportHandler(IMarketResearchService service)
    : IRequestHandler<GenerateAiMarketReportCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GenerateAiMarketReportCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Command.Keyword))
            return ServiceResponse.CreateError("Pazar strateji raporu için keyword gereklidir.", HttpStatusCode.BadRequest);

        var report = await service.GenerateAiMarketReportAsync(request.Command, cancellationToken);
        return ServiceResponse.CreateSuccess("Pazar raporu başarıyla üretildi.", report);
    }
}

// 6. Analyze Competitor Shop
public sealed record AnalyzeCompetitorShopQuery(string ShopNameOrUrl, int Limit, string AuthShopId) : IRequest<ServiceResponse>;

public sealed class AnalyzeCompetitorShopHandler(IMarketResearchService service)
    : IRequestHandler<AnalyzeCompetitorShopQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(AnalyzeCompetitorShopQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopNameOrUrl))
            return ServiceResponse.CreateError("Rakip mağaza adı veya Etsy mağaza URL'si gereklidir.", HttpStatusCode.BadRequest);

        var result = await service.AnalyzeCompetitorShopAsync(
            shopNameOrUrl: request.ShopNameOrUrl.Trim(),
            limit: request.Limit,
            authShopId: request.AuthShopId,
            cancellationToken: cancellationToken);

        return ServiceResponse.CreateSuccess("Rakip mağaza analizi tamamlandı.", result);
    }
}

// 7. Get Seasonal Trends
public sealed record GetSeasonalTrendsQuery : IRequest<ServiceResponse>;

public sealed class GetSeasonalTrendsHandler(IMarketResearchService service)
    : IRequestHandler<GetSeasonalTrendsQuery, ServiceResponse>
{
    public Task<ServiceResponse> Handle(GetSeasonalTrendsQuery request, CancellationToken cancellationToken)
    {
        var trends = service.GetSeasonalTrends();
        return Task.FromResult(ServiceResponse.CreateSuccess("Dönemsel trendler getirildi.", trends));
    }
}

// 8. Get Listing Inventory
public sealed record GetListingInventoryQuery(long ListingId, string ShopId) : IRequest<ServiceResponse>;

public sealed class GetListingInventoryHandler(IMarketResearchService service)
    : IRequestHandler<GetListingInventoryQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetListingInventoryQuery request, CancellationToken cancellationToken)
    {
        if (request.ListingId <= 0)
            return ServiceResponse.CreateError("Geçersiz listingId.", HttpStatusCode.BadRequest);

        var inventory = await service.GetListingInventoryAsync(request.ListingId, request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess("İlan envanteri getirildi.", inventory);
    }
}

// 9. Improve Listing LQS
public sealed record ImproveListingLqsCommand(AiLqsImprovementCommand Command) : IRequest<ServiceResponse>;

public sealed class ImproveListingLqsHandler(IMarketResearchService service)
    : IRequestHandler<ImproveListingLqsCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ImproveListingLqsCommand request, CancellationToken cancellationToken)
    {
        if (request.Command == null || string.IsNullOrWhiteSpace(request.Command.Title))
            return ServiceResponse.CreateError("LQS denetimi için ürün başlığı gereklidir.", HttpStatusCode.BadRequest);

        var result = await service.ImproveListingLqsWithAiAsync(request.Command, cancellationToken);
        return ServiceResponse.CreateSuccess("LQS iyileştirme tamamlandı.", result);
    }
}
