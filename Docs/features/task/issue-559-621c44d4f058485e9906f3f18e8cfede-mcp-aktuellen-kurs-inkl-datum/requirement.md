# Anforderung — Issue #559: MCP: aktuellen Kurs inkl. Datum direkt in Wertpapier-Stammdaten ausgeben

## Fachliche Zusammenfassung

Das MCP-Lesetool `fm_securities_analysis` (`FmSecuritiesAnalysisHandler`, `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`) soll jedem ausgegebenen Wertpapier-Objekt ein neues Top-Level-Feld `latestPrice` mitgeben, das den zuletzt **gespeicherten** Schlusskurs (`close`) samt Kursdatum (`date`) enthält — in der Listen- wie in der Einzelabfrage und unabhängig von `includePrices`/`priceTake`. `latestPrice` ist `null`, wenn kein Kurs gespeichert ist. Das bestehende `prices`-Array (Kurshistorie) bleibt unverändert optional; `latestPrice` löst den impliziten Workaround `includePrices: true` + `priceTake: 1` ab und macht die Kursaktualität über das Datum transparent (keine serverseitige Staleness-Bewertung).

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein Wertpapier-Ausgabeobjekt in der Antwort von `fm_securities_analysis` — sowohl im Listenpfad (`securityId` nicht gesetzt) als auch im Einzelpfad (`securityId` gesetzt). Zum Scope gehören ausschließlich die bereits per `McpRecordScopeFilter` freigegebenen Wertpapiere des Zugangsbesitzers (`context.Access.OwnerUserId`).
- **Auslösende Ereignisse:** Jeder `tools/call`-Aufruf von `fm_securities_analysis` durch einen autorisierten MCP-Zugang. Kein Nutzer-UI-Ereignis, kein Zeit-/Systemtrigger.
- **Erwartete Wirkung:** Jede Antwort enthält pro Wertpapier `latestPrice: { close, date }` bzw. `null` — der MCP-Client kann den letzten bekannten Kurs aller Positionen direkt ablesen, ohne `includePrices`/`priceTake` setzen zu müssen. `null` signalisiert „kein Kurs gespeichert". Das Verhalten bei `hasPriceError: true` ist vor Umsetzung festzulegen (siehe Offene Fragen); die Betreuer-Empfehlung lautet: letzter gespeicherter Kurs wird unabhängig vom Fehlerflag ausgegeben, `hasPriceError` bleibt eigenständiges Signal.
- **Umgang mit fehlenden Objekten:** Wertpapier ohne gespeicherte `SecurityPrice`-Sätze → `latestPrice: null` (kein Erzeugen, kein Fehler). Ein `SecurityPriceError`-Flag ohne gespeicherten Kurs → ebenfalls `null`. Fehlende/fremde `securityId` im Einzelpfad → bestehendes Verhalten unverändert (leere `securities`-Liste bzw. nicht vorhanden).
- **Scope-Ausschlüsse:**
  - Kein Live-/Realtime-Kurs und kein externer Kursabruf — ausschließlich persistierte `SecurityPrice`-Daten.
  - Keine serverseitige Staleness-Bewertung oder Schwellwerte; die Aktualitätsbeurteilung obliegt dem Aufrufer anhand von `date`.
  - Keine Änderung am `prices`-Array, an den Eingabeparametern (`includePrices`, `priceTake` bleiben wirksam), am Tool-Schema, an den Required Scopes oder an der Sicherheitsklasse (`Lesend`).
  - Kein persistiertes/denormalisiertes Feld am `Security`-Entity (keine EF-Migration, keine Backup-DTO-Pflicht) — reine Laufzeitermittlung.
  - Keine Ausweitung auf `GET /api/securities`/`SecurityDto`/Blazor-UI, sofern die Scope-Frage (Offene Fragen) nicht anders entschieden wird.

## Betroffene Klassen und Komponenten

- `FmSecuritiesAnalysisHandler` (`FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`, `InvokeAsync` Zeilen 73–162): anonymes Ausgabeobjekt um `latestPrice` erweitern; Ermittlung des letzten Kurses **nach** `McpRecordScopeFilter.ApplyAsync` (Zeilen 111–113), im selben Per-Wertpapier-Schritt wie `prices`, aber unabhängig von `includePrices`.
- `ISecurityPriceService` (`FinanceManager.Application/Securities/ISecurityPriceService.cs`): voraussichtlich neue Batch-Methode, z. B. `GetLatestPricesAsync(ownerUserId, securityIds, ct)` → Mapping `SecurityId → SecurityPriceDto` (oder vergleichbar), um das N+1-Problem bei Always-on-Listenabfragen zu vermeiden. `GetLatestDateAsync` (Zeile 51) liefert nur das Datum und reicht nicht; `ListAsync` mit `take: 1` wäre funktional korrekt (absteigende Sortierung, Zeile 42), verursacht aber eine Abfrage pro Wertpapier.
- `SecurityPriceService` (`FinanceManager.Infrastructure/Securities/SecurityPriceService.cs`): Implementierung der Batch-Abfrage auf `_db.SecurityPrices` (Gruppierung je `SecurityId`, max. `Date`, analog `ListAsync` Zeilen 82–94 / `GetLatestDateAsync` Zeilen 106–113; Besitzerprüfung über die übergebene, bereits besitzer-/scope-gefilterte `SecurityId`-Menge).
- `SecurityPriceDto` (`FinanceManager.Shared/Dtos/Securities/SecurityPriceDtos.cs`): als Ergebnistyp wiederverwendbar (`Date`, `Close`).
- `SecurityDto` + `SecuritiesController` (`FinanceManager.Shared/Dtos/Securities/SecurityDto.cs`, `FinanceManager.Web/Controllers/SecuritiesController.cs`): nur betroffen, falls Scope-Variante (b) gewählt wird — dann wirkt das Feld auch auf `GET /api/securities` und die Blazor-UI.
- Tests: `FinanceManager.Tests/Mcp/` — bislang deckt `McpReadOnlyToolsTests.cs` für T07 nur Descriptor/Scopes ab (Zeilen 75, 114); handlerbezogene Tests für `latestPrice` sind neu zu erstellen (Feld vorhanden, `null`-Fall, Auswahl des letzten Kurses, `hasPriceError`-Fall, Reihenfolge Record-Filter vor Kursabfrage). Bei neuer Service-Methode: Tests unter `FinanceManager.Tests/Infrastructure/Securities/` (vgl. `SecurityPriceServiceUpsertTests.cs`).
- Dokumentation: `Docs/help/mcp-server/api.md` (Tabelle „Werkzeugverträge (Detailergänzungen)", Zeile ~78 ff. — `fm_securities_analysis` erhält einen Vertragseintrag zu `latestPrice` inkl. `hasPriceError`-Semantik und Datumsformat); ggf. `Docs/planung/mcp-integration/scope-und-toolkatalog.md` (T07, Zeile 102).

## Implementierungsansatz

- `latestPrice` wird auf Top-Level des anonymen Ausgabeobjekts ergänzt (neben `openQuantity`, `hasPriceError`) — **nicht** unter `untrusted_data`, da es sich um serverseitig ermittelte Zahlen/Daten handelt. Shape: `{ close: decimal, date: … }` bzw. `null`.
- Für die Liste werden die Kurse aller sichtbaren Wertpapiere in einer Batch-Abfrage geholt (Empfehlung des Betreuer-Kommentars: neue `ISecurityPriceService`-Methode statt Schleife über `ListAsync(0, 1)`); für den Einzelpfad genügt die Einzelabfrage desselben Wegs.
- Der Record-Scope-Filter (`McpRecordScopeFilter.ApplyAsync`, Dimension Wertpapierkategorien über `Security.CategoryId`) greift weiterhin **vor** jeder Kursabfrage — keine Kursdaten gefilterter Wertpapiere sind abrufbar. Es sind keine neuen Scopes (`(Wertpapiere, Lesen)`, `(Wertpapierkategorien, Lesen)` bleiben ausreichend) und kein neuer Eingabeparameter erforderlich.
- Datumsformat: zu entscheiden zwischen (i) identischer Serialisierung wie `prices[].date` (`DateTime` via `System.Text.Json`) und (ii) bewusst date-only `yyyy-MM-dd`. Empfehlung: Konsistenz mit `prices[].date` bzw. date-only nur mit Begründung.
- `hasPriceError`-Semantik: zu entscheiden (siehe Offene Fragen); Empfehlung: `latestPrice` liefert den letzten gespeicherten Kurs unabhängig vom Fehlerflag.
- Keine neue Klasse für die Ausgabe nötig (anonyme Objekte wie bisher); bei Variante (b) wäre `SecurityDto` um ein `LatestPrice`-Feld zu erweitern — mit Auswirkung auf API-Controller und Blazor-ViewModels.

## Konfiguration

Keine Konfiguration vorgesehen — das Feld ist immer aktiv („always-on"), unabhängig von Tool-Parametern und ohne Schalter.

## Offene Fragen

1. **Semantik bei `hasPriceError: true`:** `latestPrice: null` oder letzter gespeicherter Kurs (mit Datum) trotz Fehlerflag? Betreuer-Empfehlung: letzter gespeicherter Kurs; `hasPriceError` bleibt eigenständiges Signal. Entscheidung ist im Vertrag (`api.md`) zu dokumentieren.
2. **Umsetzungsort:** Variante (a) nur MCP-Ausgabeobjekt vs. Variante (b) `SecurityDto`-Erweiterung (Wirkung auf `GET /api/securities` und Blazor-UI). Betreuer-Empfehlung: (a). Der Issue-Titel („Wertpapier-Stammdaten") deutet auf (b), die Beschreibung und die genannten Komponenten auf (a) — verbindlich festzulegen.
3. **Datumsformat von `latestPrice.date`:** gleiche Serialisierung wie `prices[].date` oder explizit `yyyy-MM-dd`? Issue-Beispiel zeigt `"2026-11-19"` (date-only); Konsistenz mit `prices[].date` spricht für identische `DateTime`-Serialisierung.
4. **Zuordnung bei Mehrfach-Kursen am selben Datum / Tie-Break:** unklar, ob pro `SecurityId` genau ein `SecurityPrice` je Datum existiert (Upsert-Semantik legt dies nahe) — bei der Batch-Abfrage ist das deterministische Verhalten (max. `Date`, ggf. stabiler Tie-Break) festzulegen.
5. **`securityId`-Einzelpfad ohne Treffer / gefiltertes Wertpapier:** bleibt `latestPrice` implizit entfallen (leere Liste), oder soll dokumentiert werden, dass ein scope-gefiltertes `securityId` wie „nicht vorhanden" behandelt wird? (Bestandsverhalten vermutlich unverändert — zu bestätigen.)
