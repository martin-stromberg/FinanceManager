# Umsetzungsplan: MCP `fm_securities_analysis` — `latestPrice` (letzter gespeicherter Kurs inkl. Datum) im Wertpapier-Ausgabeobjekt (Issue #559)

## Übersicht

Das MCP-Lesetool `fm_securities_analysis` erhält pro ausgegebenem Wertpapier ein always-on Top-Level-Feld `latestPrice { close, date }` bzw. `null`, das den zuletzt **gespeicherten** Schlusskurs samt Datum liefert — in Listen- wie Einzelabfrage, unabhängig von `includePrices`/`priceTake`. Dazu wird `ISecurityPriceService` um die Batch-Methode `GetLatestPricesAsync` erweitert (eine Abfrage statt N+1), implementiert in `SecurityPriceService` und konsumiert von `FmSecuritiesAnalysisHandler`. Betroffen sind außerdem der Test-Spy `SpySecurityPriceService` (neues Interface-Member), neue Handler-/Service-Tests sowie die MCP-Vertragsdokumentation.

## Scope-Klärung

Verbindliche fachliche Abgrenzung (übernommen aus `requirement.md` → „Fachlicher Scope"):

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Wertpapier-Ausgabeobjekt in der `fm_securities_analysis`-Antwort — Listenpfad (`securityId` nicht gesetzt) und Einzelpfad (`securityId` gesetzt); nur die per `McpRecordScopeFilter` freigegebenen Wertpapiere des `context.Access.OwnerUserId` |
| Auslösende Ereignisse | Jeder `tools/call`-Aufruf von `fm_securities_analysis` durch einen autorisierten MCP-Zugang. Kein UI-Ereignis, kein Zeit-/Systemtrigger |
| Erwartete Wirkung | Jedes Wertpapier-Objekt trägt `latestPrice: { close, date }` bzw. `null` — der Client liest den letzten bekannten Kurs direkt, ohne `includePrices`/`priceTake` zu setzen. `hasPriceError` bleibt eigenständiges Signal und unterdrückt `latestPrice` **nicht** |
| Umgang mit fehlenden Objekten | Wertpapier ohne gespeicherte `SecurityPrice`-Sätze → `latestPrice: null` (kein Erzeugen, kein Fehler). `hasPriceError: true` ohne gespeicherten Kurs → ebenfalls `null`. Fehlende/fremde/scope-gefilterte `securityId` im Einzelpfad → bestehendes Verhalten unverändert (leere `securities`-Liste, „nicht vorhanden"-Semantik). Leere sichtbare Menge → keine Kursabfrage, leere `securities`-Liste |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Live-/Realtime-Kurs und kein externer Kursabruf — ausschließlich persistierte `SecurityPrice`-Daten | Issue-Abgrenzung; Kurspflege ist bewusst manuell | bestätigt (Issue #559 Abgrenzung + Umsetzungsauftrag) |
| 2 | Keine serverseitige Staleness-Bewertung oder Schwellwerte | Aktualitätsbeurteilung obliegt dem Aufrufer anhand von `date`; unregelmäßige manuelle Kurspflege macht Schwellwerte unsinnig | bestätigt (Issue #559 Abgrenzung) |
| 3 | Keine Änderung an `prices`, an Eingabeparametern (`includePrices`, `priceTake` bleiben wirksam), am Tool-Schema, an Required Scopes oder an der Sicherheitsklasse (`Lesend`) | Issue-Abgrenzung; additives Feld ohne Contract-Bruch | bestätigt (Issue #559 Abgrenzung) |
| 4 | Kein persistiertes/denormalisiertes Feld am `Security`-Entity (keine EF-Migration, keine Backup-DTO-Pflicht) | Reine Laufzeitermittlung reicht; Betreuer-Analyse stuft persistierte Variante als oversized ein | bestätigt (Issue #559 Abgrenzung) |
| 5 | Keine Ausweitung auf `SecurityDto`/`GET /api/securities`/Blazor-UI (Variante b entfällt) | Betreuer-Empfehlung Variante (a): kleinster Eingriff, kein API-/UI-Blast-Radius; Issue-Beschreibung nennt nur das MCP-Tool | bestätigt (Betreuer-Empfehlung + Umsetzungsauftrag) |
| 6 | Kein `includeLatestPrice`-Opt-out-Parameter | „Always-on" ist Kern des Komfortziels; Performance-Zielkonflikt wird über die Batch-Abfrage gelöst | bestätigt (Issue #559 Vorschlag) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Umsetzungsort `latestPrice` | Nur im anonymen MCP-Ausgabeobjekt des `FmSecuritiesAnalysisHandler` (Variante a), Top-Level neben `openQuantity`/`hasPriceError` | Betreuer-Empfehlung; es gibt keinen geteilten „API-Response" (anonyme Objekte), eine `SecurityDto`-Erweiterung hätte Wirkung auf `GET /api/securities` und Blazor-UI ohne Anforderungsnutzen. Serverseitig ermittelte Zahlen → nicht unter `untrusted_data` |
| Shape und Datumsformat von `latestPrice` | Gleiche anonyme Projektion `{ date, close }` aus `SecurityPriceDto` wie die `prices[]`-Einträge; `date` als `DateTime` via `System.Text.Json` serialisiert (identisch zu `prices[].date`, ISO-8601 mit Zeitanteil) | Betreuer-Empfehlung „konsistent zu `prices[].date`": ein einziges Datumsformat in derselben Antwort, Clients parsen beide Felder gleich, kein neues Serialisierungsverfahren. `SecurityPrice.Date` ist konstruktorseitig auf `.Date` normalisiert → Zeitanteil ist immer `00:00:00`, kein Informationsverlust. Die Abweichung vom illustrativen Issue-Beispiel `"2026-11-19"` wird im Vertrag (`api.md`) dokumentiert |
| `hasPriceError`-Semantik | `latestPrice` liefert den letzten gespeicherten Kurs **unabhängig** vom Fehlerflag; `hasPriceError` bleibt eigenständiges Signal im selben Objekt | Betreuer-Empfehlung: `null` bei Fehlerflag verwürfe einen existierenden Kurs und widerspräche der eigenen Staleness-Begründung (das Datum macht das Alter transparent). Entscheidung wird in `api.md` dokumentiert |
| Kurs-Ermittlung | Neue Batch-Methode `GetLatestPricesAsync` am `ISecurityPriceService` statt Schleife über `ListAsync(0, 1)` | Betreuer-Empfehlung: bei always-on-Feld im Listenpfad würde das vorhandene N+1-Muster auf jede Listenausgabe ausgedehnt. `GetLatestDateAsync` reicht nicht (nur Datum, kein Kurs) |
| Rückgabetyp der Batch-Methode | `Task<IReadOnlyDictionary<Guid, SecurityPriceDto>>` — `SecurityId` → letzter Kurs | Lookup-freundliche Form für den Per-Wertpapier-Schritt; Präzedenzfall im Projekt: `ISavingsPlanAccountLinkService.GetLinkedAccountIdsByPlanAsync` (Batch → `IReadOnlyDictionary`). `SecurityPriceDto` wird wiederverwendet — kein neuer DTO nötig |
| „Letzter Kurs" / Tie-Break | `GroupBy(SecurityId)` + `Max(Date)` mit Rück-Join auf `(SecurityId, Date)` für `Close` | Unique-Index `{ SecurityId, Date }` (`AppDbContext.cs` Zeile 721) garantiert höchstens einen Kurs je Wertpapier und Datum → deterministisch, kein Tie-Break nötig. Max+Join ist der auf allen EF-Providern sicher übersetzbare Weg (anders als `OrderByDescending().First()` pro Gruppe) |
| Besitzerprüfung in der Batch-Methode | Join/Filter über `_db.Securities` auf `OwnerUserId == ownerUserId`, nicht allein Vertrauen in die caller-gefilterte Id-Menge | Konvention von `SecurityPriceService`: jede Methode prüft den Besitzerverweis selbst (`ListAsync` Zeile 88, `GetLatestDateAsync` Zeile 109). Fremde Ids werden schweigend ausgeschlossen — analog der „leere Liste bei Fremdbesitz"-Semantik von `ListAsync` |

## Programmabläufe

### Listenabfrage `fm_securities_analysis` mit `latestPrice`

1. `tools/call fm_securities_analysis` → `FmSecuritiesAnalysisHandler.InvokeAsync`.
2. Argumente parsen via `McpToolArguments` (`securityId`, `onlyActive`, `includePrices`, `priceTake`, `includeCategories`, `skip`, `take`) — unverändert.
3. `securities` über `_securities.ListAsync(owner, true/false)` je `onlyActive`, danach `Skip(skip).Take(take)`; `includePrices ??= false` — unverändert.
4. `McpRecordScopeFilter.ApplyAsync` (Dimension `SecurityCategoryId = s.CategoryId`) → `visible` — unverändert; erst danach erfolgt jede Kursabfrage.
5. **Neu:** Id-Menge der sichtbaren Wertpapiere bilden (`visible.Select(s => s.Id)`) und **eine** Batch-Abfrage `_prices.GetLatestPricesAsync(owner, ids, ct)` ausführen → `IReadOnlyDictionary<Guid, SecurityPriceDto>`. Bei leerer `visible`-Liste wird kein Service-Aufruf benötigt (bzw. liefert der Service ein leeres Dictionary).
6. Pro sichtbarem Wertpapier wie bisher das anonyme Ausgabeobjekt bauen; **neu** `latestPrice` aus dem Dictionary: Treffer → `{ close = p.Close, date = p.Date }`, kein Treffer → `null`. `prices` bleibt unverändert nur bei `includePrices == true` über `_prices.ListAsync` gefüllt.
7. Optionale `categories` unverändert; Rückgabe `{ securities, categories }`.

Beteiligte Klassen/Komponenten: `FmSecuritiesAnalysisHandler`, `ISecurityService`, `ISecurityPriceService` (`GetLatestPricesAsync`, `ListAsync`), `McpRecordScopeFilter`, `ISecurityCategoryService`, `SecurityPriceDto`.

### Einzelabfrage (`securityId` gesetzt)

1. `_securities.GetAsync(securityId, owner, ct)`; `null` (fehlend/fremd) → leere `securities`-Liste — unverändert.
2. `includePrices ??= true` — unverändert.
3. Danach identischer Weg wie im Listenablauf ab Schritt 4: Record-Filter → Batch-Abfrage mit einstufiger Id-Menge (kein separater Codepfad) → `latestPrice` im Ausgabeobjekt. Ein scope-gefiltertes `securityId` verhält sich weiterhin wie „nicht vorhanden" (leere Liste, kein `latestPrice`).

Beteiligte Klassen/Komponenten: wie Listenpfad.

### `SecurityPriceService.GetLatestPricesAsync` (Batch-Abfrage)

1. Leere Id-Menge → leeres `Dictionary`, kein Datenbankzugriff (Short-Circuit).
2. Eine Abfrage über `_db.SecurityPrices` (`AsNoTracking`), gefiltert auf `securityIds.Contains(p.SecurityId)`, ver joint mit `_db.Securities` zur Besitzerprüfung (`s.OwnerUserId == ownerUserId`) — fremde/unbekannte Ids fallen schweigend heraus.
3. `GroupBy(SecurityId)` → `Max(Date)` je Wertpapier; Rück-Join auf `(SecurityId, Date)` liefert den zugehörigen `Close` (eindeutig dank Unique-Index).
4. Projektion `new SecurityPriceDto(Date, Close)` → `IReadOnlyDictionary<Guid, SecurityPriceDto>`; Wertpapiere ohne Kurs fehlen im Dictionary → der Handler gibt dafür `null` aus.

Beteiligte Klassen/Komponenten: `SecurityPriceService`, `AppDbContext` (`SecurityPrices`, `Securities`), `SecurityPriceDto`.

## Neue Klassen

Keine neuen Produktiv-Klassen — die Umsetzung erweitert Interface, Service und Handler nach Bestandsmustern. Neue Testklassen siehe Abschnitt „Tests".

## Änderungen an bestehenden Klassen

### `ISecurityPriceService` (Interface, `FinanceManager.Application/Securities/ISecurityPriceService.cs`)

- **Neue Methoden:** `GetLatestPricesAsync(Guid ownerUserId, IReadOnlyCollection<Guid> securityIds, CancellationToken ct)` → `Task<IReadOnlyDictionary<Guid, SecurityPriceDto>>` — liefert je angefragtem Wertpapier den zuletzt gespeicherten Kurs (`SecurityId`-Schlüssel); Wertpapiere ohne Kurs oder ohne Besitzbezug fehlen im Ergebnis (Semantik analog `ListAsync`: Fremdbesitz → schweigend ausgeschlossen, kein Throw). XML-Dokumentation im Stil der Bestandsmethoden.

### `SecurityPriceService` (`FinanceManager.Infrastructure/Securities/SecurityPriceService.cs`)

- **Neue Methoden:** `GetLatestPricesAsync` — Implementierung gemäß Ablauf „Batch-Abfrage": Short-Circuit bei leerer Menge, `AsNoTracking`-Query mit Owner-Join auf `_db.Securities`, `GroupBy`/`Max(Date)` + Rück-Join für `Close`, Ergebnis als `Dictionary<Guid, SecurityPriceDto>`. Logging im Stil der Bestandsmethoden (`_logger.LogInformation`).

### `FmSecuritiesAnalysisHandler` (`FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`)

- **Geänderte Methoden:** `InvokeAsync` (Zeilen 73–162) — nach `McpRecordScopeFilter.ApplyAsync` (Zeilen 111–113) die sichtbaren `SecurityId`s sammeln und **einmalig** `GetLatestPricesAsync` aufrufen; im Per-Wertpapier-Schritt (Zeilen 115–147) das anonyme Ausgabeobjekt um `latestPrice` (`{ close, date }` bzw. `null`, Top-Level neben `hasPriceError`/`openQuantity`) erweitern — unabhängig von `includePrices`/`priceTake`. `prices`-Logik, Defaults und Rückgabeshape bleiben unverändert.

### `SpySecurityPriceService` (private Testklasse in `FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs`, Zeilen 37–85)

- **Neue Methoden:** `GetLatestPricesAsync` — Delegate an `_inner` im Stil der übrigen Member (Zeilen 64–73); ohne Erweiterung bricht der Build der Test-Assembly.

### Dokumentation

- `Docs/help/mcp-server/api.md` — neuer Tabelleneintrag in „Werkzeugverträge (Detailergänzungen)" (Zeile ~79 ff.) für `fm_securities_analysis`: `latestPrice`-Shape `{ close, date }` bzw. `null`, always-on in beiden Pfaden, `close` = letzter **gespeicherter** Schlusskurs in der `currencyCode`-Währung, `date` identisch serialisiert wie `prices[].date`, Unabhängigkeit von `hasPriceError`, `null`-Semantik, Verhalten bei fehlender/fremder/gefilterter `securityId` (leere Liste).
- `Docs/planung/mcp-integration/scope-und-toolkatalog.md` — T07-Zeile (Zeile 102): `ISecurityPriceService`-Referenz um `GetLatestPricesAsync` konkretisieren.

## Datenbankmigrationen

Keine — reine Laufzeitermittlung über das vorhandene `SecurityPrice`-Modell.

## Validierungsregeln

Keine — es entstehen keine neuen oder geänderten Eingaben. Verhaltensfestlegungen der neuen Service-Methode (keine Schnittstellenvalidierung): leere Id-Menge → leeres Ergebnis ohne Fehler; fremde/unbekannte `SecurityId`s → schweigend ausgeschlossen (Bestandskonvention der Klasse, keine `ArgumentException`).

## Konfigurationsänderungen

Keine — das Feld ist immer aktiv, ohne Schalter.

## Seiteneffekte und Risiken

- **Compile-Bruch `SpySecurityPriceService`:** manuelle `ISecurityPriceService`-Implementierung in `SecurityPriceErrorRecoveryTests` muss das neue Member mitbekommen (Pflichtschritt in der Reihenfolge). `Mock<ISecurityPriceService>`-Stellen (`SecuritiesControllerTests` Zeile 48 u. a.) sind davon nicht betroffen.
- **Antwortvertrag `fm_securities_analysis`:** additives Feld — tolerant deserialisierende MCP-Clients sind unbeeinflusst; kein Contract-Bruch. `Registry_AcceptsTheFullReadOnlyCatalog`/`DeclaredScopes_MatchTheToolCatalog` bleiben grün (Schema und Scopes unverändert).
- **Performance:** eine Batch-Abfrage pro Tool-Call statt N Einzelabfragen. Der `includePrices`-Historienpfad bleibt bewusst per Wertpapier (Bestandsverhalten, kein Regressionsrisiko, kein Scope).
- **Scheinbarer Widerspruch `hasPriceError: true` + gefülltem `latestPrice`:** fachlich gewollt (letzter Abruf schlug fehl, letzter gespeicherter Kurs existiert trotzdem) — wird im Vertrag dokumentiert.
- **Einheitliche Besitzer-Semantik:** die Batch-Methode filtert über `OwnerUserId` serverseitig — eine Kapselverletzung (Kurse fremder Wertpapiere) ist auch bei fehlerhaftem Aufrufer ausgeschlossen.

## Umsetzungsreihenfolge

1. **`ISecurityPriceService.GetLatestPricesAsync` deklarieren**
   - Voraussetzungen: keine (Interface besteht; `SecurityPriceDto` vorhanden)
   - Beschreibung: Interface-Member mit Signatur `Task<IReadOnlyDictionary<Guid, SecurityPriceDto>> GetLatestPricesAsync(Guid ownerUserId, IReadOnlyCollection<Guid> securityIds, CancellationToken ct)` inkl. XML-Doku hinzufügen.

2. **`SpySecurityPriceService` um das neue Interface-Member erweitern**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Delegate `GetLatestPricesAsync` an `_inner` in `FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs` — stellt die Kompilierbarkeit der Test-Assembly unmittelbar nach der Interface-Erweiterung sicher.

3. **`SecurityPriceService.GetLatestPricesAsync` implementieren**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Batch-Abfrage gemäß Ablauf (Short-Circuit, Owner-Join, `GroupBy`/`Max(Date)`, Rück-Join für `Close`, `IReadOnlyDictionary`-Rückgabe).

4. **`FmSecuritiesAnalysisHandler.InvokeAsync` erweitern**
   - Voraussetzungen: Schritte 1 und 3
   - Beschreibung: Batch-Aufruf nach `McpRecordScopeFilter.ApplyAsync`; `latestPrice`-Feld im anonymen Ausgabeobjekt für beide Pfade; `prices` unverändert.

5. **Service-Tests für `GetLatestPricesAsync` anlegen**
   - Voraussetzungen: Schritt 3; Testinfrastruktur EF Core InMemory + Seeding-Muster aus `SecurityPriceServiceUpsertTests` vorhanden
   - Beschreibung: neue Testklasse unter `FinanceManager.Tests/Infrastructure/Securities/` (z. B. `SecurityPriceServiceLatestPricesTests`).

6. **Handler-Tests für `latestPrice` anlegen**
   - Voraussetzungen: Schritt 4; Referenzmuster `McpBudgetOverviewToolTests`/`McpReadOnlyToolsTests` (Hilfsmethoden `NewAccess`, `NewCallContext`, `NewFilter`, `Args`, `Serialize`) vorhanden
   - Beschreibung: neue Testklasse `FmSecuritiesAnalysisToolTests` unter `FinanceManager.Tests/Mcp/` mit `Mock<ISecurityPriceService>`.

7. **`/mcp`-Integrationstest für `fm_securities_analysis` ergänzen**
   - Voraussetzungen: Schritt 4; `TestWebApplicationFactory` + `AppDbContext`-Seeding + `tools/call`-Hilfsmuster aus `McpSavingsPlanLinkTests` vorhanden
   - Beschreibung: Wire-Level-Test (Seed `User` + `Security` + `SecurityPrice`, PAT anlegen, `tools/call` gegen `/mcp`, `structuredContent.securities[].latestPrice` assertieren — u. a. Konsistenz des `date`-Formats zu `prices[].date` im Einzelpfad).

8. **Dokumentation aktualisieren**
   - Voraussetzungen: Schritt 4
   - Beschreibung: `api.md`-Vertragseintrag; `scope-und-toolkatalog.md` T07-Zeile.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetLatestPricesAsync_ReturnsLatestPricePerSecurity` | `SecurityPriceServiceLatestPricesTests` (`FinanceManager.Tests/Infrastructure/Securities/`) | Je Wertpapier wird der Kurs mit maximalem `Date` geliefert (mehrere Kurse je Wertpapier geseedet, inkl. `Close`-Korrektheit) |
| `GetLatestPricesAsync_OmitsSecuritiesWithoutPrices` | dto. | Wertpapier ohne `SecurityPrice` fehlt im Dictionary (→ `null`-Semantik im Handler) |
| `GetLatestPricesAsync_ExcludesForeignOwnedSecurities` | dto. | Ids fremder Wertpapiere / falscher `ownerUserId` liefern keine Einträge |
| `GetLatestPricesAsync_EmptyIds_ReturnsEmpty` | dto. | Short-Circuit: leere Id-Menge → leeres Dictionary, kein Fehler |
| `Invoke_ListPath_IncludesLatestPrice_WithoutIncludePrices` | `FmSecuritiesAnalysisToolTests` (`FinanceManager.Tests/Mcp/`) | `latestPrice` im Listenpfad vorhanden, obwohl `includePrices` nicht gesetzt ist; `prices` bleibt `null`; `close`/`date` aus dem Batch-Ergebnis |
| `Invoke_LatestPrice_Null_WhenNoStoredPrice` | dto. | `latestPrice: null` bei fehlendem Kurs |
| `Invoke_LatestPrice_SelectsMostRecentDate` | dto. | Auswahl des jüngsten gespeicherten Kurses bei mehreren Daten |
| `Invoke_LatestPrice_ReturnedDespiteHasPriceError` | dto. | `hasPriceError: true` + gespeicherter Kurs → Kurs in `latestPrice`, Flag bleibt `true` |
| `Invoke_SinglePath_IncludesLatestPrice` | dto. | Einzelpfad (`securityId`) liefert `latestPrice` ebenso (konsistentes Schema) |
| `Invoke_RecordFilter_AppliesBeforePriceLookup` | dto. | Scope-gefiltertes Wertpapier erscheint nicht; `Verify` auf `GetLatestPricesAsync`: nur sichtbare Ids werden abgefragt (Filter vor Kursabfrage) |
| `ToolsCall_SecuritiesAnalysis_ReturnsLatestPrice` | `McpEndpointTests` oder neue `McpSecuritiesAnalysisTests` (`FinanceManager.Tests.Integration`) | Wire-Nachweis über `/mcp`: `structuredContent.securities[0].latestPrice { close, date }`; `date`-Serialisierung konsistent zu `prices[].date` (Einzelpfad, `includePrices`-Default `true` erlaubt Direktvergleich) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `SpySecurityPriceService` in `SecurityPriceErrorRecoveryTests` (`FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs` Zeilen 37–85) | Neues `ISecurityPriceService`-Member muss implementiert werden (sonst Build-Bruch); reine Delegation an `_inner` |

Keine weiteren bestehenden Tests betroffen — `fm_securities_analysis` ist bislang nicht handler-seitig getestet, Descriptor-/Scope-Tests bleiben unverändert gültig.

### E2E-Tests (primärer Funktionsnachweis)

Keine Playwright-E2E-Tests erforderlich: Die Anforderung betrifft keinen Benutzerfluss der Blazor-UI, sondern eine rein maschinelle Schnittstelle (MCP-Client → `tools/call`). Der primäre Funktionsnachweis auf Wire-Ebene wird durch den geplanten `/mcp`-Integrationstest erbracht (echter HTTP-Endpunkt, PAT-Authentifizierung, `structuredContent`-Assert — Muster `McpSavingsPlanLinkTests`). Die neun vorbestehenden E2E-Fehlschläge (`inventory/tests.md`) stehen in keinem fachlichen Zusammenhang und bleiben außerhalb dieser Aufgabe.

Bestehende E2E-Tests anzupassen: Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Listenabfrage | MCP-Client: `tools/call fm_securities_analysis` ohne Argumente | Autorisierter Zugang (Level `Analyse`); mehrere eigene Wertpapiere, teils mit gespeicherten Kursen unterschiedlichen Datums | Argumente parsen → `ListAsync` → Record-Filter → eine Batch-Abfrage `GetLatestPricesAsync` → Ausgabeobjekte | Jedes Wertpapier trägt `latestPrice { close, date }` des jüngsten gespeicherten Kurses bzw. `null`; `prices` bleibt `null` (Default); keine `includePrices`-Notwendigkeit | Handler-Tests + `/mcp`-Integrationstest |
| 2 | `null`-Fall ohne Kursdaten | wie #1 | Wertpapier ohne `SecurityPrice`-Sätze | wie #1 | `latestPrice: null` für dieses Wertpapier, übrige Objekte normal | Handler-Test `Invoke_LatestPrice_Null_WhenNoStoredPrice` |
| 3 | `hasPriceError` mit gespeichertem Kurs | wie #1 | Wertpapier mit `HasPriceError = true` und älterem gespeichertem Kurs | wie #1 | `hasPriceError: true` **und** `latestPrice { close, date }` im selben Objekt — Flag bleibt eigenständiges Signal | Handler-Test `Invoke_LatestPrice_ReturnedDespiteHasPriceError` |
| 4 | Einzelabfrage | `tools/call` mit `securityId` | Eigenes Wertpapier mit Kursen | `GetAsync` → Record-Filter → Batch-Abfrage (einelementig) → Ausgabe | Objekt trägt `latestPrice` zusätzlich zum Default-`prices` (Historie) | Handler-Test `Invoke_SinglePath_IncludesLatestPrice` |
| 5 | Record-Scope-Filter | `tools/call` Listenabfrage | Zugang Stufe `Individuell` mit `SecurityCategoryIdsCsv`-Whitelist; Wertpapier außerhalb der Whitelist mit Kursen | Record-Filter entfernt das Wertpapier **vor** der Kursabfrage | Wertpapier fehlt in `securities`; seine Kursdaten sind nicht abrufbar (Batch erhält nur sichtbare Ids) | Handler-Test `Invoke_RecordFilter_AppliesBeforePriceLookup` |
| 6 | Fehlende/fremde `securityId` | `tools/call` mit `securityId` | `securityId` nicht vorhanden, fremd oder scope-gefiltert | `GetAsync` → `null` bzw. Record-Filter | `securities` leer — wie „nicht vorhanden", kein `latestPrice`, kein Fehler | Bestandsverhalten (unverändert); indirekt durch Filter-Test abgesichert |

## Offene Punkte

Keine — alle fünf offenen Fragen der Anforderung sind geklärt: `hasPriceError`-Semantik (letzter gespeicherter Kurs), Umsetzungsort (Variante a, nur MCP-Ausgabeobjekt) und Batch-Abfrage am `ISecurityPriceService` folgen den beauftragten Betreuer-Empfehlungen; das Datumsformat ist auf Konsistenz mit `prices[].date` festgelegt (Abweichung vom illustrativen Issue-Beispiel in `api.md` dokumentiert); der Tie-Break ist durch den Unique-Index `{ SecurityId, Date }` modellseitig ausgeschlossen; das Verhalten bei fehlender/fremder/gefilterter `securityId` ist durch die Bestandsaufnahme bestätigt (leere `securities`-Liste, unverändert).
