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
    # 0. Akıllı Sürüm Kontrolü (Gereksiz 96 MB indirmeyi engeller)
    Write-Host "[0/5] Surum kontrol ediliyor..." -ForegroundColor Cyan
    $remoteBytes = 0
    try {
        $headReq = [System.Net.HttpWebRequest]::Create($downloadUrl)
        $headReq.Method = "HEAD"
        $headReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) EtsyMarketPlace-VDS-Updater"
        $headReq.Timeout = 12000
        $headResp = $headReq.GetResponse()
        $remoteBytes = $headResp.ContentLength
        $headResp.Close()
    } catch { }

    if ((Test-Path $targetExe) -and ($remoteBytes -gt 10MB)) {
        $localBytes = (Get-Item $targetExe).Length
        if ($localBytes -eq $remoteBytes) {
            Write-Host "   ✅ Programiniz zaten dev-latest son surumunde ($([math]::Round($localBytes/1MB, 2)) MB)!" -ForegroundColor Green
            Write-Host "   ⚡ Yeniden indirme gerekmiyor, uygulama baslatiliyor..." -ForegroundColor Green
            
            $running = Get-Process -Name "SimilarProductsWinForms" -ErrorAction SilentlyContinue
            if (-not $running) {
                Start-Process -FilePath $targetExe -WorkingDirectory $appDir
            } else {
                Write-Host "   Uygulama zaten acik durumda." -ForegroundColor Yellow
            }
            Start-Sleep -Seconds 2
            exit 0
        }
    }

    # 1. Yeni sürümü geçici dosyaya indir
    Write-Host "[1/5] GitHub 'dev-latest' surumu indiriliyor (~92 MB)..." -ForegroundColor Yellow
    if (Test-Path $tempDownload) { Remove-Item $tempDownload -Force }

    $maxRetries = 4
    $retryDelaySeconds = 5
    $downloadSuccess = $false

    # Öncelik 1: Windows BITS Motoru (Genellikle 10-15 saniyede tamamlar)
    try {
        Import-Module BitsTransfer -ErrorAction SilentlyContinue
        Write-Host "      [Motor: Windows BITS Hizlandirici] Indirme baslatildi..." -ForegroundColor Cyan
        Start-BitsTransfer -Source $downloadUrl -Destination $tempDownload -DisplayName "EtsyMarketPlace-Update" -Priority Foreground
        if ((Test-Path $tempDownload) -and ((Get-Item $tempDownload).Length -gt 10MB)) {
            $downloadSuccess = $true
        }
    } catch {
        Write-Host "      BITS motoru desteklenmedi, standart akisa geciliyor..." -ForegroundColor DarkYellow
    }

    # Öncelik 2: BITS başarısız olursa canlı akış ve hız göstergeli HttpWebRequest
    if (-not $downloadSuccess) {
        for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
            try {
                if ($attempt -gt 1) {
                    Write-Host "      [Deneme $attempt/$maxRetries] Yeniden deneniyor..." -ForegroundColor Yellow
                }

                $req = [System.Net.HttpWebRequest]::Create($downloadUrl)
                $req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) EtsyMarketPlace-VDS-Updater"
                $req.Timeout = 120000
                $resp = $req.GetResponse()
                $totalBytes = $resp.ContentLength
                $stream = $resp.GetResponseStream()
                $fileStream = [System.IO.File]::Create($tempDownload)

                $buffer = New-Object byte[] 65536
                $totalRead = 0
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $lastReport = 0

                while (($bytesRead = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $fileStream.Write($buffer, 0, $bytesRead)
                    $totalRead += $bytesRead

                    if ($sw.ElapsedMilliseconds - $lastReport -ge 300) {
                        $lastReport = $sw.ElapsedMilliseconds
                        $mb = [math]::Round($totalRead / 1MB, 1)
                        $totalMb = if ($totalBytes -gt 0) { [math]::Round($totalBytes / 1MB, 1) } else { 92.0 }
                        $pct = if ($totalBytes -gt 0) { [math]::Round(($totalRead / $totalBytes) * 100) } else { 0 }
                        $speed = if ($sw.Elapsed.TotalSeconds -gt 0) { [math]::Round(($totalRead / 1MB) / $sw.Elapsed.TotalSeconds, 1) } else { 0 }
                        Write-Host "`r      Indiriliyor: $mb MB / $totalMb MB (%$pct) - $speed MB/s   " -NoNewline -ForegroundColor Cyan
                    }
                }

                $fileStream.Flush()
                $fileStream.Close()
                $stream.Close()
                $resp.Close()
                $sw.Stop()
                Write-Host ""

                $downloadSuccess = $true
                break
            } catch {
                if (Test-Path $tempDownload) { Remove-Item $tempDownload -Force -ErrorAction SilentlyContinue }
                if ($attempt -lt $maxRetries) {
                    Write-Host ""
                    Write-Host "      [Bekleme] GitHub yeni surumu hazirliyor veya baglanti yavas. $retryDelaySeconds sn bekleniyor..." -ForegroundColor DarkYellow
                    Start-Sleep -Seconds $retryDelaySeconds
                    $retryDelaySeconds += 5
                } else {
                    throw $_
                }
            }
        }
    }

    $downloadedItem = Get-Item $tempDownload
    $fileSizeMb = [math]::Round($downloadedItem.Length / 1MB, 2)
    Write-Host "      Basariyla indirildi! Boyut: $fileSizeMb MB" -ForegroundColor Green

    if ($downloadedItem.Length -lt 10MB) {
        throw "Indirilen dosya boyutu cok kucuk ($fileSizeMb MB). Indirme hatasi olusmus olabilir!"
    }

    # 2. Çalışan mevcut programı ve hayalet süreçleri kapat
    Write-Host "[2/5] Calisan uygulama ve kilitli surecler temizleniyor..." -ForegroundColor Yellow
    try {
        cmd.exe /c "taskkill /F /IM SimilarProductsWinForms.exe /T 2>nul" | Out-Null
    } catch { }
    $runningProcesses = Get-Process -Name "SimilarProductsWinForms" -ErrorAction SilentlyContinue
    if ($runningProcesses) {
        $runningProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
    Write-Host "      Surecler temizlendi." -ForegroundColor Gray

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

    # 4. Yeni sürümü devreye al ve Windows SmartScreen engellerini kaldır
    Write-Host "[4/5] Yeni surum yerlestiriliyor ve guvenlik engelleri kaldiriliyor..." -ForegroundColor Yellow
    Move-Item -Path $tempDownload -Destination $targetExe -Force
    try {
        Unblock-File -Path $targetExe -ErrorAction SilentlyContinue
    } catch { }
    Write-Host "      $exeName guncellendi ve unblock edildi ($targetExe)!" -ForegroundColor Green

    # publish_vds_standalone dizini varsa orayı da anında güncelle
    $standaloneDir = Join-Path $appDir "publish_vds_standalone"
    if (Test-Path $standaloneDir) {
        $standaloneExe = Join-Path $standaloneDir $exeName
        Copy-Item -Path $targetExe -Destination $standaloneExe -Force -ErrorAction SilentlyContinue
        try { Unblock-File -Path $standaloneExe -ErrorAction SilentlyContinue } catch { }
        Write-Host "      publish_vds_standalone\$exeName guncellendi!" -ForegroundColor Green
    }

    # Masaüstüne doğrudan çalıştırılabilir EtsyMarketPlace kısayolu oluştur
    try {
        $desktopPath = [Environment]::GetFolderPath("Desktop")
        $shortcutPath = Join-Path $desktopPath "EtsyMarketPlace.lnk"
        $wshShell = New-Object -ComObject WScript.Shell
        $shortcut = $wshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $targetExe
        $shortcut.WorkingDirectory = $appDir
        $shortcut.Description = "EtsyMarketPlace Magaza Yonetim Paneli"
        $shortcut.Save()
        Write-Host "      Masaustu kisayolu hazirlandi: $shortcutPath" -ForegroundColor Green
    } catch { }

    # 5. Programı başlat
    Write-Host "[5/5] Yeni surum baslatiliyor..." -ForegroundColor Yellow
    Start-Process -FilePath $targetExe -WorkingDirectory $appDir
    
    $logFile = Join-Path $appDir "vds_update.log"
    $logMsg = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] BASARILI: $exeName dev-latest surumune basariyla guncellendi."
    Add-Content -Path $logFile -Value $logMsg -ErrorAction SilentlyContinue

    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "   ✅ VDS Basariyla Guncellendi ve Program Calistirildi!" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "Pencere 4 saniye icinde kapanacak..." -ForegroundColor Gray
    Start-Sleep -Seconds 4
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
        try { Unblock-File -Path $targetExe -ErrorAction SilentlyContinue } catch { }
        Write-Host "Mevcut surumle program aciliyor..." -ForegroundColor Yellow
        Start-Process -FilePath $targetExe -WorkingDirectory $appDir
    }
    Write-Host "Kapatmak icin bir tusa basin..." -ForegroundColor Yellow
    Read-Host
}
