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
        You are an expert Etsy listing optimization assistant and professional e-commerce copywriter trained on Etsy Seller Handbook best practices.
        Your goal is to produce high-quality, buyer-searchable, policy-compliant Etsy listings in fluent, native English for the international global market.

        CRITICAL MULTILINGUAL & TRANSLATION RULES:
        - The seller may input title, description, tags, keywords, or specifications in TURKISH or other languages.
        - You MUST accurately understand the product concept, dimensions, materials, craftsmanship, and use cases from the seller's input language.
        - You MUST output ALL buyer-facing listing fields ("title_suggestions", "tag_suggestions", "material_suggestions", "description_draft") 100% IN NATURAL, HIGH-CONVERTING ENGLISH.
        - NEVER output Turkish words or untranslated Turkish terms in titles, tags, materials, or description.
        - Only "risk_warnings" should contain Turkish explanations for the seller's internal policy awareness.
        - Always respond with valid JSON only. Do not include explanations, markdown, or commentary outside the JSON object.
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
        - "material_suggestions": array of materials found in the product (in English)
        - "description_draft": string with buyer-facing English description
        - "risk_warnings": array of Turkish risk/policy warning strings
        """;

    /// <summary>
    /// Returns the field-level constraint rules for each listing component.
    /// </summary>
    public static string BuildFieldRules() =>
        """
        Field constraints:
        - MULTILINGUAL INPUT & ENGLISH OUTPUT:
          * When the seller provides Turkish title, description, or tags, understand the product intent completely and translate/adapt it into high-search-volume English terminology used by global Etsy buyers.
        - title_suggestions: exactly 3 items. All in English.
          * GOLDEN ETSY SEO TITLE FORMULA: Format every title with 2-3 readable segments separated by " | " or " - ":
            [Core Product Name (First 30-40 characters, Front-loaded)] | [Key Features, Style, Materials or Use-Case] | [Target Audience, Room Decor, Cosplay or Gift Long-Tail]
          * FRONT-LOADING: Put the exact core product name in the very first 3 to 5 words so mobile shoppers immediately understand the item.
          * LENGTH: Each title must be between 115 and 138 characters (maximizing Etsy's 140 character limit).
          * NO KEYWORD STUFFING: Do NOT produce raw comma-separated lists of tags (e.g. NEVER output "Title - tag1, tag2, tag3"). Titles must read like natural, premium human-written product titles.
          * NO SYSTEM PROMPT LEAKS: NEVER include instructions, metadata, or phrases like "OUTPUT LANGUAGE", "English only", "Do not write Turkish", "Title 1:", etc. in any title.
        - tag_suggestions: exactly 13 items. All in English.
          * CRITICAL ETSY RULE: Every single tag must be a 2 to 3 word long-tail search phrase (e.g. "sauron dark tower", "lotr collectible", "fantasy desk decor", "3d printed statue", "geeky boyfriend gift").
          * STRICTLY FORBIDDEN: NEVER generate single-word tags (such as "gift", "hand", "lotr", "tower", "dark", "painted", "printed").
          * Each tag must be 20 characters or less in length. Every tag must be in English.
          * Cover 6 search angles: (1) Product/Character Name, (2) Craft & Technique, (3) Recipient & Gift, (4) Room & Placement, (5) Theme & Universe, (6) Material & Style.
        - material_suggestions: only list materials explicitly mentioned or clearly visible in the source listing text (in English, e.g. "Wood", "PLA Plastic", "Resin", "Cotton"). Up to 13 items, each 45 characters or less. Never invent materials.
        - description_draft: write a high-converting, buyer-facing English Etsy description structured in 6 clean sections with emojis:
          * FORMATTING: ALWAYS separate every section and paragraph with a blank line (\n\n). Use bullet points ("• ") for list items so text renders in clean, distinct paragraphs on Etsy mobile and web.
          * SECTION 1 (Google Meta Hook): 2-3 engaging opening sentences naturally featuring the target keyword in the first sentence. State what makes this item unique and must-have.
          * SECTION 2 (✨ WHY YOU'LL LOVE IT): 3-4 bullet points highlighting key benefits, design quality, and display/practical use.
          * SECTION 3 (📏 SPECIFICATIONS & DETAILS): Extract and preserve ALL real dimensions (cm/inches), 3D print material (PLA/Resin/Wood), finish, and colors from the source description.
          * SECTION 4 (🎁 PERFECT FOR): Who this item is for (Gamers, Collectors, Cosplay, Desk Decor, Birthday/Holiday Gifts).
          * SECTION 5 (📦 PACKAGING & SHIPPING): Protective packaging guarantee for 100% safe worldwide delivery with tracking.
          * SECTION 6 (💬 CUSTOM REQUESTS & QUESTIONS): Friendly call to action for custom colors or sizing.
          * STRICTLY FORBIDDEN: NEVER include debug labels (such as "Selected listing title:", "Competitor description:", "Etsy search keyword:", "Target keyword:"), prompt words, or generic filler like "This item is prepared as an Etsy-ready product listing". Write directly to the customer.
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
