[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string]$PackagePath,
	[string]$InstallPath = "C:\Users\Administrator\Desktop\EtsyM\Api",
	[int]$Port = 5263,
	[string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"
$exeName = "EtsyMarketPlace.Api.exe"
$targetExe = Join-Path $InstallPath $exeName

if (-not (Test-Path (Join-Path $PackagePath $exeName))) {
	throw "API publish paketi bulunamadi: $(Join-Path $PackagePath $exeName)"
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
New-Item -ItemType Directory -Force -Path "C:\ProgramData\EtsyMarketPlace\keys" | Out-Null

$running = Get-CimInstance Win32_Process -Filter "Name = '$exeName'" -ErrorAction SilentlyContinue
foreach ($process in $running) {
	Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

$backupPath = Join-Path $InstallPath "backup-$(Get-Date -Format yyyyMMdd-HHmmss)"
if (Test-Path $targetExe) {
	New-Item -ItemType Directory -Force -Path $backupPath | Out-Null
	Copy-Item (Join-Path $InstallPath "*") $backupPath -Recurse -Force -ErrorAction SilentlyContinue
}

Copy-Item (Join-Path $PackagePath "*") $InstallPath -Recurse -Force

$appSettings = Join-Path $InstallPath "appsettings.Production.json"
if (-not (Test-Path $appSettings)) {
	@{
		EtsyIntegration = @{ DatabasePath = "%ProgramData%\\EtsyMarketPlace\\etsy-finance.db" }
		Security = @{ ApiKey = $ApiKey; AllowedShopIds = @("523236321") }
	} | ConvertTo-Json -Depth 5 | Set-Content $appSettings -Encoding UTF8
}

if (-not (Get-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -ErrorAction SilentlyContinue)) {
	New-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
}

$env:ASPNETCORE_ENVIRONMENT = "Production"
$arguments = "--urls http://0.0.0.0:$Port"
if ($ApiKey) {
	Write-Host "API key ortam degiskeniyle veya appsettings.Production.json ile ayarlanmalidir." -ForegroundColor Yellow
}

Start-Process -FilePath $targetExe -ArgumentList $arguments -WorkingDirectory $InstallPath -WindowStyle Hidden
Start-Sleep -Seconds 3

if (-not (Get-Process -Name "EtsyMarketPlace.Api" -ErrorAction SilentlyContinue)) {
	throw "API baslatilamadi: $targetExe"
}

Write-Host "API calisiyor: http://0.0.0.0:$Port" -ForegroundColor Green
Invoke-RestMethod "http://127.0.0.1:$Port/health"
