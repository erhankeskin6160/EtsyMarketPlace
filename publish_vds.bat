@echo off
echo ======================================================================
echo   EtsyMarketPlace VDS Standalone (.NET Runtime Dahil) Publish Ediliyor
echo ======================================================================
dotnet publish SimilarProductsWinForms.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o publish_vds_standalone
echo.
echo ======================================================================
echo   VDS Otomatik Guncellemesi Tetikleniyor (development push)...
echo ======================================================================
git add publish_vds_standalone\SimilarProductsWinForms.exe
git commit -m "chore: VDS standalone binary publish guncellemesi"
git push origin development
echo.
echo ======================================================================
echo   Tamamlandi! VDS 20 saniye icinde yeni EXE ile otomatik yenilenecektir.
echo ======================================================================
