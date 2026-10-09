# Antigravity Proje Kuralları & Çalışma Prensipleri

## 🚨 1. ZORUNLU İLK ADIM: SİSTEM MİMARİSİ VE KODLAMA EL KİTABINI OKUMA
**HERHANGİ BİR KODLAMA YAPMADAN VEYA DOSYA DEĞİŞTİRMEDEN ÖNCE:**
Tüm yapay zeka modelleri ve geliştiriciler, projenin kök dizininde bulunan **`SYSTEM_ARCHITECTURE_AND_AGENT_HANDBOOK.md`** dosyasını **mutlaka okumakla yükümlüdür**.
- Masaüstü (WinForms), Web (Angular 18), VDS API ve Etsy API arasındaki veri akışı, mimari kurallar ve iş mantığı bu el kitabında tanımlanmıştır.
- Bu el kitabındaki mimariye, 13 etiket kuralına, anti-ban önlemlerine ve sıfır mock ilkesine aykırı kod yazılamaz.

## 🚨 2. Git Dallanma (Branch) ve İş Akışı Kuralı (KESİNLİKLE ZORUNLU)

Her geliştirme görevi, hata düzeltmesi veya yeni özellik için aşağıdaki akış **İSTİSNASIZ** uygulanacaktır:

1. **Yeni Branch Açma:**
   - Asla doğrudan `development` üzerinde kod yazılmaz!
   - `git checkout development`
   - `git pull origin development`
   - `git checkout -b feature/<kisa-ad>` veya `git checkout -b fix/<kisa-ad>`
2. **Feature/Fix Dalında Geliştirme ve Test:**
   - Tüm kodlama, frontend tasarımları ve testler bu özel dalda yapılır.
   - `dotnet build SimilarProductsWinForms.csproj` ve `dotnet test` ile 0 hata doğrulanır.
   - `git add -A` ve `git commit -m "feat/fix: ..."` ile commit'lenir.
3. **development Dalına Merge Etme:**
   - `git checkout development`
   - `git pull origin development`
   - `git merge <feature-veya-fix-dali>`
4. **development Dalına Push:**
   - `git push origin development`
   - GitHub Actions üzerinden VDS otomatik derlemesi tetiklenir.
   - Sonuç kullanıcıya şeffaf ve açık şekilde raporlanır.

## 🚨 3. FİNANSAL VERİ MİMARİSİ (GÜNCEL — VDS SUNUCU FİNANS MOTORU)
Web finans KPI'ları, VDS API'nin **sunucu finans motorundan** (Etsy ödeme hesabı defteri, canlı) beslenir.
Masaüstü uygulamasının gönderdiği günlük özetler ve `financial_transactions` ham satırları **web KPI
kaynağı DEĞİLDİR** (yalnız yedek/uyumluluk içindir). Parite sözleşmesi, referans değerler ve doğrulama
adımları: **`docs/finans-motoru-ve-parite.md`** (ayrıca `SYSTEM_ARCHITECTURE_AND_AGENT_HANDBOOK.md` §4 —
"Finansal Veri Akışı"). Finansla ilgili kod değiştirmeden önce bu iki belgeyi okuyun.
