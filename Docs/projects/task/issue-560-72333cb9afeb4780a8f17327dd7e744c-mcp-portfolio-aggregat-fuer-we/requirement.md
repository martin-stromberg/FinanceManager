# Projektanforderung – MCP-Erweiterungen Wertpapiere & granulare Genehmigung

**Quelle:** GitHub-Issues #560, #561, #562, #578 (martin-stromberg/FinanceManager), Auftrag via Issue-Kommentar: gemeinsame Umsetzung, ein gemeinsamer PR, Statuskommentare je Issue, Screenshots als Beleg.

**Branch:** `task/issue-560-72333cb9afeb4780a8f17327dd7e744c-mcp-portfolio-aggregat-fuer-we`

## Ziel

Vier MCP-bezogene Anforderungen werden in einem Projekt umgesetzt:

1. **#560 – Portfolio-Aggregat:** `fm_securities_analysis` erhält eine optionale Aggregationsfunktion analog zu `includeStatistics` bei `fm_accounts_overview`.
2. **#561 – Fremdwährungsumrechnung:** EUR-Gegenwert je Position bzw. Aggregat über automatisiert abrufbare Wechselkurse (z. B. EZB-Referenzkurse), mit transparentem Stand.
3. **#562 – Datenauffälligkeits-Transparenz:** `hasPriceError` wird durch die bereits persistierte `PriceErrorMessage`/`PriceErrorSinceUtc` erklärbar; Tool-Doku erläutert leere `prices` und negative `openQuantity`.
4. **#578 – Granularer Genehmigungsmodus:** Optionaler Expertenmodus je MCP-Zugang mit Vorab-Freigaben auf einer dokumentierten Genehmigungseinheit (bevorzugt `(McpDataType, McpRightKind)`), inklusive operationsgenauer Gate-Entscheidung für gemischte Admin-Tools.

## Fachliche Anforderungen (aus den Issues belegt)

### #560 – Portfolio-Aggregat

- Neuer opt-in Parameter (Vorschlag `includeStatistics`), der der Antwort ein `statistics`-Objekt ergänzt; ohne Parameter bleibt die Antwort unverändert.
- Inhalt (Vorschlag): `securityCount`/`positionCount`, `byCurrency` (`marketValue`, `positionCount`), `byCategory` (`categoryId`, `marketValue`, `positionCount`, Kategoriename in `untrusted_data`), `positionsWithoutPrice`, optional `asOf` bzw. ältestes verwendetes Kursdatum.
- Bewertungsbasis: gehaltene Menge × letzter bekannter Schlusskurs je Position.
- Statistik über dieselbe gefilterte Menge wie die Liste (`securityId`, `onlyActive`), aber unabhängig von `skip`/`take`.
- **Record-Scope-Filter muss auf die Statistik angewendet werden** (Aggregate nur über sichtbare Datensätze — bewusste Abweichung vom `fm_accounts_overview`-Präzedenzfall).
- `untrusted_data`-Konvention für Kategorie-/Wertpapiernamen (sicherheitskonzept.md §5.2).
- Kein N+1: letzter Schlusskurs je `SecurityId` in einer gruppierten Abfrage.
- Währungsreine Teilsummen; kein falscher Einzel-Gesamtwert über Währungen hinweg (Gesamtwert nur bei einheitlicher Währung, solange #561 nicht greift).
- Tool-Descriptor dokumentiert die neuen Felder; Tests analog zu bestehenden MCP-Tool-Tests.

### #561 – Fremdwährungsumrechnung (EUR-Gegenwert)

- EUR-Gegenwert je Position optional ergänzen (Vorschlag `valueEur` = Menge × letzter Kurs × Wechselkurs zum Kursdatum), weitere Vorschläge: `fxRate { rate, date, source }` zur Nachvollziehbarkeit.
- Wechselkurs-Datum möglichst zum Kursdatum passend; bei fehlendem Kurs oder Wechselkurs `valueEur: null` statt Weglassen.
- Positionen in EUR benötigen keinen Abruf (Faktor 1).
- Quelle: EZB-Referenzkurse (schlüssellos) als naheliegende Default-Quelle; AlphaVantage-FX wäre Alternative über vorhandene Provider-Infrastruktur.
- Semantik: „Wert zum Kursdatum", kein Tageswert — Benennung/Doku entsprechend; Opt-in-Parameter erwägenswert.
- Offline-/Fehlerfälle: graceful `null`, kein Tool-Fehler; Abrufe deduplizieren/cachen, kein HTTP-Call pro Tool-Aufruf.
- Klassische Falle: EZB quotiert Fremdwährung pro 1 EUR → Division, keine Multiplikation.

### #562 – Transparenz bei Datenauffälligkeiten

- Bestehende `Security.PriceErrorMessage` und `PriceErrorSinceUtc` über `SecurityDto` bis in die `fm_securities_analysis`-Response exponieren (z. B. `priceErrorMessage`/`priceErrorSinceUtc`; Provider-Rohtext konventionsgemäß unter `untrusted_data`).
- Tool-Beschreibung von `fm_securities_analysis` ergänzen:
  - `hasPriceError` = letzter Provider-Abruf fehlgeschlagen; Flag wird nur durch erfolgreichen Abruf gelöscht; `prices` bleibt auf letztem erfolgreichem Stand.
  - Leeres `prices` ≠ Fehler (fehlender `AlphaVantageCode`, Provider-`NoData`, kein Kurs im Zeitraum).
  - Negative `openQuantity` = Σ Verkäufe > Σ Käufe → Hinweis auf fehlende/doppelte Buchungen oder nicht abgebildete Kapitalmaßnahmen, kein Leerverkauf.

### #578 – Granularer Genehmigungsmodus (Expertenmodus)

1. Einfacher Modus (`PerAction`/`PreApproved`) bleibt Standard und unverändert; keine implizite Migration.
2. Opt-in-Expertenmodus je Zugang mit sichtbarer Konfiguration der vorab freigegebenen Genehmigungseinheiten.
3. Mindestgranularität: `Administration` getrennt von fachlichen Datentypen steuerbar; bevorzugt Konfiguration je `(McpDataType, McpRightKind)`.
4. Operationsgenaue Gate-Entscheidung: lesende/nicht genehmigungspflichtige Teiloperationen gemischter Tools (`fm_admin_updates: status`/`check`, `fm_admin_backups: list`/`download`, `fm_admin_notifications: list`, `fm_admin_settings: get`) laufen bei passender Administration-Lesekonfiguration direkt.
5. Sichere Defaults: Update-Installation/-Planung, Backup-Create/Upload/Delete/Apply, Admin-Settings-Set bleiben ohne passende Vorab-Freigabe `Pending`; `Administration,Lesen` deckt diese nicht ab.
6. Keine Rechteausweitung: Scopes, Filter, AccountShare-Deckel, Admin-Gate, Kill-Switch, Write-Stop-Lid bleiben unberührt.
7. Dokumentierte Genehmigung: genehmigungspflichtige Wirkungen erzeugen auch im Expertenmodus eine `AiActionRequest`-Spur (bei passender Freigabe direkt `Approved`).
8. Konfigurationsänderungen als `AccessEvent` mit Vorher/Nachher-Delta protokollieren.
9. Persistenz/Migration: Backup-DTOs mit Defaults; alte Backups ohne neue Abschnitte bleiben importierbar; EF-Migration per `dotnet ef`.
10. UI: Expertenmodus erkennbar und erklärt (de/en-Resx), nur gültige Matrixzellen, `Administration` nur Admin-Besitzern, kritische Bestätigungen für risikoreiche Freigaben.
11. Tests: Gate-/Dispatcher-Tests (Klasse × Modus × Genehmigungseinheit), Handler-Level-Tests Admin-Suboperationen, Persistenz-/Backup-Tests, Enum-/DTO-Abdeckung, UI-/ViewModel-Tests.

## Betroffene Bereiche

- `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`, `FmAdmin*Tool.cs`, `McpToolDispatcher`
- `FinanceManager.Domain/Mcp/` (McpAccess, McpApprovalMode, AiActionRequest, ggf. neue Grant-Entität), `Security`, `SecurityPrice`, ggf. neue FX-Referenzentität
- `FinanceManager.Application/Mcp/` (Scope-Matrix, Gate-Interfaces), Security-Services
- `FinanceManager.Infrastructure/Mcp/` (McpApprovalGate, McpApprovalService, AiActionExecutionEngine, McpAccessService), Preis-/FX-Provider-Infrastruktur, EF-Migrationen
- `FinanceManager.Shared/Dtos/` (SecurityDto, McpAccessDtos)
- `FinanceManager.Web/Components/Shared/Mcp*.razor` + ViewModels + `Resources/*.resx` (de/en)
- `FinanceManager.Tests{,.Integration,.E2E}` (MCP-Tool-, Approval-, Backup-, Enum-, UI-Tests)
- `Docs/planung/mcp-integration/` (fachkonzept, berechtigungsmodell, approval-konzept, scope-und-toolkatalog, datenmodell-erweiterungen)

## Akzeptanzkriterien (Kondensat der Issues)

- #560: `includeStatistics: true` liefert `statistics` mit finalen Feldern; Filter + Record-Scope respektiert; `skip`/`take` ohne Einfluss; Positionen ohne Kurs ausgewiesen; währungsreine Teilsummen.
- #561: EUR-Gegenwert mit Kurs- und FX-Datum transparent; `null` bei fehlenden Daten; ohne FX-Verfügbarkeit kein Tool-Fehler.
- #562: Fehlergrund in der Response sichtbar (untrusted); Tool-Doku erklärt alle drei Auffälligkeiten.
- #578: siehe fachliche Anforderungen 1–11; insbesondere Admin-Routineaufrufe laufen bei `Administration,Lesen`-Freigabe direkt, Endwirkungen bleiben Pending; Audit-Spur bleibt erhalten; alte DBs/Backups kompatibel.

## Explizite Nicht-Ziele

- Kein voller `PortfolioAnalysisReportDto`-Export über MCP (Performance-/Cashflow-/Depotentwicklung) — reduzierte Marktwert-Struktur genügt (#560).
- Keine währungsübergreifende Einzelsumme ohne FX-Basis; Gesamt-EUR-Wert erst, soweit #561 die Umrechnung liefert.
- Kein neues Short-Selling-/Leerverkaufs-Feature (#562 dokumentiert nur die Semantik).
- Keine Änderung an Scopes/Filtern/Admin-Rollenmodell/Kill-Switch durch den Expertenmodus (#578).
- Kein separates neues MCP-Tool für FX-Kurse oder Portfolio-Übersicht, sofern die Erweiterung von `fm_securities_analysis` ausreicht.
- Kein Umbau von `fm_accounts_overview`-Statistik-Filterung (Präzedenz wird dokumentiert, nicht im selben Projekt geändert).

## Annahmen und zu treffende Entscheidungen (kein interaktiver Anwender verfügbar)

Die Issues führen offene Punkte auf. Ohne interaktive Rückfrage werden folgende Annahmen getroffen und in den Issue-Kommentaren transparent gemacht:

- **#560:** Aggregationsmenge = Positionen mit `openQuantity != 0` innerhalb der aktiven Filtermenge (`onlyActive` wie Liste); bei `securityId`-Einzelabruf Statistik über genau dieses Papier. Mengenbasis = `openQuantity` (bereits im DTO, billig). „Verwertbarer Kurs" = vorhandene neuester `SecurityPrice`-Zeile (kein Staleness-Cutoff, aber Kursdatum/`asOf` wird ausgegeben; `hasPriceError`-Positionen ohne Kurs zählen zu `positionsWithoutPrice`). Vertragsform = `includeStatistics` auf `fm_securities_analysis`.
- **#561:** EZB-Referenzkurse (frankfurter.dev bzw. EZB-XML) als Default-Quelle, konfigurierbar; Persistenz als benutzerübergreifende Referenz-/Cache-Tabelle mit dokumentiertem globalem Datenpool (AGENT.md-Ausnahme dokumentieren) oder Memory-Cache + gelegentlichem Refresh — Entscheidung in der Detailplanung anhand der Bestandsaufnahme. Abruf lazy mit Cache statt neuem Worker; Zielwährung EUR fest verdrahtet; Fallback = letzter Kurs ≤ Kursdatum mit Maximalalter; Opt-in-Parameter.
- **#562:** `priceErrorMessage` unter `untrusted_data`; kein neues `priceErrorReason`-Enum (Rohtext deckt Kernfall); `SecurityDto`-Erweiterung akzeptiert (kommt auch der Web-UI zugute).
- **#578:** Genehmigungseinheit = `(McpDataType, McpRightKind)`; Datenmodell = `McpApprovalMode.Granular` + neue Grant-Collection an `McpAccess`; freigebbar sind `Read`, `Complete`, `Modify`, `Create` (Matrix-valide) — `Suggest`, `Book`, `Delete` bleiben nicht vorab freigebbar; `fm_admin_updates: check` gilt als Routine; `dismiss` wird als genehmigungspflichtige Operation `(Administration, Modify)` katalogisiert; bei Grant-Entzug werden nicht ausgeführte grant-basiert auto-genehmigte Requests zurückgezogen (Präzedenz `WithdrawScopeUncovered` — bewusst strenger als „unverändert bis Ablauf"); manuell genehmigte Requests bleiben unberührt; UI = Freigabematrix im bestehenden Rechte-Editor.
