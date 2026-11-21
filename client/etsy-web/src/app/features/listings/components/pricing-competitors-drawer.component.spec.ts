import { Injectable } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco, Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { PricingCompetitorsDrawerComponent } from './pricing-competitors-drawer.component';
import { CompetitorPriceSampleDto, VariationMarketBenchmarkDto } from '../../../core/services/etsy-api.service';

@Injectable()
class ListingsTrTranslocoLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      competitorsDrawer: {
        badgeOutlier: '⚠️ Medyandan Elendi',
        defaultSourceModule: 'Pazar Araştırması Modülü (Etsy API v3)'
      }
    });
  }
}

describe('PricingCompetitorsDrawerComponent', () => {
  let fixture: ComponentFixture<PricingCompetitorsDrawerComponent>;
  let component: PricingCompetitorsDrawerComponent;

  const sampleCompetitors: CompetitorPriceSampleDto[] = [
    {
      listingId: 1001,
      title: 'Thinking Alien Resin Figurine Desk Decor',
      priceUsd: 24.9,
      shopName: 'SculptLab',
      shopSales: 1200,
      hasFreeShipping: true,
      estimatedMonthlySales: 48,
      imageUrl: 'https://img.example.com/1001.jpg',
      listingUrl: 'https://www.etsy.com/listing/1001',
      shopUrl: 'https://www.etsy.com/shop/SculptLab',
      favorites: 340,
      views: 4200,
      marketScore: 88,
      isBestseller: true,
      isOutlierFiltered: false
    },
    {
      listingId: 1002,
      title: 'Thinking About Aliens Ceramic Coffee Mug',
      priceUsd: 14.3,
      shopName: 'MugFactory',
      shopSales: 400,
      hasFreeShipping: false,
      estimatedMonthlySales: 12,
      imageUrl: 'https://img.example.com/1002.jpg',
      listingUrl: 'https://www.etsy.com/listing/1002',
      shopUrl: 'https://www.etsy.com/shop/MugFactory',
      favorites: 65,
      views: 900,
      marketScore: 54,
      isBestseller: false,
      isOutlierFiltered: false
    },
    {
      listingId: 1003,
      title: 'Digital Alien Sticker PDF Download',
      priceUsd: 2.5,
      shopName: 'StickerDownloads',
      shopSales: 85,
      hasFreeShipping: false,
      estimatedMonthlySales: 5,
      imageUrl: '',
      listingUrl: 'https://www.etsy.com/listing/1003',
      shopUrl: 'https://www.etsy.com/shop/StickerDownloads',
      favorites: 10,
      views: 180,
      marketScore: 22,
      isBestseller: false,
      isOutlierFiltered: true
    }
  ];

  const sampleBenchmark: VariationMarketBenchmarkDto = {
    sourceModule: 'Pazar Araştırması Modülü (Etsy API v3)',
    competitorCount: 2,
    minPriceUsd: 14.3,
    medianPriceUsd: 19.6,
    averagePriceUsd: 19.6,
    p75PriceUsd: 24.9,
    maxPriceUsd: 24.9,
    filteredOutlierCount: 1,
    freeShippingRatioPct: 50,
    usedLiveMarketData: true,
    topCompetitors: sampleCompetitors.slice(0, 2),
    scannedCompetitors: sampleCompetitors
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PricingCompetitorsDrawerComponent],
      providers: [
        provideTransloco({
          config: {
            availableLangs: ['en', 'tr'],
            defaultLang: 'tr',
            reRenderOnLangChange: true,
            missingHandler: { logMissingKey: false }
          },
          loader: ListingsTrTranslocoLoader
        })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PricingCompetitorsDrawerComponent);
    component = fixture.componentInstance;
    component.isOpen = true;
    component.searchKeywordUsed = 'thinking alien';
    component.benchmark = sampleBenchmark;
    component.ngOnChanges({
      searchKeywordUsed: {
        currentValue: 'thinking alien',
        previousValue: '',
        firstChange: true,
        isFirstChange: () => true
      },
      benchmark: {
        currentValue: sampleBenchmark,
        previousValue: null,
        firstChange: true,
        isFirstChange: () => true
      }
    });
    fixture.detectChanges();
  });

  it('renders all scanned competitors including source module badge and Etsy links', () => {
    const el = fixture.nativeElement as HTMLElement;
    const cards = el.querySelectorAll('.comp-product-card');
    expect(cards.length).toBe(3);
    expect(el.textContent).toContain('Pazar Araştırması Modülü (Etsy API v3)');
    expect(el.textContent).toContain('Thinking Alien Resin Figurine Desk Decor');
    expect(el.textContent).toContain('Medyandan Elendi');

    const etsyLinks = Array.from(el.querySelectorAll('.btn-open-etsy')) as HTMLAnchorElement[];
    expect(etsyLinks.some(a => a.href.includes('etsy.com/listing/1001'))).toBeTrue();
  });

  it('filters competitors by free_shipping, bestseller, outliers, and search query', () => {
    component.setFilter('free_shipping');
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1001]);

    component.setFilter('bestseller');
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1001]);

    component.setFilter('outliers');
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1003]);

    component.setFilter('all');
    component.textFilter = 'MugFactory';
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1002]);
  });

  it('sorts competitors by price, monthly sales, and favorites', () => {
    component.sortBy = 'price_asc';
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1003, 1002, 1001]);

    component.sortBy = 'price_desc';
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1001, 1002, 1003]);

    component.sortBy = 'sales';
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1001, 1002, 1003]);

    component.sortBy = 'favorites';
    expect(component.filteredCompetitors.map(c => c.listingId)).toEqual([1001, 1002, 1003]);
  });

  it('emits rescanWithKeyword with trimmed query and closed event on close click', () => {
    const rescanSpy = spyOn(component.rescanWithKeyword, 'emit');
    const closeSpy = spyOn(component.closed, 'emit');

    component.rescanKeywordInput = '  thinking alien figurine  ';
    component.submitRescan();
    expect(rescanSpy).toHaveBeenCalledWith('thinking alien figurine');

    component.closePanel();
    expect(closeSpy).toHaveBeenCalledTimes(1);
  });
});
