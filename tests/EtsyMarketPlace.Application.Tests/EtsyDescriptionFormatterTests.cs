namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class EtsyDescriptionFormatterTests
{
    [Fact]
    public void NormalizeForEtsy_HandlesEmptyAndNullGracefully()
    {
        Assert.Equal("", EtsyDescriptionFormatter.NormalizeForEtsy(null));
        Assert.Equal("", EtsyDescriptionFormatter.NormalizeForEtsy("   "));
    }

    [Fact]
    public void NormalizeForEtsy_ConvertsMarkdownHeadingsToEmojiHeaders()
    {
        var raw = "### WHY YOU'LL LOVE IT\n* Premium finish\n* Great gift\n\n### SPECIFICATIONS & DETAILS\n* Size: 15cm";
        var result = EtsyDescriptionFormatter.NormalizeForEtsy(raw);

        Assert.Contains("✨ WHY YOU'LL LOVE IT", result);
        Assert.Contains("📏 SPECIFICATIONS & DETAILS", result);
        Assert.Contains("• Premium finish", result);
        Assert.Contains("• Size: 15cm", result);
        Assert.Contains("\r\n\r\n", result);
    }

    [Fact]
    public void NormalizeForEtsy_StripsBoldAndConvertsBullets()
    {
        var raw = "**Important:** This is a **handmade** item.\n- Fast shipping\n+ Extra gift\n* Safe box";
        var result = EtsyDescriptionFormatter.NormalizeForEtsy(raw);

        Assert.DoesNotContain("**", result);
        Assert.Contains("Important: This is a handmade item.", result);
        Assert.Contains("• Fast shipping", result);
        Assert.Contains("• Extra gift", result);
        Assert.Contains("• Safe box", result);
    }

    [Fact]
    public void FormatToStandardTemplate_Generates6SectionsWithDoubleNewlines()
    {
        var raw = "Handmade Minas Tirith model 3d printed 15cm height with PLA plastic.";
        var result = EtsyDescriptionFormatter.FormatToStandardTemplate(
            raw,
            "Minas Tirith Lamp Lord of the Rings Desk Decor",
            ["lotr lamp", "minas tirith", "fantasy decor"],
            ["PLA", "LED light"],
            "lotr lamp");

        Assert.Contains("✨ WHY YOU'LL LOVE IT:", result);
        Assert.Contains("📏 SPECIFICATIONS & DETAILS:", result);
        Assert.Contains("🎁 PERFECT FOR:", result);
        Assert.Contains("📦 PACKAGING & SHIPPING:", result);
        Assert.Contains("💬 CUSTOM REQUESTS & QUESTIONS:", result);
        Assert.Contains("PLA, LED light", result);
        Assert.Contains("15cm", result);
        Assert.Contains("\r\n\r\n", result);
    }

    [Fact]
    public void NormalizeForEtsy_UnmergesCrampedEmojiHeadersAndBullets()
    {
        var cramped = "Elevate your space with this unique figure! Tailored for fans. ✨ WHY YOU'LL LOVE IT: • Premium Craftsmanship: Smooth finish. • Eye-Catching Display: Great look. 📏 SPECIFICATIONS & DETAILS: • Materials: Resin";
        var result = EtsyDescriptionFormatter.NormalizeForEtsy(cramped);

        Assert.Contains("✨ WHY YOU'LL LOVE IT:", result);
        Assert.Contains("\r\n• Premium Craftsmanship: Smooth finish.", result);
        Assert.Contains("\r\n• Eye-Catching Display: Great look.", result);
        Assert.Contains("📏 SPECIFICATIONS & DETAILS:", result);
        Assert.Contains("\r\n• Materials: Resin", result);
        Assert.Contains("\r\n\r\n", result);
    }

    [Fact]
    public void FormatToStandardTemplate_AdaptsHookAndAudienceForMusicTheme()
    {
        var raw = "Michael Jackson 3D printed statue bust height 25cm. Hand painted resin for collectors.";
        var result = EtsyDescriptionFormatter.FormatToStandardTemplate(
            raw,
            "Michael Jackson Statue King of Pop Bust",
            ["michael jackson", "king of pop", "statue", "music gift"],
            ["Resin", "Paint"],
            "michael jackson");

        Assert.Contains("Celebrate the legendary icon", result);
        Assert.Contains("Dedicated music fans", result);
        Assert.DoesNotContain("anime lovers", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("25cm", result);
        Assert.Contains("hand painted", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatToStandardTemplate_AdaptsHookAndAudienceForLightingTheme()
    {
        var raw = "Desk reading light 3d printed lamp with USB power cable and warm glow for bedroom.";
        var result = EtsyDescriptionFormatter.FormatToStandardTemplate(
            raw,
            "LED Desk Reading Light Lamp",
            ["desk lamp", "reading light", "ambient lamp"],
            ["PLA", "LED"],
            "desk lamp");

        Assert.Contains("ambient glow", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Home decor lovers", result);
        Assert.DoesNotContain("anime lovers", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatToStandardTemplate_AdaptsHookAndAudienceForSpaceTheme()
    {
        var raw = "Astronaut figurine night light with gentle glow for kids bedroom or nursery.";
        var result = EtsyDescriptionFormatter.FormatToStandardTemplate(
            raw,
            "LED Light Up Astronaut Figurine Night Light",
            ["astronaut lamp", "night light", "space decor"],
            ["PLA", "LED"],
            "astronaut night light");

        Assert.Contains("cosmic journey", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Space enthusiasts", result);
        Assert.DoesNotContain("anime lovers", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cosplay enthusiasts", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatToStandardTemplate_PreservesAlreadyStructuredEtsyDescription()
    {
        var structured = """
            Embark on a celestial adventure with this stunning LED Astronaut Night Light!
            
            ✨ WHY YOU'LL LOVE IT:
            • Gentle Eye-Safe Glow: Perfect for restful bedtime.
            • Hand-Finished Lunar Surface: Detailed helmet visor.
            
            📏 SPECIFICATIONS & DETAILS:
            • Materials: PLA Plastic, LED
            • Dimensions: 15cm x 8cm
            
            🎁 PERFECT FOR:
            • Kids, toddlers, and aspiring space explorers.
            
            📦 PACKAGING & SHIPPING:
            • Multi-layer foam protection.
            
            💬 CUSTOM REQUESTS & QUESTIONS:
            • Contact us anytime for custom visor colors!
            """;

        var result = EtsyDescriptionFormatter.FormatToStandardTemplate(
            structured,
            "LED Astronaut Night Light",
            ["astronaut lamp"],
            ["PLA"],
            "astronaut lamp");

        Assert.Contains("Hand-Finished Lunar Surface: Detailed helmet visor.", result);
        Assert.Contains("Gentle Eye-Safe Glow: Perfect for restful bedtime.", result);
        Assert.Contains("Kids, toddlers, and aspiring space explorers.", result);
        Assert.DoesNotContain("Collector & Fan Approved:", result);
    }
}
