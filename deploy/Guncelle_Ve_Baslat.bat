@echo off
title EtsyMarketPlace VDS Otomatik Guncelleyici
echo ======================================================================
echo    🚀 EtsyMarketPlace - VDS Son Gelistirme Surumu Indiriliyor
echo ======================================================================
powershell -ExecutionPolicy Bypass -File "%~dp0vds_update.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ======================================================================
    echo    ⚠️ HATA FARK EDILDI! Detaylar yukaridaki satirda yazmaktadir.
    echo ======================================================================
    echo.
    pause
)
