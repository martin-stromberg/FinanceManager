### Fachliche Zusammenfassung

Das Report-Dashboard wird um eine zusätzliche Vergleichsoption **Hochrechnung** erweitert. Diese Option ist nur aktivierbar, wenn die Berichtsauswahl ausschließlich die Buchungsart `PostingKind.Security` (UI: "Wertpapiere") enthält, und wird gemeinsam mit den übrigen Favoriten-Einstellungen persistiert. Bei aktivierter Hochrechnung liefert die Aggregation je Berichtspunkt neben `Amount` einen zusätzlichen Hochrechnungsbetrag, der aus bereits erfassten Dividenden des Betrachtungszeitraums plus erwarteten Dividenden aus der gleichen Vorjahresperiode berechnet wird.

Die Hochrechnung ist fachlich auf Wertpapier-Dividenden begrenzt. Dividenden der Vorjahresperiode gelten als bestätigt, wenn im aktuellen Betrachtungszeitraum ein äquivalenter Dividendeneintrag vorhanden ist; nicht bestätigte Vorjahresdividenden werden als erwartet in den Hochrechnungsbetrag einbezogen.

---

### Betroffene Klassen und Komponenten

#### Datenmodellklassen

- **`ReportFavorite`** (`FinanceManager.Domain.Reports.ReportFavorite`)
  - Neues persistiertes Flag für die Hochrechnung, z. B. `bool ProjectionEnabled` oder `bool CompareProjection`.
  - Erweiterung von Konstruktor, `Update(...)`, `ToBackupDto()` und `AssignBackupDto(...)`.
  - Erweiterung des Backup-Records `ReportFavoriteBackupDto`.

#### DTOs / Shared

- **`ReportAggregationQuery`** (`FinanceManager.Shared.Dtos.Reports.ReportAggregationQuery`)
  - Neues Query-Flag für die Hochrechnung, z. B. `bool CompareProjection`.

- **`ReportAggregatesQueryRequest`** (`FinanceManager.Shared.Dtos.Reports.ReportAggregatesQueryRequest`)
  - Neues Request-Flag für die Übergabe aus UI/API.

- **`ReportAggregationResult`** (`FinanceManager.Shared.Dtos.Reports.ReportAggregationResult`)
  - Neues Ergebnis-Flag, z. B. `bool ComparedProjection`, analog zu `ComparedPrevious` und `ComparedYear`.

- **`ReportAggregatePointDto`** (`FinanceManager.Shared.Dtos.Reports.ReportAggregatePointDto`)
  - Neue optionale Spalte nach `Amount`, z. B. `decimal? ProjectionAmount`.
  - Die Position im Record sollte fachlich der gewünschten Ausgabereihenfolge "Betrag" gefolgt von "Hochrechnung" entsprechen.

- **`ReportFavoriteDto`** (`FinanceManager.Shared.Dtos.Reports.ReportFavoriteDto`)
  - Neues Flag zur Rückgabe gespeicherter Favoriten.

- **`ReportFavoriteCreateRequest`** / **`ReportFavoriteUpdateRequest`** (`FinanceManager.Shared.Dtos.Reports`)
  - Neues Flag zum Speichern der Hochrechnungsoption.

- **`ReportFavoriteCreateApiRequest`** / **`ReportFavoriteUpdateApiRequest`** (`FinanceManager.Shared.Dtos.Reports`)
  - Neues API-Payload-Feld für die Hochrechnungsoption.

#### Logikklassen / Services

- **`IReportAggregationService`** (`FinanceManager.Application.Reports.IReportAggregationService`)
  - Keine zwingende Signaturänderung, sofern die Hochrechnung über `ReportAggregationQuery` transportiert wird.

- **`ReportAggregationService`** (`FinanceManager.Infrastructure.Reports.ReportAggregationService`)
  - Erweiterung der Aggregation um die Berechnung der Hochrechnung für reine Wertpapierberichte.
  - Wiederverwendung bzw. Erweiterung des vorhandenen Spezialpfads `QuerySecurityDividendsNetAsync(...)`, da dieser bereits Netto-Dividenden für `PostingKind.Security` und Dividend-Untertypen berechnet.
  - Ermittlung der Vorjahresperiode als gleicher Monatsbereich im Vorjahr bezogen auf den aktuellen Betrachtungszeitraum.
  - Vergleich aktueller Dividenden mit Dividenden der Vorjahresperiode je Berichtspunkt.

- **`ReportFavoriteService`** (`FinanceManager.Infrastructure.Reports.ReportFavoriteService`)
  - Mapping des neuen Favoriten-Flags zwischen Entity und DTO.
  - Übergabe des Flags bei Create/Update.
  - Berücksichtigung bei Konflikt-/Duplikatsprüfung, falls Favoriten anhand ihrer Kriterien verglichen werden.

- **`ReportsController`** (`FinanceManager.Web.Controllers.ReportsController`)
  - Übergabe des neuen Request-Flags in `ReportAggregationQuery`.
  - Übergabe des Favoriten-Flags in `ReportFavoriteCreateRequest` und `ReportFavoriteUpdateRequest`.
  - Validierung: Hochrechnung darf serverseitig nur akzeptiert oder wirksam werden, wenn ausschließlich `PostingKind.Security` gewählt ist.

#### UI-Komponenten / Controller

- **`ReportDashboardViewModel`** (`FinanceManager.Web.ViewModels.Reports.ReportDashboardViewModel`)
  - Neues UI-State-Property für Hochrechnung.
  - Hilfsproperty für die Aktivierbarkeit, z. B. nur wenn `SelectedKinds.Count == 1 && PrimaryKind == PostingKind.Security`.
  - Zurücksetzen bzw. Deaktivieren der Hochrechnung, sobald andere Buchungsarten ausgewählt werden.
  - Übergabe an `LoadAsync(...)`, `SaveFavoriteAsync(...)`, `UpdateFavoriteAsync(...)` und `SubmitFavoriteDialogAsync(...)`.
  - Einbeziehung der Hochrechnung in Summen, falls die Tabelle eine Gesamtzeile für die neue Spalte ausgeben soll.

- **`ReportDashboard.razor`** (`FinanceManager.Web.Components.Pages.ReportDashboard`)
  - Neue Checkbox/Option "Hochrechnung" im Einstellungspanel "Vergleiche".
  - Checkbox nur editierbar bei ausschließlicher Wertpapierauswahl.
  - Neue Tabellenspalte "Hochrechnung" direkt nach der Spalte "Betrag".
  - Anwendung gespeicherter Favoriten auf den neuen UI-State.

- **Lokalisierungen** (`FinanceManager.Web.Resources.Components.Pages.ReportDashboard.de.resx`, `ReportDashboard.en.resx`)
  - Neue Labels für "Hochrechnung" und ggf. Tooltip/Hinweis bei deaktivierter Option.

#### Datenbankschicht

- **EF-Migration** (`FinanceManager.Infrastructure.Migrations` oder `FinanceManager.Infrastructure.Data.Migrations.Identity`, abhängig von der vorhandenen Zuordnung für `ReportFavorite`)
  - Neue Spalte für das Hochrechnungs-Flag in der Favoriten-Persistenz.

#### Tests

- **`FinanceManager.Tests.Reports`**
  - Tests für die Hochrechnungsberechnung bei Wertpapier-Dividenden:
    - aktuelle Dividende bestätigt Vorjahresdividende trotz abweichendem Betrag;
    - fehlende aktuelle Dividende aus Vorjahresperiode wird als erwartet addiert;
    - Hochrechnung entspricht Summe aktueller Dividenden plus erwarteter Vorjahresdividenden;
    - keine Hochrechnung für nicht reine Wertpapierauswahl.

- **`FinanceManager.Tests.Web.ViewModels`**
  - Tests für Aktivierbarkeit und Zurücksetzen der Hochrechnungsoption im `ReportDashboardViewModel`.
  - Tests für Übergabe des Flags beim Laden und Speichern von Favoriten.

- **`FinanceManager.Tests.Reports.ReportFavoriteServiceTests`**
  - Tests für Persistenz, DTO-Mapping und Update des neuen Favoriten-Flags.

- **`FinanceManager.Tests.E2E.Tests.Reports.ReportingFlowPlaywrightTests`**
  - Erweiterung des Favoriten-Flows: Hochrechnung aktivieren, Favorit speichern, Favorit neu laden, Einstellung bleibt erhalten.

---

### Implementierungsansatz

1. **Option und Persistenz ergänzen**: Das neue Hochrechnungs-Flag wird analog zu `ComparePrevious`, `CompareYear` und `UseValutaDate` durch Domain-Entity, DTOs, API-Requests, API-Client, Controller und Favoriten-Service geführt. Bestehende Favoriten erhalten per Migration den Default `false`.

2. **UI-Regel abbilden**: `ReportDashboardViewModel` stellt eine zentrale Aktivierbarkeitsprüfung bereit. Die UI bindet die neue Checkbox im Bereich "Vergleiche" daran. Wenn die ausgewählten Buchungsarten nicht exakt `[PostingKind.Security]` sind, wird die Option deaktiviert und nicht wirksam an die Aggregation übergeben.

3. **Aggregation erweitern**: `ReportAggregationService.QueryAsync(...)` berücksichtigt die Hochrechnung nur, wenn das neue Flag aktiv ist und die effektiven `PostingKinds` ausschließlich `PostingKind.Security` enthalten. Für Dividendenberichte wird der vorhandene Netto-Dividendenpfad `QuerySecurityDividendsNetAsync(...)` erweitert oder durch eine klar abgegrenzte Hilfsmethode ergänzt, die aktuelle und Vorjahresperiode parallel auswertet.

4. **Vorjahresperiode bestimmen**: Die Vorjahresperiode deckt die gleichen Monate ab wie der aktuelle Betrachtungszeitraum, nur ein Jahr früher. Für den Betrachtungszeitraum Januar bis Mai 2026 wird also Januar bis Mai 2025 herangezogen; für Juni 2026 wird Juni 2025 herangezogen. Die technische Ableitung sollte auf dem bestehenden `AnalysisDate`, `Interval` und `Take` bzw. dem aktuell im Report angezeigten Periodenfenster basieren.

5. **Äquivalenz und Erwartungsbetrag berechnen**: Je Wertpapier-Gruppe werden Dividenden des Betrachtungszeitraums mit Dividenden aus der Vorjahresperiode verglichen. Eine Vorjahresdividende ist bestätigt, sobald im aktuellen Zeitraum ein äquivalenter Dividendeneintrag existiert; der aktuelle Betrag ersetzt den Vorjahresbetrag nicht für diese Einzelprüfung, sondern zählt bereits über `Amount`. Nicht bestätigte Vorjahresdividenden werden als erwartete Beträge addiert. `ProjectionAmount` ergibt sich je Berichtspunkt aus `Amount + erwartete Vorjahresdividenden`.

6. **Ausgabe erweitern**: `ReportAggregatePointDto` erhält `ProjectionAmount`. Die Tabelle in `ReportDashboard.razor` rendert die Spalte "Hochrechnung" unmittelbar nach "Betrag", sofern die Option aktiv ist bzw. das Ergebnis `ComparedProjection` meldet. Vergleichsspalten für Vorperiode und Vorjahr bleiben unverändert.

---

### Konfiguration

Die Hochrechnungsoption ist eine benutzerspezifische Report-Favoriten-Einstellung. Sie wird pro `ReportFavorite` gespeichert und beim Laden des Favoriten wieder auf das Report-Dashboard angewendet. Eine globale Anwendungskonfiguration ist nicht erforderlich.

---

### Offene Fragen

1. **Äquivalenzkriterium für Dividenden**: Soll eine Dividende ausschließlich über `SecurityId` als äquivalent gelten, oder müssen weitere Merkmale wie Dividendenmonat, Buchungs-/Valutadatum, `GroupId`, Buchungstext oder Betrag berücksichtigt werden?
2. **Netto oder Brutto**: Soll die Hochrechnung auf dem bestehenden Netto-Dividendenpfad (`Dividend + Fee + Tax`) basieren, oder sollen ausschließlich Buchungen mit `SecurityPostingSubType.Dividend` ohne Steuer-/Gebühren-Gegenbuchungen verwendet werden?
3. **Datumstyp**: Soll für die Bestätigung und Vorjahresperiode das aktuell gewählte Datumsverhalten `UseValutaDate` gelten, oder immer das Buchungsdatum?
4. **Intervallverhalten**: Soll die Hochrechnung nur für YTD-/Monats-Dividendenanalysen gelten oder auch für Quartal, Halbjahr, Jahr und AllHistory berechnet werden?
5. **Kategorie- und Summenzeilen**: Soll `ProjectionAmount` für Kategorie- und Typ-Summen serverseitig aggregiert werden oder nur auf Wertpapier-Einzelzeilen erscheinen?
