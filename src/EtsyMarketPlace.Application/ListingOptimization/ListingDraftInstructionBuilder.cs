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
        - "current_seo_score": integer from 0 to 100 representing your professional SEO rating of the seller's input listing
        - "optimized_seo_score": integer from 0 to 100 representing the projected SEO rating of your new draft (typically 90-98)
        - "seo_critique": Turkish string explaining why points were deducted from the current listing and how your draft improves it
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
        - title_suggestions: exactly 3 items. All in English. Max 140 characters limit (STRICT TARGET: 130-139 characters).
          * NO ROBOTIC TAG CHAINS: NEVER produce raw lists of short tags joined by pipes (e.g. NEVER output "Item | Tag1 | Tag2 | Tag3 | Tag4 | Tag5 | Tag6"). Titles must read like natural, premium human-written product titles that maximize click-through rate (CTR).
          * TWO-ZONE ARCHITECTURE MANDATE (MANDATORY FOR ALL 3 TITLES):
            - ZONE 1: FRONT-LOADING (FIRST 40-55 CHARS) - CRITICAL MOBILE CUTOFF RULE:
              * Etsy mobile apps and mobile browsers truncate listing titles at approximately 50-55 characters.
              * The FIRST 40-55 CHARACTERS MUST clearly communicate the essential buying factors before truncation:
                (1) Exact core item name (e.g. "Hand-Painted Hulk Figure 30cm" or "Valorant Kuronami Knife Replica 25cm"),
                (2) Key standout attribute or theme (e.g. "Marvel Superhero Statue" or "Cosplay Prop Weapon"),
                (3) Size or key specification if critical.
              * A mobile shopper looking at search cards must instantly recognize WHAT the item is in the first 50-55 characters.
              * NEVER start with generic fluff like "Gift for Him", "Personalized Gift", "Unique Decor", or "Awesome Present". Front-load the product itself!
            - ZONE 2: MAXIMUM CAPACITY & FULL LENGTH (STRICT TARGET: 130-139 CHARACTERS):
              * PUSH THE LENGTH AS CLOSE TO 140 CHARACTERS AS POSSIBLE! Do NOT stop at 80-110 characters.
              * Fill the remaining ~85 characters with high-intent search angles: gift occasions ("Gamer Gift, Birthday Present for Boyfriend"), room placement ("Desk Display, Gaming Room Decor"), style/fan details, and craft materials.
              * Never produce a title shorter than 120 characters, and NEVER exceed 140 characters. Target sweet spot: 128 - 139 characters.
          * 3 DISTINCT CREATIVE STRATEGIES:
            1. Title 1 (High-Converting Mobile Hook): [Core Product & Standout Feature (First 50 chars)] - [Artisan Craft & Material] | [Room Placement & Gift Long-Tail]
            2. Title 2 (Collector & Aesthetic Display): [Handcrafted Showcase Identity (First 50 chars)] | [Atmospheric Vibe & Material] | [Collector Niche & Room Decor]
            3. Title 3 (Gift & Occasion Keepsake): [Memorable Product Name & Sizing (First 50 chars)] - [Gift for Him/Her & Enthusiasts] | [Fine Craft Details]
          * NO SYSTEM PROMPT LEAKS: NEVER include instructions, metadata, or phrases like "OUTPUT LANGUAGE", "English only", "Do not write Turkish", "Title 1:", etc. in any title.
        - tag_suggestions: exactly 13 items. All in English.
          * TAG REFRESH & NO ECHO RULE: When input listing already has tags, DO NOT simply echo or repeat them back! Keep at most 2-3 high-relevance terms if critical, and replace at least 10 tags with FRESH, high-intent search terms across gift, audience, style, placement, and materials.
          * CRITICAL ETSY RULE: Every single tag must be a 2 to 3 word long-tail search phrase that is 100% specific to THIS EXACT product being listed. NEVER borrow examples from these instructions — generate tags exclusively from the product's own identity, materials, audience, and use case.
          * STRICTLY FORBIDDEN: NEVER generate single-word tags (such as "gift", "hand", "statue", "music", "decor").
          * STRICTLY FORBIDDEN: NEVER use tags that belong to a DIFFERENT product category or niche (e.g. do NOT use lamp/space/nursery tags for a collectible figure; do NOT use anime/gaming tags for kitchenware; do NOT use food tags for wall art).
          * DIVERSITY & NO REPETITION (FREQUENCY CAP): Do NOT repeat the same root keyword across more than 2 tags! Etsy indexes all words collectively; repeating root words wastes valuable tag slots.
          * Spread all 13 tags across 6 distinct search angles derived exclusively from THIS PRODUCT's actual attributes:
            1. Core Identity & Sub-category: What IS this product? (e.g. for a music legend figure → "michael jackson figure", "printed pop statue")
            2. Material & Craft Technique: How is it made? (e.g. for a 3D printed item → "3d printed figure", "pla plastic model")
            3. Recipient & Gift Occasion: Who buys it as a gift and when? (e.g. "music fan gift", "birthday collector gift")
            4. Room & Placement: Where is it displayed or used? (e.g. "music room decor", "shelf display piece")
            5. Theme, Era & Style: What theme/era/style does it evoke? (e.g. "pop music legend", "80s music icon")
            6. Niche Alias & Long-Tail: Alternative buyer search phrases? (e.g. "king of pop gift", "celebrity fan statue")
          * Each tag must be 20 characters or less in length. Every tag must be in English.
        - material_suggestions: only list materials explicitly mentioned or clearly visible in the source listing text (in English, e.g. "Wood", "PLA Plastic", "Resin", "Cotton"). Up to 13 items, each 45 characters or less. Never invent materials.
        - description_draft: Craft a natural, authentic, and compelling English Etsy description tailored specifically to this product's character, style, and niche.
          * COMPLETE CREATIVE FREEDOM & UNIQUE VOICE (NO REPETITIVE FORMULAS):
            - STRICTLY FORBIDDEN: NEVER force products into a rigid, robotic copy-paste template (such as always using "WHY YOU'LL LOVE IT", "SPECIFICATIONS & DETAILS", "PERFECT FOR", "PACKAGING & SAFE SHIPPING", "CUSTOM REQUESTS & QUESTIONS").
            - AS AN EXPERT COPYWRITER, DECIDE THE BEST STRUCTURE, TONE, AND FORMAT YOURSELF based on what makes THIS particular product sell:
              • For collectibles, statues, and fan art: Focus on sculpting drama, shelf presence, character attitude, and collector prestige.
              • For home decor, lighting, and ceramics: Write with warmth, atmosphere, cozy room mood, and artisan craft aesthetic.
              • For jewelry, luxury goods, and keepsakes: Highlight emotional gifting sentiment, timeless beauty, and refined elegance.
              • For everyday items, tech gear, and tools: Highlight practical durability, ergonomics, and seamless daily utility.
              • You may use storytelling narrative, sleek minimalist layouts, engaging bullet points ("• "), or specialized custom sections — whichever presents THIS product most convincingly.
          * PRESERVE ALL REAL SPECIFICATIONS & DETAILS:
            - If the seller provided real measurements (cm, mm, inches), scale, materials, colors, package contents, or care instructions (even in Turkish), translate and weave EVERY SINGLE DETAIL seamlessly into the description. Never drop or omit user-provided technical specs!
          * FORMATTING & SCAN-ABILITY:
            - Always separate paragraphs and sections with clean blank lines (\n\n) so text renders with airy, readable spacing on both mobile apps and web browsers.
            - Write directly to the buyer as a passionate artisan or boutique shop owner.
          * STRICTLY FORBIDDEN: NEVER include debug labels (such as "Selected listing title:", "Competitor description:", "Etsy search keyword:", "Target keyword:"), prompt words, or generic filler like "This item is prepared as an Etsy-ready product listing". Write directly to the customer.
          * ZERO TURKISH IN BUYER FIELDS: Ensure 100% of description_draft is in natural English. No Turkish words allowed in description_draft.
        - risk_warnings: Turkish language notes. Flag any brand, character, movie, game, or fan-art terms. Include Turkish explanations in parentheses. Example: "Ben 10 ve Omnitrix terimleri telif riski tasiyabilir (Cartoon Network markasi)."
        - current_seo_score & optimized_seo_score:
          * current_seo_score: Integer (0-100). Honestly evaluate the input listing's real Etsy SEO strength based on title clarity, front-loading, keyword searchability, tag count/quality, and description depth.
          * optimized_seo_score: Integer (0-100, typically 92-99). The projected SEO rating of your newly generated draft.
        - seo_critique: Turkish explanation (1-3 concise sentences) detailing why points were deducted from the seller's current listing and how your optimized output fixes them.
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
        Current description & seller specifications:
        {description}

        CRITICAL SELLER SPECIFICATIONS MANDATE:
        The text above may contain raw product notes, dimensions, materials, and features written in TURKISH or informal shorthand.
        You MUST preserve EVERY single specification, dimension (cm/mm/inches), material, package item, and functional feature!
        Translate all Turkish terms into 100% natural, fluent American English.
        Under NO circumstances should any seller-provided measurement or feature be dropped or swallowed!
        """;
}
