namespace EtsyMarketPlace.Application.AiGateway;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.ListingOptimization;

public interface IEvrenAiGatewayService
{
    Task<EvrenTestConnectionResult> TestConnectionAsync(
        EvrenTestConnectionCommand command,
        CancellationToken cancellationToken = default);

    Task<EvrenChatCompletionResult> CompleteChatAsync(
        EvrenChatCompletionCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class EvrenAiGatewayService : IEvrenAiGatewayService
{
    public const string ProviderName = "Evren";
    public const string DefaultBaseUrl = "https://evren-llmapi.ssyz.org.tr/v1";
    public const string DefaultModel = "deepseek-v4.1-flash";

    private readonly IEvrenLlmApiClient _apiClient;
    private readonly IAiUsageRecorder? _usageRecorder;

    public EvrenAiGatewayService(
        IEvrenLlmApiClient apiClient,
        IAiUsageRecorder? usageRecorder = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _usageRecorder = usageRecorder;
    }

    public static string NormalizeEvrenModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return DefaultModel;

        var trimmed = model.Trim();
        if (trimmed.Equals("DeepSeek-V4-Flash", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("deepseek-chat", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("deepseek-reasoner", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultModel;
        }

        return trimmed;
    }

    public async Task<EvrenTestConnectionResult> TestConnectionAsync(
        EvrenTestConnectionCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiKey = command?.ApiKey?.Trim() ?? string.Empty;
        var resolvedModel = NormalizeEvrenModel(command?.Model);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 8)
        {
            return new EvrenTestConnectionResult(
                Success: false,
                LatencyMs: 0,
                Message: "EVREN API anahtarı boş veya çok kısa. Lütfen evren.ssyz.org.tr üzerinden aldığınız 'evren_llm_...' anahtarını giriniz.",
                ResolvedModel: resolvedModel,
                AvailableModels: Array.Empty<string>());
        }

        var sw = Stopwatch.StartNew();
        var (success, models, errorMessage, statusCode) = await _apiClient.ListModelsAsync(apiKey, cancellationToken);
        sw.Stop();

        if (!success)
        {
            var msg = statusCode == 401 || statusCode == 403
                ? $"EVREN Yetkilendirme Hatası (HTTP {statusCode}): API anahtarı geçersiz veya yetkisiz. Lütfen evren.ssyz.org.tr panelindeki anahtarınızı kontrol edin."
                : (string.IsNullOrWhiteSpace(errorMessage)
                    ? $"EVREN API bağlantısı başarısız (HTTP {statusCode})."
                    : errorMessage);

            return new EvrenTestConnectionResult(
                Success: false,
                LatencyMs: sw.ElapsedMilliseconds,
                Message: msg,
                ResolvedModel: resolvedModel,
                AvailableModels: Array.Empty<string>());
        }

        var modelAvailable = models.Any(m => m.Equals(resolvedModel, StringComparison.OrdinalIgnoreCase));
        var availabilityNote = modelAvailable
            ? $"Seçili model ({resolvedModel}) aktif."
            : $"Bağlantı aktif ({models.Count} model bulundu; seçili model: {resolvedModel}).";

        return new EvrenTestConnectionResult(
            Success: true,
            LatencyMs: sw.ElapsedMilliseconds,
            Message: $"Başarılı! T.C. SSB EVREN API bağlantısı {sw.ElapsedMilliseconds}ms içinde doğrulandı. {availabilityNote}",
            ResolvedModel: resolvedModel,
            AvailableModels: models);
    }

    public async Task<EvrenChatCompletionResult> CompleteChatAsync(
        EvrenChatCompletionCommand command,
        CancellationToken cancellationToken = default)
    {
        var apiKey = command?.ApiKey?.Trim() ?? string.Empty;
        var resolvedModel = NormalizeEvrenModel(command?.Model);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 8)
        {
            return new EvrenChatCompletionResult(
                Success: false,
                Content: null,
                ReasoningContent: null,
                ErrorMessage: "EVREN API anahtarı eksik veya geçersiz.",
                StatusCode: 400,
                ResolvedModel: resolvedModel,
                PromptTokens: 0,
                CompletionTokens: 0,
                TotalTokens: 0);
        }

        if (command?.Messages == null || command.Messages.Count == 0)
        {
            return new EvrenChatCompletionResult(
                Success: false,
                Content: null,
                ReasoningContent: null,
                ErrorMessage: "En az bir sohbet mesajı (messages) gönderilmelidir.",
                StatusCode: 400,
                ResolvedModel: resolvedModel,
                PromptTokens: 0,
                CompletionTokens: 0,
                TotalTokens: 0);
        }

        var normalizedCommand = command with
        {
            ApiKey = apiKey,
            Model = resolvedModel
        };

        var result = await _apiClient.SendChatCompletionAsync(normalizedCommand, cancellationToken);

        if (_usageRecorder != null)
        {
            var moduleName = string.IsNullOrWhiteSpace(command.ModuleName)
                ? "EVREN SSB AI"
                : command.ModuleName.Trim();

            var llmRecord = new LlmJsonResult(
                Success: result.Success,
                Content: result.Content,
                ErrorMessage: result.ErrorMessage,
                PromptTokens: result.PromptTokens,
                CompletionTokens: result.CompletionTokens,
                TotalTokens: result.TotalTokens,
                Provider: ProviderName,
                ResolvedModel: result.ResolvedModel);

            await _usageRecorder.RecordAsync(moduleName, llmRecord, null, cancellationToken);
        }

        return result;
    }
}
