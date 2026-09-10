@echo off
title EtsyMarketPlace VDS Otomatik Guncelleyici
echo ======================================================================
echo    EtsyMarketPlace - VDS Son Gelistirme Surumu Indiriliyor
echo ======================================================================
powershell -ExecutionPolicy Bypass -File "%~dp0vds_update.ps1"
