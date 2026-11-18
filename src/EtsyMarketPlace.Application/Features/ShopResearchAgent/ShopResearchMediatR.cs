namespace EtsyMarketPlace.Application.Features.ShopResearchAgent;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Application.ShopResearchAgent.Services;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using MediatR;

public sealed record GetShopDnaQuery(string OwnerUserId, string ShopId, bool ForceRefresh = false)
    : IRequest<ServiceResponse>;

public sealed record UpdateShopDnaCommand(string OwnerUserId, string ShopId, UpdateShopDnaRequest Dna)
    : IRequest<ServiceResponse>;

public sealed record RunShopResearchCommand(
    string OwnerUserId,
    string ShopId,
    ResearchTriggerType TriggerType,
    RunShopResearchRequest? Request) : IRequest<ServiceResponse>;

public sealed record GetResearchRunsQuery(string OwnerUserId, string ShopId, int Limit = 20)
    : IRequest<ServiceResponse>;

public sealed record GetResearchRunByIdQuery(string OwnerUserId, string ShopId, Guid RunId)
    : IRequest<ServiceResponse>;

public sealed record RecordCandidateDecisionCommand(
    string OwnerUserId,
    string ShopId,
    Guid CandidateId,
    RecordCandidateDecisionRequest Request) : IRequest<ServiceResponse>;

public sealed class GetShopDnaHandler(ShopResearchAgentOrchestrator orchestrator)
    : IRequestHandler<GetShopDnaQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetShopDnaQuery request, CancellationToken cancellationToken)
    {
        var profile = await orchestrator.GetOrBuildDnaAsync(
            request.OwnerUserId, request.ShopId, request.ForceRefresh, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, profile);
    }
}

public sealed class UpdateShopDnaHandler(IShopResearchRepository repository)
    : IRequestHandler<UpdateShopDnaCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpdateShopDnaCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetDnaProfileAsync(request.OwnerUserId, request.ShopId, cancellationToken);
        var dto = request.Dna;

        var updated = new ShopDnaProfile
        {
            OwnerUserId = request.OwnerUserId,
            ShopId = request.ShopId,
            DnaVersion = (existing?.DnaVersion ?? 0) + 1,
            PrimaryNiche = string.IsNullOrWhiteSpace(dto.PrimaryNiche) ? (existing?.PrimaryNiche ?? "Genel Etsy Mağazası") : dto.PrimaryNiche.Trim(),
            TargetAudience = dto.TargetAudience?.Trim() ?? existing?.TargetAudience ?? string.Empty,
            TargetPriceMinUsd = Math.Max(1m, dto.TargetPriceMinUsd),
            TargetPriceMaxUsd = Math.Max(Math.Max(1m, dto.TargetPriceMinUsd), dto.TargetPriceMaxUsd),
            MinAcceptableNetMarginPct = Math.Clamp(dto.MinAcceptableNetMarginPct, 5m, 85m),
            EstimatedCogsRatio = Math.Clamp(dto.EstimatedCogsRatio, 0.05m, 0.85m),
            Supports3DPrint = dto.Supports3DPrint,
            PreferredMaterials = dto.PreferredMaterials ?? Array.Empty<string>(),
            TopPerformingTags = existing?.TopPerformingTags ?? Array.Empty<string>(),
            CategoryKeywords = existing?.CategoryKeywords ?? Array.Empty<string>(),
            ForbiddenKeywords = dto.ForbiddenKeywords ?? Array.Empty<string>(),
            ScheduleCadence = dto.ScheduleCadence,
            LastScheduledRunAtUtc = existing?.LastScheduledRunAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await repository.UpsertDnaProfileAsync(updated, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, updated);
    }
}

public sealed class RunShopResearchHandler(ShopResearchAgentOrchestrator orchestrator)
    : IRequestHandler<RunShopResearchCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(RunShopResearchCommand request, CancellationToken cancellationToken)
    {
        var run = await orchestrator.RunResearchAsync(
            request.OwnerUserId,
            request.ShopId,
            request.TriggerType,
            request.Request,
            cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, run);
    }
}

public sealed class GetResearchRunsHandler(IShopResearchRepository repository)
    : IRequestHandler<GetResearchRunsQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetResearchRunsQuery request, CancellationToken cancellationToken)
    {
        var runs = await repository.GetRunsAsync(request.OwnerUserId, request.ShopId, request.Limit, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, runs);
    }
}

public sealed class GetResearchRunByIdHandler(IShopResearchRepository repository)
    : IRequestHandler<GetResearchRunByIdQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetResearchRunByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await repository.GetRunByIdAsync(request.OwnerUserId, request.ShopId, request.RunId, cancellationToken);
        if (run == null)
        {
            return ServiceResponse.CreateError("Araştırma oturumu bulunamadı.", HttpStatusCode.NotFound);
        }
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, run);
    }
}

public sealed class RecordCandidateDecisionHandler(IShopResearchRepository repository)
    : IRequestHandler<RecordCandidateDecisionCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(RecordCandidateDecisionCommand request, CancellationToken cancellationToken)
    {
        var candidate = await repository.GetCandidateByIdAsync(
            request.OwnerUserId, request.ShopId, request.CandidateId, cancellationToken);

        if (candidate == null)
        {
            return ServiceResponse.CreateError("Aday ürün bulunamadı veya bu hesaba ait değil.", HttpStatusCode.NotFound);
        }

        await repository.UpdateCandidateDecisionAsync(
            request.OwnerUserId,
            request.ShopId,
            request.CandidateId,
            request.Request.Decision,
            request.Request.Reason,
            cancellationToken);

        await repository.AppendDecisionEventAsync(new CandidateDecisionEvent
        {
            EventId = Guid.NewGuid(),
            CandidateId = candidate.CandidateId,
            OwnerUserId = request.OwnerUserId,
            ShopId = request.ShopId,
            CandidateTitle = candidate.Title,
            Category = candidate.Category,
            Tags = candidate.SuggestedTags,
            Decision = request.Request.Decision,
            Reason = request.Request.Reason,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, new { candidateId = request.CandidateId, decision = request.Request.Decision });
    }
}
