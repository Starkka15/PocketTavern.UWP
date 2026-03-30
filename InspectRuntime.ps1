$appxPath = 'C:\Program Files (x86)\Microsoft SDKs\UWPNuGetPackages\runtime.win10-x64.microsoft.net.uwpcoreruntimesdk\2.2.14\tools\Appx\Microsoft.NET.CoreRuntime.2.2.appx'
$extractTo = 'C:\Temp\CoreRuntime'

if (Test-Path $extractTo) { Remove-Item $extractTo -Recurse -Force }
New-Item $extractTo -ItemType Directory | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($appxPath, $extractTo)

Write-Host "=== Files in CoreRuntime.2.2 ==="
Get-ChildItem $extractTo -Recurse -File | Select-Object -ExpandProperty Name | Sort-Object
