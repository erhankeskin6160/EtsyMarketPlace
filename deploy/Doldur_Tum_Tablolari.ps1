# ==============================================================================
# 🚀 EtsyMarketPlace - Tüm PostgreSQL Tablolarını Endpoint'ler Üzerinden Doldurma Betiği
# ==============================================================================
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5263",
    [string]$ShopId = "53236321"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "   🚀 PostgreSQL TÜM TABLOLARI DOLDURMA SÜRECİ BAŞLATILIYOR" -ForegroundColor Cyan
Write-Host "   Hedef API URL : $BaseUrl" -ForegroundColor Yellow
Write-Host "   Mağaza ID     : $ShopId" -ForegroundColor Yellow
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Veritabanı Göçü ve Temel Tohumlama (app_users, shop_connections, bank_payouts, financial_transactions, sync_states, order_addresses)
Write-Host "[1/9] 🔄 Sistem Göçü ve SQLite Tohumlaması Yapılıyor..." -ForegroundColor Cyan
try {
    $res1 = Invoke-RestMethod -Uri "$BaseUrl/api/system/migrate-postgres" -Method Post -TimeoutSec 30
    Write-Host "   ✅ Başarılı: $($res1.message)" -ForegroundColor Green
    Write-Host "      • Kullanıcı: $($res1.usersSeeded) | Mağaza: $($res1.shopConnectionsSeeded)" -ForegroundColor Gray
    Write-Host "      • Payouts: $($res1.bankPayoutsMigrated) | Transactions: $($res1.financialTransactionsMigrated)" -ForegroundColor Gray
} catch {
    Write-Host "   ⚠️ Göç uyarısı: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 2. AI Model Ayarları (shop_ai_settings)
Write-Host "`n[2/9] 🤖 AI Model Ayarları Kaydediliyor (shop_ai_settings)..." -ForegroundColor Cyan
try {
    $bodyAi = @{
        shopId = $ShopId
        settingsJson = '{"Provider":"Gemini","Model":"gemini-1.5-pro","AutoOptimize":true,"Temperature":0.7}'
    } | ConvertTo-Json
    $resAi = Invoke-RestMethod -Uri "$BaseUrl/api/settings/ai" -Method Post -Body $bodyAi -ContentType "application/json"
    Write-Host "   ✅ shop_ai_settings tablosu başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ AI ayarları kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 3. Telegram Ayarları (shop_telegram_settings)
Write-Host "`n[3/9] 📱 Telegram Bildirim Ayarları Kaydediliyor (shop_telegram_settings)..." -ForegroundColor Cyan
try {
    $bodyTg = @{
        shopId = $ShopId
        botToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz"
        chatId = "-100123456789"
        enableOrderAlerts = $true
        enableDailyReport = $true
        enableStockAlerts = $true
    } | ConvertTo-Json
    $resTg = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/settings/telegram" -Method Post -Body $bodyTg -ContentType "application/json"
    Write-Host "   ✅ shop_telegram_settings tablosu başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ Telegram ayarları kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 4. Etsy Geliştirici Kimlikleri (etsy_app_credentials)
Write-Host "`n[4/9] 🔑 Etsy Geliştirici Anahtarları Kaydediliyor (etsy_app_credentials)..." -ForegroundColor Cyan
try {
    $bodyCreds = @{
        shopId = $ShopId
        apiKey = "etsy_live_keystring_39x0293kd9"
        sharedSecret = "etsy_live_secret_482910"
        redirectUri = "http://localhost:4200/settings/etsy-api"
    } | ConvertTo-Json
    $resCreds = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/settings/credentials" -Method Post -Body $bodyCreds -ContentType "application/json"
    Write-Host "   ✅ etsy_app_credentials tablosu başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ Credentials kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 5. Kargo Taşıyıcı Oturumu (shop_carrier_sessions)
Write-Host "`n[5/9] 📦 Kargo Taşıyıcı Oturumları Kaydediliyor (shop_carrier_sessions)..." -ForegroundColor Cyan
try {
    $bodyCarrier = @{
        shopId = $ShopId
        carrierCode = "YURTICI"
        sessionToken = "eyJhY2Nlc3NfdG9rZW4iOiJ5dXJ0aWNpX3Rlc3Rfc2Vzc2lvbl85OTIifQ=="
        expiresAt = (Get-Date).AddMonths(3).ToString("o")
    } | ConvertTo-Json
    $resCarrier = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/settings/carrier-sessions" -Method Post -Body $bodyCarrier -ContentType "application/json"
    Write-Host "   ✅ shop_carrier_sessions tablosu başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ Kargo oturumu kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 6. A/B Test Deneyleri (listing_ab_tests)
Write-Host "`n[6/9] 🧪 A/B Test Deneyi Kaydediliyor (listing_ab_tests)..." -ForegroundColor Cyan
try {
    $bodyAb = @{
        listingId = "1849204910"
        shopId = $ShopId
        variantATitle = "Custom Handmade Wooden Desk Organizer - Minimalist Wood Decor"
        variantBTitle = "Solid Wood Desk Organizer with Phone Stand - Personalized Office Gift"
        status = "Active"
        startDate = (Get-Date).ToString("yyyy-MM-dd")
        variantAViews = 154
        variantAFavorites = 18
        variantAOrders = 5
        variantBViews = 182
        variantBFavorites = 24
        variantBOrders = 7
    } | ConvertTo-Json
    $resAb = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/ab-tests" -Method Post -Body $bodyAb -ContentType "application/json"
    Write-Host "   ✅ listing_ab_tests tablosu başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ A/B test kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 7. Batch Kuyruk Öğeleri (batch_queue_items)
Write-Host "`n[7/9] 📋 Toplu İş Kuyruğu Kaydediliyor (batch_queue_items)..." -ForegroundColor Cyan
try {
    $bodyQueue = @{
        items = @(
            @{
                listingId = "1849204910"
                shopId = $ShopId
                originalTitle = "Custom Handmade Wooden Desk Organizer"
                proposedTitle = "Luxury Handcrafted Oak Wood Desk Organizer"
                status = "Pending"
                optimizationType = "TitleAndTags"
            }
        )
    } | ConvertTo-Json -Depth 5
    $resQueue = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/batch-queue/enqueue" -Method Post -Body $bodyQueue -ContentType "application/json"
    Write-Host "   ✅ batch_queue_items tablosu başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ Batch kuyruğu kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 8. Ürün Takip ve Anlık Görüntü (tracking_items & tracking_snapshots)
Write-Host "`n[8/9] 📈 Rakip / İlan Takip Geçmişi Kaydediliyor (tracking_items & tracking_snapshots)..." -ForegroundColor Cyan
try {
    $bodyTrack = @{
        listingId = 1849204910
        title = "Handmade Wooden Organizer"
        price = 45.99
        currencyCode = "USD"
        views = 1250
        favorites = 135
        sales = 28
        shopName = "NaturalWoodStudio"
        score = 88.5
        dailyViewVelocity = 12.4
        dailyFavoriteVelocity = 2.1
    } | ConvertTo-Json
    $resTrack = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/tracking/capture" -Method Post -Body $bodyTrack -ContentType "application/json"
    Write-Host "   ✅ tracking_items ve tracking_snapshots tabloları başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ Takip verisi kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 9. Tam Finans, Aylık Siparişler ve Trafik Senkronizasyonu (monthly_order_summaries, order_costs, listing_traffic_daily, chart_snapshots)
Write-Host "`n[9/9] 🌐 Tam Finans ve Sipariş Senkronizasyonu (sync-all)..." -ForegroundColor Cyan
try {
    $bodySync = @{
        shopId = $ShopId
        periodStart = (Get-Date).AddMonths(-3).ToString("yyyy-MM-01")
        periodEnd = (Get-Date).ToString("yyyy-MM-dd")
        transactions = @(
            @{
                shopId = $ShopId
                referenceId = "daily-20261021"
                occurredAt = (Get-Date).ToString("o")
                grossAmount = 450.00
                netAmount = 395.50
                feesAmount = -38.50
                refundsAmount = 0.00
                currency = "USD"
                status = "posted"
                entryType = "sale"
                description = "Daily Sales Summary"
            }
        )
        payouts = @(
            @{
                shopId = $ShopId
                referenceId = "payout-20261020"
                occurredAt = (Get-Date).AddDays(-1).ToString("o")
                amount = 1250.75
                currency = "USD"
                exchangeRateToTry = 34.80
                status = "completed"
                description = "Etsy Payout to Bank"
            }
        )
        orderAlerts = @(
            @{
                shopId = $ShopId
                receiptId = "3298491028"
                orderDate = (Get-Date).AddDays(-2).ToString("o")
                currency = "USD"
                grandTotal = 85.00
                productionCost = 25.00
                shippingCost = 14.50
                statusMessage = "Tamamlandı"
            }
        )
        monthlyOrders = @(
            @{
                shopId = $ShopId
                yearMonth = "2026-10"
                orderCount = 42
                unitsSold = 56
                grossRevenue = 2890.50
                avgOrderValue = 68.82
                shippedOrders = 39
                unfulfilledOrders = 3
                currency = "USD"
            },
            @{
                shopId = $ShopId
                yearMonth = "2026-09"
                orderCount = 68
                unitsSold = 84
                grossRevenue = 4120.00
                avgOrderValue = 60.58
                shippedOrders = 68
                unfulfilledOrders = 0
                currency = "USD"
            }
        )
        trafficRecords = @(
            @{
                shopId = $ShopId
                listingId = "1849204910"
                date = (Get-Date).ToString("yyyy-MM-dd")
                title = "Handmade Wooden Desk Organizer"
                views = 350
                favorites = 42
                visits = 280
                orders = 12
                unitsSold = 14
                revenue = 750.00
                conversionRate = 4.28
                imageUrl = "https://i.etsystatic.com/sample.jpg"
                listingUrl = "https://etsy.com/listing/1849204910"
            }
        )
        charts = @(
            @{
                chartType = "bar"
                imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
                periodStart = (Get-Date).AddMonths(-1).ToString("yyyy-MM-01")
                periodEnd = (Get-Date).ToString("yyyy-MM-dd")
            }
        )
    } | ConvertTo-Json -Depth 10

    $resSync = Invoke-RestMethod -Uri "$BaseUrl/api/etsy/sync-all" -Method Post -Body $bodySync -ContentType "application/json"
    Write-Host "   ✅ monthly_order_summaries, order_costs, listing_traffic_daily ve chart_snapshots başarıyla dolduruldu!" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️ sync-all kaydedilemedi: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================================================" -ForegroundColor Green
Write-Host "   🎉 TÜM ENDPOINT'LER ÇALIŞTIRILDI VE TABLOLAR DOLDURULDU!" -ForegroundColor Green
Write-Host "   pgAdmin'e gidip F5 yaparak tüm tabloların dolduğunu görebilirsin." -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Green
Write-Host ""
