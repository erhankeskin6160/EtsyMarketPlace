namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiGateway;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v{version:apiVersion}")]
[Route("api")]
public sealed class AiGatewayController : BaseApiController
{
    private readonly IEvrenAiGatewayService _evrenGateway;

    public AiGatewayController(IEvrenAiGatewayService evrenGateway)
    {
        _evrenGateway = evrenGateway ?? throw new ArgumentNullException(nameof(evrenGateway));
    }

    [HttpPost("ai/evren/test-connection")]
    [EndpointSummary("T.C. SSB EVREN Milli Yapay Zeka API bağlantı ve model doğrulama testi")]
    public async Task<IActionResult> TestEvrenConnection(
        [FromBody] EvrenTestConnectionCommand request,
        CancellationToken cancellationToken = default)
    {
        var result = await _evrenGateway.TestConnectionAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ai/evren/chat-completions")]
    [EndpointSummary("T.C. SSB EVREN OpenAI-uyumlu Chat Completions CORS-güvenli VDS geçidi")]
    public async Task<IActionResult> CompleteEvrenChat(
        [FromBody] EvrenChatCompletionCommand request,
        CancellationToken cancellationToken = default)
    {
        var result = await _evrenGateway.CompleteChatAsync(request, cancellationToken);
        if (!result.Success)
        {
            // Upstream EVREN 401/403 must NEVER be returned as HTTP 401/403 from VDS,
            // otherwise the Angular JWT/validation interceptor misinterprets it as an expired user session.
            var httpStatus = result.StatusCode == 400 ? 400 : 502;
            var msg = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "EVREN API isteği başarısız oldu."
                : result.ErrorMessage;

            return StatusCode(httpStatus, new
            {
                success = false,
                message = msg,
                error = msg,
                errorMessage = msg,
                upstreamStatusCode = result.StatusCode,
                resolvedModel = result.ResolvedModel
            });
        }

        return Ok(result);
    }
}
