# Projektplan – MCP-Erweiterungen Wertpapiere & granulare Genehmigung

## Übersicht

**Projekttitel:** MCP: Portfolio-Aggregat, EUR-Gegenwert, Preisfehler-Transparenz und granularer Genehmigungsmodus (Issues #560, #561, #562, #578)

**Gesamtziel:** `fm_securities_analysis` wird um ein opt-in Statistik-Aggregat (#560) und einen EUR-Gegenwert über automatisierte Wechselkurse (#561) erweitert; Preisfehler werden transparent (#562); MCP-Zugänge erhalten einen optionalen Expertenmodus mit granularen Vorab-Freigaben und operationsgenauer Gate-Entscheidung (#578). Umsetzung in vier sequenziellen Entwicklungsschritten auf dem Projekt-Basisbranch, danach ein gemeinsamer PR gegen `staging`.

**Auftrags-Deliverables (Abschlussphase, nach allen Schritten):** Push des Basisbranch; gemeinsamer PR gegen `staging` mit Closing-Tags für alle vier Issues; Statuskommentar am jeweiligen Issue mit Umsetzungsstand und dokumentierten Annahmen; Screenshots als Beleg (UI-Nachweis für Schritt 4, Tool-Response-/Test-Nachweise für Schritte 1–3); Überwachung der PR-Checks bis zum Erfolg.

**Betroffene Bereiche:** MCP-Tools (`FmSecuritiesAnalysisTool`, `FmAdmin*Tool`), Domain (`Security`, `McpAccess`, neue Entitäten `CurrencyExchangeRate`, `McpAccessApprovalGrant`), Application/Infrastructure (Security-/FX-Services, `McpApprovalGate`, `McpApprovalService`, `McpAccessService`), Web-UI (`McpRightsEditor`, `McpAccessCreateWizard`, `McpAccessExtrasPanel`, ViewModels, Resx de/en), EF-Migrationen (2), Backup (`McpAccessBackupDto`, neue optionale Sektion), Tests (Unit + Integration + ggf. E2E), Planungs-/Hilfe-Doku, Release Notes.

## Grobe Vorgehensentscheidungen

1. **Reihenfolge nach Kopplung und Risiko:** Zuerst der kleine Transparenz-Schritt (#562), dann das Statistik-Aggregat (#560) im selben Tool, dann die FX-Erweiterung (#561), die auf dem Statistik-Vertrag aufbaut; der unabhängige, größte Block (#578) zum Schluss. Schritte 1–3 berühren alle `FmSecuritiesAnalysisTool` — Sequenzialität vermeidet Merge-Konflikte.
2. **Statistik ohne neue DB-Abfrage-Infrastruktur:** Die Statistik aggregiert serverseitig über die bereits geladene, record-gefilterte Menge (`openQuantity` liegt im `SecurityDto`, `GetLatestPricesAsync` liefert Latest-Close gruppiert). Bewusste Abweichung vom `fm_accounts_overview`-Präzedenzfall: der Record-Scope-Filter wird auf die Statistik angewendet.
3. **FX als persistenter globaler Referenz-Cache:** Neue Entität ohne `OwnerUserId` (dokumentierte Ausnahme analog `McpSettings`/`WellKnownSettings`, XML-Doku + `Docs/`-Vermerk), schlüssellose EZB-Referenzkurse via Frankfurter-kompatiblem HTTP-Endpunkt (Basis-URL konfigurierbar), lazy beim Bedarf je Währung+Datum mit Abruf des relevanten Zeitraums, Fallback „letzter gespeicherter Kurs ≤ Kursdatum", Offline → `valueEur: null`. Rechnung `value / rate` (EZB quotiert Fremdwährung je 1 EUR) wird durch Tests abgesichert.
4. **Expertenmodus als dritter Enum-Wert + Grant-Collection + Aktionskatalog:** `McpApprovalMode.Granular`; neue Kind-Entität `McpAccessApprovalGrant (McpAccessId, OwnerUserId, DataType, Right)`; statischer Aktionskatalog bildet `(tool, actionKey)` auf `{ erfordertGenehmigung, DataType, Right }` ab — vollständig für die vier Admin-Tools, für Master-Data-Tools nach feststellbarer Op→Recht-Zuordnung; nicht katalogisierte Operationen behalten `PerAction`-Semantik. Grants decken exakt `(DataType, Right)`; Rechte `Book`/`Delete`/`Suggest` sind nicht vorab freigebbar.
5. **Gate-Erweiterung um actionKey + auto-Approved-Enqueue über Grants:** `EvaluateAsync` erhält den ActionKey; Routine-Operationen laufen bei passendem Grant direkt, sonst Queue. Genehmigungspflichtige Wirkungen erzeugen auch bei Grant eine `AiActionRequest`-Spur, die via Grant-Prüfung im `EnqueueAsync` direkt `Approved` startet (persistiertes `PreApprovalKind`-Feld als Diskriminator). Grant-Entzug zieht nicht ausgeführte, grant-basiert auto-genehmigte Requests zurück (Präzedenz `WithdrawScopeUncoveredForAccessAsync`); manuell genehmigte Requests bleiben unberührt.
6. **Backup:** Grants als verschachtelte Collection im `McpAccessBackupDto` mit Default (alte Backups importierbar), zusätzlich Export der Grant-Rows als optionale Sektion analog `McpAccessScopes`; `RequiredDataProperties` unverändert. FX-Tabelle als optionale Sektion exportieren + `TryGetProperty`-Guard im Import (globale Referenzdaten, re-importierbar).
7. **UI minimalinvasiv:** Dritter Radio-Button im Wizard (gleiche Level-Bedingung wie heute), Grant-Matrix als eigener Abschnitt im `McpRightsEditor` (nur gültige Matrixzellen, `Administration` nur Admin-Besitzern, risikoreiche Freigaben mit kritischer Bestätigung), Hinweis-Banner analog PreApproved-Banner; alle Texte in `Pages.de/.en.resx`.
8. **Doku mitführen:** `Docs/planung/mcp-integration/` (approval-konzept, berechtigungsmodell, scope-und-toolkatalog, datenmodell-erweiterungen, sicherheitskonzept) und `Docs/help/mcp-server/` pro Schritt aktualisieren; RELEASE_NOTES am Projektende gesammelt.

## Entwicklungsschritte

### Schritt 1: MCP-Transparenz bei Preisfehlern und Mengenanomalien (Issue #562)

`fm_securities_analysis` exponiert den Preisfehler-Grund und erklärt Datenauffälligkeiten in der Tool-Beschreibung.

**Fachlicher Umfang:**
- `SecurityDto` um `PriceErrorMessage` (string?) und `PriceErrorSinceUtc` (DateTime?) erweitern; `SecurityService.MapToDtoAsync` mappt die bereits persistierten Entity-Felder.
- `FmSecuritiesAnalysisTool`: je Position `priceErrorSinceUtc` als strukturiertes Feld sowie `priceErrorMessage` **unter `untrusted_data`** (Provider-Rohtext) ausgeben — nur wenn `hasPriceError` gesetzt bzw. Werte vorhanden; konsistent für Listen- und Einzelabruf.
- Tool-Beschreibung (`Descriptor.Description` bzw. Schema-Beschreibung) ergänzen: Semantik von `hasPriceError` (letzter Provider-Abruf fehlgeschlagen; nur durch erfolgreichen Abruf gelöscht; `prices` bleibt letzter erfolgreicher Stand), leeres `prices` ≠ Fehler (kein `AlphaVantageCode`, Provider-`NoData`, kein Kurs im Zeitraum), negative `openQuantity` = Buchungsinkonsistenz (Σ Verkäufe > Σ Käufe), kein Leerverkauf.
- Unit-Tests `FmSecuritiesAnalysisToolTests` (Fehlerfelder inkl. untrusted-Platzierung, Single-/List-Modus), DTO-Mapping-Test; Integrationstest auf Wire-Ebene (`McpSecuritiesAnalysisTests`) ergänzen.

**Akzeptanzkriterien:**
- Response enthält bei Positionen mit Preisfehler den Grund (`priceErrorMessage` in `untrusted_data`) und `priceErrorSinceUtc`; fehlerfreie Positionen unverändert.
- Tool-Beschreibung erklärt alle drei Auffälligkeiten.
- `SecurityDto`-Erweiterung ist abwärtskompatibel (optionale Felder); keine Persistenz-/Backup-Änderung.
- Alle bisherigen Tests bestehen; neue Tests decken die Felder ab.

**Abhängigkeiten:** Keine.

**Betroffene Bereiche:** `FmSecuritiesAnalysisTool.cs`, `SecurityDto`, `SecurityService` (Mapping), MCP-Tests.

### Schritt 2: Statistik-Aggregat `includeStatistics` für `fm_securities_analysis` (Issue #560)

Opt-in `includeStatistics`-Parameter ergänzt die Antwort um ein `statistics`-Objekt mit währungs- und kategoriereinen Aggregaten.

**Fachlicher Umfang:**
- Neues `SecurityStatisticsDto` (`Shared/Dtos/Securities/`): `securityCount`, `positionCount` (Positionen mit `openQuantity != 0`), `byCurrency [{ currencyCode, marketValue, positionCount, positionsWithoutPrice? }]`, `byCategory [{ categoryId, marketValue, positionCount, untrusted_data:{ name } }]`, `positionsWithoutPrice` (Anzahl + IDs), `totalMarketValue` + `totalCurrencyCode` nur bei einheitlicher Währung (sonst `null`), `asOf`/`oldestPriceDate` (ältestes verwendetes Kursdatum) als Stichtags-Transparenz.
- Aggregationsmenge = record-gefilterte (`McpRecordScopeFilter`) und fachlich gefilterte (`securityId`, `onlyActive`) Menge, **ungeachtet `skip`/`take`**; Positionen ohne Kurs fließen mit `openQuantity` in `positionCount`, aber nicht in `marketValue` ein.
- **Wichtige Implementierungsvorgabe:** `McpRecordScopeFilter.ApplyAsync` läuft heute erst nach `Skip/Take` auf dem Paging-Fenster. Für die Statistik ist der Record-Scope-Filter auf die ungepagte Filtermenge anzuwenden und das Paging erst danach für die Anzeige zu schneiden; der Preis-Batch (`GetLatestPricesAsync`) läuft über die volle sichtbare ID-Menge, nicht nur über die Seite. Die damit verbundene Umordnung (Record-Filter vor Pagination) ist eine bewusste Korrektur — sichtbare Liste und Statistik beziehen sich so auf dieselbe Menge und Seiten füllen sich korrekt.
- Bewertungsbasis: `openQuantity` × letzter bekannter Schlusskurs (`GetLatestPricesAsync`, ein Batch-Aufruf — kein N+1).
- Implementierung als eigenständiger Service (z. B. `ISecurityStatisticsService` in Application/Infrastructure) oder als Tool-interne Aggregation — Entscheidung im Step-Plan; Statistik-Contract getrennt vom Positions-Payload.
- `includeStatistics` ins Input-Schema + Descriptor-Doku; bei `securityId` Statistik über genau dieses Papier.
- Unit-Tests (Aggregationslogik inkl. Währungsreinheit, positionsWithoutPrice, Filterung, Record-Scope-Respekt) + Integrationstest.

**Akzeptanzkriterien:**
- `includeStatistics: true` liefert `statistics` mit obigen Feldern; ohne Parameter `statistics: null`/unverändert.
- Record-Scope-Filter, `securityId`, `onlyActive` wirken auf die Statistik; `skip`/`take` nicht.
- Positionen ohne verwertbaren Kurs sind ausgewiesen und nicht in Marktwerten enthalten; kein falscher Gesamtwert bei Mehrwährung.
- Kein N+1; `untrusted_data`-Konvention für Kategorienamen.

**Abhängigkeiten:** Schritt 1 (gleiches Tool — technisch, nicht fachlich).

**Betroffene Bereiche:** `FmSecuritiesAnalysisTool.cs`, neues DTO + Service, MCP-Tests.

### Schritt 3: EUR-Gegenwert für Wertpapierpositionen via Wechselkurs (Issue #561)

Opt-in EUR-Bewertung je Position (und EUR-Gesamtwert in der Statistik) über automatisierte EZB-Referenzkurse.

**Fachlicher Umfang:**
- Neue globale Referenzentität `CurrencyExchangeRate` (Id, CurrencyCode, Date, RatePerEur, Source, CreatedUtc; **dokumentierte `OwnerUserId`-Ausnahme** — benutzerübergreifender Cache, XML-Doku + Docs-Eintrag), DbSet + EF-Konfiguration (Unique-Index je Währung+Datum, Zusammensetzung s. `IFxRateService`-Punkt) + `dotnet ef`-Migration.
- `IFxRateProvider` (Application) + Frankfurter-/EZB-kompatible Implementierung (Web): named `HttpClient`, Basis-URL + Aktivierung konfigurierbar (`Fx:…`), Timeout, Retry-Muster an AlphaVantage angelehnt; liefert Tages-/Zeitreihenkurse „Fremdwährung je 1 EUR".
- `IFxRateService` (Application/Infrastructure): `GetRateAsync(currency, date)` → letzter verfügbarer Kurs ≤ Datum; fehlende Bereiche werden beim Provider abgerufen und persistiert (Dedupe: ein Abruf je Währung+Zeitraum; parallele Lazy-Fills derselben Währung per Sperre/In-Flight-Dedupe absichern); bei Abruffehler Fallback auf älteren gespeicherten Kurs; EUR → Faktor 1 ohne Abruf; keine Netzverfügbarkeit → `null` (kein Tool-Fehler). Konfigurierbares Maximalalter als dokumentierte Option. Unique-Index `(CurrencyCode, Date, Source)` bzw. `(CurrencyCode, Date)` mit einheitlicher Quelle — im Step-Plan final entscheiden.
- `fm_securities_analysis`: Opt-in `includeEurValue` (Default false). Je Position zusätzlich `valueEur` (= `openQuantity × latestPrice.close / rate` des Kursdatums; `null` bei fehlendem Kurs oder fehlendem Wechselkurs) und `fxRate { rate, date, source }` bei Fremdwährung; Semantik „Wert zum Kursdatum" im Descriptor dokumentieren. Statistik (Schritt 2) um `marketValueEur` je `byCurrency`-Zeile sowie `totalMarketValueEur` ergänzen (nur wenn `includeStatistics` **und** `includeEurValue`; `null`-tolerant je Zeile).
- Backup: `CurrencyExchangeRates` als optionale Sektion exportieren und im `SetupImportService` mit `TryGetProperty`-Guard importieren (fehlende Sektion = leere Liste); **nicht** in `RequiredDataProperties`.
- Tests: Rate-Mathematik (Division, nicht Multiplikation), EUR-Shortcut, Fallback/Staleness, Offline-`null`, Backup-Roundtrip, Mock-HTTP-Provider-Tests, MCP-Tool-Tests, Enum-/DTO-Abdeckung.

**Akzeptanzkriterien:**
- `includeEurValue: true` liefert `valueEur` + `fxRate`-Metadaten je Position; `null` statt Fehler bei fehlenden Daten.
- Wechselkursdatum passt zum Kursdatum (Fallback ≤ Datum sichtbar via `fxRate.date`/`source`).
- Statistik zeigt EUR-Summen, soweit alle Werte einer Zeile umgerechnet werden konnten.
- Kein HTTP-Call pro Tool-Aufruf bei vorhandenen Kursen; Quelle konfigurierbar; Backup/Restore abgesichert.

**Abhängigkeiten:** Schritt 2 (baut auf dem `statistics`-Vertrag auf; gleiche Tool-Datei).

**Betroffene Bereiche:** Domain (neue Entität), Application (FX-Contracts), Infrastructure (FX-Service, DbContext, Migration), Web (Provider, Tool), Shared (kein DTO nötig bzw. klein), Backup/Import, Tests, `appsettings`, Docs.

### Schritt 4: Granularer Genehmigungsmodus (Expertenmodus) für MCP-Zugänge (Issue #578)

Optionaler dritter Genehmigungsmodus mit Vorab-Freigaben je `(McpDataType, McpRightKind)` und operationsgenauer Entscheidung für gemischte Tools.

**Fachlicher Umfang:**
- Domain: `McpApprovalMode.Granular` (EnumCoverageTests + Referenz-Test + Resx `EnumType_Mcp*`/Anzeigetexte + Konzept-Doku §2.2); neue Entität `McpAccessApprovalGrant` (Id, McpAccessId, OwnerUserId, DataType, Right) + Collection `ApprovalGrants` an `McpAccess` inkl. Set-Methoden mit Validierung (nur Matrix-valide Tupel; freigebbar `Read`, `Complete`, `Modify`, `Create` — `Suggest`, `Book`, `Delete` nicht zulässig; `Administration` nur bei Admin-Besitzer).
- `McpApprovalActionCatalog` (Application): statische Zuordnung `(tool, actionKey) → { requiresApproval, requiredDataType, requiredRight }`, vollständig für `fm_admin_updates`, `fm_admin_backups`, `fm_admin_notifications` (inkl. `dismiss` als genehmigungspflichtige Operation `(Administration, Modify)` — unter `PerAction` queued der Aufruf heute faktisch am Klassen-Gate; unter `Granular` ohne passenden Grant bleibt er `Pending`, mit Grant dokumentiert auto-`Approved`), `fm_admin_settings`; für Master-Data-Tools nach feststellbarer Op→Recht-Zuordnung; unbekannt → `null` (konservativ).
- Gate: `IMcpApprovalGate.EvaluateAsync` erhält `actionKey`; bei `Granular`: `Schreibvorbereitend` → `Direct`, wenn Katalog-Eintrag „Routine" und Grant `(DataType,Right)` passt, sonst `RequiresApproval`; `Genehmigungspflichtig` → `RequiresApproval`; Lesend/Vorschlagend/Write-Stop/Pending-Limit unverändert.
- Enqueue: `McpApprovalService.EnqueueAsync` prüft bei `Granular` den Grant gegen Katalog-Profil → `CreatePreApproved` mit eigener DecisionNote; sonst Pending. Handler-Fallback-Pfad analog.
- Diskriminator: `AiActionRequest` erhält ein persistiertes Feld `PreApprovalKind` (Enum `{ None, AccessPreApproved, GrantPreApproved }`, `HasConversion<short>`, BackupDto-Default, EnumCoverageTests) statt Stringvergleich auf `DecisionNote` — robust für den Withdrawal-Abgleich.
- Konsistenz: Bei Grant-Änderung werden nicht ausgeführte Requests mit `PreApprovalKind = GrantPreApproved`, deren Grant entfallen ist, zurückgezogen (`WithdrawBySystem`, Präzedenz `WithdrawScopeUncoveredForAccessAsync`); manuell genehmigte und access-pauschal-vorab-genehmigte Requests bleiben bestehen.
- Service/API: Grants in `McpAccessDetailDto`, Update-Pfad (z. B. `PUT {id}/approval-grants` analog Scopes oder im Update-Request), Validierung + `AccessEvent`-Audit-Delta (`ApprovalGrantsChanged`).
- Persistenz: DbSet + Konfiguration (Unique `(McpAccessId, DataType, Right)`, Cascade), `dotnet ef`-Migration (deckt auch `AiActionRequest.PreApprovalKind` ab); `McpAccessBackupDto` um `ApprovalGrants` (Default `= []` bzw. `= null`) erweitern + optionale Export-Sektion `McpAccessApprovalGrants` analog `McpAccessScopes`; kein `RequiredDataProperties`-Eintrag. Die Ist-Konvention „MCP-Sektionen werden exportiert, aber nicht importiert" (SetupImportService) wird übernommen und in `Docs/planung/mcp-integration/datenmodell-erweiterungen.md` §10 explizit als begründete Abweichung zur AGENT.md-Importregel dokumentiert.
- UI: Wizard-Radio „Expertenmodus" (de/en), Grant-Matrix-Abschnitt im `McpRightsEditor` (gültige Zellen; `Administration` nur Admins; risikoreiche Freigaben — `Administration` Modify/Create sowie Schreibrechte — mit kritischer Bestätigung), Hinweis-Banner analog PreApproved, `McpAccessText`/Resx-Erweiterungen, `McpAccessCardViewModel`-Anbindung.
- Tests: `McpApprovalGateTests` (Klasse × Modus × Grant), Dispatcher-Tests (Routine direkt bei Grant, sonst Queue), `McpApprovalServiceTests` (auto-Approved-Enqueue, Withdrawal bei Grant-Entzug), `McpAdminToolsTests` (Suboperationen), `McpAccessServiceTests` (CRUD, Validierung, Audit-Delta), Backup-Tests, `EnumCoverageTests` + Hook-konforme Member-Referenz, `McpApprovalResourceTests` bei neuen `Err_*`/Labels, ViewModel-Tests, ggf. Integrationstest `McpApprovalFlowTests`.
- Docs: approval-konzept, berechtigungsmodell, fachkonzept, scope-und-toolkatalog, datenmodell-erweiterungen aktualisieren; `Docs/help/mcp-server/` ergänzen.

**Akzeptanzkriterien:**
- Neuer/bestehender Zugang ohne Zusatzwahl unverändert (`PerAction`/`PreApproved`); keine implizite Migration.
- `Granular` + Grant `(Administration, Read)` lässt `fm_admin_updates: status`/`check`, `fm_admin_backups: list`/`download`, `fm_admin_notifications: list`, `fm_admin_settings: get` direkt laufen; `install`, `schedule`, `create`, `delete`, `upload`, `apply`, `set` bleiben ohne `(Administration, Modify|Create)`-Grant `Pending`; `dismiss` bleibt ohne `(Administration, Modify)`-Grant `Pending`, mit Grant dokumentiert auto-`Approved`.
- Passender Grant lässt genehmigungspflichtige Wirkungen dokumentiert als `Approved`-`AiActionRequest` starten; `(Administration, Read)` deckt keine Endwirkung ab.
- Scopes/Filter/AccountShare/Admin-Gate/Kill-Switch/Write-Stop unverändert wirksam; Konfigurationsänderungen als `AccessEvent` mit Delta; alte DBs und Backups kompatibel.
- UI erklärt den Modus (de/en), zeigt nur gültige Zellen und bestätigt risikoreiche Freigaben kritisch.

**Abhängigkeiten:** Keine fachlichen; zeitlich nach Schritt 3 geplant (getrennte Subsysteme, aber größter Umfang — als letzter Schritt sauber abnehmbar).

**Betroffene Bereiche:** Domain (`McpApprovalMode`, neue Entität), Application (Katalog, Gate-/Service-Verträge, Matrix), Infrastructure (Gate, Services, DbContext, Migration), Web (Dispatcher-Signatur, UI-Komponenten, Resx), Shared (DTOs), Tests, Docs.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | MCP-Transparenz Preisfehler & Mengenanomalien (#562) | Keine | FmSecuritiesAnalysisTool, SecurityDto, SecurityService, Tests |
| 2 | includeStatistics-Aggregat (#560) | 1 | FmSecuritiesAnalysisTool, SecurityStatisticsDto + Service, Tests |
| 3 | EUR-Gegenwert via FX (#561) | 2 | Domain/Infra (FX-Entität, Provider, Service, Migration), FmSecuritiesAnalysisTool, Backup, Tests |
| 4 | Granularer Genehmigungsmodus (#578) | Keine (zeitlich nach 3) | Domain/Application/Infra (Grant, Katalog, Gate, Enqueue, Withdraw, Migration), Web-UI + Resx, DTOs, Backup, Tests, Docs |

## Offene Punkte

Die Issues nennen zahlreiche Einzelentscheidungen. Da kein interaktiver Anwender verfügbar ist, werden folgende Annahmen als behandelt dokumentiert (Begründung jeweils in Klammern); sie werden in den Issue-Kommentaren transparent gemacht:

- **#560 Aggregationsmenge:** record-gefilterte + fachlich gefilterte Menge; `positionCount` zählt `openQuantity != 0` (Bestandsbegriff; `IsActive` steuert nur über `onlyActive`-Filter mit). Bei `securityId` Statistik über dieses eine Papier (konsistent zu „volle Filtermenge").
- **#560 Mengenbasis:** `openQuantity` (bereits berechnet, billig; FIFO-`TotalSharesHeld` wäre aufwendiger und für ein „schlankes Aggregat" nicht nötig).
- **#560 „verwertbarer Kurs":** vorhandene neuester `SecurityPrice`-Zeile; kein Staleness-Cutoff, aber Stichtagstransparenz über `asOf` (ältestes verwendetes Kursdatum) und `positionsWithoutPrice`.
- **#561 Persistenz:** persistierte Referenztabelle als globaler Pool (re-fetchbar, aber robust für Offline-Neustarts und historische Kursdatum-Lookups; dokumentierte `OwnerUserId`-Ausnahme).
- **#561 Abrufmodell:** lazy beim Tool-Aufruf mit Cache-Füllung je Währung+Zeitraum (kein neuer Worker; EZB-Daten ändern sich nicht rückwirkend).
- **#561 Quelle/Ziel:** Frankfurter-API (EZB-Daten, schlüssellos) als konfigurierbarer Default; Zielwährung EUR fest.
- **#562 Platzierung/Enum:** `priceErrorMessage` unter `untrusted_data`; kein neues `priceErrorReason`-Enum (Rohtext genügt Kernfall); `SecurityDto`-Erweiterung (steht dann auch der UI zur Verfügung).
- **#578 Genehmigungseinheit:** `(McpDataType, McpRightKind)`; `Book`/`Delete`/`Suggest` nicht freigebbar (destruktive Wirkungen bleiben manuell; Vorschlagsrechte bedürfen keiner Vorab-Freigabe).
- **#578 Routine-Definition:** `status`/`check`/`list`/`download`/`get` der Admin-Tools als Routine (`check` löst Netzprüfung aus, ist aber lesend und im Anlass explizit genannt); `dismiss` wird als genehmigungspflichtige Operation `(Administration, Modify)` katalogisiert — unter `PerAction` queued der Aufruf heute faktisch bereits am Klassen-Gate, unter `Granular` bleibt er ohne Grant `Pending` und startet mit Grant dokumentiert auto-`Approved` (AiActionRequest-Spur).
- **#578 Policy-Änderung:** Grant-Entzug zieht nicht ausgeführte grant-basiert auto-genehmigte Requests zurück (Sicherheitskonsistenz mit `WithdrawScopeUncovered`); alle anderen offenen Requests unverändert.
- **#578 UI-Modell:** eigene Grant-Matrix-Sektion im `McpRightsEditor` statt dritter Dimension in der Scope-Matrix (klare Trennung Scope ≠ Freigabe).
