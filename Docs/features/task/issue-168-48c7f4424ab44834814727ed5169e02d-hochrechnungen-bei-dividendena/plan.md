# Umsetzungsplan - Hochrechnungen bei Dividendenreports

## Ziel

Das Report-Dashboard erhaelt eine neue Vergleichsoption `Hochrechnung` fuer reine Wertpapier-Dividendenreports. Bei aktivierter Option liefert die Aggregation pro Berichtspunkt zusaetzlich zu `Amount` ein optionales `ProjectionAmount`. Der Wert besteht aus den bereits gebuchten Netto-Dividenden des aktuellen Betrachtungszeitraums plus erwarteten Netto-Dividenden aus der gleichen Vorjahresperiode, soweit diese im aktuellen Zeitraum noch nicht durch eine aequivalente Dividende bestaetigt wurden. Die Option wird in Favoriten persistiert.

## Fachliche Entscheidungen

- Flag-Name: Durchgaengig `CompareProjection`, analog zu `ComparePrevious` und `CompareYear`.
- Gueltigkeit: Die Option ist nur wirksam, wenn die effektive Buchungsartenauswahl exakt `PostingKind.Security` ist. Bei anderen Kombinationen wird serverseitig `ComparedProjection = false` geliefert und `ProjectionAmount` bleibt `null`; die UI deaktiviert und loescht das Flag beim Wechsel auf ungueltige Auswahlen.
- Betragstyp: Die Hochrechnung nutzt den vorhandenen Netto-Dividendenpfad, also Dividendengruppe inklusive `Dividend`, `Fee` und `Tax`. Das vermeidet eine abweichende Semantik zwischen `Amount` und `ProjectionAmount`.
- Datumstyp: Der Dividendenspezialpfad wird auf `UseValutaDate` erweitert. Filterung und Dividend-Anker nutzen bei aktivem Flag `ValutaDate ?? BookingDate`, sonst `BookingDate`.
- Intervallverhalten: Die Hochrechnung wird fuer alle Intervalle ausser `AllHistory` berechnet. Monats-, Quartals-, Halbjahres-, Jahres- und YTD-Perioden verwenden jeweils die gleiche Periode im Vorjahr. `AllHistory` bleibt ohne Hochrechnung, weil dort keine sinnvolle offene Vorjahresperiode existiert.
- Aequivalenz: Eine Vorjahresdividende gilt als bestaetigt, wenn im aktuellen Vergleichszeitraum fuer dieselbe `SecurityId` mindestens eine Dividendengruppe im korrespondierenden Periodenbucket existiert. Betrag, Text und `GroupId` werden nicht verglichen, weil diese Werte zwischen Jahren typischerweise variieren.
- Summenzeilen: `ProjectionAmount` wird serverseitig fuer Kategorie- und Typzeilen aus den Kindzeilen aggregiert. Die UI summiert Top-Level-Zeilen analog zu `Amount`.

## Datenmodell und DTOs

1. `FinanceManager.Shared/Dtos/Reports/ReportAggregationQuery.cs`
   - Neues bool-Feld `CompareProjection` nach `CompareYear` einfuegen, Default `false` beibehalten.
   - Konstruktor-Call-Sites anpassen.

2. `FinanceManager.Shared/Dtos/Reports/ReportAggregatesQueryRequest.cs`
   - Neues bool-Feld `CompareProjection` nach `CompareYear` aufnehmen.
   - Bestehende JSON-Kompatibilitaet bleibt durch Default `false` erhalten.

3. `FinanceManager.Shared/Dtos/Reports/ReportAggregationResult.cs`
   - Neues bool-Feld `ComparedProjection` nach `ComparedYear` aufnehmen.
   - Alle Result-Erzeugungen explizit aktualisieren.

4. `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs`
   - Neues optionales Feld `decimal? ProjectionAmount` direkt nach `Amount` einfuegen.
   - Reihenfolge: `Amount`, `ProjectionAmount`, `ParentGroupKey`, `PreviousAmount`, `YearAgoAmount`.
   - Alle positional constructor-Aufrufe in Infrastruktur, Web und Tests anpassen.

5. Favoriten-DTOs erweitern:
   - `ReportFavoriteDto`
   - `ReportFavoriteCreateRequest`
   - `ReportFavoriteUpdateRequest`
   - `ReportFavoriteCreateApiRequest`
   - `ReportFavoriteUpdateApiRequest`
   - `CompareProjection` nach `CompareYear` aufnehmen, Default `false`.

## Favoritenpersistenz

1. `FinanceManager.Domain/Reports/ReportFavorite.cs`
   - Property `public bool CompareProjection { get; private set; }` ergaenzen.
   - Konstruktor und `Update(...)` um `compareProjection` erweitern.
   - `ReportFavoriteBackupDto`, `ToBackupDto()` und `AssignBackupDto(...)` erweitern.
   - Restore fehlender alter Backup-Felder ist ueber JSON-Default `false` akzeptabel.

2. `FinanceManager.Infrastructure/Reports/ReportFavoriteService.cs`
   - Mapping in `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync` erweitern.
   - `CompareProjection` beim Erzeugen und Aktualisieren in Entity und DTO durchreichen.
   - Duplikatslogik unveraendert lassen, da sie heute nur `{ OwnerUserId, Name }` prueft.

3. EF-Migration
   - Neue Migration im aktiven Pfad `FinanceManager.Infrastructure/Migrations/` erstellen.
   - Spalte `CompareProjection` auf `ReportFavorites` als `bit`/bool mit Default `false`.
   - `AppDbContextModelSnapshot.cs` aktualisieren.
   - Wenn `dotnet ef` nicht laeuft, Migration manuell nach bestehendem Muster erstellen und Snapshot konsistent halten.

## API und Controller

1. `FinanceManager.Web/Controllers/ReportsController.cs`
   - `ReportAggregatesQueryRequest.CompareProjection` in `ReportAggregationQuery` mappen.
   - Create-/Update-Favoritenrequests um `CompareProjection` erweitern.
   - Keine harte `BadRequest`-Validierung fuer ungueltige PostingKinds einfuehren; der Service macht das Flag wirkungslos. So bleiben alte oder externe Clients robust.

2. API-Client
   - `FinanceManager.Shared/ApiClient.Reports.cs` braucht voraussichtlich keine Logikaenderung.
   - Aufrufende Payload-Erzeugungen muessen das neue Feld setzen.

## Aggregation

1. Aktivierung in `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
   - In `QueryAsync(...)` `CompareProjection` nur fuer effektive `kinds == [PostingKind.Security]` aktivieren.
   - Bei aktiver Projektion den bestehenden `QuerySecurityDividendsNetAsync(...)`-Pfad erzwingen, auch wenn `IncludeDividendRelated` nicht gesetzt ist, solange Security-Dividenden angefragt sind oder keine widersprechenden Security-Subtypes gesetzt sind.
   - Normale Aggregationspfade liefern `ComparedProjection = false`.

2. Dividendenspezialpfad umbauen
   - Interne Ereignisstruktur einfuehren, z. B. `DividendEvent(SecurityId, Date, Month, NetAmount, GroupId)`.
   - Zeitraum fuer die Datenladung erweitern: aktueller Ergebniszeitraum plus korrespondierende Vorjahresperiode. Fuer `Take = n` muss mindestens `startMonth.AddYears(-1)` bis `endExclusive` geladen werden.
   - Datumsfeld zentral bestimmen: `DividendDate = query.UseValutaDate ? (ValutaDate ?? BookingDate) : BookingDate`.
   - Filterung der Postings anhand dieses Datums statt fest `BookingDate`.
   - Netto pro Dividendengruppe weiter ueber `GroupId` und Subtypes `Dividend`, `Fee`, `Tax` berechnen.

3. Projektion berechnen
   - Aktuelle Events und Vorjahres-Events je `SecurityId` und Zielperiode indexieren.
   - Fuer jeden Ausgabe-Berichtspunkt:
     - `Amount` bleibt die Summe aktueller Netto-Dividenden.
     - Erwartet ist die Summe der Vorjahres-Events derselben `SecurityId` im korrespondierenden Vorjahresbucket, falls im aktuellen Bucket kein Event derselben `SecurityId` existiert.
     - `ProjectionAmount = Amount + erwarteteVorjahresNettoDividenden`.
   - Fuer Kategorie- und Typzeilen `ProjectionAmount` aus Kindzeilen summieren.
   - Bei `AllHistory` kein `ProjectionAmount` setzen und `ComparedProjection = false`.

4. Intervalltransformation
   - Beim Transformieren von Monatsdaten nach `Quarter`, `HalfYear`, `Year` und `Ytd` `ProjectionAmount` separat summieren.
   - YTD nutzt den aktuellen Cutoff-Monat: Jahr 2026 mit Analyse Mai vergleicht Januar bis Mai 2025.
   - Vergleichsspalten `PreviousAmount` und `YearAgoAmount` bleiben unveraendert.

5. Ergebnisbereinigung
   - Beim Entfernen kuenstlicher Nullzeilen auch `ProjectionAmount` beruecksichtigen: Eine Zeile mit `Amount == 0` darf bleiben, wenn `ProjectionAmount` einen erwarteten Wert enthaelt.
   - Sortierung unveraendert lassen.

## UI

1. `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
   - Property `CompareProjection` ergaenzen.
   - Helper ergaenzen:
     - `IsSecurityOnlySelection`
     - `CanCompareProjection`
     - `ShowProjectionColumn`, idealerweise aus Serverergebnis `ComparedProjection` oder `CompareProjection && CanCompareProjection`.
   - Beim Wechsel von PostingKinds auf nicht reine Security-Auswahl `CompareProjection = false` setzen.
   - `LoadAsync(...)`, `ReloadAsync(...)`, `SaveFavoriteAsync(...)`, `UpdateFavoriteAsync(...)` um `compareProjection` erweitern.
   - `GetTotals()` auf `(decimal Amount, decimal? Projection, decimal? Prev, decimal? Year)` erweitern.
   - `IsNegative(...)` bei Bedarf um `ProjectionAmount` erweitern, damit Null-Amount mit negativer Projektion konsistent markiert wird.

2. `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
   - Lokales Feld `_compareProjection` einfuehren.
   - In der Vergleichsgruppe eine Checkbox `Label_CompareProjection` anzeigen.
   - Checkbox deaktivieren, wenn nicht reine Security-Auswahl oder `AllHistory`.
   - Beim Deaktivieren der Auswahl `_compareProjection = false`.
   - `LoadAsync()`, Favorit speichern, Favorit aktualisieren und `ApplyFavorite(...)` synchronisieren.
   - Tabellenspalte `Th_Projection` direkt nach `Th_Amount` rendern.
   - Alle Zeilentypen aktualisieren: Top-Level, Child, Grandchild und Summenzeile.

3. Ressourcen
   - `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.de.resx`
     - `Label_CompareProjection` = `Hochrechnung`
     - `Th_Projection` = `Hochrechnung`
     - optional `Hint_ProjectionSecurityOnly`
   - `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.en.resx`
     - `Label_CompareProjection` = `Projection`
     - `Th_Projection` = `Projection`
     - optional `Hint_ProjectionSecurityOnly`

## Tests

1. Aggregationstests in `FinanceManager.Tests/Reports`
   - Neuer oder erweiterter Test fuer Netto-Hochrechnung:
     - Vorjahresdividende ohne aktuelle Bestaetigung wird zu `ProjectionAmount` addiert.
     - Aktuelle Dividende bestaetigt Vorjahresdividende trotz abweichendem Betrag.
     - `ProjectionAmount == Amount + erwarteteVorjahresdividenden`.
     - Fee/Tax werden wie im Netto-Pfad einbezogen.
   - Test fuer `UseValutaDate`: Dividend-Anker folgt `ValutaDate`, Fallback auf `BookingDate`.
   - Test fuer Nicht-Security oder Multi-Kind: `ComparedProjection == false`, alle `ProjectionAmount == null`.
   - Test fuer Kategorie-/Summenzeilen bei `IncludeCategory`.
   - Test fuer YTD-Cutoff und mindestens ein weiteres Intervall (`Quarter` oder `Year`).

2. ViewModel-Tests in `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs`
   - `CompareProjection` wird nur bei reiner Security-Auswahl aktivierbar.
   - Wechsel auf Multi-Kind oder Nicht-Security setzt das Flag zurueck.
   - `LoadAsync`, `SaveFavoriteAsync`, `UpdateFavoriteAsync` senden das Flag.
   - `GetTotals()` summiert `ProjectionAmount`.

3. Favoriten-Tests in `FinanceManager.Tests/Reports/ReportFavoriteServiceTests.cs`
   - Create, Get/List und Update persistieren `CompareProjection`.
   - Backup/Restore-Test fuer `ReportFavoriteBackupDto` erweitern, falls bestehende Backup-Coverage direkt ReportFavorites prueft.

4. Integration/E2E
   - `FinanceManager.Tests.Integration/ApiClient/ApiClientReportsTests.cs` um Request-/Favorite-Feld erweitern.
   - `FinanceManager.Tests.E2E/Tests/Reports/ReportingFlowPlaywrightTests.cs` optional erweitern, wenn stabile Dividendendaten im Testflow erzeugbar sind: Security-Favorit mit Hochrechnung speichern und nach Reload pruefen.

## Umsetzungsschritte

1. Shared DTOs und alle compilerrelevanten Constructor-Call-Sites aktualisieren.
2. Domain-Entity, Favoriten-Service und Controller-Mappings erweitern.
3. EF-Migration fuer `ReportFavorites.CompareProjection` erstellen.
4. Aggregationsservice erweitern:
   - Security-only-Aktivierung,
   - Valuta-faehiger Dividendenspezialpfad,
   - Ereignisstruktur und Projektion,
   - Intervall- und Summenaggregation.
5. ViewModel und Razor-Komponente inklusive Ressourcen aktualisieren.
6. Tests fuer Aggregation, ViewModel, Favoriten und API anpassen/ergaenzen.
7. Build und Tests ausfuehren:
   - `dotnet build`
   - gezielt `dotnet test` fuer betroffene Testprojekte
   - falls E2E angepasst wurde: entsprechender Playwright-Test.

## Risiken und Gegenmassnahmen

- Positional records erzeugen viele Compilerfehler nach Signaturaenderung. Gegenmassnahme: erst DTOs aendern, dann alle `new ReportAggregatePointDto(...)` und `new ReportAggregationResult(...)` Call-Sites per Compiler abarbeiten.
- Der Dividendenspezialpfad nutzt aktuell fest `BookingDate`. Gegenmassnahme: Datumsselektion in eine lokale Expression/Hilfsmethode kapseln und mit Valuta-Test absichern.
- Projektion auf aggregierten Monatsdaten wuerde Ereignis-Aequivalenz verlieren. Gegenmassnahme: Projektion aus Dividendengruppen-Events vor Monatsaggregation berechnen.
- Tabellenstruktur ist mehrfach dupliziert. Gegenmassnahme: Header, Top-Level, Child, Grandchild und Total in einem Schritt aktualisieren und mit Component/ViewModel-Tests pruefen.
- Manuelle EF-Migration kann Snapshot-Inkonsistenzen erzeugen. Gegenmassnahme: bevorzugt `dotnet ef migrations add`, andernfalls Migration und Snapshot nach bestehendem Pattern gemeinsam pruefen.

## Offene Punkte

Keine verbleibenden offenen Punkte. Die fachlichen Unsicherheiten wurden oben konservativ entschieden.
