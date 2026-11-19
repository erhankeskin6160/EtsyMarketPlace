import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EtsyApiService } from './etsy-api.service';
import { AiSettingsService } from './ai-settings.service';

export type ResearchScheduleCadence = 0 | 1 | 2 | 3; // 0=Off, 1=Daily, 2=Weekly, 3=Monthly
export type CandidateDecisionStatus = 0 | 1 | 2 | 3; // 0=Pending, 1=Accepted, 2=Rejected, 3=SentToDraft
export type CandidateRejectionReason = 0 | 1 | 2 | 3 | 4 | 5;

export interface ShopDnaProfileDto {
  ownerUserId: string;
  shopId: string;
  dnaVersion: number;
  primaryNiche: string;
  targetAudience: string;
  targetPriceMinUsd: number;
  targetPriceMaxUsd: number;
  minAcceptableNetMarginPct: number;
  estimatedCogsRatio: number;
  supports3DPrint: boolean;
  preferredMaterials: string[];
  topPerformingTags: string[];
  categoryKeywords: string[];
  forbiddenKeywords: string[];
  scheduleCadence: ResearchScheduleCadence;
  lastScheduledRunAtUtc: string | null;
  updatedAtUtc: string;
  defaultShippingCostUsd?: number;
  defaultPackagingCostUsd?: number;
  returnReserveRatePct?: number;
}

export interface ResearchFunnelTelemetryDto {
  scannedCount: number;
  blockedByIpCount: number;
  rejectedByMemoryCount: number;
  rejectedByPriceCount: number;
  rejectedByMarginCount: number;
  rejectedByDeadNicheCount: number;
  rejectedByCannibalizationCount: number;
  winningCount: number;
  criticIterations: number;
  etsyCallsUsed: number;
  etsyCallBudget: number;
  activeSeasonName: string | null;
}

export interface VariationTierDto {
  tierName: string;
  label: string;
  priceUsd: number;
  valueProp: string;
}

export interface ProductBlueprintDto {
  mobileTitle55: string;
  fullTitle140: string;
  longTailTags: string[];
  variationLadder: VariationTierDto[];
  differentiationAngle: string;
  competitorGap: string;
  aiMockupBrief: string;
  recommendedMaterials: string[];
}

export interface ResearchProgressEventDto {
  runId: string;
  stageKey: string;
  stageLabel: string;
  stageIndex: number;
  totalStages: number;
  scannedCount: number;
  remainingCount: number;
  detail: string;
  timestampUtc: string;
}

export interface ResearchCandidateDto {
  candidateId: string;
  runId: string;
  title: string;
  suggestedTitle: string;
  suggestedTags: string[];
  sourcePlatform: string;
  sourceUrl: string;
  imageUrl?: string;
  category: string;
  estimatedSellPriceUsd: number;
  estimatedNetProfitUsd: number;
  estimatedNetMarginPct: number;
  estimatedShippingUsd?: number;
  estimatedPackagingUsd?: number;
  estimatedReturnReserveUsd?: number;
  estimatedCogsUsd?: number;
  netProfitOffsiteAds12Usd: number;
  netProfitOffsiteAds15Usd: number;
  effectiveFeeRatePct: number;
  competitionCount: number;
  topShopConcentrationPct?: number;
  freeShippingRatioPct?: number;
  seasonalBoostPct?: number;
  momentumScore?: number;
  demandSignalScore: number;
  demandScore: number;
  demandConfidence: number; // 0=Low, 1=Medium, 2=High
  opportunityScore: number;
  riskScore: number;
  riskDecision: number; // 0=Allow, 1=Review, 2=Block
  riskReasons: string[];
  warningFlags?: string[];
  cannibalizationScore: number;
  cannibalizationWarning: string | null;
  isExplorationCandidate: boolean;
  rationale: string;
  evidenceJson: string;
  mobileTitle55?: string;
  blueprintJson?: string;
  decisionStatus: CandidateDecisionStatus;
  rejectionReason: CandidateRejectionReason;
  decidedAtUtc: string | null;
}

export interface ResearchRunDto {
  runId: string;
  ownerUserId: string;
  shopId: string;
  triggerType: number;
  status: number;
  startedAtUtc: string;
  completedAtUtc: string | null;
  searchQueriesUsed: string[];
  explorationQueryUsed: string | null;
  effectiveShopFeeRatePct: number;
  funnelTelemetryJson?: string;
  criticIterations?: number;
  summary: string;
  errorMessage: string | null;
  candidates: ResearchCandidateDto[];
}

export interface UpdateShopDnaPayload {
  primaryNiche: string;
  targetAudience: string;
  targetPriceMinUsd: number;
  targetPriceMaxUsd: number;
  minAcceptableNetMarginPct: number;
  estimatedCogsRatio: number;
  supports3DPrint: boolean;
  preferredMaterials: string[];
  forbiddenKeywords: string[];
  scheduleCadence: ResearchScheduleCadence;
  defaultShippingCostUsd?: number;
  defaultPackagingCostUsd?: number;
  returnReserveRatePct?: number;
}

@Injectable({ providedIn: 'root' })
export class ShopResearchAgentService {
  private readonly http = inject(HttpClient);
  private readonly etsyApi = inject(EtsyApiService);
  private readonly aiSettings = inject(AiSettingsService);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/etsy/research-agent`;

  private shopQuery(refresh = false): string {
    const shopId = encodeURIComponent(this.etsyApi.activeShopId());
    return refresh ? `?shopId=${shopId}&refresh=true` : `?shopId=${shopId}`;
  }

  getDna(refresh = false): Observable<ShopDnaProfileDto> {
    return this.http.get<ShopDnaProfileDto>(`${this.baseUrl}/dna${this.shopQuery(refresh)}`);
  }

  updateDna(payload: UpdateShopDnaPayload): Observable<ShopDnaProfileDto> {
    return this.http.put<ShopDnaProfileDto>(`${this.baseUrl}/dna${this.shopQuery()}`, payload);
  }

  runResearch(focusHint?: string, maxCandidates = 10): Observable<ResearchRunDto> {
    const s = this.aiSettings.settings();
    let model: string | null = null;
    let apiKey: string | null = null;

    if (s.provider !== 'Offline') {
      const activeKey = this.aiSettings.getActiveApiKey(s.provider);
      if (activeKey) {
        model = this.aiSettings.activeModelName();
        apiKey = activeKey;
      } else if (s.provider === 'Gemini') {
        model = this.aiSettings.activeModelName();
        apiKey = null;
      }
    }

    return this.http.post<ResearchRunDto>(`${this.baseUrl}/run${this.shopQuery()}`, {
      focusHint: focusHint?.trim() || null,
      maxCandidates,
      model,
      apiKey
    });
  }

  streamProgress(): Observable<ResearchProgressEventDto> {
    return new Observable<ResearchProgressEventDto>((subscriber) => {
      const controller = new AbortController();
      const token = localStorage.getItem('etsy_web_jwt_token');
      const url = `${this.baseUrl}/stream${this.shopQuery()}`;

      fetch(url, {
        method: 'GET',
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        signal: controller.signal
      })
        .then(async (response) => {
          if (!response.ok || !response.body) {
            subscriber.complete();
            return;
          }

          const reader = response.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });

            const blocks = buffer.split(/\r?\n\r?\n/);
            buffer = blocks.pop() ?? '';

            for (const block of blocks) {
              const lines = block.split(/\r?\n/);
              let eventName = 'message';
              let dataPayload = '';
              for (const line of lines) {
                if (line.startsWith('event:')) {
                  eventName = line.slice(6).trim();
                } else if (line.startsWith('data:')) {
                  dataPayload += line.slice(5).trim();
                }
              }

              if (eventName === 'progress' && dataPayload) {
                try {
                  const parsed = JSON.parse(dataPayload) as ResearchProgressEventDto;
                  subscriber.next(parsed);
                } catch {
                  // Ignore malformed event chunk
                }
              }
            }
          }

          subscriber.complete();
        })
        .catch(() => {
          subscriber.complete();
        });

      return () => {
        controller.abort();
      };
    });
  }

  getRuns(limit = 15): Observable<ResearchRunDto[]> {
    const shopId = encodeURIComponent(this.etsyApi.activeShopId());
    return this.http.get<ResearchRunDto[]>(`${this.baseUrl}/runs?shopId=${shopId}&limit=${limit}`);
  }

  getRunById(runId: string): Observable<ResearchRunDto> {
    return this.http.get<ResearchRunDto>(`${this.baseUrl}/runs/${runId}${this.shopQuery()}`);
  }

  recordDecision(
    candidateId: string,
    decision: CandidateDecisionStatus,
    reason: CandidateRejectionReason = 0
  ): Observable<{ candidateId: string; decision: number }> {
    return this.http.post<{ candidateId: string; decision: number }>(
      `${this.baseUrl}/candidates/${candidateId}/decision${this.shopQuery()}`,
      { decision, reason }
    );
  }
}
