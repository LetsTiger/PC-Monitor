param(
    [switch]$SkipTests,
    [string]$SourceUrl
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$artifacts = Join-Path $root 'artifacts'
if (Test-Path -LiteralPath $artifacts) {
    Remove-Item -LiteralPath $artifacts -Recurse -Force
}
New-Item $artifacts -ItemType Directory | Out-Null

& (Join-Path $PSScriptRoot 'test-release-metadata.ps1')

dotnet clean PcMonitor.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet clean wurde mit Exit-Code $LASTEXITCODE beendet." }
dotnet restore PcMonitor.slnx -r win-x64
if ($LASTEXITCODE -ne 0) { throw "dotnet restore wurde mit Exit-Code $LASTEXITCODE beendet." }
dotnet build PcMonitor.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build wurde mit Exit-Code $LASTEXITCODE beendet." }
if (-not $SkipTests) {
    dotnet test PcMonitor.Tests/PcMonitor.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "dotnet test wurde mit Exit-Code $LASTEXITCODE beendet." }
}

$installerApp = Join-Path $artifacts 'installer-source\app'
dotnet publish PcMonitor.App/PcMonitor.App.csproj -c Release -r win-x64 --self-contained true --no-restore -o $installerApp /p:IncludePresentMon=false /p:DebugSymbols=false /p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw "dotnet publish wurde mit Exit-Code $LASTEXITCODE beendet." }
Copy-Item LICENSE $installerApp
Copy-Item README.md $installerApp
Copy-Item PRIVACY.md $installerApp

if ([string]::IsNullOrWhiteSpace($SourceUrl)) {
    $remoteUrl = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($remoteUrl)) {
        $SourceUrl = $remoteUrl -replace '\.git$', ''
    } else {
        throw 'Eine Quellcode-URL fehlt. Übergib -SourceUrl oder konfiguriere den Git-Remote origin.'
    }
}

& (Join-Path $PSScriptRoot 'prepare-legal-files.ps1') -Destination 'artifacts\installer-source\app' -SourceUrl $SourceUrl

& (Join-Path $PSScriptRoot 'build-installer.ps1')
$installerPath = Join-Path $artifacts 'PC-Monitor-0.0.1-beta-win-x64-Setup.exe'
$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$installerPath.sha256.txt"
Set-Content -LiteralPath $checksumPath -Value "$installerHash  $(Split-Path -Leaf $installerPath)" -Encoding ascii

Write-Host "Release erstellt: $installerPath"
Write-Host "SHA-256: $installerHash"
