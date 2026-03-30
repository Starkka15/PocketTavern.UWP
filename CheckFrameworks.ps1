Write-Host "=== Installed .NET Native / CoreRuntime packages ==="
Get-AppxPackage | Where-Object { $_.Name -like "*CoreRuntime*" -or $_.Name -like "*CoreFramework*" -or $_.Name -like "*NETCore*" } |
    Select-Object Name, Version, Architecture | Format-Table -AutoSize

Write-Host "=== Manifest requires ==="
Write-Host "Microsoft.NET.CoreRuntime.2.2  >= 2.2.31331.1"
Write-Host "Microsoft.NET.CoreFramework.Debug.2.2 >= 2.2.31327.1"
