# Run as Administrator
$pfxPath = 'C:\Users\Starkka15\source\repos\PocketTavern.UWP\PocketTavern.UWP\PocketTavern_TemporaryKey.pfx'
$password = ConvertTo-SecureString -String 'DevOnly' -Force -AsPlainText
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
    $pfxPath, $password,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::MachineKeySet
)
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPeople', 'LocalMachine')
$store.Open('ReadWrite')
$store.Add($cert)
$store.Close()
Write-Host "Imported: $($cert.Subject) [$($cert.Thumbprint)]"
