using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

namespace EtsyMarketPlace.Application.ListingOptimization.Validation;

public sealed class FastListingPublishCommandValidator : AbstractValidator<FastListingPublishCommand>
{
    private const int MaxTitleLength = 140;
    private const int MaxTagCount = 13;
    private const int MaxTagLength = 20;
    private const int MaxVariationCombinations = 70;

    public FastListingPublishCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) belirtilmedi.",
                "Shop ID was not specified."));

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Ürün başlığı zorunludur.",
                "Product title is required."))
            .MaximumLength(MaxTitleLength)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                $"Ürün başlığı en fazla {MaxTitleLength} karakter olabilir.",
                $"Product title can be at most {MaxTitleLength} characters."));

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Fiyat 0'dan büyük bir değer olmalıdır.",
                "Price must be greater than 0."));

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Stok adedi en az 1 olmalıdır.",
                "Stock quantity must be at least 1."));

        When(x => x.Tags != null && x.Tags.Count > 0, () =>
        {
            RuleFor(x => x.Tags!.Count)
                .LessThanOrEqualTo(MaxTagCount)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    $"Etsy kurallarına göre en fazla {MaxTagCount} etiket girilebilir.",
                    $"Per Etsy rules, at most {MaxTagCount} tags can be entered."));

            RuleForEach(x => x.Tags)
                .MaximumLength(MaxTagLength)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    $"Her bir etiket en fazla {MaxTagLength} karakter olabilir.",
                    $"Each tag can be at most {MaxTagLength} characters."));
        });

        When(x => x.VariationGroups != null && x.VariationGroups.Count > 0, () =>
        {
            RuleFor(x => x.VariationGroups)
                .Must(groups =>
                {
                    if (groups == null || groups.Count == 0) return true;
                    var total = groups.Aggregate(1, (acc, g) => acc * Math.Max(1, g.Values.Count));
                    return total <= MaxVariationCombinations;
                })
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    $"Varyasyon kombinasyon sayısı Etsy üst limiti olan {MaxVariationCombinations}'i aşamaz.",
                    $"Number of variation combinations cannot exceed the Etsy maximum limit of {MaxVariationCombinations}."));
        });
    }
}
