[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[string]$PackagePath,
	[string]$InstallPath = "C:\Users\Administrator\Desktop\EtsyM\Api",
	[int]$Port = 5263,
	[string]$ApiKey = ""
)

$ErrorActionPreference = "Continue"
$exeName = "EtsyMarketPlace.Api.exe"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   🚀 VDS API Dağıtım Betiği Başlatılıyor" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Gelen PackagePath: $PackagePath" -ForegroundColor Gray
Write-Host "Hedef InstallPath: $InstallPath" -ForegroundColor Gray
Write-Host "Port: $Port" -ForegroundColor Gray

# 1. PackagePath kontrolü ve alt klasör arama
if (-not (Test-Path (Join-Path $PackagePath $exeName))) {
	Write-Host "Ana dizinde $exeName bulunamadi, alt dizinler taraniyor..." -ForegroundColor Yellow
	$found = Get-ChildItem -Path $PackagePath -Filter $exeName -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
	if ($found) {
		$PackagePath = $found.DirectoryName
		Write-Host "   ✅ Paket klasoru bulundu: $PackagePath" -ForegroundColor Green
	} else {
		Write-Host "Klasor icerigi:" -ForegroundColor Red
		Get-ChildItem -Path $PackagePath -Recurse -ErrorAction SilentlyContinue | Select-Object -First 20 FullName | Out-String | Write-Host -ForegroundColor Red
		throw "API publish paketi bulunamadi: $exeName ($PackagePath icinde)"
	}
}

$targetExe = Join-Path $InstallPath $exeName
New-Item -ItemType Directory -Force -Path $InstallPath -ErrorAction SilentlyContinue | Out-Null
New-Item -ItemType Directory -Force -Path "C:\ProgramData\EtsyMarketPlace\keys" -ErrorAction SilentlyContinue | Out-Null

# 2. Port ve Process temizliği
Write-Host "[1/5] Port $Port dinleyen ve calisan API surecleri durduruluyor..." -ForegroundColor Cyan

try {
	$listeningPids = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
	foreach ($pidToKill in $listeningPids) {
		if ($pidToKill -gt 4) {
			Write-Host "      Port $Port dinleyen PID durduruluyor: $pidToKill" -ForegroundColor Yellow
			Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
		}
	}
} catch { }

try {
	$procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { 
		$_.Name -eq $exeName -or 
		($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*EtsyMarketPlace.Api*")
	}
	foreach ($process in $procs) {
		Write-Host "      API sureci durduruluyor: $($process.Name) (PID: $($process.ProcessId))..." -ForegroundColor Yellow
		Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
	}
} catch { }

Start-Sleep -Seconds 3

# 3. appsettings.Production.json yedeğini al
Write-Host "[2/5] Mevcut ayarlar kontrol ediliyor..." -ForegroundColor Cyan
$appSettings = Join-Path $InstallPath "appsettings.Production.json"
$savedProdSettings = $null
if (Test-Path $appSettings) {
	$savedProdSettings = Get-Content $appSettings -Raw -ErrorAction SilentlyContinue
}

# 4. Dosyaları kopyala
Write-Host "[3/5] Yeni API dosyalari kopyalaniyor..." -ForegroundColor Cyan
$copySuccess = $false
for ($attempt = 1; $attempt -le 5; $attempt++) {
	try {
		$sourceItems = Get-ChildItem -Path $PackagePath
		foreach ($item in $sourceItems) {
			Copy-Item -Path $item.FullName -Destination $InstallPath -Recurse -Force -ErrorAction Stop
		}
		$copySuccess = $true
		break
	} catch {
		Write-Warning "Dosya kopyalama denemesi $attempt/5 basarisiz: $($_.Exception.Message). 2 sn bekleniyor..."
		Start-Sleep -Seconds 2
	}
}

if (-not $copySuccess) {
	throw "API dosyalari kopyalanamadi: $InstallPath"
}

# 5. Ayarları geri yükle
if ($savedProdSettings) {
	Set-Content $appSettings -Value $savedProdSettings -Encoding UTF8 -ErrorAction SilentlyContinue
} elseif (-not (Test-Path $appSettings)) {
	@{
		EtsyIntegration = @{ DatabasePath = "%ProgramData%\\EtsyMarketPlace\\etsy-finance.db" }
		Security = @{ ApiKey = $ApiKey; AllowedShopIds = @("523236321") }
	} | ConvertTo-Json -Depth 5 | Set-Content $appSettings -Encoding UTF8 -ErrorAction SilentlyContinue
}

# 6. Güvenlik duvarı kuralı
try {
	if (-not (Get-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -ErrorAction SilentlyContinue)) {
		New-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -ErrorAction SilentlyContinue | Out-Null
	}
} catch { }

# 7. API'yi başlat
Write-Host "[4/5] API baslatiliyor..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Production"
$arguments = "--urls http://0.0.0.0:$Port"

Start-Process -FilePath $targetExe -ArgumentList $arguments -WorkingDirectory $InstallPath -WindowStyle Hidden

# 8. Sağlık kontrolü
Write-Host "[5/5] Saglik kontrolu yapiliyor (http://127.0.0.1:$Port/health)..." -ForegroundColor Cyan
$healthy = $false
for ($i = 1; $i -le 20; $i++) {
	Start-Sleep -Seconds 1
	try {
		$health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 2
		if ($health.status -eq "Healthy") {
			$healthy = $true
			break
		}
	} catch {
		Write-Host "   Bekleniyor ($i/20)..." -ForegroundColor DarkGray
	}
}

if (-not $healthy) {
	throw "API 20 saniye icinde saglikli duruma gecmedi: $targetExe"
}

Write-Host "   ✅ API calisiyor: http://0.0.0.0:$Port" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
