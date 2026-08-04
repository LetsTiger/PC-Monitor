$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

& (Join-Path $PSScriptRoot 'get-presentmon.ps1')

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($iscc) {
    $isccPath = $iscc.Source
} else {
    $isccPath = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $isccPath) { throw 'ISCC.exe wurde nicht gefunden. Installiere Inno Setup 6.' }

$requiredFiles = @(
    'artifacts\installer-source\app\PC Monitor.exe',
    'LICENSE',
    'Tools\PresentMon\PresentMon.exe',
    'Tools\PresentMon\LICENSE.txt',
    'Tools\PresentMon\THIRD_PARTY.txt'
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $requiredFile))) {
        throw "Erforderliche Installer-Datei fehlt: $requiredFile"
    }
}

& $isccPath installer\PCMonitor.iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup wurde mit Exit-Code $LASTEXITCODE beendet." }
