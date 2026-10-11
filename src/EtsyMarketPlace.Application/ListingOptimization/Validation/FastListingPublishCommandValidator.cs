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
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Ürün başlığı zorunludur.")
            .MaximumLength(MaxTitleLength)
            .WithMessage($"Ürün başlığı en fazla {MaxTitleLength} karakter olabilir.");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Fiyat 0'dan büyük bir değer olmalıdır.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Stok adedi en az 1 olmalıdır.");

        When(x => x.Tags != null && x.Tags.Count > 0, () =>
        {
            RuleFor(x => x.Tags!.Count)
                .LessThanOrEqualTo(MaxTagCount)
                .WithMessage($"Etsy kurallarına göre en fazla {MaxTagCount} etiket girilebilir.");

            RuleForEach(x => x.Tags)
                .MaximumLength(MaxTagLength)
                .WithMessage($"Her bir etiket en fazla {MaxTagLength} karakter olabilir.");
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
                .WithMessage($"Varyasyon kombinasyon sayısı Etsy üst limiti olan {MaxVariationCombinations}'i aşamaz.");
        });
    }
}
