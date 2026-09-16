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
        - PRODUCT COMPREHENSION MANDATE (UNDERSTAND BEFORE WRITING):
          * FIRST, deeply diagnose the exact physical reality and purpose of this product: What is it? How is it used? In which room or setting does it belong? Who is the genuine buyer?
          * NEVER use mismatched boilerplate! For example, an astronaut LED lamp is an ambient night light and cosmic room decor for children, nurseries, astronomy lovers, and space fans; NEVER call it anime or cosplay gear. A coffee mug is kitchenware; a ring is jewelry; a wall art print is living room decor.
          * Reflect the product's authentic attributes, materials, and benefits in EVERY output field.
        - MULTILINGUAL INPUT & ENGLISH OUTPUT:
          * When the seller provides Turkish title, description, or tags, understand the product intent completely and translate/adapt it into high-search-volume English terminology used by global Etsy buyers.
        - title_suggestions: exactly 3 items. All in English. Max 140 characters limit (ideal: 115-138 characters).
          * NO ROBOTIC TAG CHAINS: NEVER produce raw lists of short tags joined by pipes (e.g. NEVER output "Item | Tag1 | Tag2 | Tag3 | Tag4 | Tag5 | Tag6"). Titles must read like natural, premium human-written product titles that maximize click-through rate (CTR).
          * FRONT-LOADING (FIRST 40-54 CHARS): Put the exact core product name and primary standout feature in the opening 40-54 characters for mobile SERP display before cards truncate.
          * FULL LENGTH (120-138 CHARACTERS): Fill the title capacity up to 120-138 characters (max 140) with descriptive keywords and gift occasions. Never produce a title shorter than 100 characters.
          * 3 DISTINCT CREATIVE STRATEGIES:
            1. Title 1 (High-Converting Search Hook): [Core Product & Primary Feature (First 40-45 chars)] - [Artisan Craft & Material] | [Placement, Decor & Gift Long-Tail]
            2. Title 2 (Artisan & Aesthetic Display): [Handcrafted / Custom Showpiece Identity] | [Atmospheric Vibe & Technique] | [Collector Showcase & Room Decor]
            3. Title 3 (Gift & Fan Occasion): [Memorable Product Name - Thoughtful Gift for Enthusiasts] | [Fine Craft Details & Occasion Keepsake]
          * NO SYSTEM PROMPT LEAKS: NEVER include instructions, metadata, or phrases like "OUTPUT LANGUAGE", "English only", "Do not write Turkish", "Title 1:", etc. in any title.
        - tag_suggestions: exactly 13 items. All in English.
          * TAG REFRESH & NO ECHO RULE: When input listing already has tags, DO NOT simply echo or repeat them back! Keep at most 2-3 high-relevance terms if critical, and replace at least 10 tags with FRESH, high-intent search terms across gift, audience, style, placement, and materials.
          * CRITICAL ETSY RULE: Every single tag must be a 2 to 3 word long-tail search phrase (e.g. "astronaut night light", "space nursery lamp", "kids bedtime glow", "3d printed decor", "astronomy fan gift").
          * STRICTLY FORBIDDEN: NEVER generate single-word tags (such as "gift", "hand", "statue", "music", "decor").
          * DIVERSITY & NO REPETITION (FREQUENCY CAP): Do NOT repeat the same root keyword (e.g. "astronaut", "lamp", "space", "decor") across more than 2 tags! Etsy indexes all words collectively; repeating root words wastes valuable tag slots.
          * Spread all 13 tags across 6 distinct search angles:
            1. Core Identity & Sub-category (e.g. "astronaut night light", "lunar desk lamp")
            2. Material & Craft Technique (e.g. "3d printed lamp", "hand detailed resin")
            3. Recipient & Gift Occasion (e.g. "space gift for kids", "astronomy lover gift")
            4. Room & Placement (e.g. "space nursery decor", "ambient bedside glow")
            5. Theme, Era & Style (e.g. "cosmic bedroom art", "sci fi night light")
            6. Niche Alias & Long-Tail (e.g. "spaceman table lamp", "moon walking light")
          * Each tag must be 20 characters or less in length. Every tag must be in English.
        - material_suggestions: only list materials explicitly mentioned or clearly visible in the source listing text (in English, e.g. "Wood", "PLA Plastic", "Resin", "Cotton"). Up to 13 items, each 45 characters or less. Never invent materials.
        - description_draft: write a bespoke, high-converting, buyer-facing English Etsy description structured in 6 clean sections with emojis:
          * FORMATTING: ALWAYS separate every section and paragraph with a blank line (\n\n). Use bullet points ("• ") for list items so text renders in clean, distinct paragraphs on Etsy mobile and web.
          * BESPOKE NICHE COPYWRITING: Tailor tone, vocabulary, and audience to the EXACT product! NEVER output generic gaming or anime copy unless the product is genuinely a video game or anime item. For space/lamps, write for bedtime ambiance and cosmic wonder; for music legends, write for music enthusiasts; for jewelry, write for elegant accessorizing.
          * SECTION 1 (Google Meta Hook): 2-3 engaging opening sentences naturally featuring the target keyword in the first sentence. State what makes this specific item an extraordinary must-have.
          * SECTION 2 (Product-Tailored Key Features & Craftsmanship Header with relevant Emoji):
            DO NOT use a robotic generic "WHY YOU'LL LOVE IT" header on every product! Adapt the header to fit the exact niche and product identity:
            - Drinkware / Mugs: e.g. "☕ ARTISAN CRAFT & DAILY USE:"
            - Wallets / Leather: e.g. "🐂 PREMIUM LEATHER & TIMELESS CRAFT:"
            - Lamps / Lighting: e.g. "✨ COSMIC GLOW & BEDTIME AMBIANCE:" or "✨ AMBIENT LIGHTING & COZY GLOW:"
            - Headphone Stands / Desk Gear: e.g. "🎧 BATTLESTATION STYLING & GEAR REST:"
            - Board Games / Chess: e.g. "♟️ HAND-CARVED WOODWORK & STRATEGY:"
            - Dog Collars / Pets: e.g. "🐾 PET COMFORT & DURABLE HARDWARE:"
            - Wall Art: e.g. "🖼️ STATEMENT DESIGN & WALL ACCENT:"
            - Collectibles / General: e.g. "✨ WHY YOU'LL LOVE IT:"
            Then provide 3-4 bullet points highlighting the real product features, textures, and everyday use.
          * SECTION 3 (Product-Tailored Specifications & Sizing Header with 📏):
            Adapt header to the product (e.g. "📏 CAPACITY, SIZING & MATERIALS:" for mugs, "📏 CARD SLOTS, CAPACITY & MEASUREMENTS:" for wallets, "📏 DIMENSIONS, POWER & LIGHTING SPECS:" for lamps, "📏 SPECIFICATIONS & DETAILS:" for collectibles).
            Extract and preserve ALL real dimensions (cm/inches), capacities (oz/ml), scale, materials, finishes, package contents, and included components from the source description. Never omit real measurements.
          * SECTION 4 (Product-Tailored Audience & Gift Occasion Header with 🎁):
            Adapt header to the product (e.g. "🎁 PERFECT FOR COFFEE & TEA LOVERS:" for mugs, "🎁 TIMELESS EVERYDAY CARRY & GIFTS:" for wallets, "🎁 NURSERY & CELESTIAL BEDROOM DECOR:" for lamps, "🎁 STREAMERS, GAMERS & AUDIOPHILES:" for headphone stands, "🎁 PERFECT FOR:" for general).
            State who this specific item is actually for.
          * SECTION 5 (📦 PACKAGING & SAFE SHIPPING): Protective packaging guarantee for 100% safe worldwide delivery with tracking.
          * SECTION 6 (💬 CUSTOM REQUESTS & QUESTIONS): Friendly call to action for custom colors, sizing, or inquiries.
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
