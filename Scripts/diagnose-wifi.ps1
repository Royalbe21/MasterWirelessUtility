Write-Host "=== WLAN Service ===" -ForegroundColor Cyan
Get-Service WlanSvc | Format-List *

Write-Host "=== Network Adapters ===" -ForegroundColor Cyan
Get-NetAdapter | Format-Table -Auto

Write-Host "=== WLAN Interfaces ===" -ForegroundColor Cyan
netsh wlan show interfaces

Write-Host "=== WLAN Drivers ===" -ForegroundColor Cyan
netsh wlan show drivers

Write-Host "=== Visible Networks ===" -ForegroundColor Cyan
netsh wlan show networks mode=bssid

Write-Host "=== Saved Profiles ===" -ForegroundColor Cyan
netsh wlan show profiles
