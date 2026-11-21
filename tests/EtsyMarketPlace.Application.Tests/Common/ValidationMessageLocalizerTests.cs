using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Auth.Validation;
using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Features.Accounting;
using EtsyMarketPlace.Application.Features.Accounting.Validation;
using EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;
using EtsyMarketPlace.Application.Features.MarketResearch;
using EtsyMarketPlace.Application.Features.MarketResearch.Validation;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ListingOptimization.Validation;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Accounting;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Common;

public sealed class ValidationMessageLocalizerTests
{
    private static T WithUiCulture<T>(string cultureName, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("tr", "Türkçe", "English", "Türkçe")]
    [InlineData("tr-TR", "Türkçe", "English", "Türkçe")]
    [InlineData("en", "Türkçe", "English", "English")]
    [InlineData("en-US", "Türkçe", "English", "English")]
    [InlineData("de-DE", "Türkçe", "English", "English")]
    [InlineData("", "Türkçe", "English", "English")]
    public void Pick_ResolvesMessageBasedOnCurrentUiCulture(string culture, string tr, string en, string expected)
    {
        var actual = WithUiCulture(culture, () => ValidationMessageLocalizer.Pick(tr, en));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidationErrorResponse_FallbackSummary_IsLocalizedPerCulture()
    {
        var enResponse = WithUiCulture("en-US", () => new ValidationErrorResponse(new Dictionary<string, string[]>()));
        var trResponse = WithUiCulture("tr-TR", () => new ValidationErrorResponse(new Dictionary<string, string[]>()));

        Assert.Equal("Validation rules failed.", enResponse.Message);
        Assert.Equal("Validasyon kuralları doğrulanamadı.", trResponse.Message);
    }

    [Fact]
    public void SingletonValidatorInstance_EvaluatesCultureDynamicallyPerCall()
    {
        // Instantiate once under TR, then validate under EN and TR to prove no eager constructor capture.
        var loginValidator = WithUiCulture("tr-TR", () => new LoginRequestValidator());
        var invalidRequest = new LoginRequest("", "");

        var enResult = WithUiCulture("en-US", () => loginValidator.Validate(invalidRequest));
        var trResult = WithUiCulture("tr-TR", () => loginValidator.Validate(invalidRequest));

        Assert.False(enResult.IsValid);
        Assert.False(trResult.IsValid);
        Assert.Contains(enResult.Errors, e => e.ErrorMessage == "Username/email and password are required.");
        Assert.Contains(trResult.Errors, e => e.ErrorMessage == "Kullanıcı adı/e-posta ve şifre gereklidir.");
    }

    [Fact]
    public void RegisterRequestValidator_LocalizesMessagesInEnAndTr()
    {
        var validator = new RegisterRequestValidator();
        var invalidRequest = new RegisterRequest("ab", "bad-email", "123", "53236321");

        var enErrors = WithUiCulture("en", () => validator.Validate(invalidRequest).Errors.Select(e => e.ErrorMessage).ToList());
        var trErrors = WithUiCulture("tr", () => validator.Validate(invalidRequest).Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("Username must be at least 3 characters.", enErrors);
        Assert.Contains("Please enter a valid email address.", enErrors);
        Assert.Contains("Password must be at least 6 characters.", enErrors);

        Assert.Contains("Kullanıcı adı en az 3 karakter olmalıdır.", trErrors);
        Assert.Contains("Geçerli bir e-posta adresi giriniz.", trErrors);
        Assert.Contains("Şifre en az 6 karakter olmalıdır.", trErrors);
    }

    [Fact]
    public void FastListingPublishCommandValidator_LocalizesEtsyRuleMessagesInEnAndTr()
    {
        var validator = new FastListingPublishCommandValidator();
        var tags = Enumerable.Range(1, 14).Select(i => $"tag{i}").ToList();
        var cmd = new FastListingPublishCommand(
            ShopId: "",
            Title: "",
            Description: "Desc",
            Price: 0m,
            Quantity: 0,
            TaxonomyId: 100,
            ShippingProfileId: null,
            ReadinessStateId: null,
            IsDigital: false,
            Tags: tags,
            Materials: null,
            State: "draft");

        var enErrors = WithUiCulture("en-US", () => validator.Validate(cmd).Errors.Select(e => e.ErrorMessage).ToList());
        var trErrors = WithUiCulture("tr-TR", () => validator.Validate(cmd).Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("Shop ID was not specified.", enErrors);
        Assert.Contains("Product title is required.", enErrors);
        Assert.Contains("Price must be greater than 0.", enErrors);
        Assert.Contains("Stock quantity must be at least 1.", enErrors);
        Assert.Contains("Per Etsy rules, at most 13 tags can be entered.", enErrors);

        Assert.Contains("Mağaza kimliği (Shop ID) belirtilmedi.", trErrors);
        Assert.Contains("Ürün başlığı zorunludur.", trErrors);
        Assert.Contains("Fiyat 0'dan büyük bir değer olmalıdır.", trErrors);
        Assert.Contains("Stok adedi en az 1 olmalıdır.", trErrors);
        Assert.Contains("Etsy kurallarına göre en fazla 13 etiket girilebilir.", trErrors);
    }

    [Fact]
    public void CrossModuleValidators_LocalizeMessagesInEnAndTr()
    {
        var expenseValidator = new UpsertOperatingExpenseCommandValidator();
        var invalidExpense = new UpsertOperatingExpenseCommand(
            Id: null,
            ShopId: "",
            EntryDate: new DateTime(1980, 1, 1),
            DocumentNo: null,
            Category: OperatingExpenseCategory.GeneralManagement_632,
            Description: "",
            Amount: 0m,
            Currency: "TRY",
            ExchangeRateToTry: 1m,
            IsTaxDeductible: true,
            Frequency: ExpenseFrequency.OneTime,
            RecurringEndDate: null);

        var enExpenseErrors = WithUiCulture("en", () => expenseValidator.Validate(invalidExpense).Errors.Select(e => e.ErrorMessage).ToList());
        var trExpenseErrors = WithUiCulture("tr", () => expenseValidator.Validate(invalidExpense).Errors.Select(e => e.ErrorMessage).ToList());
        Assert.Contains("Shop ID is required.", enExpenseErrors);
        Assert.Contains("Expense amount must be greater than 0.", enExpenseErrors);
        Assert.Contains("Mağaza kimliği (Shop ID) zorunludur.", trExpenseErrors);
        Assert.Contains("Gider tutarı 0'dan büyük olmalıdır.", trExpenseErrors);

        var checkoutValidator = new CreateCheckoutSessionValidator();
        var invalidCheckout = new CreateCheckoutSessionCommand(
            UserId: "",
            RoleHint: null,
            UsernameHint: null,
            EmailHint: null,
            Request: new CreateCheckoutSessionRequest("invalid_plan", "Weekly", "EUR"));

        var enCheckoutErrors = WithUiCulture("en", () => checkoutValidator.Validate(invalidCheckout).Errors.Select(e => e.ErrorMessage).ToList());
        var trCheckoutErrors = WithUiCulture("tr", () => checkoutValidator.Validate(invalidCheckout).Errors.Select(e => e.ErrorMessage).ToList());
        Assert.Contains("User ID is required.", enCheckoutErrors);
        Assert.Contains("Billing cycle must be 'Monthly' or 'Yearly'.", enCheckoutErrors);
        Assert.Contains("Currency must be 'USD' or 'TRY'.", enCheckoutErrors);
        Assert.Contains("Kullanıcı kimliği gereklidir.", trCheckoutErrors);
        Assert.Contains("Faturalama dönemi 'Monthly' veya 'Yearly' olmalıdır.", trCheckoutErrors);
        Assert.Contains("Para birimi 'USD' veya 'TRY' olmalıdır.", trCheckoutErrors);

        var marketValidator = new SearchMarketQueryValidator();
        var invalidMarket = new SearchMarketQuery(new MarketSearchQuery("", 500));
        var enMarketErrors = WithUiCulture("en", () => marketValidator.Validate(invalidMarket).Errors.Select(e => e.ErrorMessage).ToList());
        var trMarketErrors = WithUiCulture("tr", () => marketValidator.Validate(invalidMarket).Errors.Select(e => e.ErrorMessage).ToList());
        Assert.Contains("A keyword must be entered for market research.", enMarketErrors);
        Assert.Contains("Search limit must be between 1 and 100.", enMarketErrors);
        Assert.Contains("Pazar araştırması için anahtar kelime (keyword) girilmelidir.", trMarketErrors);
        Assert.Contains("Arama limiti 1 ile 100 arasında olmalıdır.", trMarketErrors);

        var carrierValidator = new ConnectCarrierValidator();
        var invalidCarrier = new ConnectCarrierRequest { Command = new ConnectCarrierCommand { CarrierKey = "" } };
        var enCarrierErrors = WithUiCulture("en", () => carrierValidator.Validate(invalidCarrier).Errors.Select(e => e.ErrorMessage).ToList());
        var trCarrierErrors = WithUiCulture("tr", () => carrierValidator.Validate(invalidCarrier).Errors.Select(e => e.ErrorMessage).ToList());
        Assert.Contains("Shipping carrier (CarrierKey) must be specified.", enCarrierErrors);
        Assert.Contains("Kargo firması (CarrierKey) belirtilmelidir.", trCarrierErrors);
    }
}
