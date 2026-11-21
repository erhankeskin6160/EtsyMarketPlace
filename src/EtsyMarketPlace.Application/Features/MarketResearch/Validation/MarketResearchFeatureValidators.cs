namespace EtsyMarketPlace.Application.Features.MarketResearch.Validation;

using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Application.Features.MarketResearch;
using FluentValidation;

public sealed class SearchMarketQueryValidator : AbstractValidator<SearchMarketQuery>
{
    public SearchMarketQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Pazar arama parametreleri boş olamaz.",
                "Market search parameters cannot be empty."));

        When(x => x.Query != null, () =>
        {
            RuleFor(x => x.Query.Keyword)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Pazar araştırması için anahtar kelime (keyword) girilmelidir.",
                    "A keyword must be entered for market research."));

            RuleFor(x => x.Query.Limit)
                .InclusiveBetween(1, 100)
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Arama limiti 1 ile 100 arasında olmalıdır.",
                    "Search limit must be between 1 and 100."));
        });
    }
}

public sealed class TrackMarketKeywordCommandValidator : AbstractValidator<TrackMarketKeywordCommand>
{
    public TrackMarketKeywordCommandValidator()
    {
        RuleFor(x => x.Keyword)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Takip edilecek anahtar kelime (keyword) zorunludur.",
                "Keyword to track is required."))
            .MaximumLength(100)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Anahtar kelime en fazla 100 karakter olabilir.",
                "Keyword can be at most 100 characters."));
    }
}

public sealed class DeleteTrackedKeywordCommandValidator : AbstractValidator<DeleteTrackedKeywordCommand>
{
    public DeleteTrackedKeywordCommandValidator()
    {
        RuleFor(x => x.Keyword)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Silinecek anahtar kelime belirtilmelidir.",
                "Keyword to delete must be specified."));
    }
}

public sealed class GenerateAiMarketReportCommandValidator : AbstractValidator<GenerateAiMarketReportCommand>
{
    public GenerateAiMarketReportCommandValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Pazar strateji raporu verileri boş olamaz.",
                "Market strategy report data cannot be empty."));

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command.Keyword)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Pazar strateji raporu üretmek için anahtar kelime gereklidir.",
                    "A keyword is required to generate a market strategy report."));
        });
    }
}

public sealed class AnalyzeCompetitorShopQueryValidator : AbstractValidator<AnalyzeCompetitorShopQuery>
{
    public AnalyzeCompetitorShopQueryValidator()
    {
        RuleFor(x => x.ShopNameOrUrl)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Rakip mağaza adı veya Etsy mağaza URL'si gereklidir.",
                "Competitor shop name or Etsy shop URL is required."));

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Analiz edilecek ürün limiti 1 ile 100 arasında olmalıdır.",
                "Product analysis limit must be between 1 and 100."));
    }
}

public sealed class GetListingInventoryQueryValidator : AbstractValidator<GetListingInventoryQuery>
{
    public GetListingInventoryQueryValidator()
    {
        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.",
                "A valid Listing ID must be specified."));
    }
}

public sealed class ImproveListingLqsCommandValidator : AbstractValidator<ImproveListingLqsCommand>
{
    public ImproveListingLqsCommandValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "LQS iyileştirme verileri boş olamaz.",
                "LQS improvement data cannot be empty."));

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command.Title)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "LQS denetimi için ürün başlığı gereklidir.",
                    "Product title is required for LQS audit."));
        });
    }
}
