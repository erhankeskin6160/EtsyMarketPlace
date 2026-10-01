[CmdletBinding()]
param(
	[string]$OutputPath = "publish-api-vds"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\EtsyMarketPlace.Api\EtsyMarketPlace.Api.csproj"
$outputFullPath = Join-Path $repoRoot $OutputPath

Write-Host "API restore ve test basliyor..." -ForegroundColor Cyan
dotnet restore $projectPath
dotnet test (Join-Path $repoRoot "SimilarProductsWinForms.sln") -c Release --no-restore

if (Test-Path $outputFullPath) {
	Remove-Item $outputFullPath -Recurse -Force
}

Write-Host "API self-contained win-x64 publish basliyor..." -ForegroundColor Cyan
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $outputFullPath

Write-Host "Publish tamamlandi: $outputFullPath" -ForegroundColor Green
Write-Host "VDS'ye bu klasorun icerigini kopyalayin." -ForegroundColor Yellow
