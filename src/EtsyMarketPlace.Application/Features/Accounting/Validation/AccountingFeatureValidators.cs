namespace EtsyMarketPlace.Application.Features.Accounting.Validation;

using EtsyMarketPlace.Application.Features.Accounting;
using FluentValidation;

public sealed class GetIncomeStatementQueryValidator : AbstractValidator<GetIncomeStatementQuery>
{
    public GetIncomeStatementQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");
    }
}

public sealed class GetOperatingExpensesQueryValidator : AbstractValidator<GetOperatingExpensesQuery>
{
    public GetOperatingExpensesQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");
    }
}

public sealed class UpsertOperatingExpenseCommandValidator : AbstractValidator<UpsertOperatingExpenseCommand>
{
    public UpsertOperatingExpenseCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Gider açıklaması zorunludur.")
            .MaximumLength(500)
            .WithMessage("Gider açıklaması en fazla 500 karakter olabilir.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .WithMessage("Gider tutarı 0'dan büyük olmalıdır.");

        RuleFor(x => x.EntryDate)
            .Must(d => d.Year >= 1990)
            .WithMessage("Geçerli bir belge/gider tarihi giriniz.");
    }
}

public sealed class DeleteOperatingExpenseCommandValidator : AbstractValidator<DeleteOperatingExpenseCommand>
{
    public DeleteOperatingExpenseCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Silinecek gider fişi kimliği geçersiz.");
    }
}

public sealed class GetFixedAssetsQueryValidator : AbstractValidator<GetFixedAssetsQuery>
{
    public GetFixedAssetsQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");
    }
}

public sealed class UpsertFixedAssetCommandValidator : AbstractValidator<UpsertFixedAssetCommand>
{
    public UpsertFixedAssetCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.AssetName)
            .NotEmpty()
            .WithMessage("Duran varlık adı zorunludur.")
            .MaximumLength(250)
            .WithMessage("Duran varlık adı en fazla 250 karakter olabilir.");

        RuleFor(x => x.AcquisitionDate)
            .Must(d => d.Year >= 1990)
            .WithMessage("Geçerli bir aktife giriş (alış) tarihi giriniz.");

        RuleFor(x => x.AcquisitionCostTry)
            .GreaterThan(0m)
            .WithMessage("Alış bedeli 0'dan büyük olmalıdır.");

        RuleFor(x => x.UsefulLifeYears)
            .InclusiveBetween(1, 50)
            .WithMessage("Ekonomik ömür 1 ile 50 yıl arasında olmalıdır.");

        RuleFor(x => x.SalvageValueTry)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Hurda değer 0'dan küçük olamaz.")
            .LessThan(x => x.AcquisitionCostTry)
            .When(x => x.AcquisitionCostTry > 0m)
            .WithMessage("Hurda değer alış bedelinden küçük olmalıdır.");
    }
}

public sealed class DeleteFixedAssetCommandValidator : AbstractValidator<DeleteFixedAssetCommand>
{
    public DeleteFixedAssetCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Silinecek duran varlık kimliği geçersiz.");
    }
}

public sealed class GetAccountingProfileQueryValidator : AbstractValidator<GetAccountingProfileQuery>
{
    public GetAccountingProfileQueryValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");
    }
}

public sealed class UpsertAccountingProfileCommandValidator : AbstractValidator<UpsertAccountingProfileCommand>
{
    public UpsertAccountingProfileCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.EExportExemptionRatePct)
            .InclusiveBetween(0m, 100m)
            .WithMessage("E-İhracat istisna oranı %0 ile %100 arasında olmalıdır.");

        RuleFor(x => x.CustomOrCorporateTaxRatePct)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Vergi oranı %0 ile %100 arasında olmalıdır.");
    }
}

public sealed class UpsertInventoryAdjustmentCommandValidator : AbstractValidator<UpsertInventoryAdjustmentCommand>
{
    public UpsertInventoryAdjustmentCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty()
            .WithMessage("Mağaza kimliği (Shop ID) zorunludur.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2020, 2100)
            .WithMessage("Geçerli bir dönem yılı (2020–2100) giriniz.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Geçerli bir dönem ayı (1–12) giriniz.");
    }
}
