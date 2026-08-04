param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [Parameter(Mandatory = $true)]
    [string]$SourceUrl
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$destinationPath = [IO.Path]::GetFullPath((Join-Path $root $Destination))
$licensesPath = Join-Path $destinationPath 'Licenses'
$packageRoot = Join-Path $env:USERPROFILE '.nuget\packages'

$requiredPackageFiles = @(
    @{ Name = 'HidSharp 2.6.4 Lizenz'; Source = Join-Path $packageRoot 'hidsharp\2.6.4\LICENSE.txt'; Destination = 'HIDSHARP-LICENSE.txt' },
    @{ Name = 'SourceGear.sqlite3 3.50.4.5 Lizenz'; Source = Join-Path $packageRoot 'sourcegear.sqlite3\3.50.4.5\LICENSE.txt'; Destination = 'SOURCEGEAR-SQLITE3-LICENSE.txt' },
    @{ Name = 'System.IO.Ports 10.0.3 Drittanbieterhinweise'; Source = Join-Path $packageRoot 'system.io.ports\10.0.3\THIRD-PARTY-NOTICES.TXT'; Destination = 'DOTNET-THIRD-PARTY-NOTICES.txt' },
    @{ Name = 'System.IO.FileSystem.AccessControl 5.0.0 Lizenz'; Source = Join-Path $packageRoot 'system.io.filesystem.accesscontrol\5.0.0\LICENSE.TXT'; Destination = 'SYSTEM.IO.FILESYSTEM.ACCESSCONTROL-LICENSE.txt' },
    @{ Name = 'System.IO.FileSystem.AccessControl 5.0.0 Drittanbieterhinweise'; Source = Join-Path $packageRoot 'system.io.filesystem.accesscontrol\5.0.0\THIRD-PARTY-NOTICES.TXT'; Destination = 'SYSTEM.IO.FILESYSTEM.ACCESSCONTROL-THIRD-PARTY-NOTICES.txt' }
)

foreach ($entry in $requiredPackageFiles) {
    if (-not (Test-Path -LiteralPath $entry.Source)) {
        throw "Lizenzdatei fehlt für $($entry.Name): $($entry.Source)"
    }
}

New-Item -Path $licensesPath -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.txt') -Destination $destinationPath
foreach ($entry in $requiredPackageFiles) {
    Copy-Item -LiteralPath $entry.Source -Destination (Join-Path $licensesPath $entry.Destination)
}
Copy-Item -LiteralPath 'C:\Program Files\dotnet\LICENSE.txt' -Destination (Join-Path $licensesPath 'DOTNET-LICENSE.txt')

$licenses = @(
    @{
        Name = 'MPL-2.0.txt'
        Uri = 'https://www.mozilla.org/media/MPL/2.0/index.f75d2927d3c1.txt'
        Sha256 = '3f3d9e0024b1921b067d6f7f88deb4a60cbe7a78e76c64e3f1d7fc3b779b9d04'
    },
    @{
        Name = 'Apache-2.0.txt'
        Uri = 'https://www.apache.org/licenses/LICENSE-2.0.txt'
        Sha256 = 'cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30'
    }
)

foreach ($license in $licenses) {
    $target = Join-Path $licensesPath $license.Name
    Invoke-WebRequest -Uri $license.Uri -OutFile $target -UseBasicParsing
    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $license.Sha256) {
        throw "Lizenztext $($license.Name) hat eine unerwartete SHA-256-Prüfsumme."
    }
}

$sourceText = @"
PC Monitor 0.0.1-beta - Quellcode
=================================

Der vollständige korrespondierende Quellcode dieser Version einschließlich
der Build- und Installer-Skripte ist verfügbar unter:

$SourceUrl

PC Monitor steht unter der GNU General Public License Version 3. Der
Lizenztext befindet sich in der Datei LICENSE im Installationsverzeichnis.
"@
Set-Content -LiteralPath (Join-Path $destinationPath 'SOURCE-CODE.txt') -Value $sourceText -Encoding utf8
