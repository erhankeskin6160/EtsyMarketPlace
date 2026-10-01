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
C:\Users\Administrator\Desktop\EtsyM\Api
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
cd C:\Users\Administrator\Desktop\EtsyM\Api
$env:ASPNETCORE_ENVIRONMENT = "Production"
Start-Process -FilePath .\EtsyMarketPlace.Api.exe -ArgumentList "--urls http://0.0.0.0:5263" -WorkingDirectory (Get-Location)
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

Self-contained publish kullanılıyorsa `dotnet` komutu gerekmez; doğrudan
`EtsyMarketPlace.Api.exe` çalıştırılır.

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
- Argüman: `C:\Users\Administrator\Desktop\EtsyM\Api\EtsyMarketPlace.Api.dll --urls http://127.0.0.1:5263`
- Başlangıç klasörü: `C:\Users\Administrator\Desktop\EtsyM\Api`

## 7. Uzaktan curl ile API kontrolü

VDS’nin dış IP adresini `VDS_IP` yerine yazın. API key etkinse her komuta
`-H "X-Api-Key: API_KEY"` ekleyin.

Health kontrolü:

```powershell
curl.exe -i "http://VDS_IP:5263/health"
```

Masaüstünden token aktarımı için örnek:

```powershell
curl.exe -i -X POST "http://VDS_IP:5263/api/etsy/token" `
  -H "Content-Type: application/json" `
  -H "X-Api-Key: API_KEY" `
  --data-raw '{"shopId":"523236321","accessToken":"ACCESS_TOKEN","refreshToken":"REFRESH_TOKEN","expiresAt":"2026-12-31T23:59:59Z","tokenType":"Bearer"}'
```

Tokenın API tarafından gerçekten okunabildiğini doğrulama:

```powershell
curl.exe -i "http://VDS_IP:5263/api/etsy/token/status?shopId=523236321" -H "X-Api-Key: API_KEY"
```

Beklenen yanıtta `exists: true` ve geçerli bir `expiresAt` bulunur. Token
değerleri status yanıtında dönmez.

Senkronizasyon:

```powershell
curl.exe -i -X POST "http://VDS_IP:5263/api/etsy/sync" `
  -H "Content-Type: application/json" `
  -H "X-Api-Key: API_KEY" `
  --data-raw '{"shopId":"523236321","startDate":"2026-09-01T00:00:00Z","endDate":"2026-09-30T23:59:59Z"}'
```

PowerShell’de `curl` yerine özellikle `curl.exe` kullanın; böylece PowerShell
alias davranışından kaçınılır.

## 8. Finans verisi senkronizasyonu

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
GET /api/etsy/financial/analysis?shopId=523236321&startDate=2026-09-01T00:00:00Z&endDate=2026-09-30T23:59:59Z
```

Finansal analiz endpoint’i senkronize SQLite verilerini kullanarak kâr marjı,
gider oranı, reklam yükü, eksik sipariş maliyetleri ve uygulanabilir öneriler
üretir. Masaüstünde **Finansal AI Analiz** menüsünden aynı sonucu görüntüleyebilirsiniz.

MCP istemcileri için analiz aracı:

```text
analyze_etsy_financials
```

Araç `shopId`, isteğe bağlı `startDate` ve `endDate` alır. Dönen içerik token
ve OAuth sırrı içermez; LLM/Gemini bağlamına güvenli finansal özet olarak
aktarılabilir.

## 9. SQLite yedekleme

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

## 10. Bu kurulumda kullanılmayanlar

İlk VDS kurulumu için aşağıdakiler gerekli değildir:

- SQL Server
- Docker
- Kubernetes
- GitHub Actions/CI/CD
- Redis
- RabbitMQ

Trafik veya kaynak ihtiyacı büyürse API, veritabanı ve deployment ayrı planlanabilir.

## 11. GitHub Actions ile otomatik API deployment

`.github/workflows/deploy-api-vds.yml` workflow’u `development` dalına API ile
ilgili bir commit pushlandığında otomatik olarak:

1. .NET 8 restore ve test çalıştırır.
2. API’yi self-contained `win-x64` olarak publish eder.
3. Publish paketini VDS’deki self-hosted GitHub Actions runner’a aktarır.
4. Mevcut API sürecini durdurup yeni sürümü `C:\Users\Administrator\Desktop\EtsyM\Api`
   klasörüne kurar.
5. `http://127.0.0.1:5263/health` ile deployment’ı doğrular.

### VDS’de bir kez yapılacak runner kurulumu

GitHub deposunda **Settings > Actions > Runners > New self-hosted runner**
adımlarından Windows runner komutlarını alın. VDS’de runner’ı şu klasöre
kurun:

```powershell
New-Item -ItemType Directory -Force C:\actions-runner | Out-Null
Set-Location C:\actions-runner
```

GitHub’ın verdiği `config.cmd` komutunda runner label olarak `vds` kullanın.
Workflow bu label’ı şu satırla seçer:

```yaml
runs-on: [self-hosted, Windows, vds]
```

Runner’ı Windows servisi olarak kurun ve servisin aşağıdaki klasörlere yazma
yetkisi olduğundan emin olun:

```text
C:\Users\Administrator\Desktop\EtsyM\Api
C:\ProgramData\EtsyMarketPlace
```

Runner kurulumu tamamlandıktan sonra bilgisayardan yalnızca şunu yapmanız
yeterlidir:

```powershell
git add .
git commit -m "feat: API güncellemesi"
git push origin development
```

GitHub Actions başarısız olursa VDS’de çalışan mevcut API sürümü korunur;
başarılı deployment sonrasında workflow health kontrolünü geçmeden tamamlanmaz.
