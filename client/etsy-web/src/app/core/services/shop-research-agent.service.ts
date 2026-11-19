import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EtsyApiService } from './etsy-api.service';

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
}

export interface ResearchCandidateDto {
  candidateId: string;
  runId: string;
  title: string;
  suggestedTitle: string;
  suggestedTags: string[];
  sourcePlatform: string;
  sourceUrl: string;
  category: string;
  estimatedSellPriceUsd: number;
  estimatedNetProfitUsd: number;
  estimatedNetMarginPct: number;
  netProfitOffsiteAds12Usd: number;
  netProfitOffsiteAds15Usd: number;
  effectiveFeeRatePct: number;
  competitionCount: number;
  demandSignalScore: number;
  demandScore: number;
  demandConfidence: number; // 0=Low, 1=Medium, 2=High
  opportunityScore: number;
  riskScore: number;
  riskDecision: number; // 0=Allow, 1=Review, 2=Block
  riskReasons: string[];
  cannibalizationScore: number;
  cannibalizationWarning: string | null;
  isExplorationCandidate: boolean;
  rationale: string;
  evidenceJson: string;
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
}

@Injectable({ providedIn: 'root' })
export class ShopResearchAgentService {
  private readonly http = inject(HttpClient);
  private readonly etsyApi = inject(EtsyApiService);
  private readonly baseUrl = `${environment.apiBaseUrl}/etsy/research-agent`;

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
    return this.http.post<ResearchRunDto>(`${this.baseUrl}/run${this.shopQuery()}`, {
      focusHint: focusHint?.trim() || null,
      maxCandidates
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
