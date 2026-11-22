namespace EtsyMarketPlace.Application.Tests.AiGateway;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiGateway;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class EvrenAiGatewayServiceTests
{
    private sealed class FakeEvrenApiClient : IEvrenLlmApiClient
    {
        public EvrenChatCompletionCommand? LastCommand { get; private set; }
        public string? LastModelsApiKey { get; private set; }

        public (bool Success, IReadOnlyList<string> Models, string? ErrorMessage, int StatusCode) ModelsResponse { get; set; }
            = (true, new[] { "deepseek-v4.1-flash", "qwen3.8-flash-next", "gemma-4-31b" }, null, 200);

        public EvrenChatCompletionResult ChatResponse { get; set; }
            = new(true, "{\"title\":\"Handmade Ceramic Mug\"}", "thinking...", null, 200, "deepseek-v4.1-flash", 120, 45, 165);

        public Task<(bool Success, IReadOnlyList<string> Models, string? ErrorMessage, int StatusCode)> ListModelsAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            LastModelsApiKey = apiKey;
            return Task.FromResult(ModelsResponse);
        }

        public Task<EvrenChatCompletionResult> SendChatCompletionAsync(
            EvrenChatCompletionCommand command,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(ChatResponse);
        }
    }

    private sealed class FakeUsageRecorder : IAiUsageRecorder
    {
        public List<(string ModuleName, LlmJsonResult Result)> Recorded { get; } = new();

        public Task RecordAsync(
            string moduleName,
            LlmJsonResult result,
            string? note = null,
            CancellationToken cancellationToken = default)
        {
            Recorded.Add((moduleName, result));
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(null, "deepseek-v4.1-flash")]
    [InlineData("", "deepseek-v4.1-flash")]
    [InlineData("DeepSeek-V4-Flash", "deepseek-v4.1-flash")]
    [InlineData("deepseek-chat", "deepseek-v4.1-flash")]
    [InlineData("deepseek-v4.1-flash", "deepseek-v4.1-flash")]
    [InlineData("qwen3.8-flash-next", "qwen3.8-flash-next")]
    [InlineData("gemma-4-31b", "gemma-4-31b")]
    public void NormalizeEvrenModel_PreservesEvrenIdsAndMapsLegacyDefaults(string? input, string expected)
    {
        Assert.Equal(expected, EvrenAiGatewayService.NormalizeEvrenModel(input));
    }

    [Fact]
    public async Task TestConnectionAsync_RejectsEmptyOrShortApiKey()
    {
        var client = new FakeEvrenApiClient();
        var sut = new EvrenAiGatewayService(client);

        var result = await sut.TestConnectionAsync(new EvrenTestConnectionCommand("short", "deepseek-v4.1-flash"));

        Assert.False(result.Success);
        Assert.Null(client.LastModelsApiKey);
        Assert.Contains("evren_llm_", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsSuccessWithModels_WhenEvrenRespondsOk()
    {
        var client = new FakeEvrenApiClient();
        var sut = new EvrenAiGatewayService(client);

        var result = await sut.TestConnectionAsync(new EvrenTestConnectionCommand("evren_llm_test_1234567890", "deepseek-v4.1-flash"));

        Assert.True(result.Success);
        Assert.Equal("evren_llm_test_1234567890", client.LastModelsApiKey);
        Assert.Equal("deepseek-v4.1-flash", result.ResolvedModel);
        Assert.Contains("deepseek-v4.1-flash", result.AvailableModels);
        Assert.Contains("aktif", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsTurkishAuthGuidance_On401()
    {
        var client = new FakeEvrenApiClient
        {
            ModelsResponse = (false, Array.Empty<string>(), "Unauthorized", 401)
        };
        var sut = new EvrenAiGatewayService(client);

        var result = await sut.TestConnectionAsync(new EvrenTestConnectionCommand("evren_llm_invalid_key", "deepseek-v4.1-flash"));

        Assert.False(result.Success);
        Assert.Contains("401", result.Message);
        Assert.Contains("evren.ssyz.org.tr", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteChatAsync_PreservesModelAndRecordsTokenUsage()
    {
        var client = new FakeEvrenApiClient();
        var recorder = new FakeUsageRecorder();
        var sut = new EvrenAiGatewayService(client, recorder);

        var msgContent = JsonSerializer.SerializeToElement("Generate Etsy title for 3D printed lamp");
        var cmd = new EvrenChatCompletionCommand(
            ApiKey: "  evren_llm_valid_key_12345  ",
            Model: "deepseek-v4.1-flash",
            Messages: new[] { new EvrenChatMessageDto("user", msgContent) },
            Temperature: 0.35,
            MaxTokens: 1024,
            JsonMode: true,
            ModuleName: "Hızlı Ürün Listeleme");

        var result = await sut.CompleteChatAsync(cmd);

        Assert.True(result.Success);
        Assert.NotNull(client.LastCommand);
        Assert.Equal("evren_llm_valid_key_12345", client.LastCommand!.ApiKey);
        Assert.Equal("deepseek-v4.1-flash", client.LastCommand.Model);
        Assert.Single(recorder.Recorded);
        Assert.Equal("Hızlı Ürün Listeleme", recorder.Recorded[0].ModuleName);
        Assert.Equal("Evren", recorder.Recorded[0].Result.Provider);
        Assert.Equal(165, recorder.Recorded[0].Result.TotalTokens);
    }

    [Fact]
    public async Task CompleteChatAsync_RejectsEmptyMessages()
    {
        var client = new FakeEvrenApiClient();
        var sut = new EvrenAiGatewayService(client);

        var result = await sut.CompleteChatAsync(new EvrenChatCompletionCommand(
            ApiKey: "evren_llm_valid_key_12345",
            Model: "deepseek-v4.1-flash",
            Messages: Array.Empty<EvrenChatMessageDto>()));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Null(client.LastCommand);
    }

    [Fact]
    public async Task CompleteChatAsync_PreservesUpstream401ErrorMessage_AndRecordsFailure()
    {
        var client = new FakeEvrenApiClient
        {
            ChatResponse = new EvrenChatCompletionResult(
                Success: false,
                Content: null,
                ReasoningContent: null,
                ErrorMessage: "EVREN API Anahtarı Reddedildi (HTTP 401): Invalid API key.",
                StatusCode: 401,
                ResolvedModel: "deepseek-v4.1-flash",
                PromptTokens: 0,
                CompletionTokens: 0,
                TotalTokens: 0)
        };
        var recorder = new FakeUsageRecorder();
        var sut = new EvrenAiGatewayService(client, recorder);

        var msgContent = JsonSerializer.SerializeToElement("Test");
        var result = await sut.CompleteChatAsync(new EvrenChatCompletionCommand(
            ApiKey: "evren_llm_revoked_key_1234",
            Model: "deepseek-v4.1-flash",
            Messages: new[] { new EvrenChatMessageDto("user", msgContent) },
            ModuleName: "Hızlı Ürün Listeleme"));

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("EVREN API Anahtarı Reddedildi", result.ErrorMessage);
        Assert.Single(recorder.Recorded);
        Assert.False(recorder.Recorded[0].Result.Success);
    }
}
