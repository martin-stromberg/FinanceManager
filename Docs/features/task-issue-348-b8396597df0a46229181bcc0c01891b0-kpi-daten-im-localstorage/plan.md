# Umsetzungsplan: KPI-Daten im LocalStorage

## Ziel

Auf der Startseite sollen Home-KPI-Daten im Browser-LocalStorage zwischengespeichert werden, wenn der Benutzer dies in den Profileinstellungen aktiviert hat. Beim nächsten Besuch werden die gespeicherten Daten sofort angezeigt, während im Hintergrund frische Daten geladen werden. Bei Deaktivierung der Funktion werden alle zwischengespeicherten KPI-Daten aus dem LocalStorage entfernt.

## Annahmen

- Die Anwendung läuft im Blazor-`InteractiveServer`-Modus mit Prerendern. LocalStorage-Abfragen sind daher erst in/ nach `OnAfterRenderAsync` möglich.
- Die Funktion ist rein clientseitig und opt-in.
- Es werden nur die eigenen, aktuell angemeldeten Benutzerdaten im LocalStorage gehalten; keine Autorisierungsüberschreitung.

## Bausteine

### 1. Datenbank / Backend

- `User` um `CacheKpisInLocalStorage` (bool, default false) erweitern.
- `UserProfileSettingsDto` und `UserProfileSettingsUpdateRequest` um `CacheKpisInLocalStorage` erweitern.
- `UserSettingsController` GET `profile` und PUT `profile` anpassen.
- EF-Migration `AddKpiLocalStorageCacheProfileSetting` erzeugen.

### 2. Cache-Service

Neues Interface und Service in `FinanceManager.Web/Services/KpiLocalStorage/`:

```
IKpiLocalStorageCache
- GetAsync<T>(string key)
- SetAsync<T>(string key, T value)
- RemoveAsync(string key)
- RemoveAllByPrefixAsync(string prefix)
- SetEnabled(bool enabled)
- bool Enabled { get; }
```

Implementierung via `IJSRuntime` mit `localStorage.getItem`, `setItem`, `removeItem` und einem Prefix `fm.kpi.{userId}.*` (oder `fm.kpi.*`, wenn kein userId praktikabel). Vor jeder Schreiboperation wird geprüft, ob `Enabled == true`; bei `false` wird nichts geschrieben und `RemoveAllByPrefixAsync` gerufen.

### 3. Profil-UI

- `SetupProfileViewModel` um `CacheKpisInLocalStorage` erweitern.
- `SetupProfileTab.razor`: Checkbox und Hilfetext einfügen.
- Neuen Lokalisierungsschlüssel in `Pages.resx` / `Pages.de.resx` / `Pages.en.resx` ergänzen.
- Beim Speichern mit `CacheKpisInLocalStorage == false` wird via `IKpiLocalStorageCache.RemoveAllByPrefixAsync` der gesamte KPI-Cache geleert.

### 4. Startseite: Setting-Propagierung

- `Home.razor` lädt in `OnInitializedAsync` `UserSettings_GetProfileAsync` und erzeugt eine `KpiLocalStorageContext` (`Enabled`, `Cache`, optional `UserId`).
- `<CascadingValue Value="_kpiContext">` umschließt `<HomeKpiGrid>`.

### 5. KPI-Kacheln: Lade-/Cache-Logik

#### 5.1 KPI-Liste (`HomeKpiGrid`)

- `OnAfterRenderAsync(firstRender)`: Wenn `Enabled`, zuerst `IKpiLocalStorageCache.GetAsync<List<HomeKpiDto>>("home.kpi.list")` versuchen, `_kpis` setzen und `StateHasChanged`.
- Danach `Api.HomeKpis_ListAsync` aufrufen, `_kpis` aktualisieren und bei `Enabled` `SetAsync`.

#### 5.2 Monatsbudget (`MonthlyBudgetKpi`)

- Neuer `[Parameter] public string? CacheKey { get; set; }`.
- `MonthlyBudgetKpiViewModel` um `RestoreFromSnapshot` und `CreateSnapshot` bzw. `LoadAsync(...)`-Overload mit `IKpiLocalStorageCache` erweitern.
- In `LoadViewModelAsync` vor dem API-Aufruf Cache wiederherstellen und sofort anzeigen, nach API-Aufruf neuen Stand speichern.

#### 5.3 Einfache Zahlen (`NumericKpi`)

- Neuer `[Parameter] public string? CacheKey { get; set; }` und Cascading `KpiLocalStorageContext`.
- Von `OnInitializedAsync` auf `OnAfterRenderAsync` (bzw. `OnAfterRenderAsync` ergänzend) umstellen, damit LocalStorage lesbar ist.
- Bei `Enabled` zuerst gecachten Wert anzeigen, dann `Load` aufrufen, danach speichern.

#### 5.4 Balkendiagramme (`AggregateBarChart`)

- Neuer `[Parameter] public string? CacheKey { get; set; }`.
- In `OnParametersSetAsync` / `LoadAsync` gecachte `TimeSeriesPoint`-Liste vor dem API-Aufruf in `ViewModel.Data` übergeben (neue ViewModel-Methode zum Setzen von gecachten Daten), danach speichern.
- Optional / zeitlich nachrangig behandeln, falls sich der Aufwand erhöht.

### 6. Tests

#### Unit / Komponenten-Tests (bUnit)

- `KpiLocalStorageCacheTests`: Mock von `IJSRuntime`; Lesen, Schreiben, `RemoveAllByPrefixAsync`, `Enabled == false` blockiert Schreiben.
- `HomeKpiGridCacheTests`:
  - Happy path: gecachte Liste wird gerendert, bevor API antwortet; API-Update ersetzt die Liste danach.
  - Negativ: `Enabled == false` -> kein `IJSRuntime`-Lesezugriff, kein Schreiben.
  - Edge: fehlender oder korrupt gecachter Wert wird ignoriert, API als Fallback.
- `MonthlyBudgetKpiCacheTests`:
  - Happy path: gecachtes DTO wird sofort gerendert, nach API-Aufruf aktualisiert.
  - Edge: Cache deaktiviert -> kein JS-Inter op.
- `NumericKpiCacheTests`:
  - Happy path: gecachter Wert sofort sichtbar, danach neuer Wert.
  - Negativ: fehlender Cache -> `Load` wird trotzdem aufgerufen.
- `SetupProfileTabCacheTests`:
  - Deaktivieren löst `RemoveAllByPrefixAsync` aus.

#### Integrationstests

- `UserSettingsControllerProfileTests` (oder in bestehende `ApiClientHomeKpisTests`):
  - `CacheKpisInLocalStorage` wird beim Speichern in der DB persistiert und beim Lesen zurückgegeben.

#### E2E-Tests (Playwright)

- `KpiLocalStorageProfileTests`:
  - Happy path: Aktivieren im Profil, Startseite besuchen, LocalStorage enthält `fm.kpi.*` Einträge nach dem Laden.
  - Negativ: Deaktivieren im Profil, LocalStorage-Einträge verschwinden.
  - Edge: Kachel-Informationen erscheinen sofort (keine leeren Kacheln) bei wiederholtem Besuch der Startseite.

## Offene Punkte

Keine.

## Sicherheit & Sichtbarkeit

- Das Flag ist Teil des eigenen Benutzerprofils; es gibt keine Sichtbarkeit gegenüber anderen Benutzern.
- LocalStorage-Daten werden mit einem Anwendungs-Prefix gespeichert; ein `RemoveAllByPrefixAsync` löscht nur diese.
- JS-Interop-Aufrufe werden in `OnAfterRenderAsync` bzw. interaktivem Rendering-Kontext ausgeführt, um Prerender-Fehler zu vermeiden.

## Releasenotes / Hilfe

- `Docs/help/de/fxxx.md` und `Docs/help/en/fxxx.md` mit Kurzanleitung zur neuen Profiloption ergänzen.
- `README.md` ggf. mit Hinweis auf neue Startseiten-Performance-Option.
