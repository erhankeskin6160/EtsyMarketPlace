@echo off
chcp 65001 >nul
title EtsyMarketPlace Hizli Guncelleyici
echo ======================================================================
echo    🚀 EtsyMarketPlace - Hizli Guncelleyici (curl)
echo ======================================================================
echo.

set "TARGET_DIR=%~dp0"
if exist "%TARGET_DIR%..\SimilarProductsWinForms.exe" (
    set "TARGET_DIR=%TARGET_DIR%..\"
)

echo [1/3] Calisan program kapatiliyor...
taskkill /F /IM SimilarProductsWinForms.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/3] GitHub 'dev-latest' surumu indiriliyor (~93 MB)...
curl.exe -f -L --progress-bar -o "%TARGET_DIR%SimilarProductsWinForms.exe" "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ======================================================================
    echo    ✅ GUNCELLEME TAMAMLANDI! Uygulama baslatiliyor...
    echo ======================================================================
    start "" "%TARGET_DIR%SimilarProductsWinForms.exe"
    timeout /t 2 >nul
    exit 0
) else (
    echo.
    echo ======================================================================
    echo    ❌ HATA: Indirme tamamlanamadi.
    echo ======================================================================
    pause
    exit 1
)
