# Run as Administrator
$binDir   = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin\x64\Debug'
$nuget    = 'C:\Users\Starkka15\.nuget\packages'
$uwpPkgs  = 'C:\Program Files (x86)\Microsoft SDKs\UWPNuGetPackages'
$winkits  = 'C:\Program Files (x86)\Windows Kits\10'
$layout   = "$binDir\AppX"

# Clean and create layout folder
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item $layout -ItemType Directory | Out-Null
New-Item "$layout\Assets"      -ItemType Directory | Out-Null
New-Item "$layout\Views"       -ItemType Directory | Out-Null
New-Item "$layout\entrypoint"  -ItemType Directory | Out-Null
New-Item "$layout\WinMetadata" -ItemType Directory | Out-Null

Write-Host "Building layout..."

# Manifest (Core\AppxManifest.xml - has VCLibs.Debug dependency)
Copy-Item "$binDir\Core\AppxManifest.xml" "$layout\AppxManifest.xml"

# Executables
Copy-Item "$binDir\Core\PocketTavern.UWP.exe" "$layout\PocketTavern.UWP.exe"
Copy-Item "$binDir\PocketTavern.UWP.exe"      "$layout\entrypoint\PocketTavern.UWP.exe"

# CoreRuntime assemblies (probe path for framework packages not working on Win10 19041)
$coreRuntime = 'C:\Temp\CoreRuntime'
Copy-Item "$coreRuntime\System.Private.CoreLib.dll" "$layout\"
Copy-Item "$coreRuntime\clrjit.dll"                 "$layout\"
Copy-Item "$coreRuntime\uwphost.dll"                "$layout\"

# CoreFramework.Debug assemblies - copy all of them
$coreFramework = 'C:\Temp\CoreFrameworkDebug'
Get-ChildItem "$coreFramework\*.dll" | Copy-Item -Destination "$layout\"

# Managed DLLs
Copy-Item "$nuget\newtonsoft.json\13.0.3\lib\netstandard2.0\Newtonsoft.Json.dll" "$layout\"
Copy-Item "$uwpPkgs\runtime.win10-x64.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x64\lib\uap10.0.15138\System.Runtime.dll" "$layout\"
Copy-Item "$nuget\sqlite-net-pcl\1.9.172\lib\netstandard2.0\SQLite-net.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.bundle_green\2.1.8\lib\netstandard2.0\SQLitePCLRaw.batteries_v2.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.core\2.1.8\lib\netstandard2.0\SQLitePCLRaw.core.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.lib.e_sqlite3\2.1.8\runtimes\win10-x64\nativeassets\uap10.0\e_sqlite3.dll" "$layout\"
Copy-Item "$nuget\sqlitepclraw.provider.e_sqlite3\2.1.8\lib\netstandard2.0\SQLitePCLRaw.provider.e_sqlite3.dll" "$layout\"
Copy-Item "$nuget\system.runtime.compilerservices.unsafe\4.5.2\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll" "$layout\"

# Assets
Copy-Item "$binDir\Assets\StoreLogo.png"         "$layout\Assets\"
Copy-Item "$binDir\Assets\Square150x150Logo.png"  "$layout\Assets\"
Copy-Item "$binDir\Assets\Square44x44Logo.png"    "$layout\Assets\"
Copy-Item "$binDir\Assets\Wide310x150Logo.png"    "$layout\Assets\"
Copy-Item "$binDir\Assets\SplashScreen.png"       "$layout\Assets\"

# XAML + resources
Copy-Item "$binDir\App.xbf"                "$layout\"
Copy-Item "$binDir\PocketTavern.UWP.xr.xml" "$layout\"
Copy-Item "$binDir\resources.pri"          "$layout\"
Copy-Item "$binDir\Views\*.xbf"            "$layout\Views\"

# Windows metadata
Copy-Item "$winkits\UnionMetadata\10.0.16299.0\Windows.winmd" "$layout\WinMetadata\"

Write-Host "Granting AppContainer read access..."
# Grant ALL APPLICATION PACKAGES to layout folder and all parent directories
$paths = @(
    'C:\Users\Starkka15\source',
    'C:\Users\Starkka15\source\repos',
    'C:\Users\Starkka15\source\repos\PocketTavern.UWP',
    'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP',
    'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin',
    'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin\x64',
    'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\bin\x64\Debug',
    $layout
)
foreach ($p in $paths) {
    icacls $p /grant "ALL APPLICATION PACKAGES:(RX)" | Out-Null
}
# Recursively grant the layout folder itself
icacls $layout /grant "ALL APPLICATION PACKAGES:(OI)(CI)(RX)" /T | Out-Null
Write-Host "Permissions granted."

# Uninstall any previous version
$existing = Get-AppxPackage PocketTavern.UWP -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing previous install..."
    Remove-AppxPackage $existing.PackageFullName
}

Write-Host "Registering loose layout..."
Add-AppxPackage -Register "$layout\AppxManifest.xml"
Write-Host "Done. Launch with:"
Write-Host "  Start-Process 'shell:AppsFolder\PocketTavern.UWP_1b7q5sa4bwdpa!App'"
