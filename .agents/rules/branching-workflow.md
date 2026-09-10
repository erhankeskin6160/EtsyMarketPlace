# Görev, Dal (Branch) ve Dağıtım (Deployment) Yönetimi Kuralı

Her geliştirme ve hata düzeltme görevi (task) için aşağıdaki iş akışı zorunludur:

## 1. Yeni Branch Açma
- Her task'e başlamadan önce en güncel `development` dalı çekilir:
  ```bash
  git checkout development && git pull origin development
  ```
- Task için amaca uygun yeni bir dal açılır (Örn: `feature/<task-adi>` veya `bugfix/<task-adi>`).
- Tüm geliştirmeler, testler ve düzeltmeler bu özel dalda yapılır.

## 2. Aktif Branch Üzerinden Ürün Dağıtımı (Deployment)
- Geliştirici o an **hangi branch üzerinde çalışıyorsa**, ürün derlemesi ve dağıtımı (deployment) doğrudan o aktif branch üzerinden yapılır.
- Tüm birim testler çalıştırılır (`dotnet test`).
- Standalone dağıtım paketi oluşturulur:
  ```bash
  dotnet publish SimilarProductsWinForms\SimilarProductsWinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish_vds_standalone
  ```
- Değişiklikler aktif branch'e commit edilir ve uzak repoya kendi branch adıyla push edilir:
  ```bash
  git push origin <aktif-branch>
  ```

## 3. development Dalına Doğrudan / Erken Merge YASAKTIR
- Geliştirme veya bugfix tamamlandığında, kodlar **asla** otomatik olarak `development` dalına merge edilmez.
- `development` dalı stabil kalmalıdır ve erken merge ile bugfix/ara durumlarla kirletilmemelidir.

## 4. Kullanıcı Doğrulaması Sonrası development Merge
- Kullanıcı aktif branch'ten üretilen güncel dağıtım sonucunu (`publish_vds_standalone\SimilarProductsWinForms.exe`) test edip doğrular.
- Kullanıcı sonucu onayladığında ("onaylıyorum / development'a al" talimatı verdiğinde):
  ```bash
  git checkout development
  git pull origin development
  git merge <aktif-branch>
  git push origin development
  ```
