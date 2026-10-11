using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Auth.Validation;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ListingOptimization.Validation;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.ValidationTests;

public sealed class FluentValidationTests
{
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly UpdateUserRequestValidator _updateUserValidator = new();
    private readonly FastListingPublishCommandValidator _listingValidator = new();

    [Theory]
    [InlineData("", "password123")]
    [InlineData("user", "")]
    [InlineData("   ", "   ")]
    public void LoginRequest_WithEmptyFields_FailsValidation(string username, string password)
    {
        var request = new LoginRequest(username, password);
        var result = _loginValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void LoginRequest_WithValidFields_PassesValidation()
    {
        var request = new LoginRequest("admin", "Admin123*!");
        var result = _loginValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("ab", "valid@example.com", "pass1234")] // Username too short
    [InlineData("validuser", "invalid-email", "pass1234")] // Invalid email
    [InlineData("validuser", "valid@example.com", "123")] // Password too short
    public void RegisterRequest_WithInvalidFields_FailsValidation(string username, string email, string password)
    {
        var request = new RegisterRequest(username, email, password, "53236321");
        var result = _registerValidator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterRequest_WithValidFields_PassesValidation()
    {
        var request = new RegisterRequest("pro_seller", "pro@seller.com", "Secret123*", "53236321");
        var result = _registerValidator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void UpdateUserRequest_WithInvalidRoleOrNegativeQuota_FailsValidation()
    {
        var invalidRole = new UpdateUserRequest("test@test.com", "SuperHackerRole", new List<string>(), 1000, true);
        var result1 = _updateUserValidator.Validate(invalidRole);
        Assert.False(result1.IsValid);

        var negativeQuota = new UpdateUserRequest("test@test.com", UserRoles.StoreOwner, new List<string>(), -50, true);
        var result2 = _updateUserValidator.Validate(negativeQuota);
        Assert.False(result2.IsValid);
    }

    [Fact]
    public void FastListingPublishCommand_WithInvalidBasics_FailsValidation()
    {
        // Empty title & negative price & zero quantity
        var cmd = new FastListingPublishCommand(
            ShopId: "53236321",
            Title: "",
            Description: "A description",
            Price: -5m,
            Quantity: 0,
            TaxonomyId: 100,
            ShippingProfileId: null,
            ReadinessStateId: null,
            IsDigital: false,
            Tags: null,
            Materials: null,
            State: "draft"
        );

        var result = _listingValidator.Validate(cmd);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
        Assert.Contains(result.Errors, e => e.PropertyName == "Price");
        Assert.Contains(result.Errors, e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void FastListingPublishCommand_Exceeding13TagsOr20Chars_FailsValidation()
    {
        var fourteenTags = Enumerable.Range(1, 14).Select(i => $"tag{i}").ToList();
        var cmd1 = CreateSampleCommand(fourteenTags);
        var result1 = _listingValidator.Validate(cmd1);
        Assert.False(result1.IsValid);

        var longTag = new List<string> { "this_tag_is_way_longer_than_twenty_characters_long" };
        var cmd2 = CreateSampleCommand(longTag);
        var result2 = _listingValidator.Validate(cmd2);
        Assert.False(result2.IsValid);
    }

    [Fact]
    public void FastListingPublishCommand_Exceeding70VariationCombinations_FailsValidation()
    {
        var group1 = new ListingVariationGroupInput("Color", Enumerable.Range(1, 10).Select(i => $"Color {i}").ToList());
        var group2 = new ListingVariationGroupInput("Size", Enumerable.Range(1, 10).Select(i => $"Size {i}").ToList()); // 10 x 10 = 100 > 70 limit

        var cmd = new FastListingPublishCommand(
            ShopId: "53236321",
            Title: "Valid 3D Printed Dragon Figure",
            Description: "Valid description",
            Price: 25.50m,
            Quantity: 5,
            TaxonomyId: 100,
            ShippingProfileId: null,
            ReadinessStateId: null,
            IsDigital: false,
            Tags: new List<string> { "dragon", "3d print" },
            Materials: new List<string> { "PLA" },
            State: "draft",
            VariationGroups: new List<ListingVariationGroupInput> { group1, group2 }
        );

        var result = _listingValidator.Validate(cmd);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FastListingPublishCommand_WithCompliantFields_PassesValidation()
    {
        var cmd = CreateSampleCommand(new List<string> { "custom toy", "3d printed", "action figure" });
        var result = _listingValidator.Validate(cmd);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static FastListingPublishCommand CreateSampleCommand(IReadOnlyList<string> tags)
    {
        return new FastListingPublishCommand(
            ShopId: "53236321",
            Title: "Articulated Robot Action Figure 3D Printed Desk Toy",
            Description: "High quality articulated action figure printed with eco-friendly PLA plastic.",
            Price: 19.99m,
            Quantity: 10,
            TaxonomyId: 100,
            ShippingProfileId: null,
            ReadinessStateId: null,
            IsDigital: false,
            Tags: tags,
            Materials: new List<string> { "PLA" },
            State: "draft"
        );
    }
}
