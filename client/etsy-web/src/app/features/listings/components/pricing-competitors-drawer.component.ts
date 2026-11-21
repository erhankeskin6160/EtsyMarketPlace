import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, provideTranslocoScope } from '@jsverse/transloco';
import { CompetitorPriceSampleDto, VariationMarketBenchmarkDto } from '../../../core/services/etsy-api.service';

export type CompetitorFilterMode = 'all' | 'free_shipping' | 'bestseller' | 'outliers';
export type CompetitorSortMode = 'score' | 'price_asc' | 'price_desc' | 'sales' | 'favorites';

@Component({
  selector: 'app-pricing-competitors-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('listings')],
  templateUrl: './pricing-competitors-drawer.component.html',
  styleUrls: ['./pricing-competitors-drawer.component.css']
})
export class PricingCompetitorsDrawerComponent implements OnChanges {
  @Input() isOpen = false;
  @Input() isLoading = false;
  @Input() searchKeywordUsed = '';
  @Input() benchmark: VariationMarketBenchmarkDto | null = null;

  @Output() closed = new EventEmitter<void>();
  @Output() rescanWithKeyword = new EventEmitter<string>();

  rescanKeywordInput = '';
  filterMode: CompetitorFilterMode = 'all';
  sortBy: CompetitorSortMode = 'score';
  textFilter = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['searchKeywordUsed']) {
      this.rescanKeywordInput = (this.searchKeywordUsed || '').trim();
    }
    if (changes['benchmark']) {
      if (this.filterMode === 'outliers' && this.outlierCount === 0) {
        this.filterMode = 'all';
      }
    }
  }

  get allCompetitors(): CompetitorPriceSampleDto[] {
    if (!this.benchmark) return [];
    if (this.benchmark.scannedCompetitors && this.benchmark.scannedCompetitors.length > 0) {
      return this.benchmark.scannedCompetitors;
    }
    return this.benchmark.topCompetitors ?? [];
  }

  get outlierCount(): number {
    return this.allCompetitors.filter(c => !!c.isOutlierFiltered).length;
  }

  get freeShippingCount(): number {
    return this.allCompetitors.filter(c => c.hasFreeShipping).length;
  }

  get bestsellerCount(): number {
    return this.allCompetitors.filter(c => !!c.isBestseller).length;
  }

  get filteredCompetitors(): CompetitorPriceSampleDto[] {
    const query = (this.textFilter || '').trim().toLowerCase();
    const list = this.allCompetitors.filter(item => {
      if (this.filterMode === 'free_shipping' && !item.hasFreeShipping) return false;
      if (this.filterMode === 'bestseller' && !item.isBestseller) return false;
      if (this.filterMode === 'outliers' && !item.isOutlierFiltered) return false;
      if (query) {
        const title = (item.title || '').toLowerCase();
        const shop = (item.shopName || '').toLowerCase();
        if (!title.includes(query) && !shop.includes(query)) return false;
      }
      return true;
    });

    const sorted = [...list];
    switch (this.sortBy) {
      case 'price_asc':
        sorted.sort((a, b) => a.priceUsd - b.priceUsd);
        break;
      case 'price_desc':
        sorted.sort((a, b) => b.priceUsd - a.priceUsd);
        break;
      case 'sales':
        sorted.sort((a, b) => (b.estimatedMonthlySales || 0) - (a.estimatedMonthlySales || 0));
        break;
      case 'favorites':
        sorted.sort((a, b) => (b.favorites || 0) - (a.favorites || 0));
        break;
      case 'score':
      default:
        sorted.sort((a, b) => {
          const outlierDiff = Number(!!a.isOutlierFiltered) - Number(!!b.isOutlierFiltered);
          if (outlierDiff !== 0) return outlierDiff;
          return (b.marketScore || 0) - (a.marketScore || 0) ||
            (b.estimatedMonthlySales || 0) - (a.estimatedMonthlySales || 0);
        });
        break;
    }
    return sorted;
  }

  setFilter(mode: CompetitorFilterMode): void {
    this.filterMode = mode;
  }

  submitRescan(): void {
    const cleaned = (this.rescanKeywordInput || '').trim();
    if (!cleaned || this.isLoading) return;
    this.rescanWithKeyword.emit(cleaned);
  }

  closePanel(): void {
    this.closed.emit();
  }

  getEtsyUrl(comp: CompetitorPriceSampleDto): string {
    if (comp.listingUrl) return comp.listingUrl;
    return comp.listingId > 0 ? `https://www.etsy.com/listing/${comp.listingId}` : '';
  }

  trackByListingId(idx: number, comp: CompetitorPriceSampleDto): number | string {
    return comp.listingId > 0 ? comp.listingId : `${comp.title}_${idx}`;
  }
}
