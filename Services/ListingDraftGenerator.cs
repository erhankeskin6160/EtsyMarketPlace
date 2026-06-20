namespace SimilarProductsWinForms.Services;

using SimilarProductsWinForms.Models;

internal static class ListingDraftGenerator
{
    public static ListingDraft Generate(ProductCandidate product)
    {
        var primaryKeyword = BuildPrimaryKeyword(product);
        var title = $"{primaryKeyword} - 3D Printed Collectible Display Prop";
        var tags = BuildTags(product);

        var shortDescription =
            $"{primaryKeyword} is a 3D printed collectible display piece designed for desks, shelves, themed rooms, cosplay setups, and fan collections.";

        var longDescription =
            $"{shortDescription}{Environment.NewLine}{Environment.NewLine}" +
            "This draft is prepared for a made-to-order 3D printed item. Before publishing, add the exact dimensions, color options, finish type, processing time, and any personalization choices available for the final product." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Suggested Etsy category: {product.EtsyCategoryDisplay}{Environment.NewLine}" +
            $"Related store product idea: {product.RelatedStoreProduct}{Environment.NewLine}" +
            $"Opportunity note: {product.Reason}{Environment.NewLine}{Environment.NewLine}" +
            "Important: This listing text is a draft. Review wording carefully and avoid implying that the item is official, licensed, endorsed, or affiliated with any brand owner.";

        var photoChecklist = string.Join(Environment.NewLine, new[]
        {
            "1. Main photo: clear front view on a clean background",
            "2. Scale photo: product next to a common object or in hand",
            "3. Use-case photo: desk, shelf, wall, cosplay, or display setup",
            "4. Detail photo: close-up of texture, paint, light, or moving parts",
            "5. Variant photo: color/size options if available",
            "6. Package photo: what the buyer receives",
            "7. Dimension photo: ruler or measurement overlay"
        });

        var packageContents = string.Join(Environment.NewLine, new[]
        {
            "- 1 x 3D printed product",
            "- Optional display stand or mounting piece if included",
            "- Protective packaging",
            "- Care note / assembly note if needed"
        });

        var productionNotes = string.Join(Environment.NewLine, new[]
        {
            "- Confirm print orientation and support cleanup plan",
            "- Check sanding, painting, primer, and clear coat needs",
            "- Test fit any moving or detachable parts",
            "- Confirm packaging before publishing large props",
            "- Add exact processing time after test print"
        });

        var safetyNotes = string.Join(Environment.NewLine, new[]
        {
            "- Draft only: seller must review before publishing",
            "- Avoid official/licensed/endorsed wording unless legally accurate",
            "- Add age, sharp edge, heat, LED, or small-part warnings if relevant",
            "- Add exact dimensions and materials before listing"
        });

        return new ListingDraft(
            title,
            shortDescription,
            longDescription,
            string.Join(", ", tags),
            "PLA/PETG/resin 3D print, primer, acrylic paint, clear coat if applicable",
            packageContents,
            photoChecklist,
            productionNotes,
            safetyNotes);
    }

    private static string BuildPrimaryKeyword(ProductCandidate product)
    {
        var words = product.Keywords
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(5);
        return string.Join(' ', words);
    }

    private static List<string> BuildTags(ProductCandidate product)
    {
        var tags = new List<string>
        {
            "3d printed gift",
            "display prop",
            "cosplay prop",
            "desk decor",
            "fan collectible",
            "game room decor",
            "shelf decor",
            "custom prop",
            "collector gift",
            "printed figure",
            "movie room decor",
            "handmade prop",
            "geek gift"
        };

        foreach (var word in product.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = word.Trim('-', ':', ',', '.', '\'');
            if (clean.Length is >= 4 and <= 20 && !tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                tags.Insert(0, clean);
            }
        }

        return tags.Take(13).ToList();
    }
}
