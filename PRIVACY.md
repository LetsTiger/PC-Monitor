# Datenschutz

PC Monitor ist als lokale Desktop-Anwendung konzipiert. Es gibt keine Telemetrie, keine Nutzungsanalyse, kein Benutzerkonto und keine automatischen Uploads.

## Lokal verarbeitete Daten

Je nach aktivierten Collectors können unter anderem folgende Daten lokal verarbeitet und in SQLite gespeichert werden:

- System-, CPU-, RAM-, GPU- und Datenträgerwerte
- Prozessnamen, Prozesskennungen und Programmpfade
- Netzwerk-Latenz, Jitter, Paketverlust und Ausfälle
- optionale FPS- und Frametime-Daten über PresentMon
- Run-Namen, Zeitpunkte, Dauer, Analysen und Empfehlungen

Die Daten dienen ausschließlich der lokalen Anzeige, Historie, Analyse und dem vom Benutzer ausgelösten Export.

## Netzwerkzugriffe

Netzwerkzugriffe erfolgen ausschließlich für die in der Anwendung konfigurierten Ping-Messungen. PC Monitor kontaktiert keinen Telemetrie-, Analyse- oder Upload-Dienst.

## Diagnosepakete

Diagnosepakete werden nur auf ausdrückliche Benutzeraktion lokal erstellt. Sie können Logs, anonymisierte Einstellungen, Windows- und Hardwareinformationen, PresentMon-Version, Run-Namen, Prozessnamen, Programmpfade, Stacktraces sowie Tabellen- und Sample-Anzahlen enthalten.

Messwertreihen und die SQLite-Datenbank werden nicht in ein Diagnosepaket aufgenommen. Vor einer Weitergabe sollte das Paket trotzdem geprüft werden. Der Benutzer entscheidet selbst, ob und an wen es übermittelt wird.

## Löschen und Deinstallation

Messdaten und Einstellungen liegen im lokalen Benutzerprofil. Die Deinstallation kann Benutzerdaten bewusst zurücklassen, damit sie bei einer erneuten Installation erhalten bleiben. Diese Daten können anschließend manuell aus dem PC-Monitor-Anwendungsdatenordner unter `%LocalAppData%` gelöscht werden.
