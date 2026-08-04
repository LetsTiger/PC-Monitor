# Zu PC Monitor beitragen

Danke für dein Interesse an PC Monitor. Fehlerberichte, nachvollziehbare Verbesserungsvorschläge und fokussierte Pull Requests sind willkommen.

## Vor einem Beitrag

- Prüfe, ob bereits ein passendes Issue existiert.
- Beschreibe bei Fehlern Windows-Version, PC-Monitor-Version, Reproduktionsschritte und das beobachtete Verhalten.
- Veröffentliche keine Diagnosepakete oder Logs ungeprüft: Sie können Run-Namen, Prozessnamen und lokale Programmpfade enthalten.
- Sicherheitsprobleme gehören nicht in öffentliche Issues. Verwende stattdessen [SECURITY.md](SECURITY.md).

## Entwicklungsablauf

`main` enthält den freigabefähigen Stand. Änderungen entstehen in kurzlebigen Branches wie `feature/name`, `fix/name` oder `docs/name` und werden per Pull Request integriert.

Vor einem Pull Request:

```powershell
dotnet restore .\PcMonitor.slnx -r win-x64
dotnet build .\PcMonitor.slnx -c Release --no-restore
dotnet test .\PcMonitor.Tests\PcMonitor.Tests.csproj -c Release --no-build
```

Bei Änderungen am Installer zusätzlich:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

## Technische Leitlinien

- Benutzertexte und Dokumentation werden auf Deutsch geschrieben.
- Zielsystem der Beta ist Windows 10 22H2 x64.
- Nullable Reference Types müssen berücksichtigt werden.
- Vor Änderungen an öffentlichen APIs sind sämtliche Aufrufer zu prüfen.
- PresentMon bleibt optional und darf nicht in die normale Anwendung eingebettet werden.
- Die Anwendung startet ohne Administratorrechte; Erhöhung ist nur für aktivierte PresentMon-Erfassung zulässig.
- Keine Telemetrie, automatischen Uploads oder zusätzlichen Netzwerkzugriffe ergänzen.
- Bestehende Funktionen nicht ohne begründete und dokumentierte Migration entfernen.

## Pull Requests

Ein Pull Request sollte:

- genau ein zusammenhängendes Problem lösen,
- Zweck und sichtbare Auswirkung beschreiben,
- Tests oder nachvollziehbare manuelle Prüfschritte nennen,
- bei UI-Änderungen möglichst Screenshots enthalten,
- keine Build-Ausgaben, Datenbanken, Logs oder lokalen IDE-Dateien enthalten.

## Releases

Releases werden aus einem geprüften Commit auf `main` erstellt. Ein Tag wie `v0.0.1-beta` startet den Release-Workflow, der Build, Tests, Installer und SHA-256-Datei erneut erzeugt. Release-Tags werden nicht verschoben oder wiederverwendet.
