# Detail: Service-Autocomplete

## Bestehende plattformspezifische Logik

`FinanceManager.Web/Services/Updates/UpdateServiceResolver.cs` entscheidet in `Resolve` zwischen Windows und Linux:

- Windows-Pfad ab Zeile 35
- Linux-Pfad ab Zeile 62

Wenn `ServiceName` gesetzt ist, wird dieser validiert und direkt verwendet. Unter Windows kann zusaetzlich `ExecutablePath` als Fallback genutzt werden (Zeilen 42 bis 45). Ohne expliziten Wert versucht der Resolver, den Dienst des aktuellen Prozesses zu erkennen:

- `FindWindowsServicesForCurrentProcess()` ab Zeile 131
- `FindLinuxServicesForCurrentProcess()` ab Zeile 168

Die Windows-Erkennung nutzt `sc.exe queryex type= service state= all` in Zeile 140 und filtert auf die aktuelle Process-ID. Die Linux-Erkennung liest zunaechst `/proc/self/cgroup` und nutzt danach `systemctl status {ProcessId}` ab Zeile 183.

## Fehlende Funktion fuer Autocomplete

Die vorhandene Probe liefert nur Dienste, die dem aktuellen Prozess entsprechen. Fuer Autocomplete werden dagegen Vorschlaege aus den Systemdiensten benoetigt. Dafuer fehlt aktuell:

- eine Methode zum Auflisten von Service-Namen unabhaengig von der aktuellen Process-ID,
- ein API-Endpunkt fuer die UI,
- eine ApiClient-Methode,
- UI-Logik in `SetupUpdateTab.razor` fuer Vorschlagsanzeige und Auswahl.

## Naheliegende Erweiterung

Eine kleine, getrennte Schnittstelle ist sinnvoller als die bestehende Resolver-Logik zu ueberladen:

- `IUpdateServiceCatalog` oder Erweiterung von `IUpdateServiceProbe`
- Methode `ListServiceNames(string? query, int take, CancellationToken ct)`
- Windows: `sc.exe query type= service state= all` oder PowerShell/ServiceController nur wenn plattformvertraeglich.
- Linux: `systemctl list-units --type=service --all --no-legend --no-pager` und robuste Extraktion von `*.service`.

Die API sollte nur fuer Admins erreichbar sein, analog zum bestehenden `UpdateController`.

## UI-Integration

Das Servicename-Feld in `SetupUpdateTab.razor` steht aktuell als normales Input in Zeile 49. Fuer Autocomplete kann entweder:

- ein HTML-`datalist` verwendet werden, wenn einfache Vorschlaege reichen,
- oder eine kleine Blazor-Overlay-Liste analog zu Lookup-Feldern in `GenericCardPage.razor` gebaut werden.

Da die Anforderung nur Autocomplete-Vorschlaege fordert, ist `datalist` wahrscheinlich ausreichend und risikoarm. Bei serverseitiger Suche sollte das Feld bei Fokus oder Eingabe Vorschlaege aus dem neuen Endpoint laden.

## Fehlerverhalten

Die Dienstermittlung muss ohne Plattformfehler funktionieren. Die bestehende Probe faengt Prozess-/Kommando-Fehler bereits ab und gibt leere Listen zurueck. Dieses Verhalten sollte fuer die neue Katalog-Funktion uebernommen werden: nicht unterstuetzte Plattform oder fehlendes Tool ergibt eine leere Vorschlagsliste, keinen UI-Fehler.
