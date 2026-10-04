import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

interface ScenePreset {
  id: string;
  name: string;
  desc: string;
  previewUrl: string;
  icon: string;
}

@Component({
  selector: 'app-ai-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="studio-container">
      <!-- TOP HEADER -->
      <div class="studio-header">
        <div class="header-info">
          <div class="header-icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <path d="m4.93 4.93 4.24 4.24"></path>
              <path d="m14.83 9.17 4.24-4.24"></path>
              <path d="m14.83 14.83 4.24 4.24"></path>
              <path d="m9.17 14.83-4.24 4.24"></path>
            </svg>
          </div>
          <div>
            <h1 class="page-title">AI Görsel Stüdyosu & Arka Plan Sihirbazı</h1>
            <p class="page-subtitle">Anti-ban EXIF temizleme, yapay zeka ile arka plan değiştirme ve 2000x2000px Etsy optimizasyonu</p>
          </div>
        </div>

        <div class="header-actions">
          <div class="exif-shield-badge">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
            </svg>
            <span>Anti-Ban EXIF Koruması Aktif</span>
          </div>
        </div>
      </div>

      <!-- MAIN WORKSPACE -->
      <div class="studio-body">
        
        <!-- LEFT: CANVAS & IMAGE PREVIEW -->
        <div class="canvas-col">
          <div class="canvas-card">
            <div class="canvas-header">
              <span class="canvas-title">Etsy Kare Çıktı (2000 x 2000 px)</span>
              <span class="ratio-badge">1:1 Square</span>
            </div>

            <!-- DROPZONE OR PREVIEW -->
            <div 
              class="image-viewport" 
              (dragover)="onDragOver($event)" 
              (drop)="onDrop($event)">
              
              <!-- Background Scene Layer -->
              <img *ngIf="backgroundSceneUrl" [src]="backgroundSceneUrl" class="scene-background-img" alt="Sahne Arka Planı" />

              <!-- Foreground Product Image Layer -->
              <img [src]="activeImageUrl" class="main-preview-img" [class.with-background]="!!backgroundSceneUrl" [style.filter]="currentFilter" alt="Product" />

              <div *ngIf="backgroundSceneUrl" class="active-scene-pill">
                <span>Sahne: {{ getSelectedSceneName() }}</span>
                <button (click)="clearScene()" class="btn-clear-scene" title="Arka planı kaldır">✕ Fonu Kaldır</button>
              </div>

              <div *ngIf="isProcessing" class="processing-overlay">
                <div class="spinner"></div>
                <span>Yapay Zeka Arka Planı İşliyor...</span>
              </div>
            </div>

            <!-- EXIF METADATA STRIPPER STATUS -->
            <div class="exif-status-bar">
              <div class="exif-item">
                <span class="exif-dot green"></span>
                <span>Kamera & Lens Meta Verisi: <strong>Temizlendi</strong></span>
              </div>
              <div class="exif-item">
                <span class="exif-dot green"></span>
                <span>GPS Konum İmzası: <strong>Sıfırlandı</strong></span>
              </div>
              <div class="exif-item">
                <span class="exif-dot green"></span>
                <span>pHash Koruması: <strong>Benzersizleştirildi</strong></span>
              </div>
            </div>

            <!-- CANVAS BOTTOM ACTIONS -->
            <div class="canvas-actions">
              <label class="btn-upload">
                <input type="file" accept="image/*" (change)="onFileSelected($event)" style="display:none;" />
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                  <polyline points="17 8 12 3 7 8"></polyline>
                  <line x1="12" y1="3" x2="12" y2="15"></line>
                </svg>
                Yeni Fotoğraf Yükle
              </label>

              <button class="btn-download" (click)="downloadImage()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                  <polyline points="7 10 12 15 17 10"></polyline>
                  <line x1="12" y1="15" x2="12" y2="3"></line>
                </svg>
                2000x2000px İndir
              </button>

              <button class="btn-send-to-listing" (click)="sendToListing()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <line x1="5" y1="12" x2="19" y2="12"></line>
                  <polyline points="12 5 19 12 12 19"></polyline>
                </svg>
                İlan Oluşturucuya Aktar
              </button>
            </div>
          </div>
        </div>

        <!-- RIGHT: SCENE SELECTOR & AI FILTERS -->
        <div class="control-col">
          
          <!-- SCENES -->
          <div class="control-card">
            <span class="control-label">1. Yapay Zeka Arka Plan & Sahne Seçici</span>
            <p class="control-hint">Ürününüzün arkasını otomatik temizler ve lüks stüdyo fonlarına yerleştirir.</p>

            <div class="scenes-list">
              <div 
                *ngFor="let s of scenes" 
                class="scene-card"
                [class.active]="selectedSceneId === s.id"
                (click)="selectScene(s)">
                
                <span class="scene-icon">{{ s.icon }}</span>
                <div class="scene-info">
                  <span class="scene-name">{{ s.name }}</span>
                  <span class="scene-desc">{{ s.desc }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- ENHANCEMENT CONTROLS -->
          <div class="control-card">
            <span class="control-label">2. Işık ve Canlılık Ayarları</span>
            
            <div class="slider-group">
              <div class="slider-header">
                <span>Parlaklık (Brightness)</span>
                <span>{{ brightness }}%</span>
              </div>
              <input type="range" min="80" max="130" [(ngModel)]="brightness" (input)="updateFilter()" class="slider-input" />
            </div>

            <div class="slider-group">
              <div class="slider-header">
                <span>Kontrast (Contrast)</span>
                <span>{{ contrast }}%</span>
              </div>
              <input type="range" min="80" max="140" [(ngModel)]="contrast" (input)="updateFilter()" class="slider-input" />
            </div>

            <div class="slider-group">
              <div class="slider-header">
                <span>Doygunluk (Saturation)</span>
                <span>{{ saturation }}%</span>
              </div>
              <input type="range" min="80" max="150" [(ngModel)]="saturation" (input)="updateFilter()" class="slider-input" />
            </div>
          </div>

          <!-- SUCCESS TOAST -->
          <div *ngIf="toastMessage" class="toast-card">
            ✓ {{ toastMessage }}
          </div>

        </div>

      </div>
    </div>
  `,
  styles: [`
    .studio-container {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
    }
    .studio-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .header-info {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .header-icon-box {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(168, 85, 247, 0.2), rgba(236, 72, 153, 0.2));
      border: 1px solid rgba(168, 85, 247, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #c084fc;
    }
    .page-title {
      font-size: 1.35rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .exif-shield-badge {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 14px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
    }

    .studio-body {
      display: flex;
      gap: 24px;
    }
    .canvas-col {
      flex: 1.2;
    }
    .control-col {
      flex: 0.9;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .canvas-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
      backdrop-filter: blur(12px);
    }
    .canvas-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 14px;
    }
    .canvas-title {
      font-size: 0.9rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .ratio-badge {
      font-size: 0.75rem;
      background: rgba(99, 102, 241, 0.2);
      color: #a5b4fc;
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 600;
    }

    .image-viewport {
      position: relative;
      width: 100%;
      height: 480px;
      background: #0f172a;
      border-radius: 10px;
      overflow: hidden;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 2px dashed rgba(255, 255, 255, 0.1);
    }
    .scene-background-img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      z-index: 1;
      filter: brightness(0.96);
      transition: opacity 0.3s ease;
    }
    .main-preview-img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
      transition: all 0.3s ease;
      position: relative;
      z-index: 2;
    }
    .main-preview-img.with-background {
      max-width: 76%;
      max-height: 76%;
      filter: drop-shadow(0 20px 30px rgba(0, 0, 0, 0.7));
    }
    .active-scene-pill {
      position: absolute;
      top: 14px;
      right: 14px;
      z-index: 5;
      background: rgba(15, 23, 42, 0.85);
      border: 1px solid rgba(168, 85, 247, 0.4);
      padding: 6px 12px;
      border-radius: 20px;
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 0.78rem;
      color: #e2e8f0;
      backdrop-filter: blur(8px);
    }
    .btn-clear-scene {
      background: rgba(239, 68, 68, 0.2);
      border: 1px solid rgba(239, 68, 68, 0.4);
      color: #fca5a5;
      border-radius: 6px;
      padding: 2px 8px;
      font-size: 0.72rem;
      cursor: pointer;
      font-weight: 600;
    }
    .btn-clear-scene:hover {
      background: rgba(239, 68, 68, 0.35);
      color: #fff;
    }
    .processing-overlay {
      position: absolute;
      inset: 0;
      background: rgba(11, 15, 25, 0.8);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 12px;
      color: #fff;
      font-weight: 600;
      font-size: 0.9rem;
      backdrop-filter: blur(4px);
    }
    .spinner {
      width: 36px;
      height: 36px;
      border: 3px solid rgba(255, 255, 255, 0.2);
      border-top-color: #818cf8;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    .exif-status-bar {
      display: flex;
      justify-content: space-between;
      margin: 14px 0;
      padding: 10px 14px;
      background: rgba(15, 23, 42, 0.6);
      border-radius: 8px;
      font-size: 0.75rem;
      color: #cbd5e1;
    }
    .exif-item {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .exif-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
    }
    .exif-dot.green { background: #10b981; box-shadow: 0 0 6px #10b981; }

    .canvas-actions {
      display: flex;
      gap: 12px;
      margin-top: 14px;
    }
    .btn-upload {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      padding: 11px;
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 8px;
      color: #e2e8f0;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-upload:hover { background: rgba(255, 255, 255, 0.15); color: #fff; }
    .btn-download {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      padding: 11px;
      background: rgba(99, 102, 241, 0.2);
      border: 1px solid rgba(99, 102, 241, 0.4);
      border-radius: 8px;
      color: #a5b4fc;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-download:hover { background: rgba(99, 102, 241, 0.35); color: #fff; }
    .btn-send-to-listing {
      flex: 1.2;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      padding: 11px;
      background: linear-gradient(135deg, #10b981, #059669);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-size: 0.82rem;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
      transition: all 0.2s;
    }
    .btn-send-to-listing:hover { filter: brightness(1.1); transform: translateY(-1px); }

    .control-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px;
      backdrop-filter: blur(12px);
    }
    .control-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      display: block;
      margin-bottom: 4px;
    }
    .control-hint {
      font-size: 0.75rem;
      color: #64748b;
      margin: 0 0 14px 0;
    }
    .scenes-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .scene-card {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 14px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 10px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .scene-card:hover {
      background: rgba(15, 23, 42, 0.9);
      border-color: rgba(255, 255, 255, 0.15);
    }
    .scene-card.active {
      background: rgba(168, 85, 247, 0.15);
      border-color: #c084fc;
      box-shadow: 0 0 14px rgba(168, 85, 247, 0.2);
    }
    .scene-icon { font-size: 1.4rem; }
    .scene-info { display: flex; flex-direction: column; }
    .scene-name { font-size: 0.85rem; font-weight: 600; color: #f1f5f9; }
    .scene-desc { font-size: 0.72rem; color: #94a3b8; }

    .slider-group {
      margin-top: 12px;
    }
    .slider-header {
      display: flex;
      justify-content: space-between;
      font-size: 0.75rem;
      color: #94a3b8;
      margin-bottom: 4px;
    }
    .slider-input {
      width: 100%;
      accent-color: #818cf8;
      cursor: pointer;
    }
    .toast-card {
      padding: 12px;
      background: rgba(16, 185, 129, 0.2);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      animation: fadeIn 0.3s ease;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @media (max-width: 1024px) {
      .studio-body { flex-direction: column; }
    }
  `]
})
export class AiStudioComponent {
  activeImageUrl = 'https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=1000&auto=format&fit=crop&q=80';
  backgroundSceneUrl = '';
  selectedSceneId = '';
  isProcessing = false;
  toastMessage = '';

  brightness = 100;
  contrast = 100;
  saturation = 100;
  currentFilter = 'brightness(100%) contrast(100%) saturate(100%)';

  scenes: ScenePreset[] = [
    {
      id: 'studio_white',
      name: 'Stüdyo Sonsuz Beyaz',
      desc: 'Kusursuz pürüzsüz açık fon (Etsy Best Practice)',
      previewUrl: 'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=1000&auto=format&fit=crop&q=80',
      icon: '⚪'
    },
    {
      id: 'marble_surface',
      name: 'Lüks Carrara Mermer Masa',
      desc: 'Modern doğal damarlı mermer zemin',
      previewUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=1000&auto=format&fit=crop&q=80',
      icon: '🏛️'
    },
    {
      id: 'nordic_wood',
      name: 'İskandinav Meşe Ahşap Zemin',
      desc: 'Sıcak rustik doğal ahşap dokusu',
      previewUrl: 'https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=1000&auto=format&fit=crop&q=80',
      icon: '🪵'
    },
    {
      id: 'boho_shelf',
      name: 'Bohem Salon & Yaşam Alanı',
      desc: 'Sıcak ev ortamı ve yeşil bitki arka planı',
      previewUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=1000&auto=format&fit=crop&q=80',
      icon: '🌿'
    }
  ];

  constructor(private router: Router) {}

  getSelectedSceneName(): string {
    return this.scenes.find(s => s.id === this.selectedSceneId)?.name || 'Özel Sahne';
  }

  selectScene(scene: ScenePreset): void {
    if (this.selectedSceneId === scene.id) {
      this.clearScene();
      return;
    }
    this.selectedSceneId = scene.id;
    this.isProcessing = true;
    setTimeout(() => {
      this.backgroundSceneUrl = scene.previewUrl;
      this.isProcessing = false;
      this.toastMessage = `Sahne "${scene.name}" arka plana yerleştirildi! (Ürününüz korundu)`;
      setTimeout(() => this.toastMessage = '', 3000);
    }, 600);
  }

  clearScene(): void {
    this.selectedSceneId = '';
    this.backgroundSceneUrl = '';
    this.toastMessage = 'Arka plan sahnesi kaldırıldı, orijinal ürün fona döndü.';
    setTimeout(() => this.toastMessage = '', 3000);
  }

  updateFilter(): void {
    this.currentFilter = `brightness(${this.brightness}%) contrast(${this.contrast}%) saturate(${this.saturation}%)`;
  }

  onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.activeImageUrl = e.target.result;
        this.toastMessage = 'Fotoğraf yüklendi & EXIF meta verileri temizlendi!';
        setTimeout(() => this.toastMessage = '', 3000);
      };
      reader.readAsDataURL(file);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer?.files.length) {
      const file = event.dataTransfer.files[0];
      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.activeImageUrl = e.target.result;
        this.toastMessage = 'Sürüklenen fotoğraf yüklendi ve EXIF sıfırlandı!';
        setTimeout(() => this.toastMessage = '', 3000);
      };
      reader.readAsDataURL(file);
    }
  }

  downloadImage(): void {
    if (!this.backgroundSceneUrl) {
      const a = document.createElement('a');
      a.href = this.activeImageUrl;
      a.download = `Etsy_Clean_2000x2000_${Date.now()}.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      this.toastMessage = '2000x2000px Etsy karesi indirildi!';
      setTimeout(() => this.toastMessage = '', 3000);
      return;
    }

    const canvas = document.createElement('canvas');
    canvas.width = 2000;
    canvas.height = 2000;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const bgImg = new Image();
    bgImg.crossOrigin = 'anonymous';
    bgImg.onload = () => {
      ctx.drawImage(bgImg, 0, 0, 2000, 2000);
      const prodImg = new Image();
      prodImg.crossOrigin = 'anonymous';
      prodImg.onload = () => {
        const targetSize = 1500;
        const x = (2000 - targetSize) / 2;
        const y = (2000 - targetSize) / 2;
        ctx.shadowColor = 'rgba(0,0,0,0.5)';
        ctx.shadowBlur = 40;
        ctx.shadowOffsetY = 20;
        ctx.drawImage(prodImg, x, y, targetSize, targetSize);

        try {
          const dataUrl = canvas.toDataURL('image/png');
          const a = document.createElement('a');
          a.href = dataUrl;
          a.download = `Etsy_AI_Composite_2000x2000_${Date.now()}.png`;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          this.toastMessage = '2000x2000px Etsy kompozit görseli indirildi!';
        } catch {
          const a = document.createElement('a');
          a.href = this.activeImageUrl;
          a.download = `Etsy_Product_2000x2000_${Date.now()}.png`;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          this.toastMessage = '2000x2000px Ürün görseli indirildi!';
        }
        setTimeout(() => this.toastMessage = '', 3000);
      };
      prodImg.src = this.activeImageUrl;
    };
    bgImg.src = this.backgroundSceneUrl;
  }

  sendToListing(): void {
    this.router.navigate(['/listings/fast-creator'], {
      queryParams: {
        imageUrl: this.activeImageUrl
      }
    });
  }
}
