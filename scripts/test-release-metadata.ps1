$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

[xml]$project = Get-Content -LiteralPath (Join-Path $root 'PcMonitor.App\PcMonitor.App.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'In PcMonitor.App.csproj fehlt die Version.'
}

$iss = Get-Content -LiteralPath (Join-Path $root 'installer\PCMonitor.iss') -Raw
$checks = [ordered]@{
    'Inno-Setup-Version' = $iss -match ('(?m)^#define MyAppVersion "' + [regex]::Escape($version) + '"\r?$')
    'Inno-Setup-App-Name' = $iss -match '(?m)^#define MyAppName "PC Monitor"\r?$'
    'Inno-Setup-Herausgeber' = $iss -match '(?m)^#define MyAppPublisher "LetsTiger"\r?$'
    'Inno-Setup-Ausgabename' = $iss -match 'OutputBaseFilename=PC-Monitor-\{#MyAppVersion\}-win-x64-Setup'
    'PresentMon-Version' = $iss -match 'PresentMon 2\.5\.1'
}

$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
$checklist = Get-Content -LiteralPath (Join-Path $root 'RELEASE-CHECKLIST.md') -Raw
$checks['README-Version'] = $readme -match [regex]::Escape($version)
$checks['README-Installername'] = $readme -match [regex]::Escape("PC-Monitor-$version-win-x64-Setup.exe")
$checks['Checkliste-Version'] = $checklist -match [regex]::Escape($version)
$checks['GPLv3-Lizenz'] = Test-Path -LiteralPath (Join-Path $root 'LICENSE')
$checks['Drittanbieterhinweise'] = Test-Path -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.txt')
$checks['PresentMon-Lizenz'] = Test-Path -LiteralPath (Join-Path $root 'Tools\PresentMon\LICENSE.txt')
$checks['PresentMon-Drittanbieterhinweise'] = Test-Path -LiteralPath (Join-Path $root 'Tools\PresentMon\THIRD_PARTY.txt')

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object Key)
if ($failed.Count -gt 0) {
    throw "Inkonsistente Release-Metadaten: $($failed -join ', ')"
}

Write-Host "Release-Metadaten konsistent: PC Monitor $version"
