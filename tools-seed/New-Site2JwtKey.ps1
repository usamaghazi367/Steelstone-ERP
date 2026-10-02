# Generate a random Jwt:Key for appsettings.Production.json (site2)
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
Write-Host "Jwt:Key (copy into site2 appsettings.Production.json):"
Write-Host $key
