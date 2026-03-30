Write-Host "=== Installed framework package publishers ==="
Get-AppxPackage | Where-Object { $_.Name -like "*CoreRuntime*" -or $_.Name -like "*CoreFramework*" } |
    Select-Object Name, Version, Publisher | Format-List

Write-Host "=== Our manifest declares ==="
Write-Host "Publisher: CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"

Write-Host "`n=== Core\AppxManifest.xml PackageDependency publishers ==="
Select-String -Path 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin\x64\Debug\Core\AppxManifest.xml' -Pattern "PackageDependency"
