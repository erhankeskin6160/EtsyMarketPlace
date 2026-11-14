namespace EtsyMarketPlace.Application.Features.OAuth.Validation;

using System;
using EtsyMarketPlace.Application.Features.OAuth;
using EtsyMarketPlace.Application.Features.Settings;
using FluentValidation;

public sealed class ImportEtsyTokenCommandValidator : AbstractValidator<ImportEtsyTokenCommand>
{
    public ImportEtsyTokenCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .WithMessage("Geçerli bir Etsy erişim anahtarı (Access Token) zorunludur.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Geçerli bir Etsy yenileme anahtarı (Refresh Token) zorunludur.");

        RuleFor(x => x.AccessTokenExpiresAt)
            .GreaterThan(_ => DateTimeOffset.UtcNow)
            .WithMessage("Token son kullanma tarihi gelecekte bir zaman olmalıdır.");
    }
}

public sealed class ExchangeEtsyOAuthCodeCommandValidator : AbstractValidator<ExchangeEtsyOAuthCodeCommand>
{
    public ExchangeEtsyOAuthCodeCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Etsy yetkilendirme kodu (Authorization Code) zorunludur.");
    }
}

public sealed class RefreshEtsyTokenCommandValidator : AbstractValidator<RefreshEtsyTokenCommand>
{
    public RefreshEtsyTokenCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Token yenileme işlemi için mağaza kimliği (Shop ID) zorunludur.");
    }
}

public sealed class SaveAiSettingsCommandRequestValidator : AbstractValidator<SaveAiSettingsCommandRequest>
{
    public SaveAiSettingsCommandRequestValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.SettingsJson)
            .NotEmpty()
            .WithMessage("Kaydedilecek AI ayar verisi boş olamaz.");
    }
}

public sealed class SaveEtsyCredentialsCommandRequestValidator : AbstractValidator<SaveEtsyCredentialsCommandRequest>
{
    public SaveEtsyCredentialsCommandRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Etsy kimlik bilgileri isteği boş olamaz.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.ShopId)
                .NotEmpty()
                .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");
        });
    }
}

public sealed class SaveTelegramSettingsCommandRequestValidator : AbstractValidator<SaveTelegramSettingsCommandRequest>
{
    public SaveTelegramSettingsCommandRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Telegram bildirim ayarları isteği boş olamaz.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.ShopId)
                .NotEmpty()
                .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

            When(x => x.Request.IsEnabled, () =>
            {
                RuleFor(x => x.Request.ChatId)
                    .NotEmpty()
                    .WithMessage("Telegram bildirimleri aktifken Chat ID alanı zorunludur.");
            });
        });
    }
}

public sealed class SaveCarrierSessionCommandRequestValidator : AbstractValidator<SaveCarrierSessionCommandRequest>
{
    public SaveCarrierSessionCommandRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Kargo taşıyıcı oturum isteği boş olamaz.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.ShopId)
                .NotEmpty()
                .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

            RuleFor(x => x.Request.CarrierId)
                .NotEmpty()
                .WithMessage("Kargo taşıyıcı kodu (Carrier ID) belirtilmelidir.");
        });
    }
}
