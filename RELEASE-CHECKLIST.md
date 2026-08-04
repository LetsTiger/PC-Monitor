# Release-Checkliste

Diese Checkliste gilt für `0.0.1-beta` und bleibt im Repository.

## Quellstand

- [ ] Arbeitsbaum enthält nur beabsichtigte Änderungen
- [ ] Versionsangaben in Projekt, Installer und Dokumentation stimmen überein
- [ ] Keine Zugangsdaten, lokalen Pfade, Logs, Datenbanken oder Diagnosepakete enthalten
- [ ] Rechte an Quellcode, Logo und weiteren Assets geklärt
- [ ] Produktname auf mögliche Marken- oder Namenskonflikte geprüft

## Automatische Validierung

- [ ] `scripts/test-release-metadata.ps1` erfolgreich
- [ ] Restore und Release-Build ohne Fehler oder Warnungen
- [ ] Alle automatisierten Tests erfolgreich
- [ ] NuGet-Sicherheitsprüfung ohne bekannte Schwachstellen
- [ ] Self-contained Publish für `win-x64` erfolgreich

## Anwendung

- [ ] Start und Beenden ohne Administratorrechte geprüft
- [ ] Einfacher und erweiterter Modus geprüft
- [ ] Dark- und Light-Theme einschließlich Dropdowns geprüft
- [ ] Skalierung bei 100 %, 125 % und 150 % geprüft
- [ ] Run gestartet, gestoppt, gewechselt, umbenannt und gelöscht
- [ ] CSV- und JSON-Export geprüft
- [ ] Score, Bottleneck, Empfehlungen und technische Analyse plausibilisiert
- [ ] Gleichzeitiger Mehrfachstart und Recovery-Verhalten geprüft

## PresentMon

- [ ] Installer ohne PresentMon geprüft
- [ ] Installer mit PresentMon 2.5.1 geprüft
- [ ] Rechteerhöhung angenommen und abgelehnt
- [ ] Anwendung bleibt bei abgelehnter Erhöhung ohne FPS-Erfassung nutzbar
- [ ] FPS- und Frametime-Auswertung nach abgeschlossenem Run geprüft

## Installer und Datenschutz

- [ ] Neuinstallation auf sauberem Windows-10-22H2-x64-System geprüft
- [ ] Upgrade von einer vorherigen Installation geprüft
- [ ] Alte `PcMonitor.App.*`-Programmdateien werden beim Upgrade entfernt
- [ ] Deinstallation geprüft; Verhalten der Benutzerdaten dokumentiert
- [ ] Diagnosepaket enthält keine Datenbank und keine Messwertreihen
- [ ] GPLv3-, PresentMon-MIT- und Drittanbieterhinweise enthalten
- [ ] `SOURCE-CODE.txt` verweist auf den veröffentlichten Commit oder Tag

## Veröffentlichung

- [ ] `scripts/build-release.ps1 -SourceUrl <öffentliche-Quellcode-URL>` erfolgreich
- [ ] Installer `PC-Monitor-0.0.1-beta-win-x64-Setup.exe` vorhanden
- [ ] SHA-256-Datei erzeugt und geprüft
- [ ] Release enthält nur Installer und SHA-256-Datei
- [ ] Tag `v0.0.1-beta` liegt auf dem geprüften `main`-Commit
- [ ] GitHub Security Advisories/private Sicherheitsmeldungen aktiviert
