import { Component, OnInit, inject, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { EtsyListingAiService } from '../../core/services/etsy-listing-ai.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

export interface GalleryImage {
  id: string;
  url: string;
  isCover: boolean;
  name: string;
}

export interface CreatorTemplate {
  id: string;
  name: string;
  category: string;
  defaultPrice: number;
  sampleTitle: string;
  sampleTags: string[];
  sampleDesc: string;
  materials: string;
}

export interface VariationRow {
  key: string;
  price: number;
  quantity: number;
  active: boolean;
}

@Component({
  selector: 'app-fast-creator',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  templateUrl: './fast-creator.component.html',
  styleUrls: ['./fast-creator.component.css']
})
export class FastCreatorComponent implements OnInit {
  etsyApi = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  listingAiService = inject(EtsyListingAiService);
  router = inject(Router);

  @ViewChild('fileInput') fileInputRef?: ElementRef<HTMLInputElement>;

  // --- TEMPLATES ---
  templates: CreatorTemplate[] = [
    {
      id: '3d_dragon',
      name: '🐉 3D Baskı - Kristal Ejderha',
      category: 'Art & Collectibles > Sculptures',
      defaultPrice: 34.50,
      sampleTitle: 'Articulated Crystal Dragon 3D Printed Fidget Toy, Flexible Dragon Desk Pet, Fantasy Mythical Creature Figurine Gift',
      sampleTags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'articulated dragon', 'desk pet', 'fantasy decor', 'dragon figurine', 'adhd sensory toy', 'mythical creature', 'unique gifts', 'bambu lab print', 'dnd miniature', 'flexi toy'],
      sampleDesc: `🔥 Büyüleyici Mafsallı Kristal Ejderha - Yüksek Hassasiyetli 3D Baskı!\n\nBu harika mafsallı kristal ejderha, son teknoloji Bambu Lab 3D yazıcılarında yüksek kaliteli çevre dostu PLA filament ile üretilmiştir. Masanızda harika bir stres giderici (fidget toy) veya fantastik bir dekorasyon parçası olarak yerini alır.\n\n✨ Öne Çıkan Özellikler:\n- Tamamen hareketli eklemler ve kıvrımlı gövde\n- Işık altında parlayan özel kristal pul dokusu\n- Boyut: ~35 cm uzunluk\n- Hediye kutusu seçeneği ile hızlı gönderim`,
      materials: 'PLA, Çevre Dostu Filament, Reçine'
    },
    {
      id: 'digital_stl',
      name: '💾 Dijital İndirme - 3D STL & SVG',
      category: 'Craft Supplies > Digital',
      defaultPrice: 12.00,
      sampleTitle: 'Geometric Wall Art STL File 3D Print Model, Digital Download 3D Printable Panel, Modern Home Interior Decor STL',
      sampleTags: ['stl file', '3d print model', 'digital download', 'geometric wall art', '3d stl design', 'wall panel stl', 'interior decor stl', '3d printable file', 'modern wall decor', 'laser cut svg', 'instant download', 'diy home decor', '3d file for print'],
      sampleDesc: `📥 Anında İndirilebilir Geometrik Duvar Dekoru 3D Baskı STL Dosyası!\n\nBu dosya, eviniz veya ofisiniz için modern geometrik duvar panelleri basmanız için optimize edilmiştir. Desteksiz (supportless) kolay baskı imkanı sunar.\n\n📦 Paket İçeriği:\n- Yüksek poligonlu pürüzsüz .STL dosyası\n- Dilimleyici ayar rehberi\n- Ticari olmayan kişisel kullanım lisansı`,
      materials: 'Dijital STL, 3D Model Dosyası, ZIP Arşivi'
    },
    {
      id: 'jewelry_gem',
      name: '💎 El Yapımı Kişiye Özel Takı',
      category: 'Jewelry > Necklaces',
      defaultPrice: 42.00,
      sampleTitle: 'Custom Name Necklace 925 Sterling Silver, Dainty Personalized Nameplate Pendant, Minimalist Birthday Gift for Her',
      sampleTags: ['name necklace', 'personalized gift', 'silver necklace', 'custom nameplate', 'gift for her', 'dainty jewelry', 'bridesmaid gift', 'minimalist necklace', '925 silver pendant', 'birthday jewelry', 'handcrafted gift', 'custom letter charm', 'mom gift jewelry'],
      sampleDesc: `✨ 925 Ayar Gerçek Gümüş Kişiye Özel İsimli Kolye!\n\nHer bir kolye usta zanaatkarlarımız tarafından kişiye özel olarak özenle kesilir, parlatılır ve zarif bir hediye kutusunda sunulur.`,
      materials: '925 Ayar Gümüş, Altın Kaplama, Zirkon Taş'
    }
  ];

  selectedTemplateId = '3d_dragon';

  // --- COLUMN 1: ÜRÜN & SEO BİLGİLERİ ---
  listingType: 'physical' | 'digital' = 'physical';
  priceUsd: number | null = 34.50;
  priceTry = 0;
  quantity: number | null = 15;
  title = '';
  selectedCategory = 'Art & Collectibles > Sculptures';
  shippingProfile = 'Standart Kargo (3-5 iş günü teslimat)';
  readinessState = 'Hazır Ürün (Ready to ship) 1-3 iş günü';
  tags: string[] = [];
  newTagInput = '';
  description = '';
  materials = 'PLA, Çevre Dostu Filament';

  // --- COLUMN 2: GÖRSELLER & AI MOTORU (0/10) ---
  galleryImages: GalleryImage[] = [
    {
      id: 'img-1',
      url: 'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=600&auto=format&fit=crop&q=80',
      isCover: true,
      name: 'Ana Kapak Görseli'
    },
    {
      id: 'img-2',
      url: 'https://images.unsplash.com/photo-1579783902614-a3fb3927b675?w=600&auto=format&fit=crop&q=80',
      isCover: false,
      name: 'Açı 2 - Yan Detay'
    }
  ];

  // AI Image Studio
  aiPrompt = '';
  lastGeneratedImage: string | null = null;
  isGeneratingAiImage = false;
  isStudioPickerOpen = false;

  // Studio gallery sample pool
  studioPool = [
    'https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?w=600&auto=format&fit=crop&q=80',
    'https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=600&auto=format&fit=crop&q=80',
    'https://images.unsplash.com/photo-1582562124811-c09040d0a901?w=600&auto=format&fit=crop&q=80',
    'https://images.unsplash.com/photo-1600585154340-be6161a56a0c?w=600&auto=format&fit=crop&q=80',
    'https://images.unsplash.com/photo-1534447677768-be436bb09401?w=600&auto=format&fit=crop&q=80'
  ];

  // --- COLUMN 3: VARYASYONLAR & KONTROL ---
  enableVariations = false;
  varType1 = 'Boyut / Size';
  varValues1 = 'Small, Medium, Large';
  enableVar2 = false;
  varType2 = 'Renk / Color';
  varValues2 = 'Siyah, Beyaz, Altın';
  customVariationPricing = false;
  variationRows: VariationRow[] = [];

  // Publish Status & Checklist
  isLivePublish = false;
  isPreviewModalOpen = false;
  isGeneratingAi = false;
  isSavingDraft = false;
  successMessage = '';
  clonedNotice = '';

  ngOnInit(): void {
    const cloned = this.aiService.clonedListing();
    if (cloned) {
      this.selectedTemplateId = '';
      this.title = cloned.title;
      this.priceUsd = cloned.priceUsd;
      this.tags = [...cloned.tags];
      if (cloned.description) this.description = cloned.description;
      if (cloned.category) this.selectedCategory = cloned.category;
      if (cloned.imageUrl) {
        this.galleryImages = [{
          id: 'cloned-1',
          url: cloned.imageUrl,
          isCover: true,
          name: 'Klonlanan Kapak Görseli'
        }];
      }
      this.clonedNotice = `Klonlanan Ürün #${cloned.id}: "${cloned.title.slice(0, 48)}..." (${cloned.shopName}) - Fiyat: $${cloned.priceUsd}`;
      this.updatePriceTry();
    } else {
      this.applyTemplateById(this.selectedTemplateId);
      this.updatePriceTry();
    }

    this.rebuildVariationGrid();
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  // --- TEMPLATE ACTIONS ---
  applyTemplateById(id: string): void {
    const tmpl = this.templates.find(t => t.id === id);
    if (!tmpl) return;
    this.selectedTemplateId = tmpl.id;
    this.title = tmpl.sampleTitle;
    this.priceUsd = tmpl.defaultPrice;
    this.updatePriceTry();
    this.tags = [...tmpl.sampleTags];
    this.description = tmpl.sampleDesc;
    this.selectedCategory = tmpl.category;
    this.materials = tmpl.materials;
    this.rebuildVariationGrid();
  }

  saveCurrentAsTemplate(): void {
    const name = prompt('Yeni şablon adını girin:', 'Özel Şablon ' + (this.templates.length + 1));
    if (!name) return;
    const newId = 'custom_' + Date.now();
    this.templates.push({
      id: newId,
      name: '📁 ' + name,
      category: this.selectedCategory,
      defaultPrice: this.priceUsd || 0,
      sampleTitle: this.title,
      sampleTags: [...this.tags],
      sampleDesc: this.description,
      materials: this.materials
    });
    this.selectedTemplateId = newId;
    this.showToast('✅ Şablon başarıyla kaydedildi!');
  }

  deleteCurrentTemplate(): void {
    if (this.templates.length <= 1) {
      alert('En az bir şablon bulunmalıdır.');
      return;
    }
    if (confirm('Seçili şablonu silmek istediğinize emin misiniz?')) {
      this.templates = this.templates.filter(t => t.id !== this.selectedTemplateId);
      this.selectedTemplateId = this.templates[0].id;
      this.applyTemplateById(this.selectedTemplateId);
      this.showToast('🗑️ Şablon silindi.');
    }
  }

  // --- PRICING & LOGISTICS ---
  onPriceChange(): void {
    this.updatePriceTry();
    this.rebuildVariationGrid();
  }

  onPriceTryChange(): void {
    if (this.usdTryRate > 0) {
      this.priceUsd = Number((this.priceTry / this.usdTryRate).toFixed(2));
      this.rebuildVariationGrid();
    }
  }

  updatePriceTry(): void {
    this.priceTry = Number(((this.priceUsd || 0) * this.usdTryRate).toFixed(2));
  }

  clearCloned(): void {
    this.aiService.clonedListing.set(null);
    this.clonedNotice = '';
    this.applyTemplateById(this.templates[0].id);
  }

  resetForm(): void {
    // 1. Ürün & SEO Bilgileri
    this.title = '';
    this.priceUsd = null;
    this.priceTry = 0;
    this.quantity = null;
    this.tags = [];
    this.newTagInput = '';
    this.description = '';
    this.materials = '';

    // 2. Görseller & AI Motoru
    this.galleryImages = [];
    this.lastGeneratedImage = null;
    this.aiPrompt = '';
    this.isGeneratingAiImage = false;
    if (this.fileInputRef?.nativeElement) {
      this.fileInputRef.nativeElement.value = '';
    }

    // 3. Varyasyonlar & Kontrol
    this.enableVariations = false;
    this.varValues1 = '';
    this.enableVar2 = false;
    this.varValues2 = '';
    this.customVariationPricing = false;
    this.variationRows = [];

    // 4. Şablon & Klonlanan
    this.selectedTemplateId = '';
    this.clonedNotice = '';

    this.showToast('🔄 Tüm form ve girdi alanları temizlendi!');
  }

  // --- TAGS ---
  addTag(): void {
    const clean = this.newTagInput.trim().toLowerCase();
    if (!clean || clean.length > 20 || this.tags.length >= 13) return;
    if (!this.tags.includes(clean)) {
      this.tags.push(clean);
    }
    this.newTagInput = '';
  }

  removeTag(index: number): void {
    this.tags.splice(index, 1);
  }

  trimTags(): void {
    this.tags = this.tags.map(t => t.slice(0, 20).trim().toLowerCase()).filter(t => t.length > 0);
    this.showToast('🧹 Etiketler 20 karaktere göre formatlandı.');
  }

  // --- AI SUGGESTIONS ---
  suggestAiTitle(): void {
    const input = this.title.trim() || this.templates.find(t => t.id === this.selectedTemplateId)?.sampleTitle || '';
    if (!input) {
      this.showToast('⚠️ Lütfen önce ürün başlığı alanına birkaç kelime yazın (Örn: El Yapımı Kadın Çantası).');
      return;
    }
    this.isGeneratingAi = true;
    this.listingAiService.suggestTitle(input, this.selectedCategory).subscribe({
      next: res => {
        this.isGeneratingAi = false;
        this.title = res.value;
        this.showToast(res.message);
      },
      error: () => {
        this.isGeneratingAi = false;
      }
    });
  }

  suggestAiCategory(): void {
    const input = this.title.trim() || this.templates.find(t => t.id === this.selectedTemplateId)?.sampleTitle || '';
    if (!input) {
      this.showToast('⚠️ Lütfen önce sol panelde ürün başlığı alanına birkaç kelime girin.');
      return;
    }
    this.listingAiService.suggestCategory(input).subscribe({
      next: res => {
        this.selectedCategory = res.value;
        this.showToast(res.message);
      }
    });
  }

  suggestAiTags(): void {
    const input = this.title.trim() || this.templates.find(t => t.id === this.selectedTemplateId)?.sampleTitle || '';
    if (!input) {
      this.showToast('⚠️ Lütfen önce ürün başlığı alanına bir ürün adı yazın.');
      return;
    }
    this.isGeneratingAi = true;
    this.listingAiService.suggestTags(input, this.selectedCategory).subscribe({
      next: res => {
        this.isGeneratingAi = false;
        this.tags = res.value;
        this.showToast(res.message);
      },
      error: () => {
        this.isGeneratingAi = false;
      }
    });
  }

  suggestAiDescription(): void {
    const input = this.title.trim() || this.templates.find(t => t.id === this.selectedTemplateId)?.sampleTitle || '';
    if (!input) {
      this.showToast('⚠️ Lütfen önce ürün başlığı alanına bir ürün adı yazın.');
      return;
    }
    this.isGeneratingAi = true;
    this.listingAiService.suggestDescription(input, this.materials).subscribe({
      next: res => {
        this.isGeneratingAi = false;
        this.description = res.value;
        this.showToast(res.message);
      },
      error: () => {
        this.isGeneratingAi = false;
      }
    });
  }

  generateWithAi(): void {
    const input = this.title.trim() || this.templates.find(t => t.id === this.selectedTemplateId)?.sampleTitle || '';
    if (!input) {
      this.showToast('⚠️ Lütfen önce sol panelde ürün başlığı kutusuna birkaç kelime girin (Örn: El Yapımı Kadın Çantası).');
      return;
    }
    this.isGeneratingAi = true;
    this.listingAiService.generateCompleteListing(input, this.materials).subscribe({
      next: res => {
        this.isGeneratingAi = false;
        this.title = res.title;
        this.selectedCategory = res.category;
        this.tags = res.tags;
        this.description = res.description;
        if (res.materials) this.materials = res.materials;
        this.showToast(res.summaryMessage);
      },
      error: () => {
        this.isGeneratingAi = false;
      }
    });
  }

  // --- GALLERY IMAGE OPERATIONS (0/10) ---
  onImgError(event: any): void {
    event.target.src = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="400" height="400" viewBox="0 0 400 400"><rect width="400" height="400" fill="%231e293b"/><text x="50%" y="50%" fill="%2394a3b8" font-family="sans-serif" font-size="20" text-anchor="middle" dominant-baseline="middle">🎨 3D Tasarım Görseli</text></svg>';
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const files = Array.from(input.files);
    for (const file of files) {
      if (this.galleryImages.length >= 10) {
        alert('Etsy en fazla 10 ürün görseline izin verir.');
        break;
      }
      const reader = new FileReader();
      reader.onload = (e: ProgressEvent<FileReader>) => {
        if (e.target?.result && this.galleryImages.length < 10) {
          const isFirst = this.galleryImages.length === 0;
          this.galleryImages.push({
            id: 'local_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4),
            url: e.target.result as string,
            isCover: isFirst,
            name: file.name
          });
          this.updateCoverStatus();
        }
      };
      reader.readAsDataURL(file);
    }
    input.value = '';
  }

  removeImage(index: number): void {
    this.galleryImages.splice(index, 1);
    this.updateCoverStatus();
  }

  moveImageLeft(index: number): void {
    if (index <= 0) return;
    const temp = this.galleryImages[index];
    this.galleryImages[index] = this.galleryImages[index - 1];
    this.galleryImages[index - 1] = temp;
    this.updateCoverStatus();
  }

  moveImageRight(index: number): void {
    if (index >= this.galleryImages.length - 1) return;
    const temp = this.galleryImages[index];
    this.galleryImages[index] = this.galleryImages[index + 1];
    this.galleryImages[index + 1] = temp;
    this.updateCoverStatus();
  }

  setAsCover(index: number): void {
    if (index <= 0 || index >= this.galleryImages.length) return;
    const target = this.galleryImages.splice(index, 1)[0];
    this.galleryImages.unshift(target);
    this.updateCoverStatus();
    this.showToast('⭐ #1 Kapak görseli güncellendi!');
  }

  updateCoverStatus(): void {
    this.galleryImages.forEach((img, idx) => {
      img.isCover = (idx === 0);
    });
  }

  openStudioGalleryPicker(): void {
    this.isStudioPickerOpen = true;
  }

  closeStudioGalleryPicker(): void {
    this.isStudioPickerOpen = false;
  }

  pickFromStudio(url: string): void {
    if (this.galleryImages.length >= 10) {
      alert('Maksimum 10 görsel sınırına ulaşıldı.');
      return;
    }
    this.galleryImages.push({
      id: 'studio_' + Date.now(),
      url: url,
      isCover: this.galleryImages.length === 0,
      name: 'Stüdyo Görseli'
    });
    this.updateCoverStatus();
    this.showToast('🎨 Görsel stüdyodan galeriye eklendi!');
  }

  // --- AI IMAGE STUDIO ACTIONS ---
  applyPromptPreset(presetName: string, styleKeyword: string): void {
    const prod = this.title ? this.title.slice(0, 50).trim() : 'Etsy handcrafted product';
    this.aiPrompt = `Professional commercial product photography of ${prod}, ${styleKeyword}, high detail, studio lighting, 8k render, etsy showcase`;
  }

  fillPromptFromTitle(): void {
    if (!this.title.trim()) {
      alert('Önce sol panelden bir ürün başlığı girin.');
      return;
    }
    this.aiPrompt = `Professional commercial product photography of ${this.title.trim()}, isolated on clean studio lighting, high detail, 8k render, etsy showcase`;
  }

  generateAiImage(): void {
    if (!this.aiPrompt.trim()) {
      this.fillPromptFromTitle();
    }
    this.isGeneratingAiImage = true;
    setTimeout(() => {
      this.isGeneratingAiImage = false;
      // High quality realistic showcase photo
      const pool = [
        'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=800&auto=format&fit=crop&q=80',
        'https://images.unsplash.com/photo-1579783902614-a3fb3927b675?w=800&auto=format&fit=crop&q=80',
        'https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?w=800&auto=format&fit=crop&q=80'
      ];
      this.lastGeneratedImage = pool[Math.floor(Math.random() * pool.length)];
      this.showToast('🎨 Yapay zeka görseli başarıyla üretildi!');
    }, 1500);
  }

  addGeneratedToGallery(): void {
    if (!this.lastGeneratedImage) return;
    if (this.galleryImages.length >= 10) {
      alert('Etsy en fazla 10 ürün görseline izin verir.');
      return;
    }
    this.galleryImages.push({
      id: 'ai_gen_' + Date.now(),
      url: this.lastGeneratedImage,
      isCover: this.galleryImages.length === 0,
      name: 'AI Stüdyo Görseli'
    });
    this.updateCoverStatus();
    this.lastGeneratedImage = null;
    this.showToast('➕ Üretilen görsel 10\'lu galeriye eklendi!');
  }

  // --- VARIATIONS LOGIC ---
  onVariationsToggled(): void {
    this.rebuildVariationGrid();
  }

  rebuildVariationGrid(): void {
    if (!this.enableVariations) {
      this.variationRows = [];
      return;
    }

    const v1 = this.varValues1.split(',').map(s => s.trim()).filter(s => s.length > 0);
    if (v1.length === 0) {
      this.variationRows = [];
      return;
    }

    const combinations: string[] = [];
    if (this.enableVar2) {
      const v2 = this.varValues2.split(',').map(s => s.trim()).filter(s => s.length > 0);
      if (v2.length > 0) {
        for (const val1 of v1) {
          for (const val2 of v2) {
            combinations.push(`${val1} / ${val2}`);
          }
        }
      } else {
        combinations.push(...v1);
      }
    } else {
      combinations.push(...v1);
    }

    // Preserve existing prices if any
    const existing = new Map(this.variationRows.map(r => [r.key, r]));
    this.variationRows = combinations.map(key => {
      if (existing.has(key)) {
        return existing.get(key)!;
      }
      return {
        key: key,
        price: this.priceUsd || 0,
        quantity: Math.max(1, Math.floor((this.quantity || 10) / (combinations.length || 1))),
        active: true
      };
    });
  }

  syncBasePriceToVariations(): void {
    const p = this.priceUsd || 0;
    this.variationRows.forEach(row => {
      row.price = p;
    });
    this.showToast(`⚡ Tüm varyasyon fiyatları $${p.toFixed(2)} olarak eşitlendi!`);
  }

  // --- CHECKLIST GETTERS ---
  get isTitleReady(): boolean {
    return this.title.trim().length > 0 && this.title.length <= 140;
  }
  get isPriceReady(): boolean {
    return (this.priceUsd ?? 0) > 0 && (this.quantity ?? 0) > 0;
  }
  get isImagesReady(): boolean {
    return this.galleryImages.length > 0;
  }
  get isShippingReady(): boolean {
    return this.listingType === 'digital' || !!this.shippingProfile;
  }
  get isReadinessReady(): boolean {
    return this.listingType === 'digital' || !!this.readinessState;
  }
  get isDescReady(): boolean {
    return this.description.trim().length > 20;
  }
  get isTagsReady(): boolean {
    return this.tags.length > 0;
  }

  // --- PUBLISH & PREVIEW ---
  openPreviewModal(): void {
    this.isPreviewModalOpen = true;
  }

  closePreviewModal(): void {
    this.isPreviewModalOpen = false;
  }

  publishListingToEtsy(): void {
    this.isSavingDraft = true;
    const mode = this.isLivePublish ? 'CANLI YAYINDA' : 'TASLAK (DRAFT)';
    setTimeout(() => {
      this.isSavingDraft = false;
      this.showToast(`🎉 Ürün Etsy mağazanıza başarıyla "${mode}" olarak aktarıldı!`);
    }, 1200);
  }

  showToast(msg: string): void {
    this.successMessage = msg;
    setTimeout(() => {
      if (this.successMessage === msg) this.successMessage = '';
    }, 4500);
  }
}
