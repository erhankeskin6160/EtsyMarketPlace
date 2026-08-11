namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Text;

/// <summary>
/// Centralizes AI prompt and instruction generation for Etsy listing drafts.
/// Encapsulates Etsy Seller Handbook rules, JSON response schema expectations,
/// and context-aware prompt construction for both optimization and creation flows.
/// </summary>
public static class ListingDraftInstructionBuilder
{
    /// <summary>
    /// Builds the system-level instruction that defines AI behavior for Etsy listing generation.
    /// Should be used as the system prompt / system_instruction for both OpenAI and Gemini providers.
    /// </summary>
    public static string BuildSystemInstruction() =>
        """
        You are an expert Etsy listing optimization assistant trained on Etsy Seller Handbook best practices.
        Your goal is to produce high-quality, buyer-searchable, policy-compliant Etsy listings.
        Always respond with valid JSON only. Do not include explanations, markdown, or commentary outside the JSON object.
        """;

    /// <summary>
    /// Builds the full user prompt for optimizing an existing listing.
    /// Combines Etsy knowledge base rules, JSON schema instructions, and the listing context.
    /// </summary>
    public static string BuildOptimizationPrompt(ListingOptimizationInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildResponseSchemaInstruction());
        builder.AppendLine();
        builder.AppendLine(EtsyListingKnowledgeBase.BuildAiInstructionBlock());
        builder.AppendLine();
        builder.AppendLine(BuildFieldRules());
        builder.AppendLine();
        builder.AppendLine(BuildListingContext(input.TargetKeyword, input.Title, input.Tags, input.Description));
        return builder.ToString();
    }

    /// <summary>
    /// Builds the full user prompt for creating a new listing draft from product discovery.
    /// Accepts optional product type and category to generate more targeted instructions.
    /// </summary>
    public static string BuildCreationPrompt(
        string title,
        string description,
        IReadOnlyList<string> tags,
        string targetKeyword,
        string? productType = null,
        string? category = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildResponseSchemaInstruction());
        builder.AppendLine();
        builder.AppendLine(EtsyListingKnowledgeBase.BuildAiInstructionBlock());
        builder.AppendLine();
        builder.AppendLine(BuildFieldRules());

        if (!string.IsNullOrWhiteSpace(productType))
        {
            var isDigital = productType.Contains("digital", StringComparison.OrdinalIgnoreCase)
                || productType.Contains("dijital", StringComparison.OrdinalIgnoreCase);
            builder.AppendLine();
            builder.AppendLine($"Product type: {productType}");
            builder.AppendLine(isDigital
                ? "This is a digital download product. Focus description on file format, resolution, usage rights, and instant download benefits. Do not reference physical shipping or materials."
                : "This is a physical product. Include dimensions, weight estimates, material quality, and shipping considerations in the description.");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            builder.AppendLine($"Target Etsy category: {category}");
        }

        builder.AppendLine();
        builder.AppendLine(BuildListingContext(targetKeyword, title, tags, description));
        return builder.ToString();
    }

    /// <summary>
    /// Returns the JSON response schema instruction block that tells the AI what keys to return.
    /// </summary>
    public static string BuildResponseSchemaInstruction() =>
        """
        Return only valid JSON with the following keys:
        - "title_suggestions": array of 3 English Etsy titles
        - "tag_suggestions": array of up to 13 English Etsy tags
        - "material_suggestions": array of materials found in the product
        - "description_draft": string with buyer-facing English description
        - "risk_warnings": array of Turkish risk/policy warning strings
        """;

    /// <summary>
    /// Returns the field-level constraint rules for each listing component.
    /// </summary>
    public static string BuildFieldRules() =>
        """
        Field constraints:
        - title_suggestions: exactly 3 items. Each must be a readable English Etsy title under 140 characters. Put the product identity in the first 3-5 words. Avoid vague claims (perfect, best, official, licensed, authentic) unless the source listing explicitly proves them. Do not keyword-stuff; keep titles natural and buyer-friendly.
        - tag_suggestions: up to 13 items. Each tag must be 20 characters or less. Use multi-word, long-tail buyer search phrases when possible. Do not repeat the same word across more than 3 tags. Every tag must be in English.
        - material_suggestions: only list materials explicitly mentioned or clearly visible in the source listing text. Up to 13 items, each 45 characters or less. Never invent materials.
        - description_draft: write unique, buyer-facing English copy for this exact product. Structure in 4-6 short paragraphs: (1) product identity and appeal, (2) who it is for / use cases, (3) materials, finish, dimensions, (4) care or usage instructions if applicable, (5) publishing review note with any brand/IP risk. Do not start with generic phrases like "This item is prepared as an Etsy-ready product listing". Naturally weave the target keyword into the first paragraph without forcing it.
        - risk_warnings: Turkish language notes. Flag any brand, character, movie, game, or fan-art terms. Include Turkish explanations in parentheses. Example: "Ben 10 ve Omnitrix terimleri telif riski tasiyabilir (Cartoon Network markasi)."
        """;

    /// <summary>
    /// Formats the current listing data as context for the AI prompt.
    /// </summary>
    private static string BuildListingContext(
        string targetKeyword,
        string title,
        IReadOnlyList<string> tags,
        string description) =>
        $"""
        Listing context:
        Target keyword: {targetKeyword}
        Current title: {title}
        Current tags: {string.Join(", ", tags)}
        Current description: {description}
        """;
}
