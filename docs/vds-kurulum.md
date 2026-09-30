# EtsyMarketPlace API - Windows VDS Kurulumu

Bu proje için başlangıç VDS kurulumu Windows üzerinde doğrudan yapılır. Bu sunucuda SQL Server, Docker veya CI/CD kurulması gerekmez. Finans verileri SQLite dosyasında tutulur.

## 1. VDS gereksinimleri

- Windows 10/11 veya Windows Server
- .NET 8 Runtime/Hosting Bundle
- Yönetici yetkisi
- En az 2 GB RAM ile düşük trafik ve tek mağaza kullanımı
- API için boş bir port (varsayılan geliştirme portu: 5263)

## 2. Yayın paketi oluşturma

Geliştirme bilgisayarında repository klasöründe PowerShell açın:

```powershell
dotnet restore .\SimilarProductsWinForms.sln
dotnet test .\SimilarProductsWinForms.sln -c Release
dotnet publish .\src\EtsyMarketPlace.Api\EtsyMarketPlace.Api.csproj -c Release -o .\publish\api
```

`publish\api` klasörünü VDS’ye örneğin şu klasöre kopyalayın:

```text
C:\Apps\EtsyMarketPlace\Api
```

## 3. VDS yapılandırması

API klasöründe `appsettings.Production.json` oluşturun. Secret değerlerini Git’e göndermeyin:

```json
{
  "EtsyIntegration": {
	"DatabasePath": "%ProgramData%\\EtsyMarketPlace\\etsy-finance.db"
  },
  "Etsy": {
	"ApiKey": "",
	"SharedSecret": "",
	"RedirectUri": "",
	"Scopes": [ "shops_r", "transactions_r", "billing_r" ]
  },
  "Security": {
	"ApiKey": "",
	"AllowedShopIds": [ "523236321" ]
  }
}
```

Production secret’larını environment variable veya güvenli sunucu yapılandırmasıyla sağlayın. API key için `X-Api-Key` header kullanılır.

## 4. İlk çalıştırma

VDS üzerinde PowerShell:

```powershell
cd C:\Apps\EtsyMarketPlace\Api
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet EtsyMarketPlace.Api.dll --urls http://127.0.0.1:5263
```

Başarılı başlangıçta SQLite dosyası otomatik oluşturulur:

```text
C:\ProgramData\EtsyMarketPlace\etsy-finance.db
C:\ProgramData\EtsyMarketPlace\keys
```

Kontrol:

```powershell
Invoke-RestMethod http://127.0.0.1:5263/health
```

## 5. Windows Firewall

İlk test için yalnızca gerekli portu açın. API’yi doğrudan internete açmadan önce API key kullanın:

```powershell
New-NetFirewallRule -DisplayName "EtsyMarketPlace API" -Direction Inbound -Protocol TCP -LocalPort 5263 -Action Allow
```

Üretimde tercihen HTTPS ve 443 portu kullanın. SQL Server portu açmayın; bu projede SQL Server kullanılmıyor.

## 6. API’yi sürekli çalıştırma

İlk aşamada Windows Task Scheduler ile `dotnet EtsyMarketPlace.Api.dll` komutunu başlatabilirsiniz. Daha ileri aşamada Windows Service veya IIS kullanılabilir.

Task Scheduler ayarları:

- Tetikleyici: Bilgisayar açıldığında
- Kullanıcı: Yönetici yetkili servis hesabı
- Program: `dotnet.exe`
- Argüman: `C:\Apps\EtsyMarketPlace\Api\EtsyMarketPlace.Api.dll --urls http://127.0.0.1:5263`
- Başlangıç klasörü: `C:\Apps\EtsyMarketPlace\Api`

## 7. Finans verisi senkronizasyonu

OAuth token kaydedildikten sonra Swagger’dan şu endpoint çağrılır:

```text
POST /api/etsy/sync
```

Body:

```json
{
  "shopId": "523236321",
  "startDate": "2026-09-01T00:00:00Z",
  "endDate": "2026-09-30T23:59:59Z"
}
```

Bu endpoint Etsy’den okuma yapar ve sonucu SQLite’a yazar. Etsy listing, ürün, fiyat veya mağaza ayarı değiştirmez.

Rapor endpoint’leri:

```text
GET /api/etsy/banking/payouts?shopId=523236321
GET /api/etsy/financial/performance?shopId=523236321&period=this_month
GET /api/etsy/orders/unfulfilled-cost-alerts?shopId=523236321
GET /api/etsy/shop/daily-brief?shopId=523236321
```

## 8. SQLite yedekleme

API durdurulduktan sonra veritabanı dosyasını yedekleyin:

```powershell
New-Item -ItemType Directory -Force C:\Backups\EtsyMarketPlace | Out-Null
Copy-Item C:\ProgramData\EtsyMarketPlace\etsy-finance.db C:\Backups\EtsyMarketPlace\etsy-finance-$(Get-Date -Format yyyyMMdd-HHmm).db
```

Data Protection anahtarları da tokenların çözülebilmesi için yedeklenmelidir:

```powershell
Copy-Item C:\ProgramData\EtsyMarketPlace\keys C:\Backups\EtsyMarketPlace\keys -Recurse -Force
```

SQLite dosyası ve `keys` klasörü aynı yedeğe ait olmalıdır. Tokenlar şifreli olsa da bu klasörleri internete açmayın.

## 9. Bu kurulumda kullanılmayanlar

İlk VDS kurulumu için aşağıdakiler gerekli değildir:

- SQL Server
- Docker
- Kubernetes
- GitHub Actions/CI/CD
- Redis
- RabbitMQ

Trafik veya kaynak ihtiyacı büyürse API, veritabanı ve deployment ayrı planlanabilir.
