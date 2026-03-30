# Run as Administrator
$binDir   = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin\x64\Debug'
$nuget    = 'C:\Users\Starkka15\.nuget\packages'
$uwpPkgs  = 'C:\Program Files (x86)\Microsoft SDKs\UWPNuGetPackages'
$winkits  = 'C:\Program Files (x86)\Windows Kits\10'
$makeappx = "$winkits\bin\10.0.16299.0\x64\makeappx.exe"
$signtool = "$winkits\bin\10.0.16299.0\x64\signtool.exe"
$pfxPath  = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\PocketTavern_TemporaryKey.pfx'
$appxOut  = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.appx'
$layout   = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\AppXLayout'

# Clean and create layout folder
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item $layout -ItemType Directory | Out-Null
New-Item "$layout\Assets"      -ItemType Directory | Out-Null
New-Item "$layout\Views"       -ItemType Directory | Out-Null
New-Item "$layout\entrypoint"  -ItemType Directory | Out-Null
New-Item "$layout\WinMetadata" -ItemType Directory | Out-Null

Write-Host "Building layout..."

# Manifest (from Core\ - has VCLibs.Debug dependency)
Copy-Item "$binDir\Core\AppxManifest.xml" "$layout\AppxManifest.xml"

# Executables - native stub at root, managed IL in entrypoint\
Copy-Item "$binDir\Core\PocketTavern.UWP.exe" "$layout\PocketTavern.UWP.exe"
Copy-Item "$binDir\PocketTavern.UWP.exe"      "$layout\entrypoint\PocketTavern.UWP.exe"

# Managed DLLs
Copy-Item "$nuget\newtonsoft.json\13.0.3\lib\netstandard2.0\Newtonsoft.Json.dll" "$layout\"
Copy-Item "$uwpPkgs\runtime.win10-x64.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x64\lib\uap10.0.15138\System.Runtime.dll" "$layout\"
Copy-Item "$nuget\sqlite-net-pcl\1.9.172\lib\netstandard2.0\SQLite-net.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.bundle_green\2.1.8\lib\netstandard2.0\SQLitePCLRaw.batteries_v2.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.core\2.1.8\lib\netstandard2.0\SQLitePCLRaw.core.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.lib.e_sqlite3\2.1.8\runtimes\win10-x64\nativeassets\uap10.0\e_sqlite3.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.provider.e_sqlite3\2.1.8\lib\netstandard2.0\SQLitePCLRaw.provider.e_sqlite3.dll" "$layout\"
Copy-Item "$nuget\system.runtime.compilerservices.unsafe\4.5.2\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll" "$layout\"

# CoreRuntime managed DLLs (framework package probe path broken on Win10 19041 - include directly)
Copy-Item 'C:\Temp\CoreRuntime\System.Private.CoreLib.dll' "$layout\"
Copy-Item 'C:\Temp\CoreRuntime\clrjit.dll'                 "$layout\"
Copy-Item 'C:\Temp\CoreRuntime\uwphost.dll'                "$layout\"
Copy-Item 'C:\Temp\CoreRuntime\clrcompression.dll'         "$layout\" -ErrorAction SilentlyContinue

# CoreFramework.Debug BCL facades (all of them)
Get-ChildItem 'C:\Temp\CoreFrameworkDebug\*.dll' | Copy-Item -Destination "$layout\"

# Assets
Copy-Item "$binDir\Assets\StoreLogo.png"         "$layout\Assets\"
Copy-Item "$binDir\Assets\Square150x150Logo.png"  "$layout\Assets\"
Copy-Item "$binDir\Assets\Square44x44Logo.png"    "$layout\Assets\"
Copy-Item "$binDir\Assets\Wide310x150Logo.png"    "$layout\Assets\"
Copy-Item "$binDir\Assets\SplashScreen.png"       "$layout\Assets\"

# XAML compiled binaries and resources
Copy-Item "$binDir\App.xbf"                   "$layout\"
Copy-Item "$binDir\PocketTavern.UWP.xr.xml"   "$layout\"
Copy-Item "$binDir\resources.pri"             "$layout\"
Copy-Item "$binDir\Views\*.xbf"               "$layout\Views\"

# Windows metadata
Copy-Item "$winkits\UnionMetadata\10.0.16299.0\Windows.winmd" "$layout\WinMetadata\"

Write-Host "Packing..."
& $makeappx pack /d $layout /p $appxOut /o
if ($LASTEXITCODE -ne 0) { Write-Error "MakeAppx failed"; exit 1 }

Write-Host "Signing..."
& $signtool sign /fd SHA256 /f $pfxPath /p DevOnly $appxOut
if ($LASTEXITCODE -ne 0) { Write-Error "SignTool failed"; exit 1 }

Write-Host "Installing..."
Add-AppxPackage -Path $appxOut -ForceApplicationShutdown
Write-Host "Done."
