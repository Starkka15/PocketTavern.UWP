$appxPath = 'C:\Program Files (x86)\Microsoft SDKs\UWPNuGetPackages\runtime.win10-x64.microsoft.net.uwpcoreruntimesdk\2.2.14\tools\Appx\Microsoft.NET.CoreFramework.Debug.2.2.appx'
$extractTo = 'C:\Temp\CoreFrameworkDebug'

if (Test-Path $extractTo) { Remove-Item $extractTo -Recurse -Force }
New-Item $extractTo -ItemType Directory | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($appxPath, $extractTo)

Write-Host "=== Contents of CoreFramework.Debug.2.2 appx ==="
Get-ChildItem $extractTo -Recurse | Select-Object FullName | Format-List
