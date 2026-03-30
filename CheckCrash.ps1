Start-Process "explorer.exe" "shell:AppsFolder\PocketTavern.UWP_1b7q5sa4bwdpa!App"
Start-Sleep -Milliseconds 2000
$proc = Get-Process | Where-Object { $_.Name -like "*PocketTavern*" }
if ($proc) { Write-Host "Process running: $($proc.Name) PID=$($proc.Id)" }
else { Write-Host "No process found - app crashed or failed to activate" }

Write-Host "`n=== Crash dumps ==="
$dumps = @("$env:LOCALAPPDATA\CrashDumps","C:\ProgramData\Microsoft\Windows\WER\ReportQueue","C:\ProgramData\Microsoft\Windows\WER\ReportArchive")
foreach ($d in $dumps) {
    if (Test-Path $d) {
        Get-ChildItem $d -Recurse -Filter "*PocketTavern*" -ErrorAction SilentlyContinue | Select-Object FullName, LastWriteTime
    }
}

Write-Host "`n=== AppModel activation errors (last 5 min) ==="
Get-WinEvent -LogName "Microsoft-Windows-AppModel-Runtime/Admin" -MaxEvents 50 -ErrorAction SilentlyContinue |
    Where-Object { $_.TimeCreated -gt (Get-Date).AddMinutes(-5) } |
    Select-Object TimeCreated, LevelDisplayName, Message | Format-List
