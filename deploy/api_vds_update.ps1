<#
.SYNOPSIS
    VDS API Otomatik Güncelleyici ve Başlatıcı (EtsyMarketPlace API Development CD)
.DESCRIPTION
    GitHub Releases 'dev-latest' etiketinden en son derlenmiş EtsyMarketPlace.Api.zip paketini
    indirir, mevcut API sürecini kapatır, yedekler, dosyaları günceller ve API'yi başlatır.
    SQLite veritabanına (*.db) veya mevcut appsettings.Production.json ayarlarına KESİNLİKLE dokunmaz.
#>

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = "Stop"

$port = 5263
$exeName = "EtsyMarketPlace.Api.exe"

# Olası API dizinleri kontrol edilir
$targetDirs = @(
    "C:\Users\Administrator\Desktop\EtsyM\Api",
    "C:\Apps\EtsyMarketPlace\Api",
    "$PSScriptRoot\Api",
    "$PSScriptRoot"
)

$apiDir = ""
foreach ($d in $targetDirs) {
    if (Test-Path $d) {
        $apiDir = $d
        break
    }
}
if (-not $apiDir) {
    $apiDir = "C:\Users\Administrator\Desktop\EtsyM\Api"
}

New-Item -ItemType Directory -Force -Path $apiDir | Out-Null
New-Item -ItemType Directory -Force -Path "C:\ProgramData\EtsyMarketPlace\keys" | Out-Null

$repo = "erhankeskin6160/EtsyMarketPlace"
$downloadUrl = "https://github.com/$repo/releases/download/dev-latest/EtsyMarketPlace.Api.zip"
$tempZip = Join-Path $env:TEMP "EtsyMarketPlace.Api.zip"
$extractTemp = Join-Path $env:TEMP "EtsyMarketPlace_Api_Extract"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   🚀 EtsyMarketPlace VDS API Guncelleyici (dev-latest)" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "API Dizini: $apiDir" -ForegroundColor Gray
Write-Host "Indirme URL: $downloadUrl" -ForegroundColor Gray
Write-Host ""

try {
    # 1. API paketini indir
    Write-Host "[1/5] GitHub 'dev-latest' API paketi indiriliyor (~56 MB)..." -ForegroundColor Yellow
    if (Test-Path $tempZip) { Remove-Item $tempZip -Force }

    $downloadSuccess = $false
    $curlAvailable = (Get-Command "curl.exe" -ErrorAction SilentlyContinue) -ne $null
    if ($curlAvailable) {
        try {
            Write-Host "      [Motor: curl.exe]" -ForegroundColor Cyan
            & curl.exe -f -L --progress-bar -o $tempZip $downloadUrl
            if ((Test-Path $tempZip) -and ((Get-Item $tempZip).Length -gt 5MB)) {
                $downloadSuccess = $true
            }
        } catch { }
    }

    if (-not $downloadSuccess) {
        Write-Host "      [Motor: BITS / HttpWebRequest]" -ForegroundColor Cyan
        Start-BitsTransfer -Source $downloadUrl -Destination $tempZip -ErrorAction SilentlyContinue
        if ((Test-Path $tempZip) -and ((Get-Item $tempZip).Length -gt 5MB)) {
            $downloadSuccess = $true
        } else {
            Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing
            if ((Test-Path $tempZip) -and ((Get-Item $tempZip).Length -gt 5MB)) {
                $downloadSuccess = $true
            }
        }
    }

    if (-not $downloadSuccess -or (Get-Item $tempZip).Length -lt 5MB) {
        throw "API zip paketi indirilemedi veya dosya boyutu cok kucuk."
    }

    $zipSizeMb = [math]::Round((Get-Item $tempZip).Length / 1MB, 2)
    Write-Host "   ✅ API paketi basariyla indirildi ($zipSizeMb MB)!" -ForegroundColor Green

    # 2. Gecici klasore ac
    Write-Host "[2/5] Dosyalar aciliyor..." -ForegroundColor Cyan
    if (Test-Path $extractTemp) { Remove-Item $extractTemp -Recurse -Force }
    Expand-Archive -Path $tempZip -DestinationPath $extractTemp -Force

    # 3. Mevcut API surecini durdur (Port 5263 dinleyen veya EtsyMarketPlace.Api isimli tum surecler)
    Write-Host "[3/5] Mevcut API surecleri kapatiliyor..." -ForegroundColor Cyan
    try {
        $listeningPids = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($pidToKill in $listeningPids) {
            Write-Host "      Port $port dinleyen surec durduruluyor (PID: $pidToKill)..." -ForegroundColor Yellow
            Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
        }
    } catch { }

    try {
        $procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { 
            $_.Name -eq $exeName -or 
            ($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*EtsyMarketPlace.Api*")
        }
        foreach ($process in $procs) {
            Write-Host "      API sureci durduruluyor: $($process.Name) (PID: $($process.ProcessId))..." -ForegroundColor Yellow
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
        }
    } catch { }
    Start-Sleep -Seconds 3

    # 4. Dosyalari guncelle (appsettings.Production.json ve *.db haric)
    Write-Host "[4/5] Yeni API surumu yukleniyor..." -ForegroundColor Cyan
    $backupPath = Join-Path $env:TEMP "backup-api-$(Get-Date -Format yyyyMMdd-HHmmss)"
    if (Test-Path (Join-Path $apiDir $exeName)) {
        try {
            New-Item -ItemType Directory -Force -Path $backupPath | Out-Null
            Get-ChildItem -Path $apiDir -Exclude "*.db", "backup-*" | Copy-Item -Destination $backupPath -Recurse -Force -ErrorAction SilentlyContinue
        } catch { }
    }

    # appsettings.Production.json yedegini al
    $prodSettings = Join-Path $apiDir "appsettings.Production.json"
    $savedProdSettings = $null
    if (Test-Path $prodSettings) {
        $savedProdSettings = Get-Content $prodSettings -Raw
    }

    # Yeni dosyalari kopyala (kilitli dosya varsa 5 deneme)
    $copied = $false
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Copy-Item (Join-Path $extractTemp "*") $apiDir -Recurse -Force
            $copied = $true
            break
        } catch {
            Write-Warning "Dosya kopyalama denemesi $attempt/5 basarisiz: $_. 2 sn bekleniyor..."
            Start-Sleep -Seconds 2
        }
    }
    if (-not $copied) {
        throw "API dosyalari kopyalanamadi: $apiDir"
    }

    # Eger onceden appsettings.Production.json varsa koru, yoksa default olustur
    if ($savedProdSettings) {
        Set-Content -Path $prodSettings -Value $savedProdSettings -Encoding UTF8
    } elseif (-not (Test-Path $prodSettings)) {
        @{
            EtsyIntegration = @{ DatabasePath = "%ProgramData%\\EtsyMarketPlace\\etsy-finance.db" }
            Security = @{ ApiKey = ""; AllowedShopIds = @("523236321") }
        } | ConvertTo-Json -Depth 5 | Set-Content $prodSettings -Encoding UTF8
    }

    # Firewall kurali kontrol et
    try {
        if (-not (Get-NetFirewallRule -DisplayName "EtsyMarketPlace API $port" -ErrorAction SilentlyContinue)) {
            New-NetFirewallRule -DisplayName "EtsyMarketPlace API $port" -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
        }
    } catch { }

    # 5. API'yi baslat ve dogrula
    Write-Host "[5/5] Yeni API baslatiliyor..." -ForegroundColor Cyan
    $targetExe = Join-Path $apiDir $exeName
    $arguments = "--urls http://0.0.0.0:$port"

    Start-Process -FilePath $targetExe -ArgumentList $arguments -WorkingDirectory $apiDir -WindowStyle Hidden

    # Health kontrolu (15 sn poll)
    $healthy = $false
    for ($i = 1; $i -le 15; $i++) {
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$port/health" -TimeoutSec 2
            if ($health.status -eq "Healthy") {
                $healthy = $true
                break
            }
        } catch { }
    }

    if (-not $healthy) {
        throw "API 15 saniye icinde saglikli duruma gecmedi: $targetExe"
    }

    Write-Host "   ✅ API Durumu: $($health.status)" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "   🎉 VDS API BASARIYLA EN SON SURUME GUNCELLENDI! (Port: $port)" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "❌ GUNCELLEME HATASI: $_" -ForegroundColor Red
    exit 1
} finally {
    if (Test-Path $tempZip) { Remove-Item $tempZip -Force -ErrorAction SilentlyContinue }
    if (Test-Path $extractTemp) { Remove-Item $extractTemp -Recurse -Force -ErrorAction SilentlyContinue }
}
