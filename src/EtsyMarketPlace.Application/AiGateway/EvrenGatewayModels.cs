namespace EtsyMarketPlace.Application.AiGateway;

using System.Collections.Generic;
using System.Text.Json;

public sealed record EvrenChatMessageDto(
    string Role,
    JsonElement Content);

public sealed record EvrenChatCompletionCommand(
    string ApiKey,
    string? Model,
    IReadOnlyList<EvrenChatMessageDto> Messages,
    double? Temperature = null,
    int? MaxTokens = null,
    bool JsonMode = false,
    string? ModuleName = null);

public sealed record EvrenChatCompletionResult(
    bool Success,
    string? Content,
    string? ReasoningContent,
    string? ErrorMessage,
    int StatusCode,
    string ResolvedModel,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

public sealed record EvrenTestConnectionCommand(
    string ApiKey,
    string? Model = null);

public sealed record EvrenTestConnectionResult(
    bool Success,
    long LatencyMs,
    string Message,
    string ResolvedModel,
    IReadOnlyList<string> AvailableModels);
