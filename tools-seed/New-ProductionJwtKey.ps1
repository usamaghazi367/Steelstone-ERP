$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
Write-Host ([Convert]::ToBase64String($bytes))
