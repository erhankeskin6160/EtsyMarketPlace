namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Diagnostics;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed class AiBalanceService : IAiBalanceService
{
    private readonly HttpClient _httpClient;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IAiUsageRepository _usageRepo;

    public AiBalanceService(
        HttpClient httpClient,
        IShopSettingsRepository settingsRepo,
        IAiUsageRepository usageRepo)
    {
        _httpClient = httpClient;
        _settingsRepo = settingsRepo;
        _usageRepo = usageRepo;
    }

    public async Task<IReadOnlyList<AiProviderBalanceInfo>> GetProviderBalancesAsync(
        string shopId,
        decimal exchangeRate = 40.0m,
        string? period = "30d",
        CancellationToken ct = default)
    {
        if (exchangeRate <= 0) exchangeRate = 40.0m;

        // 1. Ayarları VDS veritabanından oku
        string activeProvider = "DeepSeek";
        string deepSeekApiKey = string.Empty;
        string deepSeekModel = "DeepSeek-V4-Flash";
        string geminiApiKey = string.Empty;
        string geminiModel = "gemini-2.5-flash";
        string openAiApiKey = string.Empty;
        string openAiModel = "gpt-4o";
        string claudeApiKey = string.Empty;
        string claudeModel = "claude-3-7-sonnet";
        string grokApiKey = string.Empty;
        string grokModel = "grok-3";

        try
        {
            var json = await _settingsRepo.GetAiSettingsJsonAsync(shopId, ct);
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("provider", out var pProp) && pProp.ValueKind == JsonValueKind.String)
                    activeProvider = pProp.GetString() ?? "DeepSeek";

                if (root.TryGetProperty("deepSeekApiKey", out var dsKey) && dsKey.ValueKind == JsonValueKind.String)
                    deepSeekApiKey = dsKey.GetString() ?? "";
                if (root.TryGetProperty("deepSeekModel", out var dsMod) && dsMod.ValueKind == JsonValueKind.String)
                    deepSeekModel = dsMod.GetString() ?? "DeepSeek-V4-Flash";

                if (root.TryGetProperty("geminiApiKey", out var gmKey) && gmKey.ValueKind == JsonValueKind.String)
                    geminiApiKey = gmKey.GetString() ?? "";
                if (root.TryGetProperty("geminiModel", out var gmMod) && gmMod.ValueKind == JsonValueKind.String)
                    geminiModel = gmMod.GetString() ?? "gemini-2.5-flash";

                if (root.TryGetProperty("openAiApiKey", out var oaKey) && oaKey.ValueKind == JsonValueKind.String)
                    openAiApiKey = oaKey.GetString() ?? "";
                if (root.TryGetProperty("openAiModel", out var oaMod) && oaMod.ValueKind == JsonValueKind.String)
                    openAiModel = oaMod.GetString() ?? "gpt-4o";

                if (root.TryGetProperty("claudeApiKey", out var clKey) && clKey.ValueKind == JsonValueKind.String)
                    claudeApiKey = clKey.GetString() ?? "";
                if (root.TryGetProperty("claudeModel", out var clMod) && clMod.ValueKind == JsonValueKind.String)
                    claudeModel = clMod.GetString() ?? "claude-3-7-sonnet";

                if (root.TryGetProperty("grokApiKey", out var grKey) && grKey.ValueKind == JsonValueKind.String)
                    grokApiKey = grKey.GetString() ?? "";
                if (root.TryGetProperty("grokModel", out var grMod) && grMod.ValueKind == JsonValueKind.String)
                    grokModel = grMod.GetString() ?? "grok-3";
            }
        }
        catch (Exception ex)
        {
            AppLog.Swallowed(ex, "AiBalanceService.ReadSettings");
        }

        // 2. VDS SQLite geçmişinden seçilen periyoda göre kullanım kayıtlarını çek
        var (since, until) = AiPeriodHelper.ResolveDateRange(period);
        var history = await _usageRepo.GetHistoryAsync(null, since, 1000, until, ct);

        // 3. DeepSeek Bilgilerini Derle ve Canlı Bakiye Sorgula
        var deepSeekInfo = new AiProviderBalanceInfo
        {
            Provider = "DeepSeek",
            ActiveModel = string.IsNullOrWhiteSpace(deepSeekModel) ? "DeepSeek-V4-Flash" : deepSeekModel,
            IsActiveProvider = activeProvider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase),
            TopUpUrl = "https://platform.deepseek.com/top_up",
            TokenUnitTariff = "$0.15 / 1M Girdi, $0.60 / 1M Çıktı",
            MaskedApiKey = AiPriceCalculator.MaskApiKey(deepSeekApiKey),
            Currency = "USD",
            IsAvailable = true
        };

        if (!string.IsNullOrWhiteSpace(deepSeekApiKey) && deepSeekApiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deepSeekApiKey);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                var response = await _httpClient.SendAsync(req, timeoutCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    var parsed = AiPriceCalculator.ParseDeepSeekBalanceJson(responseJson, deepSeekApiKey);
                    deepSeekInfo.TotalBalanceUsd = parsed.TotalBalanceUsd;
                    deepSeekInfo.GrantedBalanceUsd = parsed.GrantedBalanceUsd;
                    deepSeekInfo.ToppedUpBalanceUsd = parsed.ToppedUpBalanceUsd;
                    deepSeekInfo.BalanceCny = parsed.BalanceCny;
                    deepSeekInfo.Currency = parsed.Currency;
                    deepSeekInfo.IsAvailable = parsed.IsAvailable;
                    deepSeekInfo.StatusMessage = parsed.StatusMessage;
                }
                else
                {
                    deepSeekInfo.TotalBalanceUsd = 0.00m;
                    deepSeekInfo.ToppedUpBalanceUsd = 0.00m;
                    deepSeekInfo.IsAvailable = false;
                    deepSeekInfo.StatusMessage = "DeepSeek API Yanıt Vermedi (Bakiye: 0.00 ₺)";
                }
            }
            catch (Exception ex)
            {
                AppLog.Swallowed(ex, "AiBalanceService.DeepSeekBalance");
                deepSeekInfo.TotalBalanceUsd = 0.00m;
                deepSeekInfo.ToppedUpBalanceUsd = 0.00m;
                deepSeekInfo.IsAvailable = false;
                deepSeekInfo.StatusMessage = "DeepSeek API Bağlantı Hatası (Bakiye: 0.00 ₺)";
            }
        }
        else
        {
            deepSeekInfo.TotalBalanceUsd = 0.00m;
            deepSeekInfo.ToppedUpBalanceUsd = 0.00m;
            deepSeekInfo.IsAvailable = false;
            deepSeekInfo.StatusMessage = "API Anahtarı Tanımlanmadı (Bakiye: 0.00 ₺)";
        }

        // Tüketim istatistiklerini bağla (Sıfır mock: SQLite'ta yoksa kesinlikle 0)
        var dsRecords = history.Where(x => x.Provider.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase)).ToList();
        deepSeekInfo.TotalTokensUsed = dsRecords.Sum(x => x.TotalTokens);
        deepSeekInfo.PromptTokensUsed = dsRecords.Sum(x => x.PromptTokens);
        deepSeekInfo.CompletionTokensUsed = dsRecords.Sum(x => x.CompletionTokens);
        deepSeekInfo.TotalApiRequests = dsRecords.Count;
        deepSeekInfo.TotalCostUsd = dsRecords.Sum(x => x.EstimatedCostUsd);
        deepSeekInfo.TotalCostTry = Math.Round(deepSeekInfo.TotalCostUsd * exchangeRate, 2);

        // 4. Google Gemini Bilgileri
        bool hasGeminiKey = !string.IsNullOrWhiteSpace(geminiApiKey);
        var geminiInfo = new AiProviderBalanceInfo
        {
            Provider = "Google Gemini",
            ActiveModel = string.IsNullOrWhiteSpace(geminiModel) ? "gemini-2.5-flash" : geminiModel,
            IsActiveProvider = activeProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase),
            TopUpUrl = "https://aistudio.google.com/app/plan_information",
            TokenUnitTariff = "$0.30 / 1M Girdi, $2.50 / 1M Çıktı (Ücretsiz Havuz)",
            MaskedApiKey = AiPriceCalculator.MaskApiKey(geminiApiKey),
            Currency = "USD",
            TotalBalanceUsd = 0.00m,
            ToppedUpBalanceUsd = 0.00m,
            IsAvailable = hasGeminiKey,
            StatusMessage = hasGeminiKey ? "Google AI Studio (Ücretsiz Kota Modu - Bakiye: 0.00 ₺)" : "API Anahtarı Tanımlanmadı (Bakiye: 0.00 ₺)"
        };
        var gmRecords = history.Where(x => x.Provider.Contains("Google", StringComparison.OrdinalIgnoreCase) || x.Provider.Contains("Gemini", StringComparison.OrdinalIgnoreCase)).ToList();
        geminiInfo.TotalTokensUsed = gmRecords.Sum(x => x.TotalTokens);
        geminiInfo.PromptTokensUsed = gmRecords.Sum(x => x.PromptTokens);
        geminiInfo.CompletionTokensUsed = gmRecords.Sum(x => x.CompletionTokens);
        geminiInfo.TotalApiRequests = gmRecords.Count;
        geminiInfo.TotalCostUsd = gmRecords.Sum(x => x.EstimatedCostUsd);
        geminiInfo.TotalCostTry = Math.Round(geminiInfo.TotalCostUsd * exchangeRate, 2);

        // 5. OpenAI Bilgileri
        bool hasOpenAiKey = !string.IsNullOrWhiteSpace(openAiApiKey);
        var openAiInfo = new AiProviderBalanceInfo
        {
            Provider = "OpenAI",
            ActiveModel = string.IsNullOrWhiteSpace(openAiModel) ? "gpt-4o" : openAiModel,
            IsActiveProvider = activeProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase),
            TopUpUrl = "https://platform.openai.com/account/billing/overview",
            TokenUnitTariff = "$2.50 / 1M Girdi, $10.00 / 1M Çıktı",
            MaskedApiKey = AiPriceCalculator.MaskApiKey(openAiApiKey),
            Currency = "USD",
            TotalBalanceUsd = 0.00m,
            ToppedUpBalanceUsd = 0.00m,
            IsAvailable = hasOpenAiKey,
            StatusMessage = hasOpenAiKey ? "OpenAI API Tanımlı (Bakiye: 0.00 ₺)" : "API Anahtarı Tanımlanmadı (Bakiye: 0.00 ₺)"
        };
        var oaRecords = history.Where(x => x.Provider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase)).ToList();
        openAiInfo.TotalTokensUsed = oaRecords.Sum(x => x.TotalTokens);
        openAiInfo.PromptTokensUsed = oaRecords.Sum(x => x.PromptTokens);
        openAiInfo.CompletionTokensUsed = oaRecords.Sum(x => x.CompletionTokens);
        openAiInfo.TotalApiRequests = oaRecords.Count;
        openAiInfo.TotalCostUsd = oaRecords.Sum(x => x.EstimatedCostUsd);
        openAiInfo.TotalCostTry = Math.Round(openAiInfo.TotalCostUsd * exchangeRate, 2);

        // 6. Anthropic Claude Bilgileri
        bool hasClaudeKey = !string.IsNullOrWhiteSpace(claudeApiKey);
        var claudeInfo = new AiProviderBalanceInfo
        {
            Provider = "Claude",
            ActiveModel = string.IsNullOrWhiteSpace(claudeModel) ? "claude-3-7-sonnet" : claudeModel,
            IsActiveProvider = activeProvider.Equals("Claude", StringComparison.OrdinalIgnoreCase),
            TopUpUrl = "https://console.anthropic.com/settings/plans",
            TokenUnitTariff = "$3.00 / 1M Girdi, $15.00 / 1M Çıktı",
            MaskedApiKey = AiPriceCalculator.MaskApiKey(claudeApiKey),
            Currency = "USD",
            TotalBalanceUsd = 0.00m,
            ToppedUpBalanceUsd = 0.00m,
            IsAvailable = hasClaudeKey,
            StatusMessage = hasClaudeKey ? "Anthropic Claude API Tanımlı (Bakiye: 0.00 ₺)" : "API Anahtarı Tanımlanmadı (Bakiye: 0.00 ₺)"
        };
        var clRecords = history.Where(x => x.Provider.Contains("Claude", StringComparison.OrdinalIgnoreCase) || x.Provider.Contains("Anthropic", StringComparison.OrdinalIgnoreCase)).ToList();
        claudeInfo.TotalTokensUsed = clRecords.Sum(x => x.TotalTokens);
        claudeInfo.PromptTokensUsed = clRecords.Sum(x => x.PromptTokens);
        claudeInfo.CompletionTokensUsed = clRecords.Sum(x => x.CompletionTokens);
        claudeInfo.TotalApiRequests = clRecords.Count;
        claudeInfo.TotalCostUsd = clRecords.Sum(x => x.EstimatedCostUsd);
        claudeInfo.TotalCostTry = Math.Round(claudeInfo.TotalCostUsd * exchangeRate, 2);

        // 7. xAI Grok Bilgileri
        bool hasGrokKey = !string.IsNullOrWhiteSpace(grokApiKey);
        var grokInfo = new AiProviderBalanceInfo
        {
            Provider = "Grok",
            ActiveModel = string.IsNullOrWhiteSpace(grokModel) ? "grok-3" : grokModel,
            IsActiveProvider = activeProvider.Equals("Grok", StringComparison.OrdinalIgnoreCase),
            TopUpUrl = "https://console.x.ai/",
            TokenUnitTariff = "$2.00 / 1M Girdi, $10.00 / 1M Çıktı",
            MaskedApiKey = AiPriceCalculator.MaskApiKey(grokApiKey),
            Currency = "USD",
            TotalBalanceUsd = 0.00m,
            ToppedUpBalanceUsd = 0.00m,
            IsAvailable = hasGrokKey,
            StatusMessage = hasGrokKey ? "xAI Grok API Tanımlı (Bakiye: 0.00 ₺)" : "API Anahtarı Tanımlanmadı (Bakiye: 0.00 ₺)"
        };
        var grRecords = history.Where(x => x.Provider.Contains("Grok", StringComparison.OrdinalIgnoreCase)).ToList();
        grokInfo.TotalTokensUsed = grRecords.Sum(x => x.TotalTokens);
        grokInfo.PromptTokensUsed = grRecords.Sum(x => x.PromptTokens);
        grokInfo.CompletionTokensUsed = grRecords.Sum(x => x.CompletionTokens);
        grokInfo.TotalApiRequests = grRecords.Count;
        grokInfo.TotalCostUsd = grRecords.Sum(x => x.EstimatedCostUsd);
        grokInfo.TotalCostTry = Math.Round(grokInfo.TotalCostUsd * exchangeRate, 2);

        return new List<AiProviderBalanceInfo>
        {
            deepSeekInfo,
            geminiInfo,
            openAiInfo,
            claudeInfo,
            grokInfo
        };
    }
}
