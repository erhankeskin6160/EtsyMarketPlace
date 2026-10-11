@echo off
chcp 65001 >nul
echo ========================================================
echo   Aras Global Güvenli Giriş Penceresi Başlatılıyor...
echo ========================================================
start "" "%~dp0bin\Debug\net8.0-windows\SimilarProductsWinForms.exe" --login-aras
