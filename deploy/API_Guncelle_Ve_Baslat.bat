@echo off
chcp 65001 >nul
title EtsyMarketPlace VDS API Otomatik Guncelleyici
echo ======================================================================
echo    🚀 EtsyMarketPlace - VDS API Otomatik Guncelleyici (dev-latest)
echo ======================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13; " ^
    "$apiDir = '%~dp0'.TrimEnd('\'); " ^
    "if (-not (Test-Path $apiDir)) { $apiDir = 'C:\Users\Administrator\Desktop\EtsyM\Api' }; " ^
    "Write-Host 'Hedef Dizin: ' $apiDir -ForegroundColor Gray; " ^
    "Write-Host '[1/4] Port 5263 dinleyen surecler durduruluyor...' -ForegroundColor Cyan; " ^
    "try { " ^
    "    $pids = Get-NetTCPConnection -LocalPort 5263 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique; " ^
    "    foreach ($p in $pids) { if ($p -gt 4) { Stop-Process -Id $p -Force -ErrorAction SilentlyContinue } } " ^
    "} catch { }; " ^
    "try { " ^
    "    $procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'EtsyMarketPlace.Api.exe' -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*EtsyMarketPlace.Api*') }; " ^
    "    foreach ($p in $procs) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue } " ^
    "} catch { }; " ^
    "Start-Sleep -Seconds 2; " ^
    "Write-Host '[2/4] GitHub dev-latest API paketi indiriliyor (~56 MB)...' -ForegroundColor Cyan; " ^
    "$zipPath = Join-Path $env:TEMP 'EtsyMarketPlace.Api.zip'; " ^
    "if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }; " ^
    "$url = 'https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/EtsyMarketPlace.Api.zip'; " ^
    "$downloaded = $false; " ^
    "if (Get-Command 'curl.exe' -ErrorAction SilentlyContinue) { " ^
    "    & curl.exe -f -L --progress-bar -o $zipPath $url; " ^
    "    if ((Test-Path $zipPath) -and ((Get-Item $zipPath).Length -gt 5MB)) { $downloaded = $true } " ^
    "}; " ^
    "if (-not $downloaded) { " ^
    "    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing; " ^
    "    if ((Test-Path $zipPath) -and ((Get-Item $zipPath).Length -gt 5MB)) { $downloaded = $true } " ^
    "}; " ^
    "if (-not $downloaded) { Write-Host '❌ Indirme basarisiz oldu!' -ForegroundColor Red; exit 1 }; " ^
    "Write-Host '[3/4] Yeni API surumu yukleniyor...' -ForegroundColor Cyan; " ^
    "$extractPath = Join-Path $env:TEMP 'EtsyApiExtract'; " ^
    "if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force -ErrorAction SilentlyContinue }; " ^
    "Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force; " ^
    "Copy-Item (Join-Path $extractPath '*') $apiDir -Recurse -Force; " ^
    "Remove-Item $extractPath -Recurse -Force -ErrorAction SilentlyContinue; " ^
    "Remove-Item $zipPath -Force -ErrorAction SilentlyContinue; " ^
    "Write-Host '[4/4] Yeni API baslatiliyor...' -ForegroundColor Cyan; " ^
    "$exePath = Join-Path $apiDir 'EtsyMarketPlace.Api.exe'; " ^
    "Start-Process -FilePath $exePath -ArgumentList '--urls http://0.0.0.0:5263' -WorkingDirectory $apiDir -WindowStyle Hidden; " ^
    "Start-Sleep -Seconds 3; " ^
    "try { " ^
    "    $health = Invoke-RestMethod 'http://127.0.0.1:5263/health' -TimeoutSec 10; " ^
    "    Write-Host '   ✅ API Durumu: ' $health.status -ForegroundColor Green; " ^
    "    Write-Host '======================================================================' -ForegroundColor Green; " ^
    "    Write-Host '   🎉 VDS API BASARIYLA EN SON SURUME GUNCELLENDI! (Port: 5263)' -ForegroundColor Green; " ^
    "    Write-Host '======================================================================' -ForegroundColor Green; " ^
    "} catch { " ^
    "    Write-Host '   ⚠️ API baslatildi. Saglik kontrolu: http://127.0.0.1:5263/health' -ForegroundColor Yellow; " ^
    "}"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ HATA OLUSTU! Detaylar yukaridaki satirda yazmaktadir.
    echo.
)
pause
