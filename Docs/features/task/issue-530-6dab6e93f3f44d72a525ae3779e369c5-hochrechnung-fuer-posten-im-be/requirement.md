# Übersetzte Anforderung – Issue #530: Hochrechnung für Posten im Bericht

Quelle: GitHub Issue #530 „Hochrechnung für Posten im Bericht" (martin-stromberg/FinanceManager)

## Fachliche Zusammenfassung

Die Berichts-Option „Hochrechnung" (`CompareProjection`) berechnet heute ausschließlich für reine Wertpapierberichte (`PostingKind.Security`) erwartete Beträge auf Basis des Vorjahresverhaltens — praktisch genutzt und verifiziert für Dividenden (Netto-Dividendenpfad `QuerySecurityDividendsNetAsync`). Die Anforderung erweitert diese Vergleichsfunktion auf die übrigen Postenarten (`Bank`, `Contact`, `SavingsPlan`) und soll dabei die vorhandene Logik unverändert übernehmen: Mustererkennung aus dem Vorjahr (monatlich / quartalsweise / jährlich / irregulär), Projektion erwarteter Buchungen in den laufenden Betrachtungszeitraum und Summendarstellung als `ProjectionAmount` je Berichtspunkt. Zielbild ist eine einzige, artübergreifende Funktionalität für alle Postenarten; eine eventuell nötige abweichende Behandlung einzelner Postenarten wird vom Auftraggeber erst nach Sichtung des Ergebnisses entschieden.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein wiederkehrender „Buchungs-Event" einer Berichtsgruppe (Entitätszeile: Konto, Kontakt, Sparplan oder Wertpapier) innerhalb einer Berichtsperiode. Beobachtet wird, ob zu einem Vorjahres-Event eine entsprechende Buchung im laufenden Betrachtungszeitraum bereits eingegangen ist; fehlt sie, wird der Vorjahresbetrag als Erwartung in die `ProjectionAmount` einbezogen.
- **Auslösende Ereignisse:** Der Anwender aktiviert die Vergleichsoption „Hochrechnung" (`CompareProjection`) im Berichts-Dashboard (`ReportDashboard.razor`, Checkbox `Label_CompareProjection`) für eine Auswahl, die die Option unterstützt, und lässt die Aggregation laufen (`ReportAggregationService.QueryAsync`). Gleiche Auslöser über gespeicherte Favoriten (`ReportFavorite.CompareProjection`), KPI-Kacheln (`HomeKpiGrid`) und MCP-Werkzeuge (`FmReportingAggregateTool`, `FmReportsConfigurationManageTool`).
- **Erwartete Wirkung:** Für jede unterstützte Postenart liefert die Aggregation `ComparedProjection = true` und je Berichtspunkt ein optionales `ProjectionAmount` = gebuchter Betrag der Periode + erwartete, noch nicht eingegangene Vorjahresbeträge. Im Dashboard erscheint die Hochrechnungsspalte (`Th_Projection`) samt Detail-Overlay (`ProjectionDetails_*`); Kategorie- und Typ-Summenzeilen aggregieren die Werte ihrer Kindzeilen. Bei `ReportInterval.AllHistory` bleibt die Option weiterhin wirkungslos.
- **Umgang mit fehlenden Objekten:**
  - Entitäten ohne Vorjahresbuchungen im Referenzzeitraum: kein erkanntes Muster, keine Erwartungen (Verwerfen — keine Erzeugung von Objekten).
  - Entitäten ohne aktuelle Buchungen, aber mit unerfüllten Vorjahres-Erwartungen: Erwartung fließt in `ProjectionAmount` ein (Wiederverwendung der bisherigen Semantik „bestätigt/erwartet").
  - Es werden keine neuen fachlichen Objekte, Konfigurationen oder Sitzungen benötigt; `ReportFavorite.CompareProjection` ist bereits artunabhängig persistiert.
- **Scope-Ausschlüsse:**
  - Keine abweichende oder spezialisierte Hochrechnungslogik pro Postenart in dieser Iteration — der Auftraggeber evaluiert nach dem Ergebnis, ob Anpassungen nötig sind.
  - `ReportInterval.AllHistory` bleibt von der Hochrechnung ausgeschlossen (Bestandsverhalten).
  - Die Bestandsprüfung (`HasPositiveHolding`) ist wertpapierspezifisch; für Postenarten ohne Bestandsbegriff kann sie wortgleich nicht gelten (Annahme, siehe Offene Fragen).
  - Die Dividenden-Netting-Logik (Verrechnung von `Fee`/`Tax` in Dividendengruppen über `GroupId`) bleibt dem Wertpapierpfad vorbehalten; andere Arten besitzen kein Subtypen-Netting.
  - Keine neue Konfigurationsoberfläche; die Option bleibt eine reine Berichts-/Favoriteneinstellung.

## Betroffene Klassen und Komponenten

- **Logik / Services**
  - `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
    - `QueryAsync` (ca. Z. 44–65): Gate `onlySecurityKind` in Zeile 60 beschränkt `compareProjection` heute auf reine Security-Auswahl — aufzuweichen.
    - `QuerySecurityDividendsNetAsync` (ab Z. 869): Enthält die komplette Projektionsimplementierung inkl. `dividendEvents`, `loadStartMonth = startMonth.AddYears(-2)` (2-Jahres-Fenster), Bestandstransaktionen und `ApplyProjectionAmounts` (Z. 1304–1622).
    - Standard-Aggregatpfad (PostingAggregates / `LoadTextFilteredRowsAsync`): liefert heute für Bank/Contact/SavingsPlan die Punkte — die Hochrechnung muss hier zusätzlich Vorjahres-Ereignisse laden.
  - Ggf. neue, artübergreifende Projektionskomponente (Annahme): Extraktion der Mustererkennung (`DividendPattern`, `DetectDividendPattern`, `FindExpectedDividends`, `ProjectionCandidate`) in einen gemeinsamen Dienst, da `QuerySecurityDividendsNetAsync` nur für Security läuft.
  - `ReportQuerySemantics` (Gruppenschlüssel `Security:`/`Account:`/`Contact:`/`SavingsPlan:`, `TryParseSecurityKey`, `ParseKindFromKey`): Parser je Postenart für die Zuordnung erwarteter Beträge zu Berichtspunkten.
- **Datenmodell / DTOs**
  - `ReportAggregatePointDto` (`ProjectionAmount`, `ProjectionExpectedDividends`): artneutral bereits vorhanden.
  - `ReportProjectionExpectedDividendDto` (`SecurityId`, `SecurityName`): wertpapierbezogen modelliert — für andere Postenarten zu verallgemeinern (Annahme: generische Entitätsreferenz) oder kompatibel wiederzuverwenden.
- **Interfaces**
  - `IReportAggregationService`, `ReportAggregationQuery` (`CompareProjection`), `ReportAggregationResult` (`ComparedProjection`): voraussichtlich unverändert.
- **UI-Komponenten**
  - `FinanceManager.Web/Components/Pages/ReportDashboard.razor`: `CanCompareProjection` (Z. 595) bindet die Option an `IsSecurityOnlySelection`; Detail-Overlay nutzt wertpapierbezogene Spalte (`ProjectionDetails_Security`, Z. 143) und `ReportProjectionExpectedDividendDto`.
  - `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`: `CanCompareProjection` (Z. 223–229), `IsSecurityOnlySelection` (Z. 217), `ShowProjectionColumn` (Z. 486), Favoriten-Speicherpfade (Z. 729/785/1355).
  - `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` (Z. 667): verarbeitet `ProjectionAmount` bereits artneutral.
- **MCP / API**
  - `FmReportingAggregateTool`, `FmReportsConfigurationManageTool`: Schema-Texte beschreiben `compareProjection` im Security-Kontext — Beschreibungen/Validierung prüfen.
- **Ressourcen**
  - `Resources/Components/Pages/ReportDashboard.de.resx` / `.en.resx`: `ProjectionDetails_Security` („Wertpapier"/„Security") und ggf. weitere `ProjectionDetails_*`-Texte artneutral formulieren.
- **Enums**
  - `PostingKind` (Bank, Contact, SavingsPlan, Security) unverändert; kein neues Enum-Mitglied zu erwarten (`EnumCoverageTests` nur bei Enum-Änderung).
- **Dokumentation**
  - `Docs/help/berichtswesen/business-rules.md`: Regel „Dividendenhochrechnung nur für Wertpapierberichte" (Z. 30–47) ist fachlich zu aktualisieren.
- **Tests**
  - `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs`: Bestehende Dividenden-Hochrechnungstests müssen unverändert bestehen; neue Fälle je Postenart (Mustererkennung, bestätigte vs. erwartete Events, Kategorie-/Typ-Aggregation, `AllHistory`-Ausschluss, Multi-Kind-Auswahl).

## Implementierungsansatz

- Kernaufgabe ist die Verallgemeinerung der bestehenden, in `QuerySecurityDividendsNetAsync`/`ApplyProjectionAmounts` gekapselten Projektkette: Vorjahres-Ereignisse laden (Fenster um ein bis zwei Jahre erweitert, analog `loadStartMonth = startMonth.AddYears(-2)`), Muster je Entität erkennen, Kandidaten `+1 Jahr` projizieren, gegen laufende Ereignisse abgleichen, Erwartungsbeträge auf `ProjectionAmount` addieren und Details je Periode/Gruppe zuordnen.
- Für Wertpapiere bleiben die Besonderheiten erhalten: Netto-Dividendenereignisse aus `GroupId`-Gruppen (Dividend + Fee + Tax), Bestandsprüfung aus Buy/Sell-Transaktionen (`HoldingTransaction`, `HasPositiveHolding`).
- Für Bank/Contact/SavingsPlan gibt es weder Subtypen-Netting noch Bestand: jede Buchung der Entität im Referenzzeitraum ist ein Kandidaten-Event; Mustererkennung und „bestätigt/erwartet"-Semantik werden 1:1 übernommen (Annahme, zu bestätigen).
- Erweiterungspunkte: `QueryAsync`-Gating, Gruppenschlüssel-Parsing (`GroupKey`-Präfixe `Account:`/`Contact:`/`SavingsPlan:`), Besitzer-Filterung über die Entitäts-Fremdschlüssel der Postings (Konten/Kontakte/Sparpläne des `OwnerUserId`).
- UI: `CanCompareProjection` (ViewModel + Seite) ist von `IsSecurityOnlySelection` zu lösen; das Detail-Overlay benötigt eine artneutrale Bezeichnung der Entitätsspalte.
- Abhängigkeiten: `PostingAggregates`-Standardpfad liefert keine Rohbuchungen — für die Projektion sind (analog Dividendenpfad) Rohpostings nötig; zudem ist zu klären, wie `SavingsPlanPostingSource.LinkedAccountPostings` abgeleitete Reihen behandelt werden.

## Konfiguration

Keine neue Konfiguration erforderlich: `CompareProjection` ist bereits ein benutzerspezifischer Favoritenparameter (`ReportFavorite.CompareProjection`) und wird über die bestehenden Dashboard-/Favoriten- sowie MCP-Pfade persistiert und wiederhergestellt.

## Offene Fragen

1. **Gültigkeitsbereich der Option:** Soll „Hochrechnung" weiterhin nur bei Einzelart-Auswahl greifen (heute `IsSecurityOnlySelection`), dann für jede der vier Arten einzeln — oder auch bei Mehrfachauswahl (`postingKinds` mit mehreren Arten)? Wie verhält sich die Summenzeile in gemischten Auswahlen?
2. **Bestandsprüfung:** Entfällt `HasPositiveHolding` für Bank/Contact/SavingsPlan ersatzlos (kein Bestandsbegriff vorhanden), oder soll eine analoge „Existenz-/Aktivitäts"-Bedingung gelten? Annahme: ersatzlos, da es kein Pendant gibt.
3. **Ereignisbegriff für Nicht-Wertpapier-Arten:** Gilt jede Buchung der Entität als Event (inkl. Stornos/`ReversalForPostingId`), oder sollen Stornos, Beträge 0 oder verknüpfte Buchungen (`LinkedPostingId`, `ParentId`) ausgenommen werden — analog der impliziten Filterung beim Dividendenpfad?
4. **Sparplan-Sonderpfad:** Soll die Hochrechnung auch für `SavingsPlanPostingSource.LinkedAccountPostings` abgeleitete Reihen gelten, oder nur für echte Sparplan-Postings?
5. **Detail-DTO:** `ReportProjectionExpectedDividendDto` trägt `SecurityId`/`SecurityName`. Soll das DTO artneutral verallgemeinert werden (z. B. `EntityId`/`EntityName`/`Kind` — API-Bruch) oder pro Art parallel geführt werden?
6. **Text- und Entitätsfilter:** Bleiben erwartete Events an die aktiven Filter gebunden (z. B. `SecurityTextFilters` am Anker-Posting; für andere Arten an Subject/Description der Buchung), sodass gefilterte Berichte auch gefilterte Erwartungen zeigen?
7. **`UseValutaDate`:** Wird für alle Arten wie im Dividendenpfad das Effektivdatum (`UseValutaDate ? ValutaDate : BookingDate`) für Eventdatum und Vorjahresabgleich verwendet? Annahme: ja.
