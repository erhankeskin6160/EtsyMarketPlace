namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Etsy ürün açıklamalarını alıcı odaklı, okunaklı, emojili ve paragraflara ayrılmış
/// standart Etsy şablonuna dönüştüren ve Etsy API v3 için çift satır atlamasını (\r\n\r\n) garanti eden formatlayıcı.
/// </summary>
public static class EtsyDescriptionFormatter
{
    private static readonly Regex MarkdownHeadingRegex = new(@"^#{1,6}\s*(.+)$", RegexOptions.Multiline);
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*|__(.+?)__");
    private static readonly Regex BulletRegex = new(@"^[\*\-\+]\s+", RegexOptions.Multiline);

    /// <summary>
    /// Ham veya AI tarafından üretilen metni Etsy standartlarında temiz, çift satır aralıklı paragraflara dönüştürür.
    /// Etsy mobil ve masaüstü arayüzünde metinlerin birbirine yapışmasını (merged block) engeller.
    /// </summary>
    public static string NormalizeForEtsy(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return "";

        // 1. Debug ve UI etiketlerini temizle (kullanıcı tüm arayüzü kopyaladıysa)
        var cleaned = rawText;
        cleaned = Regex.Replace(cleaned, @"\[SATIŞ\s+ODAKLI\s+AÇIKLAMA\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[MEVCUT\s+BAŞLIK[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[MEVCUT\s+TAGLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[PUAN\s+&\s+METRİKLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[TESPİT\s+EDİLEN\s+EKSİKLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[RİSK\s+VE\s+KURAL\s+UYARILARI[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[ÖNERİLEN\s+MATERYALLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[ÖNERİLEN\s+13\s+LONG-TAIL\s+TAG[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[AI\s+İLE\s+OPTİMİZE\s+EDİLMİŞ\s+BAŞLIK[^\]]*\]", "", RegexOptions.IgnoreCase);

        // 2. Markdown başlıklarını emoji/büyük harfli Etsy başlıklarına çevir
        cleaned = MarkdownHeadingRegex.Replace(cleaned, m =>
        {
            var text = m.Groups[1].Value.Trim();
            return EnsureSectionHeaderEmoji(text);
        });

        // 3. Kalın / İtalik markdown işaretlerini kaldır
        cleaned = BoldRegex.Replace(cleaned, m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);

        // 4. Madde işaretlerini standart '• ' ile değiştir
        cleaned = BulletRegex.Replace(cleaned, "• ");

        // 4.5. Yan yana sıkışmış olabilecek emoji başlıkları ve madde imlerini yeni satıra taşı
        cleaned = Regex.Replace(cleaned, @"(?<=[^\r\n])\s*(✨|📏|🎁|📦|💬|🧼)\s*", "\r\n\r\n$1 ");
        cleaned = Regex.Replace(cleaned, @"(?<=[^\r\n])\s+(•\s+)", "\r\n$1");

        // 5. Satır sonlarını ayrıştır ve temiz bloklar oluştur
        var rawLines = cleaned.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<string>();
        var currentBlock = new StringBuilder();

        foreach (var line in rawLines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (currentBlock.Length > 0)
                {
                    blocks.Add(currentBlock.ToString().Trim());
                    currentBlock.Clear();
                }
                continue;
            }

            // Eğer satır bir bölüm başlığı ise önceki bloğu bitir ve başlığı ayrı blok yap
            if (IsSectionHeader(trimmed))
            {
                if (currentBlock.Length > 0)
                {
                    blocks.Add(currentBlock.ToString().Trim());
                    currentBlock.Clear();
                }
                blocks.Add(EnsureSectionHeaderEmoji(trimmed));
                continue;
            }

            // Madde işareti ise
            if (trimmed.StartsWith("•") || trimmed.StartsWith("-") || trimmed.StartsWith("*"))
            {
                if (!trimmed.StartsWith("• "))
                {
                    trimmed = "• " + trimmed.TrimStart('•', '-', '*', ' ');
                }
            }

            if (currentBlock.Length > 0)
            {
                // Eğer satır veya mevcut blok madde işareti içeriyorsa her madde yeni satırda olmalı
                if (trimmed.StartsWith("• ") || currentBlock.ToString().Contains("• "))
                {
                    currentBlock.Append("\r\n").Append(trimmed);
                }
                else
                {
                    currentBlock.Append(" ").Append(trimmed);
                }
            }
            else
            {
                currentBlock.Append(trimmed);
            }
        }

        if (currentBlock.Length > 0)
        {
            blocks.Add(currentBlock.ToString().Trim());
        }

        // 6. Blokları Etsy'de ferah görünecek şekilde çift satır sonu (\r\n\r\n) ile birleştir
        var result = new StringBuilder();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i].Trim();
            if (string.IsNullOrWhiteSpace(block)) continue;

            result.Append(block);
            if (i < blocks.Count - 1)
            {
                result.Append("\r\n\r\n");
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Checks whether the text is already a well-formed 6-section Etsy description.
    /// </summary>
    public static bool IsAlreadyStructuredEtsyDescription(string? desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return false;
        int sectionCount = 0;
        if (desc.Contains("WHY YOU'LL LOVE IT", StringComparison.OrdinalIgnoreCase)) sectionCount++;
        if (desc.Contains("SPECIFICATIONS", StringComparison.OrdinalIgnoreCase) || desc.Contains("DETAILS", StringComparison.OrdinalIgnoreCase)) sectionCount++;
        if (desc.Contains("PERFECT FOR", StringComparison.OrdinalIgnoreCase)) sectionCount++;
        if (desc.Contains("PACKAGING", StringComparison.OrdinalIgnoreCase) || desc.Contains("SHIPPING", StringComparison.OrdinalIgnoreCase)) sectionCount++;
        return sectionCount >= 2;
    }

    /// <summary>
    /// Herhangi bir ham metni, ürün başlığı ve etiketlerini kullanarak 6 bölümlü Altın Etsy Paragraf Şablonuna dönüştürür.
    /// Eğer metin zaten AI veya formatlayıcı tarafından yapılandırılmışsa, özgün metni korur ve sadece satır sonlarını normalize eder.
    /// </summary>
    public static string FormatToStandardTemplate(
        string? rawDesc,
        string title,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? materials = null,
        string? targetKeyword = null)
    {
        if (IsAlreadyStructuredEtsyDescription(rawDesc))
        {
            return NormalizeForEtsy(rawDesc);
        }

        var cleanTitle = SanitizeTitle(title);
        var target = !string.IsNullOrWhiteSpace(targetKeyword) 
            ? targetKeyword.Trim() 
            : (tags?.FirstOrDefault() ?? cleanTitle);

        var matList = materials != null && materials.Count > 0 
            ? string.Join(", ", materials.Take(4)) 
            : "High-grade materials & precision craft";

        var cleanSource = ExtractSourceDetails(rawDesc);
        var theme = DetectProductTheme(cleanTitle, rawDesc, tags);

        var sb = new StringBuilder();

        // BÖLÜM 1: Google Meta Hook & Giriş Paragrafı (Kategoriye & Ürüne Özel Canlı Kanca)
        sb.AppendLine(BuildDynamicHook(theme, cleanTitle, target));
        sb.AppendLine();

        // BÖLÜM 2: Öne Çıkan Özellikler (Ürünün Gerçek Özellikleri)
        sb.AppendLine("✨ WHY YOU'LL LOVE IT:");
        if (cleanSource.KeyFeatures.Count > 0)
        {
            foreach (var feat in cleanSource.KeyFeatures.Take(3))
            {
                sb.AppendLine($"• {feat}");
            }
        }
        else
        {
            sb.AppendLine($"• Premium Craftsmanship: Expertly manufactured with durable {matList} for a smooth, high-detail finish.");
            sb.AppendLine(BuildThemeDisplayHighlight(theme));
        }
        sb.AppendLine(BuildThemeFeatureBullet(theme));
        sb.AppendLine();

        // BÖLÜM 3: Boyut ve Teknik Özellikler
        sb.AppendLine("📏 SPECIFICATIONS & DETAILS:");
        sb.AppendLine($"• Materials: {matList}");
        if (!string.IsNullOrWhiteSpace(cleanSource.Dimensions))
        {
            sb.AppendLine($"• Dimensions: {cleanSource.Dimensions}");
        }
        else
        {
            sb.AppendLine("• Dimensions: Standard display size (detailed dimensions available upon request).");
        }
        if (!string.IsNullOrWhiteSpace(cleanSource.IncludedItems))
        {
            sb.AppendLine($"• Package Includes: {cleanSource.IncludedItems}");
        }
        sb.AppendLine("• Finish: Clean, hand-finished surface with vibrant, durable detailing.");
        if (!string.IsNullOrWhiteSpace(cleanSource.ExtraNotes))
        {
            sb.AppendLine($"• Note: {cleanSource.ExtraNotes}");
        }
        sb.AppendLine();

        // BÖLÜM 4: Kimler İçin Uygun / Hediye (Ürün Temasına Özel)
        sb.AppendLine("🎁 PERFECT FOR:");
        sb.AppendLine(BuildThemeAudience(theme));
        sb.AppendLine(BuildThemeGiftOccasion(theme));
        sb.AppendLine();

        // BÖLÜM 5: Güvenli Paketleme & Kargo
        sb.AppendLine("📦 PACKAGING & SHIPPING:");
        sb.AppendLine("• Carefully wrapped in multi-layer protective packaging to guarantee 100% safe worldwide arrival.");
        sb.AppendLine("• Every order includes tracked dispatch sent directly to your email upon shipment.");
        sb.AppendLine();

        // BÖLÜM 6: Özel İstekler & İletişim
        sb.AppendLine("💬 CUSTOM REQUESTS & QUESTIONS:");
        sb.AppendLine("• Looking for a custom color, size, or special personalization? Feel free to reach out anytime—we are happy to help!");

        return NormalizeForEtsy(sb.ToString());
    }

    private static string BuildDynamicHook(ProductTheme theme, string title, string target) => theme switch
    {
        ProductTheme.SpaceAndAstronomy =>
            $"Embark on a cosmic journey with this captivating {title}! Perfect for shoppers searching for {target}, this artisan creation brings celestial wonder, imaginative discovery, and starry ambiance to any room.",
        ProductTheme.KidsAndNursery =>
            $"Delight little dreamers with this adorable {title}! Handcrafted for shoppers searching for {target}, this charming creation brings comforting warmth, playful imagination, and cheerful style to any nursery or child's bedroom.",
        ProductTheme.MusicOrCelebrity =>
            $"Celebrate the legendary icon with this stunning {title}! Tailored for shoppers searching for {target}, this handcrafted tribute brings extraordinary detail, charisma, and presence to your space.",
        ProductTheme.LampOrLighting =>
            $"Transform your space with the ambient glow of this {title}! Perfect for shoppers searching for {target}, this artisan creation seamlessly blends cozy atmosphere, captivating lighting, and modern decor.",
        ProductTheme.KitchenAndDining =>
            $"Brighten your daily rituals with this beautifully crafted {title}! Thoughtfully designed for shoppers searching for {target}, this piece combines everyday practicality with timeless artisan charm.",
        ProductTheme.ApparelAndFashion =>
            $"Express your unique style with this comfortable, premium {title}! Designed for shoppers searching for {target}, this piece brings standout personality, everyday comfort, and high-quality craftsmanship to your wardrobe.",
        ProductTheme.WallArtAndPrints =>
            $"Make a striking visual statement with this stunning {title}! Created for shoppers searching for {target}, this artisan wall art brings character, vibrant texture, and conversation-starting beauty to any wall.",
        ProductTheme.CosplayOrProp =>
            $"Complete your setup with this show-stopping {title}! Specially designed for shoppers searching for {target}, this piece delivers authentic presence, fine craftsmanship, and durable detail.",
        ProductTheme.JewelryOrWearable =>
            $"Add a touch of distinctive artisan charm with this elegant {title}! Handcrafted for shoppers searching for {target}, this piece combines refined beauty, comfort, and timeless character.",
        ProductTheme.GamingOrAnime =>
            $"Level up your sanctuary with this authentic {title}! Tailored for shoppers searching for {target}, this piece brings standout craftsmanship and unmistakable character to your setup.",
        ProductTheme.HomeDecorOrArt =>
            $"Elevate your interior aesthetics with this handcrafted {title}! Designed for shoppers searching for {target}, this distinct showpiece brings warmth, style, and conversation-starting artistry to any room.",
        ProductTheme.LeatherAndAccessories =>
            $"Upgrade your everyday essentials with this impeccably crafted {title}! Designed for shoppers searching for {target}, this piece pairs timeless style, premium durability, and minimalist elegance for your daily carry.",
        ProductTheme.BoardGamesAndToys =>
            $"Elevate game night and display elegance with this masterfully crafted {title}! Crafted for shoppers searching for {target}, this piece combines strategic fun, heirloom-quality craftsmanship, and conversation-starting artistry.",
        ProductTheme.PetSupplies =>
            $"Pamper your beloved companion with this bespoke, high-quality {title}! Handcrafted for shoppers searching for {target}, this piece combines comfort, reliable strength, and charming personalized style for every walk.",
        ProductTheme.AudioAndHeadphoneStands =>
            $"Showcase your favorite gear in style with this distinctive {title}! Engineered for shoppers searching for {target}, this artisan stand combines desktop organization, rock-solid stability, and eye-catching desk aesthetics.",
        _ =>
            $"Discover the exceptional craftsmanship of this unique {title}! Carefully designed for shoppers searching for {target}, this artisan piece brings authentic quality, thoughtful design, and distinctive charm to your home."
    };

    private static string BuildThemeDisplayHighlight(ProductTheme theme) => theme switch
    {
        ProductTheme.SpaceAndAstronomy => "• Celestial Ambience: Creates an inspiring cosmic focal point for desks, nightstands, bedrooms, or bookshelves.",
        ProductTheme.KidsAndNursery => "• Comforting Companion: Adds a cheerful, comforting presence that makes bedtime and playtime feel magical.",
        ProductTheme.MusicOrCelebrity => "• Iconic Tribute: A must-have centerpiece for music studios, vinyl shelves, entertainment rooms, or display cabinets.",
        ProductTheme.LampOrLighting => "• Atmospheric Ambiance: Creates a soothing, aesthetic lighting effect for desks, nightstands, and living rooms.",
        ProductTheme.KitchenAndDining => "• Everyday Delight: Built for daily enjoyment, elevating your morning routine with artisan charm.",
        ProductTheme.ApparelAndFashion => "• Premium Feel: Soft, breathable, and designed for lasting wear through everyday adventures.",
        ProductTheme.WallArtAndPrints => "• Gallery-Worthy Presentation: Crisp detailing and rich contrast that immediately draws the eye in any room.",
        ProductTheme.CosplayOrProp => "• Display & Cosplay Ready: Perfectly weighted and proportioned for photo shoots, cosplay events, or premium wall display.",
        ProductTheme.GamingOrAnime => "• Battlestation Ready: Designed to sit proudly next to your PC setup, gaming console, or collector bookcase.",
        ProductTheme.LeatherAndAccessories => "• Everyday Durability: Designed for practical daily carry, developing a rich, unique patina over years of use.",
        ProductTheme.BoardGamesAndToys => "• Heirloom Craftsmanship: Smooth hand-finished surfaces and weighted pieces designed for both intense gameplay and distinguished tabletop display.",
        ProductTheme.PetSupplies => "• Pet-Safe & Durable: Sturdy construction and smooth hardware engineered for everyday walks, active adventures, and pet comfort.",
        ProductTheme.AudioAndHeadphoneStands => "• Battlestation & Studio Ready: Keeps premium headphones safe, organized, and beautifully displayed with a stable, weighted base.",
        _ => "• Handcrafted Excellence: Thoughtfully finished with attention to detail and long-lasting durability."
    };

    private static string BuildThemeAudience(ProductTheme theme) => theme switch
    {
        ProductTheme.SpaceAndAstronomy =>
            "• Space enthusiasts, aspiring astronauts, stargazers, kids' rooms, and celestial decor lovers.",
        ProductTheme.KidsAndNursery =>
            "• Kids, toddlers, parents designing nursery spaces, and thoughtful baby shower or birthday gift shoppers.",
        ProductTheme.MusicOrCelebrity =>
            "• Dedicated music fans, pop culture enthusiasts, vinyl collectors, and tribute art lovers.",
        ProductTheme.LampOrLighting =>
            "• Home decor lovers, night owls, bedroom aesthetics, and cozy workspace setups.",
        ProductTheme.KitchenAndDining =>
            "• Coffee lovers, tea drinkers, home cooks, and thoughtful housewarming gift shoppers.",
        ProductTheme.ApparelAndFashion =>
            "• Fashion-forward trendsetters, style enthusiasts, and anyone who appreciates comfortable bespoke apparel.",
        ProductTheme.WallArtAndPrints =>
            "• Art lovers, interior decorators, gallery wall enthusiasts, and modern home stylists.",
        ProductTheme.CosplayOrProp =>
            "• Cosplayers, convention goers, fantasy fans, and theatrical prop collectors.",
        ProductTheme.JewelryOrWearable =>
            "• Style enthusiasts, vintage jewelry collectors, and anyone who appreciates bespoke handcrafted accessories.",
        ProductTheme.GamingOrAnime =>
            "• Gamers, anime lovers, cosplay enthusiasts, and tabletop/novelty decor collectors.",
        ProductTheme.HomeDecorOrArt =>
            "• Interior design lovers, aesthetic home stylists, art enthusiasts, and modern decor collectors.",
        ProductTheme.LeatherAndAccessories =>
            "• Everyday carry (EDC) enthusiasts, discerning professionals, travelers, and thoughtful gift shoppers seeking a timeless classic.",
        ProductTheme.BoardGamesAndToys =>
            "• Chess players, tabletop gaming enthusiasts, strategy fans, and collectors searching for an unforgettable heirloom gift.",
        ProductTheme.PetSupplies =>
            "• Dedicated pet parents, dog and cat lovers, new puppy owners, and thoughtful pet adoption gift shoppers.",
        ProductTheme.AudioAndHeadphoneStands =>
            "• Audiophiles, music producers, PC gamers, streamers, and tech enthusiasts upgrading their desk setup.",
        _ =>
            "• Discerning collectors, home decor enthusiasts, and anyone looking for a memorable, one-of-a-kind handcrafted gift."
    };

    private static string BuildThemeFeatureBullet(ProductTheme theme) => theme switch
    {
        ProductTheme.SpaceAndAstronomy => "• Starry Dreamer Delight: Gentle illumination designed to comfort little stargazers and inspire wonder.",
        ProductTheme.KitchenAndDining => "• Food-Safe & Daily Functional: Crafted with durable, food-safe glazes for everyday hot and cold beverage enjoyment.",
        ProductTheme.LeatherAndAccessories => "• Timeless EDC Classic: Slim, pocket-friendly profile that organizes your essentials without unnecessary bulk.",
        ProductTheme.BoardGamesAndToys => "• Tabletop Heirloom Quality: Beautifully weighted and detailed for memorable game nights with family and friends.",
        ProductTheme.PetSupplies => "• Pet Comfort & Security: Smooth edges and heavy-duty hardware built for safe, happy daily walks.",
        ProductTheme.AudioAndHeadphoneStands => "• Safe Gear Rest: Ergonomically curved to protect headphone headbands from indentation and wear.",
        ProductTheme.WallArtAndPrints => "• Ready to Hang: Designed to add instant warmth and character to gallery walls, living rooms, and offices.",
        ProductTheme.KidsAndNursery => "• Safe for Nurseries: Soft, calming glow with child-friendly materials to help toddlers drift into sweet dreams.",
        ProductTheme.MusicOrCelebrity => "• Collector & Fan Approved: Meticulously inspected and finished with exceptional attention to detail.",
        ProductTheme.CosplayOrProp => "• Convention & Stage Ready: Built to withstand active costume use while looking cinematic on display.",
        ProductTheme.GamingOrAnime => "• Battlestation Approved: Crafted with gamer aesthetics to elevate your streaming and desk setup.",
        ProductTheme.JewelryOrWearable => "• Hypoallergenic & Lightweight: Designed for comfortable all-day wear with premium skin-safe finishes.",
        _ => "• Artisan Quality Guaranteed: Individually inspected to ensure clean lines, durable construction, and lasting beauty."
    };

    private static string BuildThemeGiftOccasion(ProductTheme theme) => theme switch
    {
        ProductTheme.SpaceAndAstronomy => "• A magical birthday, Christmas, or baby shower gift for space fans and curious explorers.",
        ProductTheme.KitchenAndDining => "• A heartwarming housewarming, birthday, or holiday gift for coffee lovers and tea enthusiasts.",
        ProductTheme.LeatherAndAccessories => "• An elegant birthday, Father's Day, anniversary, or groomsmen gift for someone who values classic style.",
        ProductTheme.BoardGamesAndToys => "• A distinguished gift for chess champions, board game fans, fathers, and strategy enthusiasts.",
        ProductTheme.PetSupplies => "• The ultimate gift for new pet adoptions, dog birthdays, or passionate pet owners.",
        ProductTheme.AudioAndHeadphoneStands => "• A sleek battlestation upgrade gift for streamers, PC gamers, sound engineers, and music fans.",
        ProductTheme.KidsAndNursery => "• A thoughtful baby shower, toddler birthday, or nursery room welcoming keepsake.",
        ProductTheme.MusicOrCelebrity => "• An unforgettable tribute gift for concert goers, music lovers, and collectors.",
        ProductTheme.WallArtAndPrints => "• A stylish housewarming or holiday gift to transform any modern living or working space.",
        _ => "• An unforgettable birthday, anniversary, holiday, or special celebration gift."
    };

    public enum ProductTheme
    {
        General,
        MusicOrCelebrity,
        SpaceAndAstronomy,
        KidsAndNursery,
        LampOrLighting,
        KitchenAndDining,
        ApparelAndFashion,
        WallArtAndPrints,
        CosplayOrProp,
        GamingOrAnime,
        JewelryOrWearable,
        HomeDecorOrArt,
        LeatherAndAccessories,
        BoardGamesAndToys,
        PetSupplies,
        AudioAndHeadphoneStands
    }

    public static ProductTheme DetectProductTheme(string title, string? desc, IReadOnlyList<string>? tags)
    {
        var blob = $"{title} {desc} {string.Join(' ', tags ?? [])}".ToLowerInvariant();

        if (blob.Contains("astronaut") || blob.Contains("space") || blob.Contains("galaxy") ||
            blob.Contains("nasa") || blob.Contains("planet") || blob.Contains("cosmic") ||
            blob.Contains("moon") || blob.Contains("stargazer") || blob.Contains("astronomy") ||
            blob.Contains("rocket") || blob.Contains("nebula"))
        {
            return ProductTheme.SpaceAndAstronomy;
        }

        if (blob.Contains("nursery") || blob.Contains("baby") || blob.Contains("toddler") ||
            blob.Contains("kids") || blob.Contains("children") || blob.Contains("playroom"))
        {
            return ProductTheme.KidsAndNursery;
        }

        if (blob.Contains("michael jackson") || blob.Contains("singer") || blob.Contains("musician") ||
            blob.Contains("king of pop") || blob.Contains("music legend") || blob.Contains("rock star") ||
            blob.Contains("guitar") || blob.Contains("vinyl") || blob.Contains("concert") || blob.Contains("pop star"))
        {
            return ProductTheme.MusicOrCelebrity;
        }

        if (blob.Contains("lamp") || blob.Contains("night light") || blob.Contains("nightlight") ||
            blob.Contains("led light") || blob.Contains("lantern") || blob.Contains("ambient light") ||
            blob.Contains("desk lamp") || blob.Contains("moon lamp") || blob.Contains("table lamp"))
        {
            return ProductTheme.LampOrLighting;
        }

        if (blob.Contains("mug") || blob.Contains("cup") || blob.Contains("coaster") ||
            blob.Contains("kitchen") || blob.Contains("coffee") || blob.Contains("tea") ||
            blob.Contains("tumbler") || blob.Contains("cutting board"))
        {
            return ProductTheme.KitchenAndDining;
        }

        if (blob.Contains("t-shirt") || blob.Contains("shirt") || blob.Contains("hoodie") ||
            blob.Contains("sweatshirt") || blob.Contains("apparel") || blob.Contains("clothing") ||
            blob.Contains("tote bag"))
        {
            return ProductTheme.ApparelAndFashion;
        }

        if (blob.Contains("headphone") || blob.Contains("headset") || blob.Contains("audio gear") ||
            blob.Contains("kulaklik"))
        {
            return ProductTheme.AudioAndHeadphoneStands;
        }

        if (blob.Contains("dog collar") || blob.Contains("cat collar") || blob.Contains("pet collar") ||
            blob.Contains("tasma") || blob.Contains("pet harness") || blob.Contains("dog leash") ||
            blob.Contains("leash") || blob.Contains("pet tag"))
        {
            return ProductTheme.PetSupplies;
        }

        if (blob.Contains("chess") || blob.Contains("board game") || blob.Contains("puzzle") ||
            blob.Contains("satranc") || blob.Contains("dice"))
        {
            return ProductTheme.BoardGamesAndToys;
        }

        if (blob.Contains("wallet") || blob.Contains("card holder") ||
            blob.Contains("cardholder") || blob.Contains("bifold") || blob.Contains("purse") ||
            blob.Contains("cuzdan") || blob.Contains("kartlik") ||
            blob.Contains("leather wallet") || blob.Contains("leather card"))
        {
            return ProductTheme.LeatherAndAccessories;
        }

        if (blob.Contains("poster") || blob.Contains("canvas") || blob.Contains("wall art") ||
            blob.Contains("wall sign") || blob.Contains("wall hanging") ||
            ((blob.Contains("print") || blob.Contains("art print")) && !blob.Contains("3d print") && !blob.Contains("printed")) ||
            blob.Contains("painting"))
        {
            return ProductTheme.WallArtAndPrints;
        }

        if (blob.Contains("cosplay") || blob.Contains("prop replica") || blob.Contains("helmet") ||
            blob.Contains("sword") || blob.Contains("dagger") || blob.Contains("shield") ||
            blob.Contains("wearable prop") || blob.Contains("costume prop"))
        {
            return ProductTheme.CosplayOrProp;
        }

        if (blob.Contains("necklace") || blob.Contains("bracelet") || blob.Contains("ring") ||
            blob.Contains("earring") || blob.Contains("pendant") || blob.Contains("jewelry"))
        {
            return ProductTheme.JewelryOrWearable;
        }

        if (blob.Contains("gaming") || blob.Contains("gamer") || blob.Contains("anime") ||
            blob.Contains("manga") || blob.Contains("video game") || blob.Contains("rpg") ||
            blob.Contains("dnd") || blob.Contains("tabletop mini"))
        {
            return ProductTheme.GamingOrAnime;
        }

        if (blob.Contains("vase") || blob.Contains("planter") || blob.Contains("shelf decor") ||
            blob.Contains("home decor") || blob.Contains("candle holder") || blob.Contains("sculpture") ||
            blob.Contains("clock"))
        {
            return ProductTheme.HomeDecorOrArt;
        }

        return ProductTheme.General;
    }

    private static bool IsSectionHeader(string line)
    {
        var upper = line.ToUpperInvariant();
        return upper.Contains("WHY YOU'LL LOVE IT") ||
               upper.Contains("SPECIFICATIONS") ||
               upper.Contains("DETAILS") ||
               upper.Contains("PERFECT FOR") ||
               upper.Contains("PACKAGING") ||
               upper.Contains("SHIPPING") ||
               upper.Contains("CUSTOM REQUESTS") ||
               upper.Contains("CARE INSTRUCTIONS") ||
               upper.Contains("HOW TO ORDER") ||
               upper.Contains("OVERVIEW");
    }

    private static string EnsureSectionHeaderEmoji(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("WHY YOU'LL LOVE IT") && !text.Contains("✨")) return "✨ " + text.Trim();
        if (upper.Contains("SPECIFICATIONS") && !text.Contains("📏")) return "📏 " + text.Trim();
        if (upper.Contains("PERFECT FOR") && !text.Contains("🎁")) return "🎁 " + text.Trim();
        if (upper.Contains("PACKAGING") && !text.Contains("📦")) return "📦 " + text.Trim();
        if (upper.Contains("CUSTOM REQUESTS") && !text.Contains("💬")) return "💬 " + text.Trim();
        if (upper.Contains("CARE") && !text.Contains("🧼")) return "🧼 " + text.Trim();
        return text;
    }

    private static string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Handcrafted Item";
        var parts = title.Split(['|', '-', ',', '–', '—', '/'], StringSplitOptions.RemoveEmptyEntries);
        var first = parts.FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(first) && first.Length >= 4 ? first : title.Trim();
    }

    private sealed record ExtractedDetails(
        string Dimensions,
        List<string> KeyFeatures,
        string IncludedItems,
        string ExtraNotes);

    private static ExtractedDetails ExtractSourceDetails(string? rawDesc)
    {
        if (string.IsNullOrWhiteSpace(rawDesc)) return new("", [], "", "");

        string dimensions = "";
        string included = "";
        string extraNotes = "";
        var features = new List<string>();

        var lines = rawDesc.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var l in lines)
        {
            var line = l.Trim();
            if (line.Contains("Elevate your space", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Elevate your collection", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Tailored for shoppers", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Premium Craftsmanship", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Eye-Catching Display", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Collector & Fan Approved", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Carefully wrapped in", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Looking for a custom", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("WHY YOU'LL LOVE IT", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("SPECIFICATIONS & DETAILS", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("PERFECT FOR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("PACKAGING & SHIPPING", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("CUSTOM REQUESTS", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Overview:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Overview:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Materials:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Craftsmanship:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Dimensions:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Finish:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Note:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Package Includes:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cleanItem = line.TrimStart('•', '-', '*', ' ').Trim();
            if (cleanItem.Length < 6) continue;
            var normLine = NormalizeForMatching(cleanItem);

            // 1. Özel Not (Öncelikli)
            if ((normLine.StartsWith("not:") ||
                 normLine.StartsWith("ozel not") ||
                 normLine.StartsWith("note:") ||
                 normLine.StartsWith("dikkat:") ||
                 normLine.StartsWith("onemli:")) && cleanItem.Length < 180)
            {
                extraNotes = cleanItem;
                continue;
            }

            // 2. Kutu içeriği (İngilizce ve Türkçe destekli)
            if (string.IsNullOrEmpty(included) &&
                (normLine.Contains("includes:") ||
                 normLine.StartsWith("package:") ||
                 normLine.StartsWith("package ") ||
                 normLine.Contains("package includes") ||
                 normLine.Contains("comes with") ||
                 normLine.Contains("box includes") ||
                 normLine.Contains("set of") ||
                 normLine.Contains("kutu icerigi") ||
                 normLine.Contains("paket icerigi") ||
                 normLine.StartsWith("paket:") ||
                 normLine.StartsWith("paket ") ||
                 normLine.Contains("hediye kutusunda") ||
                 normLine.Contains("pakette") ||
                 normLine.Contains("icerik:") ||
                 normLine.Contains("kutuda")))
            {
                if (cleanItem.Length < 140)
                {
                    var inc = cleanItem;
                    if (inc.StartsWith("Package:", StringComparison.OrdinalIgnoreCase))
                        inc = inc["Package:".Length..].Trim();
                    else if (inc.StartsWith("Paket:", StringComparison.OrdinalIgnoreCase))
                        inc = inc["Paket:".Length..].Trim();
                    else if (inc.StartsWith("Includes:", StringComparison.OrdinalIgnoreCase))
                        inc = inc["Includes:".Length..].Trim();
                    else if (inc.StartsWith("Kutu Icerigi:", StringComparison.OrdinalIgnoreCase))
                        inc = inc["Kutu Icerigi:".Length..].Trim();
                    included = inc;
                    continue;
                }
            }

            // 3. Boyut ayıklama (İngilizce ve Türkçe destekli)
            if (string.IsNullOrEmpty(dimensions) &&
                (normLine.Contains("cm") ||
                 normLine.Contains("mm") ||
                 normLine.Contains("inch") ||
                 normLine.Contains("\"") ||
                 normLine.Contains("dimension") ||
                 normLine.Contains("height") ||
                 normLine.Contains("width") ||
                 normLine.Contains("length") ||
                 normLine.Contains("depth") ||
                 normLine.Contains("size:") ||
                 normLine.Contains("scale:") ||
                 normLine.Contains("boyut") ||
                 normLine.Contains("olcu") ||
                 normLine.Contains("ebat") ||
                 normLine.Contains("yukseklik") ||
                 normLine.Contains("genislik") ||
                 normLine.Contains("derinlik") ||
                 normLine.Contains("agirlik") ||
                 normLine.Contains("weight") ||
                 normLine.Contains("gram")))
            {
                if (cleanItem.Length < 140)
                {
                    dimensions = cleanItem;
                    continue;
                }
            }

            // 4. Önemli ürün nitelikleri (el boyaması, LED, özel kaplama, RGB, dokunmatik vb.)
            if (features.Count < 3 && cleanItem.Length is >= 10 and <= 120 &&
                (normLine.Contains("hand-painted") ||
                 normLine.Contains("handcrafted") ||
                 normLine.Contains("high detail") ||
                 normLine.Contains("articulated") ||
                 normLine.Contains("magnetic") ||
                 normLine.Contains("custom") ||
                 normLine.Contains("textured") ||
                 normLine.Contains("durable") ||
                 normLine.Contains("led") ||
                 normLine.Contains("smooth finish") ||
                 normLine.Contains("resin") ||
                 normLine.Contains("wood") ||
                 normLine.Contains("silicone") ||
                 normLine.Contains("anti-slip") ||
                 normLine.Contains("non-slip") ||
                 normLine.Contains("feet") ||
                 normLine.Contains("feature") ||
                 normLine.Contains("food-safe") ||
                 normLine.Contains("dishwasher") ||
                 normLine.Contains("microwave") ||
                 normLine.Contains("el yapimi") ||
                 normLine.Contains("el boyamasi") ||
                 normLine.Contains("ozel tasarim") ||
                 normLine.Contains("rgb") ||
                 normLine.Contains("dokunmatik") ||
                 normLine.Contains("sarjli") ||
                 normLine.Contains("kablosuz") ||
                 normLine.Contains("ozellik") ||
                 normLine.Contains("taban") ||
                 normLine.Contains("silikon") ||
                 normLine.Contains("ayaklar")))
            {
                if (!IsSectionHeader(cleanItem) && !features.Contains(cleanItem))
                {
                    features.Add(cleanItem);
                    continue;
                }
            }

            // 5. Genel ekstra not (eğer başka bir alana girmediyse)
            if (string.IsNullOrEmpty(extraNotes) && cleanItem.Length is >= 15 and <= 180 && !IsSectionHeader(cleanItem))
            {
                extraNotes = cleanItem;
            }
        }

        return new ExtractedDetails(dimensions, features, included, extraNotes);
    }

    private static string NormalizeForMatching(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ş', 's')
            .Replace('ö', 'o')
            .Replace('ç', 'c')
            .Replace('İ', 'i');
    }
}
