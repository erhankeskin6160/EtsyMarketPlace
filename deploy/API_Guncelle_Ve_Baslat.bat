@echo off
title EtsyMarketPlace VDS API Otomatik Guncelleyici
echo ======================================================================
echo    🚀 EtsyMarketPlace - VDS API Son Gelistirme Surumu Indiriliyor
echo ======================================================================
powershell -ExecutionPolicy Bypass -File "%~dp0api_vds_update.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ======================================================================
    echo    ⚠️ HATA FARK EDILDI! Detaylar yukaridaki satirda yazmaktadir.
    echo ======================================================================
    echo.
    pause
)
