@echo off
chcp 65001 >nul
title EtsyMarketPlace Otomatik Guncelleyici (Client)
echo ======================================================================
echo    🚀 EtsyMarketPlace - Son Gelistirme Surumu Indiriliyor (dev-latest)
echo ======================================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "APP_DIR=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%SimilarProductsWinForms.exe" (
    set "TARGET_EXE=%SCRIPT_DIR%SimilarProductsWinForms.exe"
) else (
    if exist "%SCRIPT_DIR%..\SimilarProductsWinForms.exe" (
        set "TARGET_EXE=%SCRIPT_DIR%..\SimilarProductsWinForms.exe"
        set "APP_DIR=%SCRIPT_DIR%..\"
    ) else (
        set "TARGET_EXE=%SCRIPT_DIR%SimilarProductsWinForms.exe"
    )
)

echo [1/3] Calisan program kontrol ediliyor...
taskkill /F /IM SimilarProductsWinForms.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/3] GitHub 'dev-latest' surumu indiriliyor (~93 MB)...
set "TEMP_DOWNLOAD=%TARGET_EXE%.download"
if exist "%TEMP_DOWNLOAD%" del /f /q "%TEMP_DOWNLOAD%"

where curl.exe >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo   [Motor: curl.exe Yuksek Hizli Indirici]
    curl.exe -f -L --progress-bar -o "%TEMP_DOWNLOAD%" "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe"
)

if not exist "%TEMP_DOWNLOAD%" (
    echo   [Motor: Alternatif WebClient]
    powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13; (New-Object System.Net.WebClient).DownloadFile('https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe', '$env:TEMP_DOWNLOAD')"
)

if exist "%TEMP_DOWNLOAD%" (
    echo [3/3] Surum guncelleniyor...
    move /y "%TEMP_DOWNLOAD%" "%TARGET_EXE%" >nul
    echo.
    echo ======================================================================
    echo    ✅ GUNCELLEME TAMAMLANDI! Uygulama baslatiliyor...
    echo ======================================================================
    start "" "%TARGET_EXE%"
    timeout /t 2 >nul
    exit 0
) else (
    echo.
    echo ======================================================================
    echo    ❌ HATA: Yeni surum indirilemedi!
    echo    💡 GitHub Actions henuz derlemeyi bitirmemis olabilir (404).
    echo    Lutfen 1-2 dakika bekleyip bu dosyayi tekrar calistirin.
    echo ======================================================================
    pause
    exit 1
)
