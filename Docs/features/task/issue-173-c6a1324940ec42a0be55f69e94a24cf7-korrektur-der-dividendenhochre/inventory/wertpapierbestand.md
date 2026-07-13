# Detail: Wertpapierbestand und Domaenenmodell

## Datenmodell

`Posting` ist die massgebliche Datenquelle fuer Wertpapierbuchungen.

Relevante Stellen:

- `FinanceManager.Domain/Postings/Posting.cs:108-122`: Konstruktor mit `bookingDate`, `valutaDate`, `securitySubType` und `quantity`.
- `FinanceManager.Domain/Postings/Posting.cs:129-138`: Setzt `SecurityId`, `SecuritySubType`, `GroupId` und `Quantity`.
- `FinanceManager.Domain/Postings/Posting.cs:242-249`: `SecuritySubType` und `Quantity` als Properties.

`Posting` hat keinen `OwnerUserId`. Benutzerbezug muss ueber `Security.OwnerUserId` oder bereits validierte Security-IDs laufen.

## Security-Subtypen

`FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs:6-17` definiert:

- `Buy = 0`
- `Sell = 1`
- `Dividend = 2`
- `Fee = 3`
- `Tax = 4`

Bestandsrelevant sind nur `Buy` und `Sell`. Dividenden, Gebuehren und Steuern duerfen keinen Bestand erzeugen.

## Buchungskonvention

Der Statement-Buchungsservice speichert Mengen in `FinanceManager.Infrastructure/Statements/StatementDraftService.cs:2205-2225`.

Wichtig:

- `StatementDraftService.cs:2212`: Transaktionstyp wird auf Security-Subtyp gemappt.
- `StatementDraftService.cs:2213`: `Buy` erhaelt positive `SecurityQuantity`, `Sell` negative `SecurityQuantity`, `Dividend` keine Menge.
- `StatementDraftService.cs:2214`: Hauptposting wird mit `tradeSub` und `qty` angelegt.
- `StatementDraftService.cs:2218-2223`: Fee/Tax-Postings erhalten keine Menge.

Damit kann ein Bestand aus `Quantity` summiert werden, sofern Sell-Mengen bereits negativ gespeichert sind. Robust waere dennoch, `Sell` ueber `-Math.Abs(quantity)` zu behandeln, weil die Return-Analysis diese Konvention bereits absichert.

## Bestehende Bestandsermittlung in Return Analysis

`FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs` laedt Wertpapiertransaktionen ueber Ownership-Join:

- `ReturnAnalysisService.cs:1263-1287`: `LoadPostingsAsync` joint `Postings` mit `Securities` und filtert `OwnerUserId`.

Die konkrete Bestandsberechnung steht in:

- `ReturnAnalysisService.cs:1600-1613`: `ComputeSharesHeldOnDate`

Logik:

- Transaktionen nach Datum bis einschliesslich Stichtag verarbeiten.
- `Buy`: `shares += tx.Quantity ?? 0m`
- `Sell`: `shares -= Math.Abs(tx.Quantity ?? 0m)`
- Rueckgabe wird mit `Math.Max(0m, shares)` auf nicht-negativ begrenzt.

Diese Logik ist fachlich sehr nah an der benoetigten Pruefung fuer Dividendenprojektionen.

## Relevanter Stichtag

Die Anforderung laesst eine offene Frage: Bestand exakt zum erwarteten Dividendentag oder pauschal zum `AnalysisDate`.

Der aktuelle Code kennt pro `ProjectionCandidate` ein `ExpectedDate`. Technisch ist daher eine Pruefung zum erwarteten Dividendentag am naheliegendsten, weil sie pro erwarteter Dividende exakt moeglich ist. Falls fachlich `AnalysisDate` gelten soll, muss die Planungsphase diese Entscheidung explizit treffen.

## Ladefenster fuer Bestandsbuchungen

Die bestehenden Dividendenevents werden ab `loadStartMonth` geladen, das bei Projektion bis zu zwei Jahre zurueckreichen kann. Fuer Bestand reicht das eventuell nicht aus, weil ein Kauf vor diesem Zeitraum liegen kann.

Eine korrekte Bestandsberechnung braucht alle Buy-/Sell-Postings der eigenen Wertpapiere bis zum relevanten Stichtag. Das obere Limit kann `projectionEnd` oder `endExclusive` sein; ein unteres Limit sollte fuer Bestand nicht gesetzt werden, ausser es gibt eine belastbare historische Startposition.

## Filterbezug

Die Bestandsermittlung sollte mindestens auf eigene Wertpapiere begrenzt sein:

- `ownedIds` aus `Securities.OwnerUserId == query.OwnerUserId`

Optional kann sie weiter auf `allowedSecurities` eingeschraenkt werden, muss aber nicht. Ein umfassendes Dictionary fuer alle eigenen Wertpapiere ist einfacher und vermeidet Sonderfaelle bei Kategoriefiltern.

## Reversals

`Posting` enthaelt Reversal-Felder (`IsReversed`, `IsReversal`). Die bestehende Dividendenhochrechnung filtert Reversals nicht sichtbar aus. Die Bestandsermittlung sollte fuer Konsistenz zunaechst dieselbe Datenbasis wie bestehende Reporting-Postings nutzen. Eine zusaetzliche Reversal-Korrektur waere fachlich groesser und ist nicht Teil der Anforderung.
