namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class FastListingTemplateStoreTests
{
    [Fact]
    public void LoadAll_ContainsDefaultBuiltInTemplates()
    {
        var templates = FastListingTemplateStore.LoadAll();

        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.Name.Contains("Kupa"));
        Assert.Contains(templates, t => t.Name.Contains("Tişört"));
        Assert.Contains(templates, t => t.Name.Contains("Dijital"));
        Assert.Contains(templates, t => t.Name.Contains("Takı"));
    }

    [Fact]
    public void BuiltInTemplates_HaveValidListingSettings()
    {
        var builtIns = FastListingTemplateStore.GetBuiltInTemplates();

        foreach (var tmpl in builtIns)
        {
            Assert.False(string.IsNullOrWhiteSpace(tmpl.Name));
            Assert.True(tmpl.DefaultPrice > 0);
            Assert.True(tmpl.DefaultQuantity > 0);
            Assert.False(string.IsNullOrWhiteSpace(tmpl.DefaultTags));
            Assert.False(string.IsNullOrWhiteSpace(tmpl.DescriptionTemplate));

            var tags = FastListingDraftHelper.SanitizeTags(tmpl.DefaultTags);
            Assert.True(tags.Count <= 13);
            Assert.All(tags, tag => Assert.True(tag.Length <= 20));
        }
    }

    [Fact]
    public void BuiltInDigitalTemplate_IsMarkedAsDigital_AndHasNoShippingRequirement()
    {
        var builtIns = FastListingTemplateStore.GetBuiltInTemplates();
        var digital = builtIns.FirstOrDefault(t => t.Id == "builtin_digital");

        Assert.NotNull(digital);
        Assert.True(digital.IsDigital);
        Assert.False(digital.EnableVariations);
    }

    [Fact]
    public void BuiltInTshirtTemplate_HasDualVariationsEnabled()
    {
        var builtIns = FastListingTemplateStore.GetBuiltInTemplates();
        var tshirt = builtIns.FirstOrDefault(t => t.Id == "builtin_tshirt");

        Assert.NotNull(tshirt);
        Assert.True(tshirt.EnableVariations);
        Assert.True(tshirt.EnableVariation2);
        Assert.Contains("Size", tshirt.VariationType1);
        Assert.Contains("Color", tshirt.VariationType2);
    }

    [Fact]
    public void CustomTemplate_CanBeSavedAndDeleted()
    {
        var customTemplate = new FastListingTemplate
        {
            Id = "test_custom_" + Guid.NewGuid().ToString("N")[..8],
            Name = "🧪 Test Ahşap Tablo",
            DefaultPrice = 45.00m,
            DefaultQuantity = 5,
            IsDigital = false,
            DefaultTags = "wood art, rustic decor, wall hanging",
            DefaultMaterials = "oak wood, natural stain",
            DescriptionTemplate = "Test description for oak wood art"
        };

        try
        {
            FastListingTemplateStore.SaveCustom(customTemplate);
            var loaded = FastListingTemplateStore.LoadAll();

            var found = loaded.FirstOrDefault(t => t.Id == customTemplate.Id);
            Assert.NotNull(found);
            Assert.Equal(customTemplate.Name, found.Name);
            Assert.Equal(45.00m, found.DefaultPrice);
        }
        finally
        {
            FastListingTemplateStore.DeleteCustom(customTemplate.Id);
            var afterDelete = FastListingTemplateStore.LoadAll();
            Assert.DoesNotContain(afterDelete, t => t.Id == customTemplate.Id);
        }
    }
}
