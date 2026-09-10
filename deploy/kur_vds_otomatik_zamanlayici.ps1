<#
.SYNOPSIS
    VDS Windows Görev Zamanlayıcısına (Task Scheduler) Otomatik Güncelleme Görevi Ekler
.DESCRIPTION
    Bu script yönetici olarak çalıştırıldığında VDS her açıldığında ve her gün belirli saatte
    otomatik olarak GitHub'dan en güncel sürümü kontrol eden bir görev tanımlar.
#>

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$updaterBat = Join-Path $scriptDir "Guncelle_Ve_Baslat.bat"

if (-not (Test-Path $updaterBat)) {
    Write-Error "$updaterBat bulunamadi!"
    exit 1
}

$taskName = "EtsyMarketPlace_VDS_AutoUpdate"
Write-Host "Zamanlanmis gorev olusturuluyor: $taskName" -ForegroundColor Cyan

# VDS her açıldığında veya oturum açıldığında çalışacak görev
$action = New-ScheduledTaskAction -Execute $updaterBat
$trigger = New-ScheduledTaskTrigger -AtLogon
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description "EtsyMarketPlace VDS Otomatik Guncelleme ve Baslatma Gorevi" -Force

Write-Host "✅ Gorev basariyla kaydedildi! VDS oturumu her acildiginda son surum otomatik indirilecek." -ForegroundColor Green
