namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public static class FastListingTemplateStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "fast-listing-templates.json");

    private static readonly List<FastListingTemplate> BuiltInTemplates =
    [
        new FastListingTemplate
        {
            Id = "builtin_mug",
            Name = "☕ Seramik Kupa & Fincan (Mug)",
            DefaultPrice = 21.90m,
            DefaultQuantity = 20,
            IsDigital = false,
            DefaultTags = "ceramic mug, coffee cup, handmade mug, gift for her, pottery mug, tea cup, cute mug, morning coffee, kitchen decor, custom mug, birthday gift, office gift, aesthetic mug",
            DefaultMaterials = "ceramic, clay, food safe glaze",
            DescriptionTemplate =
                "✨ El yapımı seramik kupa. Günlük kahve ve çay keyfiniz için özenle üretilmiştir.\n\n" +
                "📦 Paketleme & Kargo:\n" +
                "Kırılmaya dayanıklı özel strafor ve hediye kutusunda güvenle gönderilir.\n\n" +
                "💧 Bakım & Kullanım:\n" +
                "Bulaşık makinesinde ve mikrodalga fırında güvenle kullanılabilir.",
            EnableVariations = true,
            VariationType1 = "Boyut / Beden (Size - 100)",
            VariationValues1 = "11 oz (325 ml), 15 oz (450 ml)",
            EnableCustomVariationPricing = true,
            VariationPrices = new Dictionary<string, decimal>
            {
                ["11 oz (325 ml)"] = 21.90m,
                ["15 oz (450 ml)"] = 26.90m
            },
            IsBuiltIn = true
        },
        new FastListingTemplate
        {
            Id = "builtin_tshirt",
            Name = "👕 Tişört & Sweatshirt (Apparel)",
            DefaultPrice = 28.50m,
            DefaultQuantity = 50,
            IsDigital = false,
            DefaultTags = "graphic tee, cotton tshirt, trendy shirt, gift for friend, oversized tee, aesthetic shirt, unisex tshirt, casual wear, vintage tshirt, minimalist tee, summer shirt, comfortable tee, street style",
            DefaultMaterials = "100% cotton, combed cotton",
            DescriptionTemplate =
                "🌿 %100 Pamuklu, nefes alan ve cilde dost kaliteli kumaş.\n\n" +
                "📏 Beden Tablosu:\n" +
                "Unisex regular kesimdir. Rahat/oversize duruş için bir beden büyük tercih edebilirsiniz.\n\n" +
                "🧼 Yıkama Talimatı:\n" +
                "30 derecede tersten yıkayınız ve tersten ütüleyiniz.",
            EnableVariations = true,
            VariationType1 = "Boyut / Beden (Size - 100)",
            VariationValues1 = "S, M, L, XL, 2XL",
            EnableVariation2 = true,
            VariationType2 = "Renk (Primary Color - 506)",
            VariationValues2 = "Siyah, Beyaz, Bej, Antrasit",
            IsBuiltIn = true
        },
        new FastListingTemplate
        {
            Id = "builtin_digital",
            Name = "💻 Dijital İndirme (Printable Art)",
            DefaultPrice = 6.50m,
            DefaultQuantity = 999,
            IsDigital = true,
            DefaultTags = "digital download, printable art, wall art prints, gallery wall, modern art print, aesthetic poster, minimalist decor, digital print, instant download, home office art, boho wall decor, living room art, diy wall art",
            DefaultMaterials = "digital file, 300 dpi jpg, high resolution pdf",
            DescriptionTemplate =
                "📥 DİKKAT: Bu ürün DİJİTAL bir indirmedir. Adresinize fiziksel bir ürün kargolanmayacaktır.\n\n" +
                "📂 Pakette Neler Var?\n" +
                "300 DPI ultra yüksek çözünürlükte, standart çerçeve ölçülerine uygun 5 farklı oran (2:3, 3:4, 4:5, ISO A1-A4, 11x14).\n\n" +
                "⚡ Satın alma işlemi onaylandığı an Etsy hesabınızdan anında indirebilirsiniz.",
            EnableVariations = false,
            IsBuiltIn = true
        },
        new FastListingTemplate
        {
            Id = "builtin_jewelry",
            Name = "💍 El Yapımı Takı & Kolye (Jewelry)",
            DefaultPrice = 34.00m,
            DefaultQuantity = 15,
            IsDigital = false,
            DefaultTags = "handmade jewelry, silver necklace, dainty jewelry, gift for her, minimal necklace, personalized gift, gold necklace, custom jewelry, everyday necklace, layering necklace, bridal jewelry, unique necklace, anniversary gift",
            DefaultMaterials = "925 sterling silver, 14k gold plated, zircon stone",
            DescriptionTemplate =
                "💎 925 Ayar Gerçek Gümüş üzeri 14K Altın kaplama zarafet.\n\n" +
                "🎁 Hediye Paketi:\n" +
                "Özel lüks takı kutusunda, hediye çantası ve kişisel not kartı ile gönderilir.\n\n" +
                "✨ Kararmaya karşı dayanıklı özel nano koruyucu cila uygulanmıştır.",
            EnableVariations = true,
            VariationType1 = "Renk (Primary Color - 506)",
            VariationValues1 = "Gümüş, 14K Altın Kaplama, Rose Gold",
            IsBuiltIn = true
        }
    ];

    public static List<FastListingTemplate> LoadAll()
    {
        var templates = new List<FastListingTemplate>(BuiltInTemplates);
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var custom = JsonSerializer.Deserialize<List<FastListingTemplate>>(json);
                if (custom != null)
                {
                    templates.AddRange(custom);
                }
            }
        }
        catch
        {
            // Fallback to built-ins if custom file cannot be read
        }

        return templates;
    }

    public static void SaveCustom(FastListingTemplate template)
    {
        try
        {
            var customList = LoadCustomOnly();
            customList.RemoveAll(t => t.Id == template.Id || t.Name.Equals(template.Name, StringComparison.OrdinalIgnoreCase));
            template.IsBuiltIn = false;
            customList.Add(template);

            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(customList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Silently fail or log in caller
        }
    }

    public static void DeleteCustom(string templateId)
    {
        try
        {
            var customList = LoadCustomOnly();
            customList.RemoveAll(t => t.Id == templateId);

            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(customList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }

    public static IReadOnlyList<FastListingTemplate> GetBuiltInTemplates() => BuiltInTemplates;

    private static List<FastListingTemplate> LoadCustomOnly()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<FastListingTemplate>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
