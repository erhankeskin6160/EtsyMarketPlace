# Antigravity Proje Kuralları & Çalışma Prensipleri

## 🚨 Git Dallanma (Branch) ve İş Akışı Kuralı (KESİNLİKLE ZORUNLU)

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
