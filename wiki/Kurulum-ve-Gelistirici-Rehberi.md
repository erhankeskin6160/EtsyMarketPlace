# 🛠️ Kurulum ve Geliştirici Rehberi

Bu rehber, EtsyMarketPlace ekosistemini yerel makinede veya VDS sunucusunda sıfırdan kurmak, derlemek ve katkıda bulunmak isteyen geliştiriciler içindir.

---

## ⚙️ Gereksinimler

- **İşletim Sistemi:** Windows 10/11 veya Windows Server 2022
- **.NET SDK:** .NET 8.0 SDK (8.0.x veya daha yenisi)
- **Node.js:** Node.js 18+ (LTS) ve npm 9+
- **Angular CLI:** `npm install -g @angular/cli@18`
- **Veritabanı:** SQLite 3 (WAL modunda çalıştırılır)

---

## 💻 Yerel Geliştirme Ortamı Kurulumu

### 1. Depoyu Klonlayın
```bash
git clone https://github.com/erhankeskin6160/EtsyMarketPlace.git
cd EtsyMarketPlace
```

### 2. Masaüstü WinForms Uygulamasını Çalıştırın
```powershell
dotnet restore
dotnet build SimilarProductsWinForms.csproj
dotnet run --project SimilarProductsWinForms.csproj
```

### 3. Web Kokpitini (Angular 18) Başlatın
```powershell
cd client/etsy-web
npm install
npm start
```
Uygulama `http://localhost:4200` adresinde açılır. Proxy yapılandırması (`proxy.conf.json`) API isteklerini yerel veya VDS sunucusuna yönlendirir.

### 4. VDS API Sunucusunu Çalıştırın
```powershell
dotnet run --project src/EtsyMarketPlace.Api/EtsyMarketPlace.Api.csproj --urls "http://localhost:5263"
```
Swagger arayüzüne `http://localhost:5263/swagger` adresinden erişebilirsiniz.

---

## 🧪 Test ve Kalite Güvencesi

Herhangi bir değişiklik commit'lenmeden önce **bütün birim testlerinin sıfır hatayla geçtiği** doğrulanmalıdır:

```powershell
# Uygulama ve API testlerinin tamamını çalıştırın (874 Test)
dotnet test

# Web frontend derleme kontrolü
npm --prefix client/etsy-web run build
```

| Test Projesi | Test Sayısı | Durum |
| :--- | :--- | :--- |
| `EtsyMarketPlace.Application.Tests` | 805 | ✅ Başarılı |
| `EtsyMarketPlace.Api.Tests` | 69 | ✅ Başarılı |
| **Toplam** | **874** | **✅ %100 Yeşil** |

---

## 🚨 Git Dallanma ve İş Akışı Kuralı (Kural 2)

Projede `development` dalına doğrudan kod yazmak kesinlikle yasaktır:

1. **Yeni Dal Açma:**
   ```bash
   git checkout development
   git pull origin development
   git checkout -b feature/<ad> # veya fix/<ad>
   ```
2. **Geliştirme & Test:**
   - Kodlamayı tamamlayın.
   - `dotnet test` ve `npm run build` ile 0 hata doğrulayın.
   - `git commit -m "feat/fix: ..."` ile commit'leyin.
3. **Merge ve Push:**
   ```bash
   git checkout development
   git pull origin development
   git merge feature/<ad>
   git push origin development
   ```
4. GitHub Actions üzerinden otomatik VDS derlemesi ve yayına alma tetiklenir.
