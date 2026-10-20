namespace EtsyMarketPlace.Application.Features.AbTests.Commands.CreateAbTest;

using FluentValidation;

public class CreateAbTestValidator : AbstractValidator<CreateAbTestRequest>
{
    public CreateAbTestValidator()
    {
        RuleFor(x => x.Experiment)
            .NotNull()
            .WithMessage("A/B test deney verileri boş olamaz.");

        When(x => x.Experiment != null, () =>
        {
            RuleFor(x => x.Experiment.ListingId)
                .NotEmpty()
                .WithMessage("Listing ID belirtilmelidir.");

            RuleFor(x => x.Experiment.ExperimentName)
                .NotEmpty()
                .WithMessage("Deney adı belirtilmelidir.");
        });
    }
}
