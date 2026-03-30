# Enable crash dumps for the app, then launch it
$dumpPath = 'C:\CrashDumps'
New-Item $dumpPath -ItemType Directory -Force | Out-Null

$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\PocketTavern.UWP.exe'
New-Item $regPath -Force | Out-Null
Set-ItemProperty $regPath -Name DumpType  -Value 2 -Type DWord
Set-ItemProperty $regPath -Name DumpCount -Value 3 -Type DWord
Set-ItemProperty $regPath -Name DumpFolder -Value $dumpPath -Type ExpandString

Write-Host "Dump collection enabled at $dumpPath"
Write-Host "Launching app..."
Start-Process "explorer.exe" "shell:AppsFolder\PocketTavern.UWP_1b7q5sa4bwdpa!App"
Start-Sleep -Seconds 5

Write-Host "`nDumps found:"
Get-ChildItem $dumpPath -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

Write-Host "`nWER reports:"
Get-ChildItem "C:\ProgramData\Microsoft\Windows\WER\ReportQueue" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "*PocketTavern*" } | Select-Object FullName, LastWriteTime
