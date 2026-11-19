import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import {
  ShopResearchAgentService,
  ShopDnaProfileDto,
  ResearchRunDto,
  ResearchCandidateDto,
  ResearchFunnelTelemetryDto,
  ProductBlueprintDto,
  ResearchProgressEventDto,
  ResearchScheduleCadence,
  CandidateDecisionStatus,
  CandidateRejectionReason
} from '../../core/services/shop-research-agent.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';

export type CandidateFilterMode = 'all' | 'safe' | 'explore' | 'low_monopoly' | 'high_demand';
export type CandidateSortMode = 'opportunity' | 'profit' | 'momentum';

@Component({
  selector: 'app-shop-research-agent',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './shop-research-agent.component.html',
  styleUrl: './shop-research-agent.component.css'
})
export class ShopResearchAgentComponent {
  private readonly agentService = inject(ShopResearchAgentService);
  private readonly aiSettings = inject(AiSettingsService);
  readonly etsyApi = inject(EtsyApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly dna = signal<ShopDnaProfileDto | null>(null);
  readonly runs = signal<ResearchRunDto[]>([]);
  readonly activeRun = signal<ResearchRunDto | null>(null);

  readonly loadingDna = signal(false);
  readonly savingDna = signal(false);
  readonly runningAgent = signal(false);
  readonly dnaSettingsExpanded = signal(false);
  readonly statusMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  // V3 Cockpit Signals
  readonly liveProgress = signal<ResearchProgressEventDto | null>(null);
  readonly filterMode = signal<CandidateFilterMode>('all');
  readonly sortMode = signal<CandidateSortMode>('opportunity');
  readonly selectedCandidateId = signal<string | null>(null);
  readonly drawerTab = signal<'evidence' | 'compare'>('evidence');
  readonly pinnedCompareIds = signal<string[]>([]);

  // Interactive TR->ABD Simulator signals
  readonly simPriceUsd = signal<number>(35);
  readonly simShippingUsd = signal<number>(10);
  readonly simCogsUsd = signal<number>(10);
  readonly simAdsPct = signal<0 | 12 | 15>(0);

  private sseSub: Subscription | null = null;

  // Editable DNA form fields
  focusHint = '';
  forbiddenKeywordsInput = '';
  materialsInput = '';
  scheduleCadence: ResearchScheduleCadence = 1;
  minMarginPct = 25;
  priceMinUsd = 15;
  priceMaxUsd = 75;
  defaultShippingCostUsd = 0;
  defaultPackagingCostUsd = 1.0;
  returnReserveRatePct = 4.0;
  supports3DPrint = false;

  readonly activeTelemetry = computed<ResearchFunnelTelemetryDto | null>(() => {
    const run = this.activeRun();
    if (!run?.funnelTelemetryJson || run.funnelTelemetryJson === '{}') return null;
    try {
      return JSON.parse(run.funnelTelemetryJson) as ResearchFunnelTelemetryDto;
    } catch {
      return null;
    }
  });

  readonly filteredCandidates = computed<ResearchCandidateDto[]>(() => {
    const run = this.activeRun();
    if (!run?.candidates?.length) return [];

    const mode = this.filterMode();
    let list = run.candidates.filter((c) => {
      switch (mode) {
        case 'safe': return c.riskDecision === 0 && (c.warningFlags?.length ?? 0) === 0;
        case 'explore': return c.isExplorationCandidate;
        case 'low_monopoly': return (c.topShopConcentrationPct ?? 0) < 30;
        case 'high_demand': return c.demandConfidence === 2 || c.demandScore >= 65;
        default: return true;
      }
    });

    const sort = this.sortMode();
    list = [...list].sort((a, b) => {
      if (sort === 'profit') return b.estimatedNetProfitUsd - a.estimatedNetProfitUsd;
      if (sort === 'momentum') return (b.momentumScore ?? 50) - (a.momentumScore ?? 50);
      return b.opportunityScore - a.opportunityScore;
    });

    return list;
  });

  readonly selectedCandidate = computed<ResearchCandidateDto | null>(() => {
    const list = this.filteredCandidates();
    const all = this.activeRun()?.candidates ?? [];
    const targetId = this.selectedCandidateId();
    if (targetId) {
      const found = all.find(c => c.candidateId === targetId);
      if (found) return found;
    }
    return list[0] ?? all[0] ?? null;
  });

  readonly selectedBlueprint = computed<ProductBlueprintDto | null>(() => {
    const c = this.selectedCandidate();
    return c ? this.parseBlueprint(c) : null;
  });

  readonly comparedCandidates = computed<ResearchCandidateDto[]>(() => {
    const all = this.activeRun()?.candidates ?? [];
    const pinned = this.pinnedCompareIds();
    const picked = all.filter(c => pinned.includes(c.candidateId));
    return picked.length >= 2 ? picked : all.slice(0, 3);
  });

  readonly simCalculation = computed(() => {
    const c = this.selectedCandidate();
    const price = Math.max(1, Number(this.simPriceUsd()) || 0);
    const ship = Math.max(0, Number(this.simShippingUsd()) || 0);
    const cogs = Math.max(0, Number(this.simCogsUsd()) || 0);
    const pack = c?.estimatedPackagingUsd ?? (Number(this.defaultPackagingCostUsd) || 1);
    const returnPct = Number(this.returnReserveRatePct) || 4;
    const adsPct = this.simAdsPct();

    const feeRate = (this.activeRun()?.effectiveShopFeeRatePct ?? 13.1) / 100;
    const standardFees = (price * feeRate) + 0.45;
    const adsFee = price * (adsPct / 100);
    const returnReserve = (price + ship + cogs) * (returnPct / 100);
    const netUsd = price - standardFees - adsFee - cogs - ship - pack - returnReserve;
    const marginPct = price > 0 ? (netUsd / price) * 100 : 0;
    const rate = this.etsyApi.exchangeRate() || 49.12;

    return {
      netUsd: Math.round(netUsd * 100) / 100,
      netTry: Math.round(netUsd * rate),
      marginPct: Math.round(marginPct * 10) / 10,
      feesUsd: Math.round((standardFees + adsFee) * 100) / 100,
      returnReserveUsd: Math.round(returnReserve * 100) / 100,
      packUsd: pack,
      status: marginPct >= 25 ? 'profitable' : marginPct >= 10 ? 'borderline' : 'loss'
    };
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.stopSseStream());

    effect(() => {
      const currentShopId = this.etsyApi.activeShopId();
      if (!currentShopId) return;

      untracked(() => {
        this.stopSseStream();
        this.dna.set(null);
        this.runs.set([]);
        this.activeRun.set(null);
        this.selectedCandidateId.set(null);
        this.pinnedCompareIds.set([]);
        this.liveProgress.set(null);
        this.statusMessage.set(null);
        this.errorMessage.set(null);
        this.loadDna(false);
        this.loadRuns();
      });
    }, { allowSignalWrites: true });

    effect(() => {
      const sel = this.selectedCandidate();
      if (!sel) return;
      untracked(() => this.syncSimulatorFromCandidate(sel));
    }, { allowSignalWrites: true });
  }

  toggleDnaSettings(): void {
    this.dnaSettingsExpanded.update(v => !v);
  }

  selectCandidate(c: ResearchCandidateDto): void {
    this.selectedCandidateId.set(c.candidateId);
    this.drawerTab.set('evidence');
  }

  toggleCompare(candidateId: string, event: Event): void {
    event.stopPropagation();
    this.pinnedCompareIds.update((prev) => {
      if (prev.includes(candidateId)) {
        return prev.filter(id => id !== candidateId);
      }
      if (prev.length >= 3) {
        return [...prev.slice(1), candidateId];
      }
      return [...prev, candidateId];
    });
  }

  isPinnedForCompare(candidateId: string): boolean {
    return this.pinnedCompareIds().includes(candidateId);
  }

  loadDna(refreshFromShop: boolean): void {
    const requestedShopId = this.etsyApi.activeShopId();
    this.loadingDna.set(true);
    this.errorMessage.set(null);
    if (refreshFromShop) {
      this.statusMessage.set('Mağaza DNA profiliniz canlı Etsy ilanlarınızdan yenileniyor...');
    }

    this.agentService.getDna(refreshFromShop)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.dna.set(profile);
          this.syncFormFromDna(profile);
          this.loadingDna.set(false);
          this.errorMessage.set(null);
          if (refreshFromShop) {
            this.statusMessage.set('Mağaza DNA profiliniz canlı Etsy ilanlarınızdan güncellendi.');
          }
        },
        error: (err) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.loadingDna.set(false);
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Mağaza DNA profili yüklenemedi.'));
        }
      });
  }

  saveDnaSettings(): void {
    const current = this.dna();
    if (!current) return;

    const requestedShopId = this.etsyApi.activeShopId();
    this.savingDna.set(true);
    this.errorMessage.set(null);
    const forbidden = this.forbiddenKeywordsInput.split(',').map(s => s.trim()).filter(Boolean);
    const materials = this.materialsInput.split(',').map(s => s.trim()).filter(Boolean);

    this.agentService.updateDna({
      primaryNiche: current.primaryNiche,
      targetAudience: current.targetAudience,
      targetPriceMinUsd: Number(this.priceMinUsd),
      targetPriceMaxUsd: Number(this.priceMaxUsd),
      minAcceptableNetMarginPct: Number(this.minMarginPct),
      estimatedCogsRatio: current.estimatedCogsRatio,
      supports3DPrint: this.supports3DPrint,
      preferredMaterials: materials,
      forbiddenKeywords: forbidden,
      scheduleCadence: Number(this.scheduleCadence) as ResearchScheduleCadence,
      defaultShippingCostUsd: Number(this.defaultShippingCostUsd) || 0,
      defaultPackagingCostUsd: Number(this.defaultPackagingCostUsd) || 1,
      returnReserveRatePct: Number(this.returnReserveRatePct) || 4
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.dna.set(updated);
          this.syncFormFromDna(updated);
          this.savingDna.set(false);
          this.errorMessage.set(null);
          this.statusMessage.set('Mağaza DNA kısıtları, navlun maliyetleri ve tarama takvimi kaydedildi.');
        },
        error: (err) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.savingDna.set(false);
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Ayarlar kaydedilemedi.'));
        }
      });
  }

  runAgentNow(): void {
    const requestedShopId = this.etsyApi.activeShopId();
    this.runningAgent.set(true);
    this.errorMessage.set(null);
    this.liveProgress.set({
      runId: '',
      stageKey: 'dna',
      stageLabel: '1. Mağaza DNA & Sezon',
      stageIndex: 1,
      totalStages: 6,
      scannedCount: 0,
      remainingCount: 0,
      detail: 'SSE veri rafinerisi başlatılıyor...',
      timestampUtc: new Date().toISOString()
    });
    this.statusMessage.set('V3 Ajanı 6 aşamalı çoklu kaynak taramasını başlattı...');
    this.startSseStream();

    this.agentService.runResearch(this.focusHint, 10)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (run) => {
          this.stopSseStream();
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.runningAgent.set(false);
          this.liveProgress.set(null);
          this.activeRun.set(run);
          this.selectedCandidateId.set(run.candidates?.[0]?.candidateId ?? null);
          this.runs.update(prev => [run, ...prev.filter(r => r.runId !== run.runId)]);
          if (run.errorMessage) {
            this.statusMessage.set(null);
            this.errorMessage.set(run.errorMessage);
          } else {
            this.errorMessage.set(null);
            this.statusMessage.set(run.summary);
          }
        },
        error: (err) => {
          this.stopSseStream();
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.runningAgent.set(false);
          this.liveProgress.set(null);
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Araştırma ajanı çalıştırılamadı.'));
        }
      });
  }

  loadRuns(): void {
    const requestedShopId = this.etsyApi.activeShopId();
    this.agentService.getRuns(15)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.runs.set(list);
          if (list.length > 0 && !this.activeRun()) {
            this.activeRun.set(list[0]);
            this.selectedCandidateId.set(list[0].candidates?.[0]?.candidateId ?? null);
          }
        },
        error: (err) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.statusMessage.set(null);
          if (!this.errorMessage()) {
            this.errorMessage.set(this.extractErrorMessage(err, 'Araştırma oturumları yüklenemedi.'));
          }
        }
      });
  }

  selectRun(runId: string): void {
    const requestedShopId = this.etsyApi.activeShopId();
    this.errorMessage.set(null);
    this.agentService.getRunById(runId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (run) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.activeRun.set(run);
          this.selectedCandidateId.set(run.candidates?.[0]?.candidateId ?? null);
        },
        error: (err) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Araştırma oturumu detayları yüklenemedi.'));
        }
      });
  }

  acceptCandidate(c: ResearchCandidateDto): void {
    this.agentService.recordDecision(c.candidateId, 1, 0)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.applyCandidateDecision(c.candidateId, 1, 0);
          this.errorMessage.set(null);
          this.statusMessage.set(`"${c.title}" onaylandı ve Thompson Bandit hafızasına güçlendirilmiş ödül eklendi.`);
        },
        error: (err) => {
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Aday onay kararı kaydedilemedi.'));
        }
      });
  }

  rejectCandidate(c: ResearchCandidateDto, reason: CandidateRejectionReason): void {
    this.agentService.recordDecision(c.candidateId, 2, reason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.applyCandidateDecision(c.candidateId, 2, reason);
          this.errorMessage.set(null);
          this.statusMessage.set(`"${c.title}" reddedildi; ajan sonraki aramalarda bu desenden kaçınacak.`);
        },
        error: (err) => {
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Aday red kararı kaydedilemedi.'));
        }
      });
  }

  sendToFastCreator(c: ResearchCandidateDto): void {
    const bp = this.parseBlueprint(c);
    const sim = this.simCalculation();
    const ladder = bp?.variationLadder ?? [];
    const targetPrice = this.selectedCandidateId() === c.candidateId ? Number(this.simPriceUsd()) : c.estimatedSellPriceUsd;

    this.agentService.recordDecision(c.candidateId, 3, 0)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.applyCandidateDecision(c.candidateId, 3, 0);
          this.errorMessage.set(null);
          const briefNotes = bp
            ? `${bp.differentiationAngle}\n\nRakip Boşluğu: ${bp.competitorGap}\n\nAI Stüdyo Mockup Brifi: ${bp.aiMockupBrief}\n\nTahmini Landed Net: $${sim.netUsd.toFixed(2)} (%${sim.marginPct.toFixed(1)})`
            : c.rationale;

          this.aiSettings.setClonedListing({
            id: c.candidateId,
            title: bp?.fullTitle140 || c.suggestedTitle || c.title,
            priceUsd: targetPrice,
            description: briefNotes,
            tags: bp?.longTailTags?.length ? bp.longTailTags : (c.suggestedTags || []),
            materials: bp?.recommendedMaterials ?? [],
            imageUrl: c.imageUrl || undefined,
            imageUrls: c.imageUrl ? [c.imageUrl] : [],
            sourceListingUrl: c.sourceUrl || undefined,
            hasVariations: ladder.length > 0,
            varType1: ladder.length > 0 ? 'Seçenek / Paket' : undefined,
            varValues1: ladder.length > 0 ? ladder.map(t => t.label).join(', ') : undefined,
            variationRows: ladder.length > 0
              ? ladder.map(t => ({ key: t.label, price: t.priceUsd, quantity: 10, active: true }))
              : undefined
          });
          this.router.navigate(['/listings/fast-creator']);
        },
        error: (err) => {
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Aday taslağa aktarılamadı.'));
        }
      });
  }

  parseBlueprint(c: ResearchCandidateDto): ProductBlueprintDto | null {
    if (!c.blueprintJson || c.blueprintJson === '{}') return null;
    try {
      return JSON.parse(c.blueprintJson) as ProductBlueprintDto;
    } catch {
      return null;
    }
  }

  warningFlagLabel(flag: string): string {
    switch (flag) {
      case 'HighMonopoly': return '⚠️ Yüksek Tekel';
      case 'FreeShippingPressure': return '🚚 Ücr. Kargo Baskısı';
      case 'FreeShippingMarginRisk': return '💸 Navlun Marj Riski';
      case 'RedOcean': return '🌊 Doygun Pazar';
      case 'SeasonalPeak': return '📅 Sezon Zirvesi';
      case 'LossLeader': return '🔻 Aşırı Düşük Fiyat';
      default: return flag;
    }
  }

  triggerLabel(triggerType: number): string {
    switch (triggerType) {
      case 1: return 'Günlük';
      case 2: return 'Haftalık';
      case 3: return 'Aylık';
      default: return 'Manuel';
    }
  }

  decisionLabel(status: number): string {
    switch (status) {
      case 1: return 'Onaylandı';
      case 2: return 'Reddedildi';
      case 3: return 'Taslağa Alındı';
      default: return 'Beklemede';
    }
  }

  demandConfidenceLabel(conf: number): string {
    switch (conf) {
      case 2: return 'Yüksek Talep';
      case 1: return 'Orta Talep';
      default: return 'Düşük Talep';
    }
  }

  riskDecisionLabel(dec: number): string {
    switch (dec) {
      case 1: return 'İncele';
      case 2: return 'Bloklu';
      default: return 'Telif Güvenli';
    }
  }

  private startSseStream(): void {
    this.stopSseStream();
    this.sseSub = this.agentService.streamProgress()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (evt) => this.liveProgress.set(evt)
      });
  }

  private stopSseStream(): void {
    if (this.sseSub) {
      this.sseSub.unsubscribe();
      this.sseSub = null;
    }
  }

  private syncSimulatorFromCandidate(c: ResearchCandidateDto): void {
    this.simPriceUsd.set(Number((c.estimatedSellPriceUsd || 30).toFixed(2)));
    const defaultShip = Number(this.defaultShippingCostUsd) > 0
      ? Number(this.defaultShippingCostUsd)
      : (c.estimatedShippingUsd ?? 9.5);
    this.simShippingUsd.set(Number(defaultShip.toFixed(2)));
    const fallbackCogs = Math.round((c.estimatedSellPriceUsd || 30) * 0.35 * 100) / 100;
    this.simCogsUsd.set(Number((c.estimatedCogsUsd ?? fallbackCogs).toFixed(2)));
  }

  private applyCandidateDecision(
    candidateId: string,
    decisionStatus: CandidateDecisionStatus,
    rejectionReason: CandidateRejectionReason
  ): void {
    const patchRun = (run: ResearchRunDto): ResearchRunDto => ({
      ...run,
      candidates: run.candidates.map(item =>
        item.candidateId === candidateId
          ? { ...item, decisionStatus, rejectionReason }
          : item
      )
    });

    this.activeRun.update(run => (run ? patchRun(run) : run));
    this.runs.update(list => list.map(patchRun));
  }

  private extractErrorMessage(err: any, fallback: string): string {
    return err?.error?.error || err?.error?.message || err?.message || fallback;
  }

  private syncFormFromDna(profile: ShopDnaProfileDto): void {
    this.scheduleCadence = profile.scheduleCadence;
    this.minMarginPct = profile.minAcceptableNetMarginPct;
    this.priceMinUsd = profile.targetPriceMinUsd;
    this.priceMaxUsd = profile.targetPriceMaxUsd;
    this.defaultShippingCostUsd = profile.defaultShippingCostUsd ?? 0;
    this.defaultPackagingCostUsd = profile.defaultPackagingCostUsd ?? 1.0;
    this.returnReserveRatePct = profile.returnReserveRatePct ?? 4.0;
    this.supports3DPrint = profile.supports3DPrint;
    this.forbiddenKeywordsInput = (profile.forbiddenKeywords || []).join(', ');
    this.materialsInput = (profile.preferredMaterials || []).join(', ');
  }
}
