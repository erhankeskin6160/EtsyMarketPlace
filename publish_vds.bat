@echo off
echo ======================================================================
echo   EtsyMarketPlace VDS Standalone (.NET Runtime Dahil) Publish Ediliyor
echo ======================================================================
dotnet publish SimilarProductsWinForms.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o publish_vds_standalone
echo.
echo ======================================================================
echo   Tamamlandi! Cikti: publish_vds_standalone\SimilarProductsWinForms.exe
echo ======================================================================
