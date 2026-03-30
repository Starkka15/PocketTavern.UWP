Get-AppxPackage | Where-Object { $_.Name -like "*VCLibs*" } | Select-Object Name, Version, Architecture | Format-Table -AutoSize
