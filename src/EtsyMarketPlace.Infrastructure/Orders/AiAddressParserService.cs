namespace EtsyMarketPlace.Infrastructure.Orders;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Shipping;
using Microsoft.Extensions.Configuration;

public sealed class AiAddressParserService : IAiAddressParserService
{
    private readonly IGeminiListingApiClient? _geminiClient;
    private readonly IShopSettingsRepository? _settingsRepo;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public AiAddressParserService(
        HttpClient httpClient,
        IConfiguration config,
        IGeminiListingApiClient? geminiClient = null,
        IShopSettingsRepository? settingsRepo = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _geminiClient = geminiClient;
        _settingsRepo = settingsRepo;
    }

    private static readonly HashSet<string> EuCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI",
        "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT",
        "NL", "PL", "PT", "RO", "SE", "SI", "SK"
    };

    public static string ResolveDefaultIossNumber(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return string.Empty;
        string code = countryCode.Trim().ToUpperInvariant();

        if (EuCountryCodes.Contains(code))
        {
            return "IM3720000224"; // Resmi Etsy AB IOSS Numarası
        }
        if (code == "GB" || code == "UK")
        {
            return "370 6004 28"; // Resmi Etsy UK VAT Numarası
        }
        return string.Empty;
    }

    public async Task<ParsedOrderAddressDto> ParseRawAddressAsync(string rawAddressText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawAddressText))
        {
            return new ParsedOrderAddressDto("", "", "", "", "", "", "", "", "", "", "", "Empty", "Metin boş.");
        }

        var text = rawAddressText.Trim();

        // 1. Telefon ve e-postayı metinden ayıkla ve adres satırlarını temizle
        var (cleanedText, phone, email) = CleanAndExtractContact(text);

        // 2. Aktif AI API anahtarını alalım
        var (provider, apiKey, model) = await ResolveActiveAiCredentialsAsync("53236321", cancellationToken);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var aiParsed = await CallAiModelAsync(provider, apiKey, model, text, cancellationToken);
                if (aiParsed != null && (!string.IsNullOrWhiteSpace(aiParsed.StreetAddress) || !string.IsNullOrWhiteSpace(aiParsed.City)))
                {
                    string finalPhone = !string.IsNullOrWhiteSpace(aiParsed.Phone) ? aiParsed.Phone : phone;
                    string finalEmail = !string.IsNullOrWhiteSpace(aiParsed.BuyerEmail) ? aiParsed.BuyerEmail : email;
                    var (aiCountryName, aiCountryCode) = NormalizeCountry(aiParsed.CountryCode);
                    string state = aiParsed.State;
                    if (UsStateHelper.RequiresState(aiCountryCode) && !string.IsNullOrWhiteSpace(state))
                    {
                        var (normCode, _) = UsStateHelper.ResolveUsOrCaState(state);
                        state = normCode;
                    }
                    string defaultIoss = ResolveDefaultIossNumber(aiCountryCode);
                    string finalIoss = !string.IsNullOrWhiteSpace(aiParsed.IossNumber) ? aiParsed.IossNumber : defaultIoss;

                    return new ParsedOrderAddressDto(
                        BuyerName: aiParsed.BuyerName,
                        BuyerEmail: finalEmail,
                        Phone: finalPhone,
                        StreetAddress: aiParsed.StreetAddress,
                        SecondAddress: aiParsed.SecondAddress,
                        City: aiParsed.City,
                        State: state,
                        PostalCode: aiParsed.PostalCode,
                        Country: !string.IsNullOrWhiteSpace(aiParsed.Country) ? aiParsed.Country : aiCountryName,
                        CountryCode: aiCountryCode,
                        IossNumber: finalIoss,
                        Source: $"AI:{provider}",
                        ConfidenceNote: $"{provider} modeli ile tam doğrulukla ayrıştırıldı.");
                }
            }
            catch
            {
                // AI çağrısı başarısız olursa yerel kural motoruna güvenli düşüş
            }
        }

        // 3. Fallback: Yerel Yüksek Performanslı EtsyAddressParser Motoru (Temizlenmiş metin ile)
        var local = EtsyAddressParser.ParseFromFormattedAddress(cleanedText);
        var (locCountryName, locCountryCode) = NormalizeCountry(local.CountryCode);
        string locState = local.State;
        if (UsStateHelper.RequiresState(locCountryCode) && !string.IsNullOrWhiteSpace(locState))
        {
            var (normCode, _) = UsStateHelper.ResolveUsOrCaState(locState);
            locState = normCode;
        }
        string localIoss = ResolveDefaultIossNumber(locCountryCode);

        return new ParsedOrderAddressDto(
            BuyerName: local.BuyerName,
            BuyerEmail: email,
            Phone: phone,
            StreetAddress: local.StreetAddress,
            SecondAddress: local.SecondAddress,
            City: local.City,
            State: locState,
            PostalCode: local.PostalCode,
            Country: locCountryName,
            CountryCode: locCountryCode,
            IossNumber: localIoss,
            Source: "LocalRuleParser",
            ConfidenceNote: "Yerel kural motoru ile ayrıştırıldı.");
    }

    private static (string CountryName, string CountryCode) NormalizeCountry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("United States", "US");
        string upper = raw.Trim().ToUpperInvariant();
        return upper switch
        {
            "US" or "USA" or "UNITED STATES" or "UNITED STATES OF AMERICA" or "AMERİKA" => ("United States", "US"),
            "CA" or "CAN" or "CANADA" or "KANADA" => ("Canada", "CA"),
            "DE" or "GERMANY" or "DEUTSCHLAND" or "ALMANYA" => ("Germany", "DE"),
            "GB" or "UK" or "UNITED KINGDOM" or "GREAT BRITAIN" or "İNGİLTERE" => ("United Kingdom", "GB"),
            "FR" or "FRANCE" or "FRANSA" => ("France", "FR"),
            "IT" or "ITALY" or "ITALIA" or "İTALYA" => ("Italy", "IT"),
            "ES" or "SPAIN" or "ESPAÑA" or "İSPANYA" => ("Spain", "ES"),
            "NL" or "NETHERLANDS" or "HOLLAND" or "HOLLANDA" => ("Netherlands", "NL"),
            "AU" or "AUSTRALIA" or "AVUSTRALYA" => ("Australia", "AU"),
            "TR" or "TURKEY" or "TÜRKİYE" => ("Turkey", "TR"),
            _ when upper.Length == 2 => (upper, upper),
            _ => (raw.Trim(), upper.Length == 2 ? upper : "US")
        };
    }

    private static (string CleanedText, string Phone, string Email) CleanAndExtractContact(string text)
    {
        string phone = ExtractPhoneNumber(text);
        string email = "";
        var emailMatch = Regex.Match(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        if (emailMatch.Success) email = emailMatch.Value.Trim();

        var lines = text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var cleanedLines = new List<string>();
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^(?:Tel|Phone|Mobile|Tlf|Telefon|Cep|Cell|Fax)\s*[:\-\.]?\s*", RegexOptions.IgnoreCase))
            {
                continue;
            }
            if (Regex.IsMatch(line, @"^(?:Email|E-posta|Mail)\s*[:\-\.]?\s*", RegexOptions.IgnoreCase))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(phone) && line.Contains(phone))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(email) && line.Contains(email))
            {
                continue;
            }

            cleanedLines.Add(line);
        }

        return (string.Join("\n", cleanedLines), phone, email);
    }

    private static string ExtractPhoneNumber(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // 1. Önce satır bazında Tel/Phone/Cep/Mobile/Tlf etiketli olanları tara
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"(?:Tel|Phone|Mobile|Tlf|Telefon|Cep|Cell|Fax)\s*[:\-\.]?\s*(\+?[\d\s\-\(\)\.]{7,25})", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                string digits = Regex.Replace(match.Groups[1].Value, @"\D", "");
                if (digits.Length >= 7)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        // 2. Etiketsiz durumlarda: Asla ZIP/Posta kodu ile çakışmamalı!
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // ZIP+4 (örn: 09464-0046 veya 90001-1234) veya açık adres/şehir satırlarını atla
            if (Regex.IsMatch(line, @"\b(?:Apo|Fpo|Dpo|Box|PSC|Street|St|Ave|Avenue|Road|Rd|Blvd|Suite|Apt)\b", RegexOptions.IgnoreCase))
            {
                continue;
            }

            // ZIP+4 formatını kesinlikle dışla (5 hane - 4 hane)
            if (Regex.IsMatch(line, @"^\d{5}-\d{4}$"))
            {
                continue;
            }

            // Satırın tamamı veya bir kısmı açık telefon formatında ise (+ ile başlayan, parantezli veya en az 10 basamaklı)
            var match = Regex.Match(line, @"(?:\+|00)\d{1,4}[-.\s]?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{2,4}(?:[-.\s]?\d{2,4})?|\(\d{3}\)[-.\s]?\d{3}[-.\s]?\d{4}");
            if (match.Success)
            {
                string digits = Regex.Replace(match.Value, @"\D", "");
                if (digits.Length >= 10 && digits.Length <= 15)
                {
                    return match.Value.Trim();
                }
            }

            // Eğer tüm satır sadece sayılardan ve ayırıcılardan oluşuyorsa ve en az 10 basamak ise (9 haneli ZIP+4 dışlandı)
            string onlyDigits = Regex.Replace(line, @"\D", "");
            if (onlyDigits.Length >= 10 && onlyDigits.Length <= 15 && Regex.IsMatch(line, @"^[\d\s\-\+\(\)\.]{10,25}$"))
            {
                return line;
            }
        }

        return string.Empty;
    }

    private async Task<(string Provider, string ApiKey, string Model)> ResolveActiveAiCredentialsAsync(string shopId, CancellationToken ct)
    {
        string provider = "DeepSeek";
        string apiKey = string.Empty;
        string model = "deepseek-chat";

        if (_settingsRepo != null)
        {
            try
            {
                var json = await _settingsRepo.GetAiSettingsJsonAsync(shopId, ct);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("provider", out var pProp) && pProp.ValueKind == JsonValueKind.String)
                        provider = pProp.GetString() ?? "DeepSeek";

                    if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                    {
                        if (root.TryGetProperty("geminiApiKey", out var gKey)) apiKey = gKey.GetString() ?? "";
                        if (root.TryGetProperty("geminiModel", out var gMod)) model = gMod.GetString() ?? "gemini-2.5-flash";
                    }
                    else if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                    {
                        if (root.TryGetProperty("openAiApiKey", out var oKey)) apiKey = oKey.GetString() ?? "";
                        if (root.TryGetProperty("openAiModel", out var oMod)) model = oMod.GetString() ?? "gpt-4o-mini";
                    }
                    else
                    {
                        provider = "DeepSeek";
                        if (root.TryGetProperty("deepSeekApiKey", out var dKey)) apiKey = dKey.GetString() ?? "";
                        if (root.TryGetProperty("deepSeekModel", out var dMod)) model = dMod.GetString() ?? "deepseek-chat";
                    }
                }
            }
            catch { }
        }

        // Config fallback if settings repo didn't have it
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = _config["DeepSeek:ApiKey"] ?? _config["Gemini:ApiKey"] ?? _config["OpenAI:ApiKey"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"])) provider = "Gemini";
            else if (!string.IsNullOrWhiteSpace(_config["DeepSeek:ApiKey"])) provider = "DeepSeek";
        }

        return (provider, apiKey, model);
    }

    private async Task<ParsedOrderAddressDto?> CallAiModelAsync(
        string provider,
        string apiKey,
        string model,
        string rawAddressText,
        CancellationToken ct)
    {
        const string systemPrompt = """
            You are an expert shipping logistics data extractor.
            Your task is to parse unstructured/raw delivery address text into structured JSON.
            Return ONLY a single JSON object with these EXACT keys:
            {
              "buyerName": "Recipient full name",
              "buyerEmail": "Recipient email address if present or empty string",
              "phone": "Recipient phone number if present or empty string",
              "streetAddress": "Street address line 1 with house/building number",
              "secondAddress": "Apartment, suite, unit, floor if any or empty string",
              "city": "City or municipality name",
              "state": "2-letter state/province abbreviation (e.g. CA, NY, TX) or full region",
              "postalCode": "Postal or zip code",
              "country": "Country name in English",
              "countryCode": "2-letter ISO-3166-1 alpha-2 country code (e.g. US, GB, DE, CA)",
              "iossNumber": "IOSS or VAT number if mentioned or empty string"
            }
            Do not include Markdown backticks or explanation. Return pure JSON only.
            """;

        string userPrompt = $"Extract the delivery address from this text:\n\n{rawAddressText}";

        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && _geminiClient != null)
        {
            var (success, content, _) = await _geminiClient.GenerateOptimizationJsonAsync(systemPrompt, userPrompt, model, apiKey, ct);
            if (success && !string.IsNullOrWhiteSpace(content))
            {
                return DeserializeAddressJson(content);
            }
        }
        else
        {
            // DeepSeek & OpenAI compatible endpoint
            string endpoint = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? "https://api.openai.com/v1/chat/completions"
                : "https://api.deepseek.com/chat/completions";

            var payload = new
            {
                model = !string.IsNullOrWhiteSpace(model) && !model.Contains("V4-Flash") ? model : "deepseek-chat",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await _httpClient.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var resBody = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(resBody);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var contentProp))
            {
                string? rawJson = contentProp.GetString();
                if (!string.IsNullOrWhiteSpace(rawJson))
                {
                    return DeserializeAddressJson(rawJson);
                }
            }
        }

        return null;
    }

    private static ParsedOrderAddressDto? DeserializeAddressJson(string json)
    {
        try
        {
            string clean = json.Trim();
            if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(7);
            }
            else if (clean.StartsWith("```"))
            {
                clean = clean.Substring(3);
            }
            if (clean.EndsWith("```"))
            {
                clean = clean.Substring(0, clean.Length - 3);
            }
            clean = clean.Trim();

            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            string name = GetProp(root, "buyerName", "name", "recipient");
            string email = GetProp(root, "buyerEmail", "email", "mail");
            string phone = GetProp(root, "phone", "phoneNumber", "mobile");
            string street = GetProp(root, "streetAddress", "street", "address1", "addressLine1");
            string second = GetProp(root, "secondAddress", "address2", "unit", "apt");
            string city = GetProp(root, "city", "town");
            string state = GetProp(root, "state", "province", "region");
            string zip = GetProp(root, "postalCode", "zip", "zipCode", "postcode");
            string country = GetProp(root, "country", "countryName");
            string code = GetProp(root, "countryCode", "isoCountryCode");
            string ioss = GetProp(root, "iossNumber", "ioss", "vatNumber", "vat");

            if (string.IsNullOrWhiteSpace(code) && country.Equals("United States", StringComparison.OrdinalIgnoreCase))
            {
                code = "US";
            }

            return new ParsedOrderAddressDto(
                BuyerName: name,
                BuyerEmail: email,
                Phone: phone,
                StreetAddress: street,
                SecondAddress: second,
                City: city,
                State: state,
                PostalCode: zip,
                Country: country,
                CountryCode: code,
                IossNumber: ioss,
                Source: "AI",
                ConfidenceNote: "");
        }
        catch
        {
            return null;
        }
    }

    private static string GetProp(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
        {
            if (elem.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
            }
        }
        return "";
    }
}
