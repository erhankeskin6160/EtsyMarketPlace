namespace EtsyMarketPlace.Application.Features.AbTests.Commands.CreateAbTest;

using EtsyMarketPlace.Application.Common.Localization;
using FluentValidation;

public class CreateAbTestValidator : AbstractValidator<CreateAbTestRequest>
{
    public CreateAbTestValidator()
    {
        RuleFor(x => x.Experiment)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "A/B test deney verileri boş olamaz.",
                "A/B test experiment data cannot be empty."));

        When(x => x.Experiment != null, () =>
        {
            RuleFor(x => x.Experiment.ListingId)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Listing ID belirtilmelidir.",
                    "Listing ID is required."));

            RuleFor(x => x.Experiment.ExperimentName)
                .NotEmpty()
                .WithMessage(_ => ValidationMessageLocalizer.Pick(
                    "Deney adı belirtilmelidir.",
                    "Experiment name is required."));
        });
    }
}
