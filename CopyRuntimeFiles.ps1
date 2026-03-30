$dest = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\RuntimeDlls'
New-Item $dest -ItemType Directory -Force | Out-Null
Copy-Item 'C:\Temp\CoreRuntime\System.Private.CoreLib.dll' $dest
Copy-Item 'C:\Temp\CoreRuntime\clrjit.dll'                 $dest
Copy-Item 'C:\Temp\CoreRuntime\uwphost.dll'                $dest
Write-Host "Copied to $dest"
