import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ShopResearchAgentService,
  ShopDnaProfileDto,
  ResearchRunDto,
  ResearchCandidateDto,
  ResearchScheduleCadence,
  CandidateDecisionStatus,
  CandidateRejectionReason
} from '../../core/services/shop-research-agent.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';

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
  private readonly etsyApi = inject(EtsyApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly dna = signal<ShopDnaProfileDto | null>(null);
  readonly runs = signal<ResearchRunDto[]>([]);
  readonly activeRun = signal<ResearchRunDto | null>(null);

  readonly loadingDna = signal(false);
  readonly savingDna = signal(false);
  readonly runningAgent = signal(false);
  readonly statusMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  // Editable DNA form fields
  focusHint = '';
  forbiddenKeywordsInput = '';
  materialsInput = '';
  scheduleCadence: ResearchScheduleCadence = 1;
  minMarginPct = 25;
  priceMinUsd = 15;
  priceMaxUsd = 75;
  supports3DPrint = false;

  constructor() {
    effect(() => {
      const currentShopId = this.etsyApi.activeShopId();
      if (!currentShopId) return;

      untracked(() => {
        this.dna.set(null);
        this.runs.set([]);
        this.activeRun.set(null);
        this.statusMessage.set(null);
        this.errorMessage.set(null);
        this.loadDna(false);
        this.loadRuns();
      });
    }, { allowSignalWrites: true });
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
      scheduleCadence: Number(this.scheduleCadence) as ResearchScheduleCadence
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.dna.set(updated);
          this.syncFormFromDna(updated);
          this.savingDna.set(false);
          this.errorMessage.set(null);
          this.statusMessage.set('Mağaza DNA kısıtları ve otomatik tarama takvimi kaydedildi.');
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
    this.statusMessage.set('Ajan mağaza DNA\'nızı analiz ediyor, pazar ve fırsat verilerini tarıyor...');

    this.agentService.runResearch(this.focusHint, 10)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (run) => {
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.runningAgent.set(false);
          this.activeRun.set(run);
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
          if (this.etsyApi.activeShopId() !== requestedShopId) return;
          this.runningAgent.set(false);
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
          this.statusMessage.set(`"${c.title}" onaylandı ve mağaza hafızasına eklendi.`);
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
    this.agentService.recordDecision(c.candidateId, 3, 0)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.applyCandidateDecision(c.candidateId, 3, 0);
          this.errorMessage.set(null);
          this.aiSettings.setClonedListing({
            id: c.candidateId,
            title: c.suggestedTitle || c.title,
            priceUsd: c.estimatedSellPriceUsd,
            description: c.rationale,
            tags: c.suggestedTags || [],
            materials: [],
            imageUrls: []
          });
          this.router.navigate(['/listings/fast-creator']);
        },
        error: (err) => {
          this.statusMessage.set(null);
          this.errorMessage.set(this.extractErrorMessage(err, 'Aday taslağa aktarılamadı.'));
        }
      });
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
      case 1: return 'İncele (Review)';
      case 2: return 'Bloklu';
      default: return 'Telif Güvenli';
    }
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
    this.supports3DPrint = profile.supports3DPrint;
    this.forbiddenKeywordsInput = (profile.forbiddenKeywords || []).join(', ');
    this.materialsInput = (profile.preferredMaterials || []).join(', ');
  }
}
