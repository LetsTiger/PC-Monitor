# Bekannte Einschränkungen

Diese Liste gilt für PC Monitor `0.0.1-beta`.

## Plattform

- Windows 10 22H2 x64 ist das primäre Beta-Testziel.
- Windows 11 x64 kann funktionieren, ist für diese Version aber noch nicht vollständig als Testziel bestätigt.
- Die Anwendung ist nicht digital signiert. Windows SmartScreen kann deshalb beim Download oder Start warnen.

## FPS und PresentMon

- FPS- und Frametime-Daten benötigen die optionale PresentMon-2.5.1-Komponente.
- Für die PresentMon-Erfassung können erhöhte Rechte oder eine passende Mitgliedschaft in „Performance Log Users“ erforderlich sein.
- Wird die Rechteerhöhung abgelehnt oder PresentMon nicht installiert, funktionieren alle übrigen Monitoring- und Analysefunktionen weiterhin.
- Live-FPS werden in der Oberfläche derzeit nicht angezeigt; FPS- und Frametime-Daten werden nach Abschluss eines Runs ausgewertet.

## Analyse

- Spiel-, Prozess- und Bottleneck-Erkennung arbeiten heuristisch und können bei Launchern, Overlays, mehreren gleichzeitig gerenderten Anwendungen oder ungewöhnlichen Hardwarekonfigurationen falsch zuordnen.
- Sehr kurze Runs oder Runs mit wenigen Samples können eine geringere Analysequalität besitzen.
- Score und Empfehlungen sind Diagnosehilfen und keine Garantie für eine bestimmte Leistung nach Änderungen am System.

Wenn du ein reproduzierbares Problem findest, erstelle bitte ein Issue mit Version, Windows-Build, Reproduktionsschritten und dem erwarteten Verhalten.
