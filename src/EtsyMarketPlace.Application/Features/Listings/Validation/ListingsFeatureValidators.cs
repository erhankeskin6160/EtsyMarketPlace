namespace EtsyMarketPlace.Application.Features.Listings.Validation;

using System;
using System.Linq;
using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Application.Features.Listings.Commands.AiOptimizeListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.SuggestListingVariations;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListingState;
using EtsyMarketPlace.Application.Features.Listings.Images;
using FluentValidation;

public sealed class UpdateListingCommandRequestValidator : AbstractValidator<UpdateListingCommandRequest>
{
    private const int MaxTitleLength = 140;
    private const int MaxTagCount = 13;
    private const int MaxTagLength = 20;

    public UpdateListingCommandRequestValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) belirtilmedi.",
                "Shop ID was not specified."));

        RuleFor(x => x.ListingId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Güncellenecek ilan kimliği (Listing ID) belirtilmelidir.",
                "Listing ID to update must be specified."));

        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "İlan güncelleme verileri boş olamaz.",
                "Listing update data cannot be empty."));

        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.Title)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Ürün başlığı zorunludur.",
                    "Product title is required."))
                .MaximumLength(MaxTitleLength)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    $"Ürün başlığı en fazla {MaxTitleLength} karakter olabilir.",
                    $"Product title can be at most {MaxTitleLength} characters."))
                .Must(title => string.IsNullOrEmpty(title) || title.Count(c => c == '&') <= 1)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Etsy kuralı gereği başlıkta '&' karakteri en fazla 1 kez kullanılabilir.",
                    "Per Etsy rules, the '&' character can be used at most once in the title."));

            When(x => x.Dto.Tags != null && x.Dto.Tags.Count > 0, () =>
            {
                RuleFor(x => x.Dto.Tags.Count)
                    .LessThanOrEqualTo(MaxTagCount)
                    .OverridePropertyName("Tags")
                    .WithMessage(_ => ValidationMessageLocalizer.Pick(
                        $"Etsy kurallarına göre en fazla {MaxTagCount} etiket girilebilir.",
                        $"Per Etsy rules, at most {MaxTagCount} tags can be entered."));

                RuleForEach(x => x.Dto.Tags)
                    .MaximumLength(MaxTagLength)
                    .OverridePropertyName("Tags")
                    .WithMessage(_ => ValidationMessageLocalizer.Pick(
                        $"Her bir etiket en fazla {MaxTagLength} karakter olabilir.",
                        $"Each tag can be at most {MaxTagLength} characters."));
            });
        });
    }
}

public sealed class UpdateListingStateCommandRequestValidator : AbstractValidator<UpdateListingStateCommandRequest>
{
    public UpdateListingStateCommandRequestValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) belirtilmedi.",
                "Shop ID was not specified."));

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.",
                "A valid Listing ID must be specified."));

        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "İlan durumu belirtilmelidir.",
                "Listing state must be specified."))
            .Must(s => !string.IsNullOrWhiteSpace(s) &&
                       (s.Trim().Equals("draft", StringComparison.OrdinalIgnoreCase) ||
                        s.Trim().Equals("active", StringComparison.OrdinalIgnoreCase) ||
                        s.Trim().Equals("inactive", StringComparison.OrdinalIgnoreCase)))
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "İlan durumu yalnızca 'draft', 'active' veya 'inactive' olabilir.",
                "Listing state can only be 'draft', 'active', or 'inactive'."));
    }
}

public sealed class AiOptimizeListingRequestValidator : AbstractValidator<AiOptimizeListingRequest>
{
    public AiOptimizeListingRequestValidator()
    {
        RuleFor(x => x.ListingId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Optimize edilecek ilan kimliği belirtilmelidir.",
                "Listing ID to optimize must be specified."));

        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Optimizasyon parametreleri boş olamaz.",
                "Optimization parameters cannot be empty."));

        When(x => x.Command != null && !string.IsNullOrEmpty(x.Command.Title), () =>
        {
            RuleFor(x => x.Command.Title)
                .MaximumLength(140)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Ürün başlığı en fazla 140 karakter olabilir.",
                    "Product title can be at most 140 characters."));
        });
    }
}

public sealed class SyncListingImagesCommandValidator : AbstractValidator<SyncListingImagesCommand>
{
    private const int MaxEtsyListingImages = 10;

    public SyncListingImagesCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) belirtilmedi.",
                "Shop ID was not specified."));

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.",
                "A valid Listing ID must be specified."));

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "İlanda en az 1 görsel bulunmalıdır.",
                "Listing must contain at least 1 image."));

        When(x => x.Items != null, () =>
        {
            RuleFor(x => x.Items.Count)
                .LessThanOrEqualTo(MaxEtsyListingImages)
                .OverridePropertyName("Items")
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    $"Etsy ilanlarında en fazla {MaxEtsyListingImages} görsel bulunabilir.",
                    $"Etsy listings can contain at most {MaxEtsyListingImages} images."));
        });
    }
}

public sealed class UpdateListingImagesAltTextCommandValidator : AbstractValidator<UpdateListingImagesAltTextCommand>
{
    private const int MaxAltTextLength = 250;

    public UpdateListingImagesAltTextCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) belirtilmedi.",
                "Shop ID was not specified."));

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.",
                "A valid Listing ID must be specified."));

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Güncellenecek görsel bulunamadı.",
                "No images found to update."));

        When(x => x.Items != null, () =>
        {
            RuleForEach(x => x.Items)
                .Must(item => string.IsNullOrEmpty(item.AltText) || item.AltText.Trim().Length <= MaxAltTextLength)
                .OverridePropertyName("AltText")
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    $"Görsel Alt-Text açıklaması en fazla {MaxAltTextLength} karakter olabilir.",
                    $"Image alt-text description can be at most {MaxAltTextLength} characters."));
        });
    }
}

public sealed class AnalyzeListingImagesCommandValidator : AbstractValidator<AnalyzeListingImagesCommand>
{
    public AnalyzeListingImagesCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) belirtilmedi.",
                "Shop ID was not specified."));

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Görsel analizi için geçerli bir ilan kimliği belirtilmelidir.",
                "A valid Listing ID must be specified for image analysis."));
    }
}

public sealed class SuggestListingVariationsCommandValidator : AbstractValidator<SuggestListingVariationsCommand>
{
    public SuggestListingVariationsCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Varyasyon öneri verileri boş olamaz.",
                "Variation suggestion data cannot be empty."));

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request)
                .Must(r => !string.IsNullOrWhiteSpace(r.Title) || !string.IsNullOrWhiteSpace(r.Description))
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "AI varyasyon önerisi için en az ürün başlığı veya açıklaması girilmelidir.",
                    "At least a product title or description must be provided for AI variation suggestions."));

            RuleFor(x => x.Request.PriceUsd)
                .GreaterThanOrEqualTo(0m)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Ürün taban fiyatı negatif olamaz.",
                    "Product base price cannot be negative."));
        });
    }
}
