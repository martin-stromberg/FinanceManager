# Umsetzungsplan - Liquiditaetsquote im Depot-Analysebericht

## Ziel

Die Cashflow-Kachel des Depot-Analyseberichts weist wieder eine Liquiditaetsquote aus. Die Quote basiert auf dem aktuellen Saldo der depotbezogenen Verrechnungskonten und dem aktuellen Marktwert des benutzerweiten Wertpapierbestands:

`LiquidityRatio = depotCashBalance / (TotalMarketValue + depotCashBalance)`

Ist der Nenner kleiner oder gleich `0`, wird `0m` geliefert. Negative Kontosalden werden nicht gekappt, damit ein negativer Cash-Bestand fachlich sichtbar bleibt. Der bestehende Bericht bleibt benutzerweit; es wird kein neues Depot- oder Konto-Zuordnungsmodell eingefuehrt.

## Fachliche Entscheidungen

- Depotbezogene Cash-Konten werden aus bestehenden Buchungsgruppen abgeleitet: Security-Postings des Benutzers bestimmen relevante `GroupId`-Werte; Bank-Postings derselben Gruppen liefern die `AccountId`.
- Ein so gefundenes Konto zaehlt mit seinem vollstaendigen aktuellen `Account.CurrentBalance` als Depot-Liquiditaet. Das ist die kleinste anschlussfaehige Loesung ohne Datenmodellmigration und entspricht der vorhandenen Struktur ohne Depot-Aggregat.
- Konten werden dedupliziert, damit mehrere Wertpapierbuchungen auf demselben Verrechnungskonto den Saldo nur einmal einbeziehen.
- Leeres Depot, Depot ohne ableitbares Cash-Konto oder Nenner `<= 0m` ergibt eine Liquiditaetsquote von `0m`.
- Die Quote wird nur als KPI-Zeile angezeigt und nicht in das bestehende Cashflow-Balkendiagramm aufgenommen, weil dort Geldbetraege und keine Prozentwerte verglichen werden.

## Codeaenderungen

### DTO und Cache

1. `FinanceManager.Shared/Dtos/Portfolio/PortfolioCashflowDto.cs`
   - Positional Record um `decimal LiquidityRatio` erweitern.
   - XML-Kommentar aktualisieren.
   - Alle Konstruktoraufrufe in Produktivcode und Tests anpassen.

2. `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportCacheService.cs`
   - `CacheSchemaVersion` von `"2"` auf `"3"` erhoehen, damit alte JSON-Cache-Eintraege ohne `LiquidityRatio` als Miss behandelt werden.

### Berichtsermittlung

3. `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportService.cs`
   - Einen internen Ladepfad fuer Depot-Cash ergaenzen, z. B. `LoadDepotCashBalanceAsync(Guid ownerUserId, CancellationToken ct)`.
   - Die Ermittlung soll:
     - Security-Postings fuer Wertpapiere des Benutzers finden (`SecurityId != null`, `SecuritySubType != null`).
     - Deren nicht-leere `GroupId`-Werte sammeln.
     - Bank-Postings derselben Gruppen mit `AccountId != null` finden.
     - Accounts des gleichen `OwnerUserId` anhand dieser Account-Ids laden.
     - `CurrentBalance` ueber distinkte Konten summieren.
   - `GetPortfolioAnalysisReportAsync` laedt den Cash-Bestand zusaetzlich zu den Positionsdaten.
   - `BuildCashflow` erhaelt den Cash-Bestand und den aktuellen Marktwert oder die fertige Struktur als Parameter.
   - Rueckgabe: `new PortfolioCashflowDto(netDeposits, dividends, realizedGains, liquidityRatio)`.

4. Query-Details fuer die Implementierung
   - Die Ermittlung darf nicht nur `Posting.AccountId` auf Security-Postings auswerten, weil der bestehende `StatementDraftService` Security-Postings mit `AccountId = null` erzeugt.
   - Fuer Performance und Uebersichtlichkeit sollte die Cash-Ermittlung als eigene Query-Schrittfolge umgesetzt werden, nicht in die Positions-Snapshot-Struktur gemischt werden.
   - Falls keine Security-Posting-Gruppen vorhanden sind, direkt `0m` zurueckgeben.

### Cache-Invalidierung bei Kontostandsaenderungen

5. `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`
   - `IPortfolioAnalysisReportCacheService?` optional injizieren, analog zum vorhandenen optionalen Budget-Report-Cache.
   - Nach dem Commit von Draft-Eintraegen den Portfolio-Analysebericht-Cache fuer `ownerUserId` invalidieren, wenn der Commit ein depotrelevantes Konto betreffen kann.
   - Konservative Bedingung: invalidate, wenn der betroffene Account `SecurityProcessingEnabled` hat oder wenn durch die gebuchte Gruppe Security-Postings erzeugt wurden. Damit werden auch spaetere Kontostandsaenderungen auf Wertpapierkonten erfasst.
   - Vorhandene Budget-Cache-Logik bleibt unveraendert.

6. Dependency Injection
   - Pruefen, ob `IPortfolioAnalysisReportCacheService` bereits in `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` registriert ist. Falls ja, nur den neuen optionalen Konstruktorparameter nutzen; falls nein, Registrierung ergaenzen.
   - Tests mit manuell instanziiertem `StatementDraftService` muessen wegen des optionalen Parameters moeglichst ohne Anpassung weiter kompilieren.

### UI und Lokalisierung

7. `FinanceManager.Web/Components/Pages/Portfolio/PortfolioCashflowCard.razor`
   - Eine vierte `portfolio-kpi-row` fuer `LiquidityRatio` einfuegen.
   - Wert mit Prozentformat anzeigen, z. B. `Data.LiquidityRatio.ToString("P2")`.
   - `KpiInfoButton` mit eigenen Lokalisierungskeys ergaenzen.
   - `_chartPoints` unveraendert bei den drei Betrag-KPIs lassen.

8. Ressourcen aktualisieren
   - In `FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx` und `Pages.en.resx` neue Keys anlegen:
     - `PortfolioReport_LiquidityRatio`
     - `PortfolioReport_Explain_LiquidityRatio_Title`
     - `PortfolioReport_Explain_LiquidityRatio_Text`
   - Text erklaert, dass die Quote den aktuellen Saldo der aus Wertpapierbuchungen abgeleiteten Verrechnungskonten ins Verhaeltnis zu Marktwert plus Cash setzt.

## Tests

1. `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportServiceTests.cs`
   - Test: Liquiditaetsquote wird aus einem Bank-Posting derselben `GroupId` wie ein Security-Posting berechnet.
   - Test: mehrere Security-Postings oder mehrere Gruppen desselben Kontos deduplizieren den Kontosaldo.
   - Test: fremde Benutzerkonten und fremde Buchungsgruppen beeinflussen die Quote nicht.
   - Test: kein Cash-Konto, leeres Depot oder Nenner `<= 0m` ergibt `0m`.

2. `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportCacheServiceTests.cs`
   - Erwartete Cache-Schema-Version auf `"3"` anpassen bzw. bestehenden Miss-Test fuer alte Parameter nutzen/erweitern.
   - Sicherstellen, dass ein alter Cache-Eintrag nicht zurueckgegeben wird.

3. Statement-Draft-Tests
   - Bestehende Tests fuer `StatementDraftService` so erweitern, dass ein Mock/Fake von `IPortfolioAnalysisReportCacheService` bei wertpapierrelevanten Buchungen `InvalidateCacheAsync(ownerUserId, ct)` erwartet.
   - Mindestens ein Negativfall: normale Kontoauszugsbuchung ohne `SecurityProcessingEnabled` und ohne Security-Posting invalidiert den Portfolio-Cache nicht.

4. UI/ViewModel/Controller-Tests
   - DTO-Fixtures in Controller- und ViewModel-Tests um `LiquidityRatio` erweitern.
   - Falls ein Komponenten- oder Render-Test fuer `PortfolioCashflowCard` existiert, neue KPI-Zeile und Prozentformat pruefen.

## Verifikation

Nach der Implementierung ausfuehren:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~PortfolioAnalysisReport"
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~StatementDraft"
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj
```

## Risiken und Gegenmassnahmen

- Gemischte Konten werden vollstaendig als Depot-Liquiditaet gezaehlt. Das wird im Info-Text transparent gemacht und bleibt ohne neues Datenmodell konsistent.
- Historisch einmal genutzte Wertpapierkonten bleiben relevant. Die Dedup-Logik verhindert Doppelzaehlung, loest aber bewusst keine zeitliche Abgrenzung.
- Negative Salden koennen negative Quoten erzeugen oder den Nenner reduzieren. Der Nenner-Guard verhindert Division durch null oder fachlich unsinnige Nenner.
- Portfolio-Cache kann bei `StatementDraftService` etwas haeufiger invalidiert werden. Das ist akzeptabel, weil Korrektheit des Analyseberichts Vorrang vor seltenen Cache-Hits hat.

## Offene Punkte

Keine.
