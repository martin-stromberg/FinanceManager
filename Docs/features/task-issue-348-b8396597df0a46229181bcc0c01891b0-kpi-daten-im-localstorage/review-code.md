# Code-Review — KPI-Daten im LocalStorage

## Umfang

Geänderte bzw. neue Dateien für die optionale Zwischenspeicherung der Startseiten-KPIs im Browser-LocalStorage.

## Feststellungen

### Architektur und Datenschutz

- Der `KpiLocalStorageCache` ist ein scoped Web-Service, der ausschließlich über `IJSRuntime` auf `localStorage` zugreift.
- Schlüssel werden mit `fm.kpi.*` und optional der Benutzer-ID (`fm.kpi.{userId}.{key}`) gebildet, sodass Daten benutzerspezifisch abgegrenzt und gezielt alle Einträge einer Anwendung entfernt werden können.
- Deaktivierung der Profil-Einstellung löst sofort `RemoveAllAsync` aus und entfernt alle `fm.kpi.*`-Einträge des aktuellen Benutzers.
- `Legal.razor` und die Ressourcendateien `Legal.de.resx`/`Legal.en.resx` wurden ergänzt, um die clientseitige Speicherung transparent zu machen.

### Blazor Interactive Server / Prerendering

- `Home.razor` liest das Profil und setzt den Cache-Kontext in `OnAfterRenderAsync` (erstes Rendern), bevor `HomeKpiGrid` über JS-Interop auf den Cache zugreift.
- `HomeKpiGrid`, `MonthlyBudgetKpi` und `NumericKpi` führen Cache-Lesevorgänge ebenfalls in `OnAfterRenderAsync` aus.
- JS-Interop-Aufrufe im Cache selbst werden nur durchführende Komponenten ausgelöst, die bereits interaktiv sind.

### Fehlerverhalten

- `KpiLocalStorageCache.GetAsync` fängt Deserialisierungs- und JS-Fehler und liefert `default` (keine Exception, Fallback auf API).
- `SetAsync` und `RemoveAllAsync` ignorieren Fehler, damit ein Browser-spezifisches `localStorage`-Problem den API-Fluss nicht blockiert.
- `MonthlyBudgetKpiViewModel.LoadAsync` setzt `DataLoaded` nicht mehr zurück, damit gecachte Werte während einer Hintergrund-Aktualisierung sichtbar bleiben. Fehler führen anschließend über `MarkLoadFailed` wieder zur Lade-/Fehleranzeige.

### Tests

- Unit-Tests für `KpiLocalStorageCache` (Lesen, Schreiben, Löschen, Deaktivierung, Benutzer-Scope, fehlerhaftes JSON).
- bUnit-Tests für `NumericKpi` mit/ohne Cache.
- `SetupProfileViewModel`-Test für Cache-Löschung bei Deaktivierung.
- Integrationstests für `UserSettings` (GET/PUT der Einstellung, Default `false`).

### UI-Action-Handler

- `SetupProfileViewModel.RaiseUiActionRequested("DetectTimezone")` hat weiterhin den passenden `switch`-Handler in `SetupProfileTab.razor` (Zeile ~100).
- Es wurden keine neuen `RaiseUiActionRequested`-Aufrufe hinzugefügt.

### Offene / dokumentierte Punkte

- Keine E2E-Playwright-Tests für den tatsächlichen Browser-LocalStorage-Fluss (wurde im Plan als sinnvoll erachtet, ist aber nicht umgesetzt).
- Hilfe-Dokumentation und Release Notes wurden ergänzt.

## Ergebnis

Der Code ist aus Architektur-, Datenschutz- und Fehlerbehandlungssicht akzeptabel. Alle relevanten Unit-/Komponenten-/Integrationstests bestanden.
