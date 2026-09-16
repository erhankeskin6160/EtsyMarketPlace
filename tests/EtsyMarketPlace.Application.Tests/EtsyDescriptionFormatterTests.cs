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
        var raw = "Astronaut night light 3d printed lamp with USB power cable and warm glow for bedroom.";
        var result = EtsyDescriptionFormatter.FormatToStandardTemplate(
            raw,
            "LED Astronaut Night Light Lamp",
            ["astronaut lamp", "night light", "space decor"],
            ["PLA", "LED"],
            "astronaut lamp");

        Assert.Contains("ambient glow", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Home decor lovers", result);
        Assert.DoesNotContain("anime lovers", result, StringComparison.OrdinalIgnoreCase);
    }
}
