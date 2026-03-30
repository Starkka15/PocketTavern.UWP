# Check UWP app launch errors
Write-Host "=== AppModel-Runtime ==="
Get-WinEvent -LogName "Microsoft-Windows-AppModel-Runtime/Admin" -MaxEvents 30 -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -like "*PocketTavern*" -or $_.LevelDisplayName -eq "Error" } |
    Select-Object TimeCreated, LevelDisplayName, Message | Format-List

Write-Host "=== AppXDeployment-Server ==="
Get-WinEvent -LogName "Microsoft-Windows-AppXDeploymentServer/Operational" -MaxEvents 20 -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -like "*PocketTavern*" } |
    Select-Object TimeCreated, LevelDisplayName, Message | Format-List

Write-Host "=== Application log (errors) ==="
Get-WinEvent -LogName Application -MaxEvents 50 -ErrorAction SilentlyContinue |
    Where-Object { $_.LevelDisplayName -eq "Error" -and $_.TimeCreated -gt (Get-Date).AddMinutes(-5) } |
    Select-Object TimeCreated, ProviderName, Message | Format-List
