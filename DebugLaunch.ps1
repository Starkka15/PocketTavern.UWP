$cdb    = 'C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe'
$logOut = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\cdb_output.txt'

Write-Host "Launching under cdb..."
& $cdb -plmPackage PocketTavern.UWP_1b7q5sa4bwdpa -plmApp App -c "g;q" *> $logOut
Write-Host "Done. Output:"
Get-Content $logOut
