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
	Write-Host "Ana dizinde $exeName bulunamadı, alt dizinler taranıyor..." -ForegroundColor Yellow
	$found = Get-ChildItem -Path $PackagePath -Filter $exeName -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
	if ($found) {
		$PackagePath = $found.DirectoryName
		Write-Host "   ✅ Paket klasörü bulundu: $PackagePath" -ForegroundColor Green
	} else {
		Write-Host "Klasör içeriği:" -ForegroundColor Red
		Get-ChildItem -Path $PackagePath -Recurse -ErrorAction SilentlyContinue | Select-Object -First 20 FullName | Out-String | Write-Host -ForegroundColor Red
		throw "API publish paketi bulunamadı: $exeName ($PackagePath içinde)"
	}
}

$targetExe = Join-Path $InstallPath $exeName
New-Item -ItemType Directory -Force -Path $InstallPath -ErrorAction SilentlyContinue | Out-Null
New-Item -ItemType Directory -Force -Path "C:\ProgramData\EtsyMarketPlace\keys" -ErrorAction SilentlyContinue | Out-Null

# 2. Port ve Süreç Temizliği (Güvenli & Zorlamalı)
Write-Host "[1/5] Port $Port dinleyen ve çalışan API süreçleri durduruluyor..." -ForegroundColor Cyan

# 2.1. taskkill ile kesin sonlandırma
try {
	taskkill.exe /F /IM $exeName /T 2>&1 | Out-Null
} catch { }

# 2.2. Port 5263'ü dinleyen PID'leri kapatma
try {
	$listeningPids = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
	foreach ($pidToKill in $listeningPids) {
		if ($pidToKill -gt 4) {
			Write-Host "      Port $Port dinleyen PID durduruluyor: $pidToKill" -ForegroundColor Yellow
			Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
		}
	}
} catch { }

# 2.3. Win32_Process taraması
try {
	$procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { 
		$_.Name -eq $exeName -or 
		($_.Name -eq "dotnet.exe" -and $_.CommandLine -like "*EtsyMarketPlace.Api*")
	}
	foreach ($process in $procs) {
		Write-Host "      API süreci durduruluyor: $($process.Name) (PID: $($process.ProcessId))..." -ForegroundColor Yellow
		Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
	}
} catch { }

Start-Sleep -Seconds 2

# 2.4. Hedef dosya kilidinin serbest kalmasını bekleme
for ($wait = 1; $wait -le 10; $wait++) {
	if (-not (Test-Path $targetExe)) { break }
	try {
		$stream = [System.IO.File]::Open($targetExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
		if ($stream) {
			$stream.Close()
			$stream.Dispose()
			break
		}
	} catch {
		Write-Host "      Hedef dosya kilitli, serbest kalması bekleniyor ($wait/10)..." -ForegroundColor Yellow
		try { taskkill.exe /F /IM $exeName /T 2>&1 | Out-Null } catch { }
		Start-Sleep -Seconds 1
	}
}

# 3. appsettings.Production.json yedeğini al
Write-Host "[2/5] Mevcut ayarlar kontrol ediliyor..." -ForegroundColor Cyan
$appSettings = Join-Path $InstallPath "appsettings.Production.json"
$savedProdSettings = $null
if (Test-Path $appSettings) {
	$savedProdSettings = Get-Content $appSettings -Raw -ErrorAction SilentlyContinue
}

# 4. Dosyaları kopyala
Write-Host "[3/5] Yeni API dosyaları kopyalanıyor..." -ForegroundColor Cyan
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
		Write-Warning "Dosya kopyalama denemesi $attempt/5 başarısız: $($_.Exception.Message). 2 sn bekleniyor..."
		try { taskkill.exe /F /IM $exeName /T 2>&1 | Out-Null } catch { }
		Start-Sleep -Seconds 2
	}
}

if (-not $copySuccess) {
	throw "API dosyaları kopyalanamadı: $InstallPath"
}

# 5. Ayarları geri yükle
if ($savedProdSettings) {
	Set-Content $appSettings -Value $savedProdSettings -Encoding UTF8 -ErrorAction SilentlyContinue
} elseif (-not (Test-Path $appSettings)) {
	@{
		EtsyIntegration = @{ DatabasePath = "%ProgramData%\\EtsyMarketPlace\\etsy-finance.db" }
		Security = @{ ApiKey = $ApiKey; AllowedShopIds = @("53236321") }
	} | ConvertTo-Json -Depth 5 | Set-Content $appSettings -Encoding UTF8 -ErrorAction SilentlyContinue
}

# 6. Güvenlik duvarı kuralı
try {
	if (-not (Get-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -ErrorAction SilentlyContinue)) {
		New-NetFirewallRule -DisplayName "EtsyMarketPlace API $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -ErrorAction SilentlyContinue | Out-Null
	}
} catch { }

# 7. API'yi başlat (GitHub Actions Runner orphan temizliğinden izole)
Write-Host "[4/5] API başlatılıyor (arka plan servisi olarak)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:RUNNER_TRACKING_ID = ""
$arguments = "--urls http://0.0.0.0:$Port"

$startParams = @{
	CommandLine = "`"$targetExe`" $arguments"
	CurrentDirectory = $InstallPath
}
$createResult = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments $startParams
if ($createResult.ReturnValue -ne 0) {
	Write-Warning "Win32_Process Create dönüş kodu: $($createResult.ReturnValue). Start-Process ile deneniyor..."
	Start-Process -FilePath $targetExe -ArgumentList $arguments -WorkingDirectory $InstallPath -WindowStyle Hidden
}

# 8. Sağlık kontrolü (45 saniye toleranslı)
Write-Host "[5/5] Sağlık kontrolü yapılıyor (http://127.0.0.1:$Port/health)..." -ForegroundColor Cyan
$healthy = $false
$lastError = ""
for ($i = 1; $i -le 45; $i++) {
	Start-Sleep -Seconds 1
	try {
		$health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 3
		if ($health.status -eq "Healthy") {
			$healthy = $true
			break
		}
	} catch {
		$lastError = $_.Exception.Message
		if ($i % 5 -eq 0) {
			Write-Host "   API sağlık kontrolü bekleniyor ($i/45)... ($lastError)" -ForegroundColor DarkGray
		}
	}
}

if (-not $healthy) {
	throw "API 45 saniye içinde sağlıklı duruma geçmedi: $targetExe (Son hata: $lastError)"
}

Write-Host "   ✅ API başarıyla çalışıyor: http://0.0.0.0:$Port" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
