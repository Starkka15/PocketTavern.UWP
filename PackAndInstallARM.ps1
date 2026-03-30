# Run as Administrator
$ilcDir   = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin\ARM\Release\ilc'
$winkits  = 'C:\Program Files (x86)\Windows Kits\10'
$makeappx = "$winkits\bin\10.0.16299.0\x64\makeappx.exe"
$signtool = "$winkits\bin\10.0.16299.0\x64\signtool.exe"
$pfxPath  = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\PocketTavern_TemporaryKey.pfx'
$appxOut  = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern_ARM.appx'
$layout   = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\AppXLayoutARM'

# Clean and create layout
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item $layout                    -ItemType Directory | Out-Null
New-Item "$layout\RuntimeDlls"      -ItemType Directory | Out-Null
New-Item "$layout\Assets"           -ItemType Directory | Out-Null

Write-Host "Building ARM layout from ilc/..."

# Manifest — remove the Debug VCLibs entry (Release VCLibs is already declared separately)
$manifest = Get-Content "$ilcDir\AppxManifest.xml" -Raw
$manifest = $manifest -replace '\s*<PackageDependency Name="Microsoft\.VCLibs\.140\.00\.Debug"[^/]*/>', ''
Set-Content "$layout\AppxManifest.xml" $manifest -Encoding UTF8

# App binaries
Copy-Item "$ilcDir\PocketTavern.UWP.exe"     "$layout\"
Copy-Item "$ilcDir\PocketTavern.UWP.dll"     "$layout\"
Copy-Item "$ilcDir\PocketTavern.UWP.xr.xml"  "$layout\"
Copy-Item "$ilcDir\resources.pri"            "$layout\"
Copy-Item "$ilcDir\clrcompression.dll"       "$layout\"
Copy-Item "$ilcDir\e_sqlite3.dll"            "$layout\"

# Runtime DLLs — keep in RuntimeDlls\ subfolder (NOT flattened to root)
Copy-Item "$ilcDir\RuntimeDlls\System.Private.CoreLib.dll" "$layout\RuntimeDlls\"
Copy-Item "$ilcDir\RuntimeDlls\clrjit.dll"                 "$layout\RuntimeDlls\"
Copy-Item "$ilcDir\RuntimeDlls\uwphost.dll"                "$layout\RuntimeDlls\"

# Assets (recursive — includes Presets, Themes, Extensions etc.)
Copy-Item "$ilcDir\Assets\*" "$layout\Assets\" -Recurse

Write-Host "Packing..."
& $makeappx pack /d $layout /p $appxOut /o
if ($LASTEXITCODE -ne 0) { Write-Error "MakeAppx failed"; exit 1 }

Write-Host "Signing..."
& $signtool sign /fd SHA256 /f $pfxPath /p DevOnly $appxOut
if ($LASTEXITCODE -ne 0) { Write-Error "SignTool failed"; exit 1 }

Write-Host "Done. APPX at: $appxOut"
