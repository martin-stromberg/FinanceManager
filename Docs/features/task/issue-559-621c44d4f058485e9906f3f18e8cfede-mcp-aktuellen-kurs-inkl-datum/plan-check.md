# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Die `requirement.md` enthält keinen explizit nummerierten Akzeptanzkriterien-Block; die Kriterien wurden aus „Fachliche Zusammenfassung", „Fachlicher Scope" und den Scope-Ausschlüssen abgeleitet.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `latestPrice { close, date }` als Top-Level-Feld pro Wertpapier in der `fm_securities_analysis`-Antwort | Designentscheidung + Änderung an `FmSecuritiesAnalysisHandler.InvokeAsync` (Umsetzungsschritt 4); Ablauf Schritte 5–6 | `Invoke_ListPath_IncludesLatestPrice_WithoutIncludePrices`, `Invoke_SinglePath_IncludesLatestPrice`, Wire-Nachweis `ToolsCall_SecuritiesAnalysis_ReturnsLatestPrice` | Abgedeckt |
| Gilt in Listen- **und** Einzelpfad | Programmablauf „Listenabfrage" und „Einzelabfrage" (gemeinsamer Weg ab Record-Filter, einelementige Id-Menge) | `Invoke_SinglePath_IncludesLatestPrice`; Akzeptanzszenario #4 | Abgedeckt |
| Unabhängig von `includePrices`/`priceTake` (always-on, kein Opt-out) | Scope-Ausschluss #6 (`includeLatestPrice`-Opt-out verworfen, begründet, Status bestätigt); Ablauf: Batch-Aufruf unabhängig von `includePrices` | `Invoke_ListPath_IncludesLatestPrice_WithoutIncludePrices` (assertiert `latestPrice` vorhanden, `prices` bleibt `null`) | Abgedeckt |
| `latestPrice: null`, wenn kein Kurs gespeichert (kein Erzeugen, kein Fehler) | Scope-Klärung „Umgang mit fehlenden Objekten"; Dictionary-Miss → `null` (Ablauf Schritt 6, Service-Ablauf Schritt 4) | `Invoke_LatestPrice_Null_WhenNoStoredPrice`; `GetLatestPricesAsync_OmitsSecuritiesWithoutPrices`; Akzeptanzszenario #2 | Abgedeckt |
| `hasPriceError: true` ohne gespeicherten Kurs → `null`; mit gespeichertem Kurs → Kurs trotz Flag ausgeben | Designentscheidung „`hasPriceError`-Semantik" (Betreuer-Empfehlung umgesetzt, Doku in `api.md` vorgesehen); Scope-Klärung erwartete Wirkung | `Invoke_LatestPrice_ReturnedDespiteHasPriceError`; Akzeptanzszenario #3 | Abgedeckt |
| `prices`-Array, Eingabeparameter, Tool-Schema, Required Scopes, Sicherheitsklasse `Lesend` unverändert | Scope-Ausschluss #3 (bestätigt); Ablauf hält `prices`-Logik unverändert | Risikoabschnitt benennt `Registry_AcceptsTheFullReadOnlyCatalog`/`DeclaredScopes_MatchTheToolCatalog` bleiben grün; `includePrices`-Default-Verhalten im Test mitassertiert | Abgedeckt |
| Kein Live-/Realtime-Kurs, keine externe Abfrage — nur persistierte `SecurityPrice`-Daten | Scope-Ausschluss #1 (bestätigt); Implementierung ausschließlich über `_db.SecurityPrices` | Indirekt durch Service-Tests (nur geseedete Kurse werden ausgegeben) | Abgedeckt |
| Keine serverseitige Staleness-Bewertung/Schwellwerte | Scope-Ausschluss #2 (bestätigt, Begründung: Aktualitätsurteil obliegt dem Aufrufer anhand `date`) | — (rein negativ; keine Umsetzung erforderlich) | Abgedeckt |
| Kein persistiertes Feld am `Security`-Entity, keine EF-Migration, keine Backup-DTO-Pflicht | Scope-Ausschluss #4 (bestätigt); Abschnitt „Datenbankmigrationen: Keine" | — | Abgedeckt |
| Keine Ausweitung auf `SecurityDto`/`GET /api/securities`/Blazor-UI (Variante a) | Scope-Ausschluss #5 (bestätigt, Betreuer-Empfehlung); Designentscheidung Umsetzungsort | — | Abgedeckt |
| Batch-Abfrage statt N+1 im Listenpfad (Betreuer-Empfehlung neue `ISecurityPriceService`-Methode) | Designentscheidung „Kurs-Ermittlung" + Rückgabetyp; Schritte 1–3 (`GetLatestPricesAsync` deklarieren, Spy erweitern, implementieren) | `GetLatestPricesAsync_ReturnsLatestPricePerSecurity`, `..._OmitsSecuritiesWithoutPrices`, `..._ExcludesForeignOwnedSecurities`, `..._EmptyIds_ReturnsEmpty` | Abgedeckt |
| Record-Scope-Filter (`McpRecordScopeFilter`) greift vor jeder Kursabfrage; keine Kursdaten gefilterter Wertpapiere | Ablauf Schritt 4→5 (Batch erst über `visible`-Ids); Designentscheidung Besitzerprüfung in Batch-Methode (Owner-Join) | `Invoke_RecordFilter_AppliesBeforePriceLookup` (Verify auf Id-Menge); `GetLatestPricesAsync_ExcludesForeignOwnedSecurities`; Akzeptanzszenario #5 | Abgedeckt |
| Fehlende/fremde/scope-gefilterte `securityId` → unverändert „nicht vorhanden"-Semantik (leere Liste) | Scope-Klärung + Ablauf Einzelpfad Schritte 1, 3; Akzeptanzszenario #6 | Bestandsverhalten unverändert; indirekt durch Filter-Test abgesichert | Abgedeckt |
| Deterministischer „letzter Kurs" / Tie-Break (offene Frage 4) | Designentscheidung: `GroupBy` + `Max(Date)` + Rück-Join; Unique-Index `{ SecurityId, Date }` schließt Duplikate aus | `GetLatestPricesAsync_ReturnsLatestPricePerSecurity` (mehrere Kurse je Wertpapier, `Close`-Korrektheit) | Abgedeckt |
| Datumsformat-Festlegung (offene Frage 3) | Designentscheidung: `DateTime`-Serialisierung identisch zu `prices[].date`, Abweichung vom Issue-Beispiel in `api.md` dokumentiert | Integrationstest assertiert Konsistenz `latestPrice.date` ↔ `prices[].date` im Einzelpfad | Abgedeckt |
| Vertragsdokumentation (`api.md`, `scope-und-toolkatalog.md`) | Umsetzungsschritt 8 inkl. konkretem Inhalt des Eintrags (`latestPrice`-Shape, `hasPriceError`-Semantik, Datumsformat, `null`-Semantik) | — | Abgedeckt |
| Compile-Bruch `SpySecurityPriceService` vermeiden | Umsetzungsschritt 2 (früh in der Reihenfolge); als betroffener bestehender Test benannt | Build-Fähigkeit der Test-Assembly | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| MCP-Client ruft `fm_securities_analysis` per `tools/call` auf und liest `latestPrice` (Protokollschnittstelle, kein UI-Fluss) | `/mcp`-Integrationstest `ToolsCall_SecuritiesAnalysis_ReturnsLatestPrice` (Schritt 7): echter HTTP-Endpunkt, PAT-Authentifizierung, Seed `User` + `Security` + `SecurityPrice`, Assert auf `structuredContent.securities[].latestPrice { close, date }` inkl. Datumsformat-Konsistenz zu `prices[].date` | Abgedeckt (reale Protokollabnahme über `tools/call` gemäß Prüfkriterium für externe Werkzeugschnittstellen) |
| Playwright-E2E für Blazor-UI | Keine geplant | Nicht erforderlich mit Begründung: Plan begründet nachvollziehbar, dass die Anforderung keinen Benutzerfluss der Blazor-UI berührt (rein maschinelle Schnittstelle); primärer Funktionsnachweis erfolgt auf Wire-Ebene |

## Hinweise

- Alle sechs Scope-Ausschlüsse tragen Begründung und Status `bestätigt`; die Scope-Klärung ist vollständig gefüllt (Verarbeitungseinheit, auslösende Ereignisse, erwartete Wirkung, Umgang mit fehlenden Objekten).
- Alle fünf offenen Fragen der Anforderung sind im Plan entschieden und die Entscheidungen sind begründet (`hasPriceError`-Semantik, Variante a, Datumsformat, Tie-Break, `securityId`-Verhalten); Abschnitt „Offene Punkte" ist sauber mit „Keine" geschlossen.
- Akzeptanzszenario #1 ist ein vollständiger Happy Path vom `tools/call`-Auslöser bis zur sichtbaren Wirkung; Szenarien #2, #3, #5, #6 benennen fehlende/gefilterte Ausgangsobjekte explizit.
- Kleinigkeit (kein Blocker): Die Unabhängigkeit von `priceTake` ist nicht als eigener Assert benannt; sie ist implizit gegeben, da `latestPrice` die Batch-Methode ohne `take`-Parameter nutzt und `Invoke_ListPath_IncludesLatestPrice_WithoutIncludePrices` die `includePrices`-Unabhängigkeit nachweist.
- Kleinigkeit (kein Blocker): Der Kombinationsfall `hasPriceError: true` **ohne** gespeicherten Kurs ist in der Scope-Klärung festgelegt (`null`), aber nicht als eigener Testfall benannt; fachlich identischer Codepfad wie `Invoke_LatestPrice_Null_WhenNoStoredPrice` (Dictionary-Miss unabhängig vom Flag). Bei Gelegenheit als zweiter Fall im selben Test mitdenkbar.
