namespace EtsyMarketPlace.Application.Features.MarketResearch.Validation;

using EtsyMarketPlace.Application.Features.MarketResearch;
using FluentValidation;

public sealed class SearchMarketQueryValidator : AbstractValidator<SearchMarketQuery>
{
    public SearchMarketQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotNull()
            .WithMessage("Pazar arama parametreleri boş olamaz.");

        When(x => x.Query != null, () =>
        {
            RuleFor(x => x.Query.Keyword)
                .NotEmpty()
                .WithMessage("Pazar araştırması için anahtar kelime (keyword) girilmelidir.");

            RuleFor(x => x.Query.Limit)
                .InclusiveBetween(1, 100)
                .WithMessage("Arama limiti 1 ile 100 arasında olmalıdır.");
        });
    }
}

public sealed class TrackMarketKeywordCommandValidator : AbstractValidator<TrackMarketKeywordCommand>
{
    public TrackMarketKeywordCommandValidator()
    {
        RuleFor(x => x.Keyword)
            .NotEmpty()
            .WithMessage("Takip edilecek anahtar kelime (keyword) zorunludur.")
            .MaximumLength(100)
            .WithMessage("Anahtar kelime en fazla 100 karakter olabilir.");
    }
}

public sealed class DeleteTrackedKeywordCommandValidator : AbstractValidator<DeleteTrackedKeywordCommand>
{
    public DeleteTrackedKeywordCommandValidator()
    {
        RuleFor(x => x.Keyword)
            .NotEmpty()
            .WithMessage("Silinecek anahtar kelime belirtilmelidir.");
    }
}

public sealed class GenerateAiMarketReportCommandValidator : AbstractValidator<GenerateAiMarketReportCommand>
{
    public GenerateAiMarketReportCommandValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("Pazar strateji raporu verileri boş olamaz.");

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command.Keyword)
                .NotEmpty()
                .WithMessage("Pazar strateji raporu üretmek için anahtar kelime gereklidir.");
        });
    }
}

public sealed class AnalyzeCompetitorShopQueryValidator : AbstractValidator<AnalyzeCompetitorShopQuery>
{
    public AnalyzeCompetitorShopQueryValidator()
    {
        RuleFor(x => x.ShopNameOrUrl)
            .NotEmpty()
            .WithMessage("Rakip mağaza adı veya Etsy mağaza URL'si gereklidir.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Analiz edilecek ürün limiti 1 ile 100 arasında olmalıdır.");
    }
}

public sealed class GetListingInventoryQueryValidator : AbstractValidator<GetListingInventoryQuery>
{
    public GetListingInventoryQueryValidator()
    {
        RuleFor(x => x.ListingId)
            .GreaterThan(0)
            .WithMessage("Geçerli bir ilan kimliği (Listing ID) belirtilmelidir.");
    }
}

public sealed class ImproveListingLqsCommandValidator : AbstractValidator<ImproveListingLqsCommand>
{
    public ImproveListingLqsCommandValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("LQS iyileştirme verileri boş olamaz.");

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command.Title)
                .NotEmpty()
                .WithMessage("LQS denetimi için ürün başlığı gereklidir.");
        });
    }
}
