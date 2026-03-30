# Launch app via shell: protocol and capture exact error
$aumid = "PocketTavern.UWP_1b7q5sa4bwdpa!App"

Write-Host "Attempting launch via shell:AppsFolder..."
$before = Get-Date

try {
    Start-Process "shell:AppsFolder\$aumid" -ErrorAction Stop
    Write-Host "Start-Process returned OK"
} catch {
    Write-Host "Start-Process error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 4

# Check if process is running
$proc = Get-Process | Where-Object { $_.Name -like "*PocketTavern*" }
if ($proc) {
    Write-Host "Process found: $($proc.Name) PID=$($proc.Id)"
} else {
    Write-Host "No PocketTavern process found (crashed or never started)"
}

# Recent activation events
Write-Host "`n--- Recent Immersive-Shell events ---"
Get-WinEvent -ProviderName 'Microsoft-Windows-Immersive-Shell' -MaxEvents 10 -ErrorAction SilentlyContinue |
    Where-Object { $_.TimeCreated -gt $before.AddSeconds(-2) } |
    Select-Object TimeCreated, Id, Message | Format-List

# AppModel state
Write-Host "`n--- AppModel Runtime state ---"
Get-WinEvent -LogName 'Microsoft-Windows-AppModel-Runtime/Operational' -MaxEvents 15 -ErrorAction SilentlyContinue |
    Where-Object { ($_.Message -like '*PocketTavern*' -or $_.Message -like '*1b7q5sa4bwdpa*') -and $_.TimeCreated -gt $before.AddSeconds(-2) } |
    Select-Object TimeCreated, Id, LevelDisplayName, Message | Format-List

# Application event log
Write-Host "`n--- Application event log (errors/warnings) ---"
Get-WinEvent -LogName 'Application' -MaxEvents 20 -ErrorAction SilentlyContinue |
    Where-Object { $_.TimeCreated -gt $before.AddSeconds(-2) -and ($_.LevelDisplayName -eq 'Error' -or $_.LevelDisplayName -eq 'Warning') } |
    Select-Object TimeCreated, Id, ProviderName, LevelDisplayName, Message | Format-List
