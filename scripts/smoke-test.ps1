param([string]$Executable = '.\artifacts\installer-source\app\PC Monitor.exe')
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Executable)) { throw "Datei nicht gefunden: $Executable" }
$process = Start-Process $Executable -PassThru
Start-Sleep -Seconds 8
if ($process.HasExited) { throw "PC Monitor wurde unerwartet beendet. Exit-Code: $($process.ExitCode)" }
Stop-Process -Id $process.Id
Write-Host 'Smoke-Test erfolgreich: Anwendung startete und blieb aktiv.'
