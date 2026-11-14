namespace EtsyMarketPlace.Application.Features.Listings.Validation;

using System;
using System.Linq;
using EtsyMarketPlace.Application.Features.Listings.Commands.AiOptimizeListing;
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
            .WithMessage("Mağaza kimliği (Shop ID) belirtilmedi.");

        RuleFor(x => x.ListingId)
            .NotEmpty()
            .WithMessage("Güncellenecek ilan kimliği (Listing ID) belirtilmelidir.");

        RuleFor(x => x.Dto)
            .NotNull()
            .WithMessage("İlan güncelleme verileri boş olamaz.");

        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.Title)
                .NotEmpty()
                .WithMessage("Ürün başlığı zorunludur.")
                .MaximumLength(MaxTitleLength)
                .WithMessage($"Ürün başlığı en fazla {MaxTitleLength} karakter olabilir.")
                .Must(title => string.IsNullOrEmpty(title) || title.Count(c => c == '&') <= 1)
                .WithMessage("Etsy kuralı gereği başlıkta '&' karakteri en fazla 1 kez kullanılabilir.");

            When(x => x.Dto.Tags != null && x.Dto.Tags.Count > 0, () =>
            {
                RuleFor(x => x.Dto.Tags.Count)
                    .LessThanOrEqualTo(MaxTagCount)
                    .OverridePropertyName("Tags")
                    .WithMessage($"Etsy kurallarına göre en fazla {MaxTagCount} etiket girilebilir.");

                RuleForEach(x => x.Dto.Tags)
                    .MaximumLength(MaxTagLength)
                    .OverridePropertyName("Tags")
                    .WithMessage($"Her bir etiket en fazla {MaxTagLength} karakter olabilir.");
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
            .WithMessage("Mağaza kimliği (Shop ID) belirtilmedi.");

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage("Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.");

        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage("İlan durumu belirtilmelidir.")
            .Must(s => !string.IsNullOrWhiteSpace(s) &&
                       (s.Trim().Equals("draft", StringComparison.OrdinalIgnoreCase) ||
                        s.Trim().Equals("active", StringComparison.OrdinalIgnoreCase) ||
                        s.Trim().Equals("inactive", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("İlan durumu yalnızca 'draft', 'active' veya 'inactive' olabilir.");
    }
}

public sealed class AiOptimizeListingRequestValidator : AbstractValidator<AiOptimizeListingRequest>
{
    public AiOptimizeListingRequestValidator()
    {
        RuleFor(x => x.ListingId)
            .NotEmpty()
            .WithMessage("Optimize edilecek ilan kimliği belirtilmelidir.");

        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("Optimizasyon parametreleri boş olamaz.");

        When(x => x.Command != null && !string.IsNullOrEmpty(x.Command.Title), () =>
        {
            RuleFor(x => x.Command.Title)
                .MaximumLength(140)
                .WithMessage("Ürün başlığı en fazla 140 karakter olabilir.");
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
            .WithMessage("Mağaza kimliği (Shop ID) belirtilmedi.");

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage("Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("İlanda en az 1 görsel bulunmalıdır.");

        When(x => x.Items != null, () =>
        {
            RuleFor(x => x.Items.Count)
                .LessThanOrEqualTo(MaxEtsyListingImages)
                .OverridePropertyName("Items")
                .WithMessage($"Etsy ilanlarında en fazla {MaxEtsyListingImages} görsel bulunabilir.");
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
            .WithMessage("Mağaza kimliği (Shop ID) belirtilmedi.");

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage("Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Güncellenecek görsel bulunamadı.");

        When(x => x.Items != null, () =>
        {
            RuleForEach(x => x.Items)
                .Must(item => string.IsNullOrEmpty(item.AltText) || item.AltText.Trim().Length <= MaxAltTextLength)
                .OverridePropertyName("AltText")
                .WithMessage($"Görsel Alt-Text açıklaması en fazla {MaxAltTextLength} karakter olabilir.");
        });
    }
}

public sealed class AnalyzeListingImagesCommandValidator : AbstractValidator<AnalyzeListingImagesCommand>
{
    public AnalyzeListingImagesCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) belirtilmedi.");

        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage("Görsel analizi için geçerli bir ilan kimliği belirtilmelidir.");
    }
}
