@echo off
chcp 65001 >nul
echo ========================================================
echo   Aras Global Masaüstü Tokeni VDS'e Aktarılıyor...
echo ========================================================
"%~dp0bin\Debug\net8.0-windows\SimilarProductsWinForms.exe" --sync-aras
echo.
echo Tamamlandı.
pause
