namespace EtsyMarketPlace.Application.Features.Accounting.Validation;

using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Application.Features.Accounting;
using FluentValidation;

public sealed class GetIncomeStatementQueryValidator : AbstractValidator<GetIncomeStatementQuery>
{
    public GetIncomeStatementQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));
    }
}

public sealed class GetOperatingExpensesQueryValidator : AbstractValidator<GetOperatingExpensesQuery>
{
    public GetOperatingExpensesQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));
    }
}

public sealed class UpsertOperatingExpenseCommandValidator : AbstractValidator<UpsertOperatingExpenseCommand>
{
    public UpsertOperatingExpenseCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Gider açıklaması zorunludur.",
                "Expense description is required."))
            .MaximumLength(500)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Gider açıklaması en fazla 500 karakter olabilir.",
                "Expense description can be at most 500 characters."));

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Gider tutarı 0'dan büyük olmalıdır.",
                "Expense amount must be greater than 0."));

        RuleFor(x => x.EntryDate)
            .Must(d => d.Year >= 1990)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir belge/gider tarihi giriniz.",
                "Please enter a valid document/expense date."));
    }
}

public sealed class DeleteOperatingExpenseCommandValidator : AbstractValidator<DeleteOperatingExpenseCommand>
{
    public DeleteOperatingExpenseCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Silinecek gider fişi kimliği geçersiz.",
                "Invalid expense voucher ID to delete."));
    }
}

public sealed class GetFixedAssetsQueryValidator : AbstractValidator<GetFixedAssetsQuery>
{
    public GetFixedAssetsQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));
    }
}

public sealed class UpsertFixedAssetCommandValidator : AbstractValidator<UpsertFixedAssetCommand>
{
    public UpsertFixedAssetCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.AssetName)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Duran varlık adı zorunludur.",
                "Fixed asset name is required."))
            .MaximumLength(250)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Duran varlık adı en fazla 250 karakter olabilir.",
                "Fixed asset name can be at most 250 characters."));

        RuleFor(x => x.AcquisitionDate)
            .Must(d => d.Year >= 1990)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir aktife giriş (alış) tarihi giriniz.",
                "Please enter a valid acquisition date."));

        RuleFor(x => x.AcquisitionCostTry)
            .GreaterThan(0m)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Alış bedeli 0'dan büyük olmalıdır.",
                "Acquisition cost must be greater than 0."));

        RuleFor(x => x.UsefulLifeYears)
            .InclusiveBetween(1, 50)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Ekonomik ömür 1 ile 50 yıl arasında olmalıdır.",
                "Useful life must be between 1 and 50 years."));

        RuleFor(x => x.SalvageValueTry)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Hurda değer 0'dan küçük olamaz.",
                "Salvage value cannot be less than 0."))
            .LessThan(x => x.AcquisitionCostTry)
            .When(x => x.AcquisitionCostTry > 0m)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Hurda değer alış bedelinden küçük olmalıdır.",
                "Salvage value must be less than acquisition cost."));
    }
}

public sealed class DeleteFixedAssetCommandValidator : AbstractValidator<DeleteFixedAssetCommand>
{
    public DeleteFixedAssetCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Silinecek duran varlık kimliği geçersiz.",
                "Invalid fixed asset ID to delete."));
    }
}

public sealed class GetAccountingProfileQueryValidator : AbstractValidator<GetAccountingProfileQuery>
{
    public GetAccountingProfileQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));
    }
}

public sealed class UpsertAccountingProfileCommandValidator : AbstractValidator<UpsertAccountingProfileCommand>
{
    public UpsertAccountingProfileCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.EExportExemptionRatePct)
            .InclusiveBetween(0m, 100m)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "E-İhracat istisna oranı %0 ile %100 arasında olmalıdır.",
                "E-Export exemption rate must be between 0% and 100%."));

        RuleFor(x => x.CustomOrCorporateTaxRatePct)
            .InclusiveBetween(0m, 100m)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Vergi oranı %0 ile %100 arasında olmalıdır.",
                "Tax rate must be between 0% and 100%."));
    }
}

public sealed class UpsertInventoryAdjustmentCommandValidator : AbstractValidator<UpsertInventoryAdjustmentCommand>
{
    public UpsertInventoryAdjustmentCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Mağaza kimliği (Shop ID) zorunludur.",
                "Shop ID is required."));

        RuleFor(x => x.Year)
            .InclusiveBetween(2020, 2100)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir dönem yılı (2020–2100) giriniz.",
                "Please enter a valid period year (2020–2100)."));

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "Geçerli bir dönem ayı (1–12) giriniz.",
                "Please enter a valid period month (1–12)."));
    }
}
