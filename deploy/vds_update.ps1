<#
.SYNOPSIS
    VDS Otomatik Güncelleyici ve Başlatıcı (EtsyMarketPlace Development CD)
.DESCRIPTION
    GitHub Releases 'dev-latest' etiketinden en son derlenmiş SimilarProductsWinForms.exe dosyasını
    indirir, mevcut açık programı kapatır, eski sürümü yedekler ve yeni sürümü başlatır.
    SQLite veritabanına (*.db) veya ayarlara KESİNLİKLE dokunmaz.
#>

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
# Eğer deploy klasörü içindeyse, ana çalışma klasörü bir üst dizindir
if ((Split-Path -Leaf $scriptDir) -eq "deploy") {
    $appDir = Split-Path -Parent $scriptDir
} else {
    $appDir = $scriptDir
}

$exeName = "SimilarProductsWinForms.exe"
$targetExe = Join-Path $appDir $exeName
$tempDownload = Join-Path $appDir "$exeName.download"
$backupDir = Join-Path $appDir "backup"

$repo = "erhankeskin6160/EtsyMarketPlace"
$downloadUrl = "https://github.com/$repo/releases/download/dev-latest/$exeName"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   🚀 EtsyMarketPlace VDS Otomatik Guncelleyici (dev-latest)" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Hedef Dizin: $appDir" -ForegroundColor Gray
Write-Host "Indirme URL: $downloadUrl" -ForegroundColor Gray
Write-Host ""

try {
    # 1. Yeni sürümü geçici dosyaya indir
    Write-Host "[1/5] GitHub 'dev-latest' surumu indiriliyor..." -ForegroundColor Yellow
    if (Test-Path $tempDownload) { Remove-Item $tempDownload -Force }

    $webClient = New-Object System.Net.WebClient
    $webClient.Headers.Add("User-Agent", "EtsyMarketPlace-VDS-Updater")
    $webClient.DownloadFile($downloadUrl, $tempDownload)

    $downloadedItem = Get-Item $tempDownload
    $fileSizeMb = [math]::Round($downloadedItem.Length / 1MB, 2)
    Write-Host "      Basariyla indirildi! Boyut: $fileSizeMb MB" -ForegroundColor Green

    if ($downloadedItem.Length -lt 10MB) {
        throw "Indirilen dosya boyutu cok kucuk ($fileSizeMb MB). Indirme hatasi olusmus olabilir!"
    }

    # 2. Çalışan mevcut programı kapat
    Write-Host "[2/5] Calisan uygulama kontrol ediliyor..." -ForegroundColor Yellow
    $runningProcesses = Get-Process -Name "SimilarProductsWinForms" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        Write-Host "      Calisan SimilarProductsWinForms tespit edildi, kapatiliyor..." -ForegroundColor Yellow
        $runningProcesses | Stop-Process -Force
        Start-Sleep -Seconds 2
    } else {
        Write-Host "      Calisan surec yok, devam ediliyor." -ForegroundColor Gray
    }

    # 3. Eski sürümü yedekle
    Write-Host "[3/5] Mevcut surum yedekleniyor..." -ForegroundColor Yellow
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir | Out-Null
    }

    if (Test-Path $targetExe) {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $backupPath = Join-Path $backupDir "SimilarProductsWinForms_$timestamp.exe"
        Copy-Item -Path $targetExe -Destination $backupPath -Force
        Write-Host "      Yedek alindi: $backupPath" -ForegroundColor Gray

        # 5'ten eski yedekleri temizle (disk dolmaması için)
        $oldBackups = Get-ChildItem -Path $backupDir -Filter "SimilarProductsWinForms_*.exe" | Sort-Object CreationTime -Descending | Select-Object -Skip 5
        foreach ($old in $oldBackups) {
            Remove-Item $old.FullName -Force -ErrorAction SilentlyContinue
        }
    }

    # 4. Yeni sürümü devreye al
    Write-Host "[4/5] Yeni surum yerlestiriliyor..." -ForegroundColor Yellow
    Move-Item -Path $tempDownload -Destination $targetExe -Force
    Write-Host "      $exeName guncellendi ($targetExe)!" -ForegroundColor Green

    # publish_vds_standalone dizini varsa orayı da anında güncelle
    $standaloneDir = Join-Path $appDir "publish_vds_standalone"
    if (Test-Path $standaloneDir) {
        $standaloneExe = Join-Path $standaloneDir $exeName
        Copy-Item -Path $targetExe -Destination $standaloneExe -Force -ErrorAction SilentlyContinue
        Write-Host "      publish_vds_standalone\$exeName guncellendi!" -ForegroundColor Green
    }

    # 5. Programı başlat
    Write-Host "[5/5] Yeni surum baslatiliyor..." -ForegroundColor Yellow
    Start-Process -FilePath $targetExe -WorkingDirectory $appDir
    
    $logFile = Join-Path $appDir "vds_update.log"
    $logMsg = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] BASARILI: $exeName dev-latest surumune basariyla guncellendi."
    Add-Content -Path $logFile -Value $logMsg -ErrorAction SilentlyContinue

    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "   ✅ VDS Basariyla Guncellendi ve Program Calistirildi!" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Start-Sleep -Seconds 2
}
catch {
    Write-Host ""
    Write-Host "❌ HATA OLUSTU: $($_.Exception.Message)" -ForegroundColor Red
    
    $logFile = Join-Path $appDir "vds_update.log"
    $logMsg = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] HATA: $($_.Exception.Message)"
    Add-Content -Path $logFile -Value $logMsg -ErrorAction SilentlyContinue

    if (Test-Path $tempDownload) { Remove-Item $tempDownload -Force -ErrorAction SilentlyContinue }
    
    # Hata durumunda eski exe varsa onu çalıştır
    if (Test-Path $targetExe) {
        Write-Host "Mevcut surumle program aciliyor..." -ForegroundColor Yellow
        Start-Process -FilePath $targetExe -WorkingDirectory $appDir
    }
    Start-Sleep -Seconds 4
}
