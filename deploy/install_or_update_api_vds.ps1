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

Write-Host "Port $Port dinleyen surecler ve calisan API surecleri tespit ediliyor..." -ForegroundColor Cyan

# 1. Port dinleyen process'leri durdur
try {
	$listeningPids = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
	foreach ($pidToKill in $listeningPids) {
		Write-Host "Port $Port dinleyen surec durduruluyor (PID: $pidToKill)..." -ForegroundColor Yellow
		Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
	}
} catch { }

# 2. EtsyMarketPlace.Api veya dotnet ile baslatilmis API sureclerini durdur
try {
	$procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { 
		$_.Name -eq $exeName -or 
		($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*EtsyMarketPlace.Api*")
	}
	foreach ($process in $procs) {
		Write-Host "API sureci durduruluyor: $($process.Name) (PID: $($process.ProcessId))..." -ForegroundColor Yellow
		Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
	}
} catch { }

Start-Sleep -Seconds 3

# 3. Gerekirse mevcut dizinin yedegini TEMP icine al (icerigini kendi icine kopyalama hatasini engeller)
$backupPath = Join-Path $env:TEMP "backup-api-$(Get-Date -Format yyyyMMdd-HHmmss)"
if (Test-Path $targetExe) {
	try {
		New-Item -ItemType Directory -Force -Path $backupPath | Out-Null
		Get-ChildItem -Path $InstallPath -Exclude "*.db", "backup-*" | Copy-Item -Destination $backupPath -Recurse -Force -ErrorAction SilentlyContinue
	} catch { }
}

# 4. appsettings.Production.json varsa sakla
$appSettings = Join-Path $InstallPath "appsettings.Production.json"
$savedProdSettings = $null
if (Test-Path $appSettings) {
	$savedProdSettings = Get-Content $appSettings -Raw
}

# 5. Yeni paket dosyalarini kopyala (kilitli dosya varsa 5 deneme)
$copied = $false
for ($attempt = 1; $attempt -le 5; $attempt++) {
	try {
		Copy-Item (Join-Path $PackagePath "*") $InstallPath -Recurse -Force
		$copied = $true
		break
	} catch {
		Write-Warning "Dosya kopyalama denemesi $attempt/5 basarisiz: $_. 2 sn bekleniyor..."
		Start-Sleep -Seconds 2
	}
}
if (-not $copied) {
	throw "API dosyalari kopyalanamadi: $InstallPath"
}

# 6. appsettings.Production.json geri yukle veya olustur
if ($savedProdSettings) {
	Set-Content $appSettings -Value $savedProdSettings -Encoding UTF8
} elseif (-not (Test-Path $appSettings)) {
	@{
		EtsyIntegration = @{ DatabasePath = "%ProgramData%\\EtsyMarketPlace\\etsy-finance.db" }
		Security = @{ ApiKey = $ApiKey; AllowedShopIds = @("523236321") }
	} | ConvertTo-Json -Depth 5 | Set-Content $appSettings -Encoding UTF8
}

# 7. Guvenlik duvari kuralini guvenli sekilde kontrol et
try {
	if (-not (Get-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -ErrorAction SilentlyContinue)) {
		New-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
	}
} catch {
	Write-Warning "Guvenlik duvari kurali kontrol edilemedi/eklenemedi (yetki gerekebilir): $_"
}

# 8. API'yi baslat
$env:ASPNETCORE_ENVIRONMENT = "Production"
$arguments = "--urls http://0.0.0.0:$Port"
if ($ApiKey) {
	Write-Host "API key ortam degiskeniyle veya appsettings.Production.json ile ayarlanmalidir." -ForegroundColor Yellow
}

Start-Process -FilePath $targetExe -ArgumentList $arguments -WorkingDirectory $InstallPath -WindowStyle Hidden

# 9. Saglik kontrolu (15 saniye boyunca poll et)
$healthy = $false
for ($i = 1; $i -le 15; $i++) {
	Start-Sleep -Seconds 1
	try {
		$health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 2
		if ($health.status -eq "Healthy") {
			$healthy = $true
			break
		}
	} catch { }
}

if (-not $healthy) {
	throw "API 15 saniye icinde saglikli duruma gecmedi: $targetExe"
}

Write-Host "API calisiyor: http://0.0.0.0:$Port" -ForegroundColor Green
Invoke-RestMethod "http://127.0.0.1:$Port/health"
