param([switch]$Force)

$ErrorActionPreference = 'Stop'
$version = '2.5.1'
$expectedSha256 = '9bec3083069f58f911e6a512f4806db51a27bd096103087bc1d05ef54c80a191'
$downloadUri = "https://github.com/GameTechDev/PresentMon/releases/download/v$version/PresentMon-$version-x64.exe"
$root = Split-Path -Parent $PSScriptRoot
$destinationDirectory = Join-Path $root 'Tools\PresentMon'
$destinationPath = Join-Path $destinationDirectory 'PresentMon.exe'

if ((Test-Path -LiteralPath $destinationPath) -and -not $Force) {
    $existingHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($existingHash -eq $expectedSha256) {
        Write-Host "PresentMon $version ist bereits vorhanden und geprüft."
        return
    }
}

New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
$temporaryPath = Join-Path $destinationDirectory "PresentMon.$([Guid]::NewGuid().ToString('N')).download"

try {
    Write-Host "PresentMon $version wird von der offiziellen Release-Quelle heruntergeladen."
    Invoke-WebRequest -Uri $downloadUri -OutFile $temporaryPath -UseBasicParsing

    $downloadedHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($downloadedHash -ne $expectedSha256) {
        throw "Die SHA-256-Prüfsumme von PresentMon stimmt nicht. Erwartet: $expectedSha256; erhalten: $downloadedHash"
    }

    Move-Item -LiteralPath $temporaryPath -Destination $destinationPath -Force
    Write-Host "PresentMon $version wurde heruntergeladen und per SHA-256 geprüft."
} finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}
