@echo off
chcp 65001 >nul
echo ========================================================================
echo   🚀 PostgreSQL Tum Tablolari Doldurma Calistiriliyor...
echo ========================================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Doldur_Tum_Tablolari.ps1"
pause
