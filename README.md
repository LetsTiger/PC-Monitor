# PC Monitor

PC Monitor ist eine lokale Windows-Anwendung zur Aufzeichnung und verständlichen Auswertung von System-, Hardware-, Prozess-, Netzwerk- und optionalen FPS-Daten.

> **Status:** `0.0.1-beta` · Zielsystem: Windows 10 22H2 x64 · Oberfläche: Deutsch

## Was PC Monitor kann

- CPU-, RAM-, GPU-, Datenträger-, Prozess- und Netzwerkdaten lokal erfassen
- Runs speichern, vergleichen, umbenennen und als CSV oder JSON exportieren
- Performance Score, Bottleneck, Stutter und konkrete Empfehlungen berechnen
- FPS und Frametime optional über PresentMon 2.5.1 erfassen
- Diagnosepakete lokal und ohne automatischen Upload erstellen
- zwischen einer kompakten und einer technischen Ansicht wechseln
- helles und dunkles Farbschema verwenden

## Einfacher und erweiterter Modus

Der **einfache Modus** konzentriert sich auf den Ablauf: Messung starten, Ergebnis verstehen und empfohlene nächste Schritte prüfen.

Der **erweiterte Modus** zeigt zusätzlich Live-Rohwerte, vollständige Messdaten, Hardware- und Prozessanalysen, Ereignis-Timelines, Vergleiche und Performance-Trends.

## Installation

PC Monitor wird ausschließlich als self-contained Windows-x64-Installer veröffentlicht. Für die Anwendung ist keine separate .NET-Installation notwendig.

1. Lade `PC-Monitor-0.0.1-beta-win-x64-Setup.exe` aus dem neuesten GitHub Release herunter.
2. Prüfe bei Bedarf die mitgelieferte SHA-256-Datei.
3. Wähle im Installer, ob PresentMon für FPS- und Frametime-Messungen installiert werden soll.
4. Starte PC Monitor ohne Administratorrechte.

Administratorrechte werden nur angefordert, wenn die optionale PresentMon-Erfassung verwendet wird. Wird die Anfrage abgelehnt, bleibt PC Monitor ohne FPS-Erfassung nutzbar.

Da die Beta noch nicht digital signiert ist, kann Windows SmartScreen eine Warnung anzeigen.

## Datenschutz

Alle Messungen und Auswertungen bleiben lokal. PC Monitor enthält keine Telemetrie und führt keine automatischen Uploads aus. Netzwerkzugriffe erfolgen nur für konfigurierte Ping-Messungen.

Weitere Einzelheiten stehen in [PRIVACY.md](PRIVACY.md).

## Lokale Entwicklung

Voraussetzungen:

- Windows 10/11 x64
- .NET 10 SDK
- optional Inno Setup 6 für den Installer

```powershell
dotnet restore .\PcMonitor.slnx -r win-x64
dotnet build .\PcMonitor.slnx -c Release --no-restore
dotnet test .\PcMonitor.Tests\PcMonitor.Tests.csproj -c Release --no-build
```

Die Anwendung lokal starten:

```powershell
dotnet run --project .\PcMonitor.App\PcMonitor.App.csproj -c Debug
```

Einen vollständigen Release inklusive Installer bauen:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

PresentMon wird beim Release-Build als optionale Installer-Komponente bezogen und nicht als Binary im Repository gespeichert.

## Projektstruktur

| Projekt | Aufgabe |
|---|---|
| `PcMonitor.App` | WPF-Oberfläche, ViewModels und Desktop-Integration |
| `PcMonitor.Application` | Anwendungslogik, Analysen und Monitoring-Abläufe |
| `PcMonitor.Collectors` | Windows-, Hardware- und PresentMon-Collector |
| `PcMonitor.Core` | Modelle, Schnittstellen und gemeinsame Domänenlogik |
| `PcMonitor.Infrastructure` | SQLite-Speicherung und Einstellungen |
| `PcMonitor.Tests` | automatisierte Tests |

## Grenzen der Beta

Die wichtigsten bekannten Einschränkungen sind in [KNOWN-ISSUES.md](KNOWN-ISSUES.md) dokumentiert. Bottleneck- und Spieleerkennung arbeiten heuristisch; Ergebnisse sollten insbesondere in ungewöhnlichen Systemkonfigurationen mit den technischen Messdaten abgeglichen werden.

## Mitwirken und Sicherheit

- Beiträge: [CONTRIBUTING.md](CONTRIBUTING.md)
- Sicherheitsmeldungen: [SECURITY.md](SECURITY.md)
- Release-Prüfung: [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md)

## Lizenz und Credits

PC Monitor wird von **LetsTiger** entwickelt und steht unter der [GNU General Public License v3.0](LICENSE).

PresentMon wird optional und separat unter der MIT-Lizenz verteilt. Weitere Abhängigkeiten und Lizenzhinweise sind in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) aufgeführt.
