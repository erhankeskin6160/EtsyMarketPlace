namespace EtsyMarketPlace.Application.Features.OAuth.Validation;

using System;
using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Application.Features.OAuth;
using EtsyMarketPlace.Application.Features.Settings;
using FluentValidation;

public sealed class ImportEtsyTokenCommandValidator : AbstractValidator<ImportEtsyTokenCommand>
{
    public ImportEtsyTokenCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir Etsy erişim anahtarı (Access Token) zorunludur.",
                "A valid Etsy access token is required."));

        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir Etsy yenileme anahtarı (Refresh Token) zorunludur.",
                "A valid Etsy refresh token is required."));

        RuleFor(x => x.AccessTokenExpiresAt)
            .GreaterThan(_ => DateTimeOffset.UtcNow)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Token son kullanma tarihi gelecekte bir zaman olmalıdır.",
                "Token expiration date must be in the future."));
    }
}

public sealed class ExchangeEtsyOAuthCodeCommandValidator : AbstractValidator<ExchangeEtsyOAuthCodeCommand>
{
    public ExchangeEtsyOAuthCodeCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Etsy yetkilendirme kodu (Authorization Code) zorunludur.",
                "Etsy authorization code is required."));
    }
}

public sealed class RefreshEtsyTokenCommandValidator : AbstractValidator<RefreshEtsyTokenCommand>
{
    public RefreshEtsyTokenCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Token yenileme işlemi için mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required to refresh the token."));
    }
}

public sealed class SaveAiSettingsCommandRequestValidator : AbstractValidator<SaveAiSettingsCommandRequest>
{
    public SaveAiSettingsCommandRequestValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.SettingsJson)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kaydedilecek AI ayar verisi boş olamaz.",
                "AI settings data to save cannot be empty."));
    }
}

public sealed class SaveEtsyCredentialsCommandRequestValidator : AbstractValidator<SaveEtsyCredentialsCommandRequest>
{
    public SaveEtsyCredentialsCommandRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Etsy kimlik bilgileri isteği boş olamaz.",
                "Etsy credentials request cannot be empty."));

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.ShopId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Mağaza kimliği (Shop ID) zorunludur.",
                    "Shop ID is required."));
        });
    }
}

public sealed class SaveTelegramSettingsCommandRequestValidator : AbstractValidator<SaveTelegramSettingsCommandRequest>
{
    public SaveTelegramSettingsCommandRequestValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Telegram bildirim ayarları isteği boş olamaz.",
                "Telegram notification settings request cannot be empty."));

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.ShopId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Mağaza kimliği (Shop ID) zorunludur.",
                    "Shop ID is required."));

            When(x => x.Request.IsEnabled, () =>
            {
                RuleFor(x => x.Request.ChatId)
                    .NotEmpty()
                    .WithMessage(_ => ValidationMessageLocalizer.Pick(
                        "Telegram bildirimleri aktifken Chat ID alanı zorunludur.",
                        "Chat ID is required when Telegram notifications are enabled."));
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
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Kargo taşıyıcı oturum isteği boş olamaz.",
                "Shipping carrier session request cannot be empty."));

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.ShopId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Mağaza kimliği (Shop ID) zorunludur.",
                    "Shop ID is required."));

            RuleFor(x => x.Request.CarrierId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Kargo taşıyıcı kodu (Carrier ID) belirtilmelidir.",
                    "Shipping carrier ID must be specified."));
        });
    }
}
