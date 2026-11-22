namespace EtsyMarketPlace.Application.AiGateway;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IEvrenLlmApiClient
{
    Task<EvrenChatCompletionResult> SendChatCompletionAsync(
        EvrenChatCompletionCommand command,
        CancellationToken cancellationToken = default);

    Task<(bool Success, IReadOnlyList<string> Models, string? ErrorMessage, int StatusCode)> ListModelsAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}
