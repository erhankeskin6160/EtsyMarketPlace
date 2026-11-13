import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, OnInit, SimpleChanges, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import {
  CanceledOrderCostTarget,
  CommissionAccountTarget,
  DepreciationMethod,
  ExpenseFrequency,
  FixedAssetItemDto,
  IncomeStatementResponseDto,
  IncomeStatementSectionDto,
  InventoryPeriodAdjustmentDto,
  InventoryValuationMethod,
  OPERATING_EXPENSE_CATEGORY_OPTIONS,
  OperatingExpenseEntryDto,
  ShippingPackagingCostTarget,
  ShopAccountingProfileDto,
  TaxRegimeType
} from '../../../core/models/income-statement.models';
import { EtsyApiService } from '../../../core/services/etsy-api.service';

@Component({
  selector: 'app-income-statement-tab',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './income-statement-tab.component.html',
  styleUrls: ['./income-statement-tab.component.css']
})
export class IncomeStatementTabComponent implements OnInit, OnChanges {
  private readonly etsyApi = inject(EtsyApiService);
  private readonly destroyRef = inject(DestroyRef);

  @Input() shopId = '53236321';
  @Input() period = 'this_month';
  @Input() startDate: Date | null = null;
  @Input() endDate: Date | null = null;
  @Input() isTry = true;
  @Input() reloadToken = 0;

  readonly categoryOptions = OPERATING_EXPENSE_CATEGORY_OPTIONS;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly toast = signal<string | null>(null);
  readonly response = signal<IncomeStatementResponseDto | null>(null);
  readonly activeSubTab = signal<'statement' | 'expenses' | 'assets' | 'vouchers' | 'policy'>('statement');
  readonly expandedCode = signal<string | null>(null);
  readonly previewYoungOverride = signal<boolean | null>(null);
  readonly expenseSubmitted = signal(false);
  readonly assetSubmitted = signal(false);

  readonly doc = computed(() => this.response()?.statement ?? null);
  readonly notes = computed(() => this.response()?.dataCompletenessNotes ?? []);
  readonly expenses = computed(() => this.response()?.configuredExpenses ?? []);
  readonly assets = computed(() => this.response()?.fixedAssets ?? []);
  readonly savedProfile = computed<ShopAccountingProfileDto>(() => this.doc()?.appliedProfile ?? this.defaultProfile());

  readonly youngQuotaPct = computed(() => {
    const tb = this.doc()?.taxBridge;
    if (!tb || tb.youngEntrepreneurAnnualLimitTry <= 0) return 0;
    const used = tb.youngEntrepreneurAppliedInPeriodTry + tb.youngEntrepreneurPriorUsedTry;
    return Math.min(100, Math.max(0, Math.round((used / tb.youngEntrepreneurAnnualLimitTry) * 100)));
  });

  expenseForm: {
    entryDate: string;
    documentNo: string;
    category: number;
    description: string;
    amount: number;
    currency: string;
    exchangeRateToTry: number;
    isTaxDeductible: boolean;
    frequency: ExpenseFrequency;
    recurringEndDate: string;
  } = this.freshExpenseForm();

  assetForm: {
    assetName: string;
    assetAccountCode: string;
    expenseTargetCategory: number;
    acquisitionDate: string;
    acquisitionCostTry: number;
    salvageValueTry: number;
    usefulLifeYears: number;
    method: DepreciationMethod;
    isActive: boolean;
  } = this.freshAssetForm();

  profileForm: ShopAccountingProfileDto = this.defaultProfile();
  invForm: InventoryPeriodAdjustmentDto = this.defaultInventoryForm();

  private initialized = false;

  ngOnInit(): void {
    this.initialized = true;
    this.refreshStatement();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.initialized) return;
    if (changes['shopId'] || changes['period'] || changes['startDate'] || changes['endDate'] || changes['reloadToken']) {
      this.refreshStatement();
    }
  }

  refreshStatement(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.etsyApi
      .getIncomeStatement(this.period, this.startDate, this.endDate, this.previewYoungOverride(), this.shopId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.loading.set(false);
          this.response.set(res);
          this.syncFormsFromResponse(res);
        },
        error: err => {
          this.loading.set(false);
          this.errorMessage.set(err?.error?.message || err?.message || 'Resmi Gelir Tablosu yüklenemedi.');
        }
      });
  }

  toggleYoungPreview(enable: boolean): void {
    this.previewYoungOverride.set(enable);
    this.refreshStatement();
  }

  persistYoungMode(enable: boolean): void {
    const updated: ShopAccountingProfileDto = { ...this.profileForm, youngEntrepreneurModeEnabled: enable };
    this.saving.set(true);
    this.etsyApi
      .saveAccountingProfile(updated, this.shopId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.previewYoungOverride.set(null);
          this.showToast(`✅ Genç Girişimci modu kalıcı olarak ${enable ? 'AKTİF' : 'KAPALI'} kaydedildi.`);
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Profil kaydedilemedi.');
        }
      });
  }

  toggleExpand(code: string): void {
    this.expandedCode.set(this.expandedCode() === code ? null : code);
  }

  isSingleSubtotalSection(sec: IncomeStatementSectionDto): boolean {
    return sec.lines.length === 1 && sec.lines[0].isSubtotal;
  }

  submitExpense(): void {
    this.expenseSubmitted.set(true);
    if (!this.expenseForm.description || this.expenseForm.description.trim().length < 2 || !(this.expenseForm.amount > 0)) {
      return;
    }

    this.saving.set(true);
    this.etsyApi
      .createOperatingExpense(
        {
          shopId: this.shopId,
          entryDate: this.expenseForm.entryDate,
          documentNo: this.expenseForm.documentNo,
          category: Number(this.expenseForm.category),
          description: this.expenseForm.description.trim(),
          amount: Number(this.expenseForm.amount),
          currency: this.expenseForm.currency,
          exchangeRateToTry: Number(this.expenseForm.exchangeRateToTry) || 0,
          isTaxDeductible: this.expenseForm.isTaxDeductible,
          frequency: Number(this.expenseForm.frequency) as ExpenseFrequency,
          recurringEndDate: this.expenseForm.frequency === 1 && this.expenseForm.recurringEndDate
            ? this.expenseForm.recurringEndDate
            : null
        },
        this.shopId
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.expenseSubmitted.set(false);
          this.expenseForm = this.freshExpenseForm();
          this.showToast('✅ Gider/KKEG fişi kaydedildi ve Gelir Tablosu güncellendi.');
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Gider fişi kaydedilemedi.');
        }
      });
  }

  removeExpense(id: number): void {
    this.saving.set(true);
    this.etsyApi
      .deleteOperatingExpense(id, this.shopId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showToast('🗑️ Gider fişi silindi.');
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Gider fişi silinemedi.');
        }
      });
  }

  submitAsset(): void {
    this.assetSubmitted.set(true);
    if (!this.assetForm.assetName.trim() || !(this.assetForm.acquisitionCostTry > 0)) {
      return;
    }

    this.saving.set(true);
    this.etsyApi
      .createFixedAsset(
        {
          shopId: this.shopId,
          assetName: this.assetForm.assetName.trim(),
          assetAccountCode: this.assetForm.assetAccountCode,
          expenseTargetCategory: Number(this.assetForm.expenseTargetCategory),
          acquisitionDate: this.assetForm.acquisitionDate,
          acquisitionCostTry: Number(this.assetForm.acquisitionCostTry),
          salvageValueTry: Number(this.assetForm.salvageValueTry) || 0,
          usefulLifeYears: Number(this.assetForm.usefulLifeYears) || 5,
          method: Number(this.assetForm.method) as DepreciationMethod,
          isActive: true
        },
        this.shopId
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.assetSubmitted.set(false);
          this.assetForm = this.freshAssetForm();
          this.showToast('✅ Duran varlık ve amortisman kartı eklendi.');
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Sabit kıymet kaydedilemedi.');
        }
      });
  }

  removeAsset(id: number): void {
    this.saving.set(true);
    this.etsyApi
      .deleteFixedAsset(id, this.shopId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showToast('🗑️ Duran varlık kartı silindi.');
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Sabit kıymet silinemedi.');
        }
      });
  }

  saveProfile(): void {
    this.saving.set(true);
    this.etsyApi
      .saveAccountingProfile(this.profileForm, this.shopId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.previewYoungOverride.set(null);
          this.showToast('✅ TDHP muhasebe politikası ve vergi ayarları kaydedildi.');
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Muhasebe politikası kaydedilemedi.');
        }
      });
  }

  saveInventory(): void {
    this.saving.set(true);
    this.etsyApi
      .saveInventoryAdjustment({ ...this.invForm, shopId: this.shopId }, this.shopId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showToast('✅ Aralıklı envanter dönem sayımı kaydedildi.');
          this.refreshStatement();
        },
        error: err => {
          this.saving.set(false);
          this.errorMessage.set(err?.error?.message || 'Stok sayımı kaydedilemedi.');
        }
      });
  }

  previewPeriodicCogs(): number {
    const netPurch = (Number(this.invForm.grossPurchasesTry) || 0) - (Number(this.invForm.purchaseReturnsAndDiscountsTry) || 0);
    return Math.max(0, (Number(this.invForm.openingInventoryTry) || 0) + netPurch - (Number(this.invForm.closingInventoryTry) || 0));
  }

  printReport(): void {
    window.print();
  }

  exportExcelCsv(): void {
    const d = this.doc();
    if (!d) return;

    const esc = (v: string | number): string => `"${String(v ?? '').replace(/"/g, '""')}"`;
    const num = (v: number): string => v.toFixed(2).replace('.', ',');

    const rows: string[] = [];
    rows.push(['RESMİ MSUGT GELİR TABLOSU (TEKDÜZEN HESAP PLANI 600–692)', '', '', '', ''].map(esc).join(';'));
    rows.push(['Mağaza', d.shopId, 'Tarih', `${d.startDate.slice(0, 10)} — ${d.endDate.slice(0, 10)}`, `Ort. Kur: ${num(d.averageExchangeRate)}`].map(esc).join(';'));
    rows.push(['Hesap Kodu', 'MSUGT Hesap / Bölüm Adı', 'Tutar (TRY)', 'USD Karşılığı', 'Net Satış Payı (%)', 'Açıklama'].map(esc).join(';'));

    for (const sec of d.sections) {
      if (!this.isSingleSubtotalSection(sec)) {
        rows.push([sec.sectionCode, sec.title, '', '', '', ''].map(esc).join(';'));
      }
      for (const l of sec.lines) {
        const valTry = l.isSubtotal || l.isFinalResult ? l.signedAmountTry : (l.isDeduction && l.amountTry > 0 ? -l.amountTry : l.amountTry);
        const valUsd = l.isSubtotal || l.isFinalResult ? l.signedAmountUsd : (l.isDeduction && l.amountUsd > 0 ? -l.amountUsd : l.amountUsd);
        rows.push([
          l.accountCode,
          l.accountName,
          num(valTry),
          num(valUsd),
          `${l.shareOfNetSalesPct.toFixed(1)}%`,
          l.note || ''
        ].map(esc).join(';'));
      }
    }

    rows.push('');
    rows.push(['VERGİ KÖPRÜSÜ (2026 GVK / GENÇ GİRİŞİMCİ)', '', '', '', '', ''].map(esc).join(';'));
    rows.push(['690 Ticari Kâr', num(d.taxBridge.commercialProfit690Try), 'KKEG (950)', num(d.taxBridge.kkegAdditions950Try), 'Bağ-Kur (890)', num(d.taxBridge.bagkurDeductionTry)].map(esc).join(';'));
    rows.push(['Genç Girişimci İstisnası', num(d.taxBridge.youngEntrepreneurAppliedInPeriodTry), 'Vergi Matrahı', num(d.taxBridge.taxableBaseTry), '691 Vergi Karşılığı', num(d.taxBridge.estimatedTax691Try)].map(esc).join(';'));

    const blob = new Blob(['\uFEFF' + rows.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `resmi-gelir-tablosu-${d.shopId}-${d.startDate.slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    this.showToast('📊 Resmi MSUGT Gelir Tablosu Excel (CSV) dosyası indirildi.');
  }

  formatAmount(tryVal: number, usdVal: number): string {
    return this.isTry ? this.formatTry(tryVal) : this.formatUsd(usdVal);
  }

  formatTry(val: number): string {
    const n = Number(val || 0);
    const abs = Math.abs(n).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return n < 0 ? `-₺${abs}` : `₺${abs}`;
  }

  formatUsd(val: number): string {
    const n = Number(val || 0);
    const abs = Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return n < 0 ? `-$${abs}` : `$${abs}`;
  }

  private syncFormsFromResponse(res: IncomeStatementResponseDto): void {
    const p = res.statement.appliedProfile;
    if (this.previewYoungOverride() === null) {
      this.profileForm = { ...p };
    }
    const now = new Date();
    const match = res.inventoryAdjustments.find(a => a.year === now.getFullYear() && a.month === now.getMonth() + 1)
      ?? res.inventoryAdjustments[0];
    if (match) {
      this.invForm = { ...match };
    }
  }

  private freshExpenseForm() {
    const today = new Date().toISOString().slice(0, 10);
    return {
      entryDate: today,
      documentNo: '',
      category: 632,
      description: '',
      amount: 0,
      currency: 'TRY',
      exchangeRateToTry: 0,
      isTaxDeductible: true,
      frequency: 0 as ExpenseFrequency,
      recurringEndDate: ''
    };
  }

  private freshAssetForm() {
    const today = new Date().toISOString().slice(0, 10);
    return {
      assetName: '',
      assetAccountCode: '253',
      expenseTargetCategory: 632,
      acquisitionDate: today,
      acquisitionCostTry: 0,
      salvageValueTry: 0,
      usefulLifeYears: 5,
      method: 0 as DepreciationMethod,
      isActive: true
    };
  }

  private defaultProfile(): ShopAccountingProfileDto {
    return {
      shopId: this.shopId,
      commissionTarget: 0 as CommissionAccountTarget,
      shippingTarget: 0 as ShippingPackagingCostTarget,
      canceledCostTarget: 0 as CanceledOrderCostTarget,
      inventoryMethod: 0 as InventoryValuationMethod,
      taxRegime: 0 as TaxRegimeType,
      youngEntrepreneurModeEnabled: true,
      youngEntrepreneurAnnualLimitTry: 400000,
      youngEntrepreneurPriorUsedTry: 0,
      eExportExemptionRatePct: 0,
      customOrCorporateTaxRatePct: 25,
      customGvkBracketsJson: null,
      includeFxSpreadAndExchangeDifference: true
    };
  }

  private defaultInventoryForm(): InventoryPeriodAdjustmentDto {
    const now = new Date();
    return {
      shopId: this.shopId,
      year: now.getFullYear(),
      month: now.getMonth() + 1,
      openingInventoryTry: 0,
      grossPurchasesTry: 0,
      purchaseReturnsAndDiscountsTry: 0,
      closingInventoryTry: 0,
      notes: ''
    };
  }

  private showToast(msg: string): void {
    this.toast.set(msg);
    setTimeout(() => {
      if (this.toast() === msg) this.toast.set(null);
    }, 4000);
  }
}

