namespace EtsyMarketPlace.Application.Features.Settings;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ShopPerformance;
using MediatR;

// 1. AI Settings
public sealed record GetAiSettingsRequest(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetAiSettingsHandler(IShopSettingsRepository repository)
    : IRequestHandler<GetAiSettingsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAiSettingsRequest request, CancellationToken cancellationToken)
    {
        var json = await repository.GetAiSettingsJsonAsync(request.ShopId, cancellationToken);
        var data = string.IsNullOrWhiteSpace(json)
            ? new { exists = false, settingsJson = "{}" }
            : new { exists = true, settingsJson = json };
        return ServiceResponse.CreateSuccess("AI ayarları başarıyla getirildi.", data);
    }
}

public sealed record SaveAiSettingsCommandRequest(string ShopId, string SettingsJson) : IRequest<ServiceResponse>;

public sealed class SaveAiSettingsHandler(IShopSettingsRepository repository)
    : IRequestHandler<SaveAiSettingsCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveAiSettingsCommandRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SettingsJson))
            return ServiceResponse.CreateError("Ayarlar boş olamaz.", HttpStatusCode.BadRequest);

        await repository.SaveAiSettingsJsonAsync(request.ShopId, request.SettingsJson, cancellationToken);
        return ServiceResponse.CreateSuccess("AI ayarları ve API anahtarları veritabanına başarıyla kaydedildi!", new { success = true });
    }
}

// 2. Etsy Credentials
public sealed record GetEtsyCredentialsRequest(string ShopId, string DefaultApiKey, string DefaultSecret, string DefaultRedirect) : IRequest<ServiceResponse>;

public sealed class GetEtsyCredentialsHandler(IShopSettingsRepository repository)
    : IRequestHandler<GetEtsyCredentialsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetEtsyCredentialsRequest request, CancellationToken cancellationToken)
    {
        var creds = await repository.GetEtsyAppCredentialsAsync(request.ShopId, cancellationToken);
        if (creds is not null)
            return ServiceResponse.CreateSuccess("Etsy kimlik bilgileri getirildi.", creds);

        static string Mask(string s) => string.IsNullOrWhiteSpace(s) ? "" : (s.Length <= 8 ? "****" : $"{s[..4]}...{s[^4..]}");
        var fallback = new EtsyAppCredentialsRecord(
            request.ShopId,
            Mask(request.DefaultApiKey),
            Mask(request.DefaultSecret),
            request.DefaultRedirect,
            DateTimeOffset.UtcNow);

        return ServiceResponse.CreateSuccess("Etsy kimlik bilgileri (varsayılan) getirildi.", fallback);
    }
}

public sealed record SaveEtsyCredentialsCommandRequest(SaveEtsyAppCredentialsRequest Request) : IRequest<ServiceResponse>;

public sealed class SaveEtsyCredentialsHandler(IShopSettingsRepository repository)
    : IRequestHandler<SaveEtsyCredentialsCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveEtsyCredentialsCommandRequest request, CancellationToken cancellationToken)
    {
        var saved = await repository.SaveEtsyAppCredentialsAsync(request.Request, cancellationToken);
        return ServiceResponse.CreateSuccess("Etsy kimlik bilgileri başarıyla kaydedildi.", saved);
    }
}

// 3. Telegram Settings
public sealed record GetTelegramSettingsRequest(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetTelegramSettingsHandler(IShopSettingsRepository repository)
    : IRequestHandler<GetTelegramSettingsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetTelegramSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetTelegramSettingsAsync(request.ShopId, cancellationToken);
        var data = settings ?? new TelegramShopSettings(request.ShopId, "", "", false, true, true, true, DateTimeOffset.UtcNow);
        return ServiceResponse.CreateSuccess("Telegram bildirim ayarları getirildi.", data);
    }
}

public sealed record SaveTelegramSettingsCommandRequest(SaveTelegramSettingsRequest Request) : IRequest<ServiceResponse>;

public sealed class SaveTelegramSettingsHandler(IShopSettingsRepository repository)
    : IRequestHandler<SaveTelegramSettingsCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveTelegramSettingsCommandRequest request, CancellationToken cancellationToken)
    {
        var saved = await repository.SaveTelegramSettingsAsync(request.Request, cancellationToken);
        return ServiceResponse.CreateSuccess("Telegram bildirim ayarları başarıyla kaydedildi.", saved);
    }
}

// 4. Carrier Sessions
public sealed record GetCarrierSessionsRequest(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetCarrierSessionsHandler(IShopSettingsRepository repository)
    : IRequestHandler<GetCarrierSessionsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetCarrierSessionsRequest request, CancellationToken cancellationToken)
    {
        var sessions = await repository.GetCarrierSessionsAsync(request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess("Kargo taşıyıcı oturumları getirildi.", sessions);
    }
}

public sealed record SaveCarrierSessionCommandRequest(SaveCarrierSessionRequest Request) : IRequest<ServiceResponse>;

public sealed class SaveCarrierSessionHandler(IShopSettingsRepository repository)
    : IRequestHandler<SaveCarrierSessionCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveCarrierSessionCommandRequest request, CancellationToken cancellationToken)
    {
        var saved = await repository.SaveCarrierSessionAsync(request.Request, cancellationToken);
        return ServiceResponse.CreateSuccess("Kargo taşıyıcı oturumu başarıyla kaydedildi.", saved);
    }
}

// 5. Shop Performance History
public sealed record GetShopPerformanceHistoryRequest(long ShopId) : IRequest<ServiceResponse>;

public sealed class GetShopPerformanceHistoryHandler(IShopPerformanceHistoryRepository repository)
    : IRequestHandler<GetShopPerformanceHistoryRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetShopPerformanceHistoryRequest request, CancellationToken cancellationToken)
    {
        var history = await repository.GetByShopAsync(request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess("Mağaza performans geçmişi getirildi.", history);
    }
}
