using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Accounting;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Common.Behaviours;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.AbTests.Commands.DeleteAbTest;
using EtsyMarketPlace.Application.Features.AbTests.Commands.UpdateAbTestStatus;
using EtsyMarketPlace.Application.Features.Accounting;
using EtsyMarketPlace.Application.Features.Accounting.Validation;
using EtsyMarketPlace.Application.Features.Admin.Commands.ToggleUserStatus;
using EtsyMarketPlace.Application.Features.Admin.Commands.UpdateUser;
using EtsyMarketPlace.Application.Features.AiUsage.Commands.RecordAiUsage;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.EnqueueBatchItems;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.ProcessBatchItem;
using EtsyMarketPlace.Application.Features.Common.Validation;
using EtsyMarketPlace.Application.Features.Financials;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListingState;
using EtsyMarketPlace.Application.Features.Listings.Images;
using EtsyMarketPlace.Application.Features.Listings.Validation;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierQuotes;
using EtsyMarketPlace.Application.Features.MarketResearch;
using EtsyMarketPlace.Application.Features.MarketResearch.Validation;
using EtsyMarketPlace.Application.Features.OAuth;
using EtsyMarketPlace.Application.Features.OAuth.Validation;
using EtsyMarketPlace.Application.Features.Orders.Commands.SyncExtensionOrders;
using EtsyMarketPlace.Application.Features.Orders.Commands.UploadOrdersCsv;
using EtsyMarketPlace.Application.Features.Orders.Queries.GetReceiptDetail;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetBankPayouts;
using EtsyMarketPlace.Application.Features.Settings;
using EtsyMarketPlace.Application.Features.Tracking.Commands.DeleteTrackingItem;
using EtsyMarketPlace.Application.Features.Tracking.Commands.SaveTrackingCapture;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Accounting;
using FluentValidation;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.ValidationTests;

public sealed class AllModulesFluentValidationTests
{
    [Fact]
    public async Task Accounting_UpsertOperatingExpense_InvalidInputs_ReturnsNormalizedValidationErrorResponse()
    {
        var behaviour = new ValidationBehaviour<UpsertOperatingExpenseCommand, ServiceResponse>(
            new IValidator<UpsertOperatingExpenseCommand>[] { new UpsertOperatingExpenseCommandValidator() });

        var command = new UpsertOperatingExpenseCommand(
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

        var response = await behaviour.Handle(
            command,
            () => Task.FromResult(ServiceResponse.CreateSuccess()),
            CancellationToken.None);

        var validation = Assert.IsType<ValidationErrorResponse>(response);
        Assert.Equal(400, validation.StatusCode);
        Assert.True(validation.Errors.ContainsKey("ShopId"));
        Assert.True(validation.Errors.ContainsKey("Description"));
        Assert.True(validation.Errors.ContainsKey("Amount"));
        Assert.True(validation.Errors.ContainsKey("EntryDate"));
    }

    [Fact]
    public void Accounting_UpsertFixedAsset_SalvageExceedingCost_FailsValidation()
    {
        var validator = new UpsertFixedAssetCommandValidator();
        var cmd = new UpsertFixedAssetCommand(
            Id: null,
            ShopId: "53236321",
            AssetName: "Bambu Lab X1C",
            AssetAccountCode: "253",
            ExpenseTargetCategory: OperatingExpenseCategory.GeneralManagement_632,
            AcquisitionDate: new DateTime(2026, 1, 1),
            AcquisitionCostTry: 10000m,
            SalvageValueTry: 15000m,
            UsefulLifeYears: 5,
            Method: DepreciationMethod.StraightLine,
            IsActive: true);

        var result = validator.Validate(cmd);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SalvageValueTry");
    }

    [Fact]
    public async Task Listings_UpdateListing_WithMultipleAmpersands_NormalizesDtoPrefix()
    {
        var behaviour = new ValidationBehaviour<UpdateListingCommandRequest, ServiceResponse>(
            new IValidator<UpdateListingCommandRequest>[] { new UpdateListingCommandRequestValidator() });

        var req = new UpdateListingCommandRequest
        {
            ShopId = "53236321",
            ListingId = "123456",
            Dto = new UpdateListingRequestDto("53236321", "A & B & C Figurine", "Desc", new[] { "tag1" })
        };

        var response = await behaviour.Handle(
            req,
            () => Task.FromResult(ServiceResponse.CreateSuccess()),
            CancellationToken.None);

        var validation = Assert.IsType<ValidationErrorResponse>(response);
        Assert.True(validation.Errors.ContainsKey("Title"));
        Assert.DoesNotContain(validation.Errors.Keys, k => k.StartsWith("Dto."));
    }

    [Fact]
    public void Listings_UpdateStateAndImages_InvalidValues_FailValidation()
    {
        var stateResult = new UpdateListingStateCommandRequestValidator().Validate(new UpdateListingStateCommandRequest
        {
            ShopId = "53236321",
            ListingId = 0,
            State = "archived"
        });
        Assert.False(stateResult.IsValid);
        Assert.Contains(stateResult.Errors, e => e.PropertyName == "ListingId");
        Assert.Contains(stateResult.Errors, e => e.PropertyName == "State");

        var altResult = new UpdateListingImagesAltTextCommandValidator().Validate(new UpdateListingImagesAltTextCommand
        {
            ShopId = "53236321",
            ListingId = 99,
            Items = new[] { new UpdateImageAltTextItem(1, 1, new string('x', 260)) }
        });
        Assert.False(altResult.IsValid);
        Assert.Contains(altResult.Errors, e => e.PropertyName.StartsWith("AltText", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MarketResearch_And_OAuth_And_Settings_FailWithCleanKeys()
    {
        var marketBehaviour = new ValidationBehaviour<SearchMarketQuery, ServiceResponse>(
            new IValidator<SearchMarketQuery>[] { new SearchMarketQueryValidator() });

        var marketResp = await marketBehaviour.Handle(
            new SearchMarketQuery(new MarketSearchQuery("", 200)),
            () => Task.FromResult(ServiceResponse.CreateSuccess()),
            CancellationToken.None);

        var marketValidation = Assert.IsType<ValidationErrorResponse>(marketResp);
        Assert.True(marketValidation.Errors.ContainsKey("Keyword"));
        Assert.True(marketValidation.Errors.ContainsKey("Limit"));

        var tokenResult = new ImportEtsyTokenCommandValidator().Validate(
            new ImportEtsyTokenCommand("53236321", "", "ref", DateTimeOffset.UtcNow.AddMinutes(-5)));
        Assert.False(tokenResult.IsValid);
        Assert.Contains(tokenResult.Errors, e => e.PropertyName == "AccessToken");
        Assert.Contains(tokenResult.Errors, e => e.PropertyName == "AccessTokenExpiresAt");

        var tgResult = new SaveTelegramSettingsCommandRequestValidator().Validate(
            new SaveTelegramSettingsCommandRequest(
                new SaveTelegramSettingsRequest("53236321", "", "", true, true, true, true)));
        Assert.False(tgResult.IsValid);
    }

    [Fact]
    public async Task RemainingModules_Validators_CatchInvalidInputs()
    {
        Assert.False(new UploadOrdersCsvRequestValidator().Validate(new UploadOrdersCsvRequest()).IsValid);
        Assert.False(new SyncExtensionOrdersRequestValidator().Validate(new SyncExtensionOrdersRequest { ShopId = "" }).IsValid);
        Assert.False(new GetReceiptDetailRequestValidator().Validate(new GetReceiptDetailRequest { ReceiptId = 0 }).IsValid);
        Assert.False(new GetCarrierQuotesRequestValidator().Validate(new GetCarrierQuotesRequest
        {
            Query = new CarrierQuotesQuery { WeightKg = 0 }
        }).IsValid);
        Assert.False(new SavePaymentReserveSnapshotCommandValidator().Validate(
            new SavePaymentReserveSnapshotCommand(new EtsyPaymentReserveSnapshotDto("53236321", 0, 0, 0, 150, 30, null, null, DateTimeOffset.UtcNow))).IsValid);
        Assert.False(new GetBankPayoutsRequestValidator().Validate(new GetBankPayoutsRequest
        {
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(-1)
        }).IsValid);
        Assert.False(new EnqueueBatchItemsRequestValidator().Validate(new EnqueueBatchItemsRequest()).IsValid);
        Assert.False(new ProcessBatchItemRequestValidator().Validate(new ProcessBatchItemRequest { Id = 0, OverallScore = 150 }).IsValid);
        Assert.False(new UpdateAbTestStatusRequestValidator().Validate(new UpdateAbTestStatusRequest { Id = 0 }).IsValid);
        Assert.False(new DeleteAbTestRequestValidator().Validate(new DeleteAbTestRequest { Id = 0 }).IsValid);
        Assert.False(new ToggleUserStatusRequestValidator().Validate(new ToggleUserStatusRequest { Id = "" }).IsValid);
        Assert.False(new SaveTrackingCaptureRequestValidator().Validate(new SaveTrackingCaptureRequest()).IsValid);
        Assert.False(new DeleteTrackingItemRequestValidator().Validate(new DeleteTrackingItemRequest { Id = 0 }).IsValid);
        Assert.False(new RecordAiUsageRequestValidator().Validate(new RecordAiUsageRequest()).IsValid);

        var adminBehaviour = new ValidationBehaviour<UpdateUserCommandRequest, ServiceResponse>(
            new IValidator<UpdateUserCommandRequest>[] { new UpdateUserCommandRequestValidator() });

        var adminResp = await adminBehaviour.Handle(
            new UpdateUserCommandRequest
            {
                Id = "u1",
                Request = new UpdateUserRequest("bad-email", "InvalidRole", new List<string>(), -1, true)
            },
            () => Task.FromResult(ServiceResponse.CreateSuccess()),
            CancellationToken.None);

        var adminValidation = Assert.IsType<ValidationErrorResponse>(adminResp);
        Assert.True(adminValidation.Errors.ContainsKey("Email"));
        Assert.True(adminValidation.Errors.ContainsKey("Role"));
    }
}
