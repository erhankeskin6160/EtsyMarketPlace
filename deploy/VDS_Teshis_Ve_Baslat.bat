@echo off
title EtsyMarketPlace VDS Teshis ve Guvenli Baslatici
echo ======================================================================
echo    EtsyMarketPlace VDS Teshis ve Guvenli Baslatici
echo ======================================================================
echo.

cd /d "%~dp0.."

echo [1/4] Kilitli veya arka planda kalmis surecler temizleniyor...
taskkill /F /IM SimilarProductsWinForms.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/4] Windows dosya guvenlik engeli kaldiriliyor (Unblock)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path '.\SimilarProductsWinForms.exe' -ErrorAction SilentlyContinue"

echo [3/4] Kontrol ve form dogrulamasi calistiriliyor (--verify-controls)...
.\SimilarProductsWinForms.exe --verify-controls
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo KRITIK HATA: Kontroller dogrulanamadi! Cikis Kodu: %ERRORLEVEL%
    echo Detaylar yukarida veya Windows Olay Goruntuleyicisinde bulunabilir.
    echo.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [4/4] Ana Program Baslatiliyor...
start "" ".\SimilarProductsWinForms.exe"

echo.
echo ======================================================================
echo    EtsyMarketPlace basariyla calistirildi!
echo ======================================================================
echo.
pause
