# Review – Schritt 4: UI-, Architektur- und Datenmodellkonzept

**Status:** Abgeschlossen – Selbstprüfung gegen die Akzeptanzkriterien
des Projektplans bestanden. Drei deutschsprachige Konzeptdokumente unter
`Docs/planung/mcp-integration/` geliefert:

- `ui-konzept.md` (~518 Zeilen)
- `architekturkonzept.md` (~567 Zeilen)
- `datenmodell-erweiterungen.md` (~416 Zeilen)

## Prüfung der Akzeptanzkriterien

|| Kriterium | Ergebnis |
|---|---|---|
| Alle drei Dokumente als deutschsprachige Markdown-Dateien unter `Docs/planung/mcp-integration/` | ✔ |
| UI-Konzept deckt alle sechs Komponenten ab | ✔ Abschnitt 4: 4.1 Zugangsverwaltung (Liste + Karte + Sections), 4.2 Tokenverwaltung (Einmalanzeige-Dialog, Rotation, Widerruf, TLS-Hinweis), 4.3 Rechteverwaltung (Stufenkarten + Matrix-Editor 18×7 + Genehmigungsmodus), 4.4 Filterverwaltung (5 Dimensionen, Semantik-Hinweise), 4.5 Audit-Ansicht (Timeline, Filter, Drilling, Export, Integritätsprüfung), 4.6 Genehmigungscenter (Eingangskorb, Wirkungsvorschau, Batch, Historie) |
| Einfache Benutzerführung + Expertenmodus + Verständlichkeit | ✔ Abschnitt 1 Leitlinien 1–6; Stufen-Auswahlkarten mit Klartext (4.3.1); Expertenmodus erst bei „Individuell"/„Anpassen" (4.3.2); Verständlichkeits-Mapping Abschnitt 7; `IConfirmationService` für kritische Aktionen |
| Blazor/MVVM/`IApiClient`-Konvention | ✔ Abschnitt 2: ViewModelBase/BaseListViewModel/BaseCardViewModel, `CardRoute`/`ListViewModelFactory`, resx de/en, `UiOverlaySpec`; ViewModels rufen nur `IApiClient`; API-Skizze Abschnitt 6 folgt `/api`-/JWT-/`ApiErrorDto`-Konventionen |
| Architektur: MCP-Server-Struktur, Service-Layer, Sicherheitslayer, Tool-Registrierung, Scope-Prüfung, Logging, Persistenzmodell (F12) | ✔ Abschnitte 4–10: Komponentendiagramm + Projektplatzierung, Adapter-Prinzip auf `I*Service` mit `ownerUserId`, 10-stufige Sicherheits-Pipeline, `McpToolRegistry`/`katalog-v<n>`, `AiActionExecutor`-Queue, transaktionaler `McpAuditWriter`, DbSet-/Migrationsübersicht |
| Betriebsform final entschieden (Annahme 1) | ✔ Abschnitt 2: in-process in `FinanceManager.Web` unter `/mcp`; Alternativen (separater Prozess, entferntes Hosting, nur-stdio) begründet verworfen |
| Transport-Entscheidung mit Begründung/Alternativen | ✔ Abschnitt 3: Streamable HTTP; Legacy-SSE, stdio, WebSocket, POST-only geprüft und begründet nicht gewählt; SDK-Hinweis mit Fallback |
| Konsistenz zu Single-Instance/SQLite/LAN | ✔ Abschnitte 2, 6.2, 9, 11: ein Prozess, SQLite-Claim-Atomarität, persistente Queue statt In-Memory, HTTP-Fallback + TLS-Zielbetrieb, Forwarded-Headers-Pflicht, Auto-Update |
| Datenmodell: alle neuen Entitäten inkl. Owner-Bezüge, Beziehungen, Konventionen | ✔ Abschnitte 3–7: `McpAccess` (`OwnerUserId`, Token-Hash, `AccessKeyHint`, Stufe, Modus, Status, Ablauf, IP-Bindung, Revoked), `McpAccessScope` (Tupel + 5 Filter-CSVs, Cascade), `AiActionRequest` (6 Zustände, Snapshot, `IdempotencyKey` unique, Claim-Felder, CorrelationId), `McpAuditEntry`+`…Change` (Kategorien, Hash-Kette, Feld-Deltas); Indizes-Übersicht, Enum→short, `Entity`-Konvention |
| Migration/Backup/keine Bestandsänderung | ✔ Abschnitte 9–11: DbSet+`OnModelCreating`+Migration via `ApplyMigrationsAndSeed`; `*BackupDto`-Aufnahme mit Token-Hash-Ausschluss; **keine** Änderung an Fachentitäten (indirekte Owner-Ableitung bleibt) |

## Konsistenz zu Vorgängerdokumenten

- **fachkonzept.md:** 5 Freigabestufen inkl. fachlicher Bedeutung,
  Token-Lifecycle (Einmalanzeige, Rotation sofort, Widerruf),
  Nutzungsprotokoll-Frage („welches KI-System hat wann was getan"),
  Filter-Semantik (unklassifiziert nicht erfasst), „Admin kein
  Datentürsteher" – konsistent übernommen. ✔
- **berechtigungsmodell.md:** Scope-Tupel, Prüfstufen 0–3 als Pipeline,
  ODER über Scopes/UND über Filterdimensionen, Laufzeitauflösung,
  `AccountShare`-Deckel, „keine Deny-Scopes", `ReportFavorite`-CSV-
  Muster für Filter. ✔
- **scope-und-toolkatalog.md:** 33 Tools T01–T33, 4 Sicherheitsklassen,
  `katalog-v<n>`, Meta-Scope (nicht persistiert, inhärent),
  `fm_task_status`/`fm_capabilities_describe` als Ergebnisabrufe,
  `IStatementImportQueryService` als offener Baustein bestätigt. ✔
- **sicherheitskonzept.md:** PAT-Format `fm_mcp_<kürzel>_<secret>`,
  `pbkdf2|…`-Hash (AccessKeyHint als Lookup), Pipeline-Reihenfolge
  „Rate Limiter → IpBlock → Token-Auth → Scope-/Filter-Prüfung →
  Tool-Adapter" aus dessen Abschnitt 9 umgesetzt (erweitert um
  Kill-Switch, Dispatcher, Approval-Gate, AuditWriter an den dort
  festgelegten Positionen), `Mcp:*`-Konfigurationssektion, IP-Bindungs-
  Feld, UI-Hinweispflichten. ✔
- **approval-konzept.md:** `AiActionRequest` 6 Zustände inkl. `Expired`,
  Felder 1:1 aus dessen Abschnitt 3 übernommen (+ Queue-Claim-Felder),
  Tabelle = persistente Queue, `IdempotencyKey`, Snapshot-Treue,
  Genehmigungscenter-Anforderungen (Abschnitt 7) vollständig im
  UI-Konzept umgesetzt, EventDriven-Benachrichtigung. ✔
- **audit-konzept.md:** 5 Ereigniskategorien als `McpAuditCategory`,
  Grundgerüst-Felder als `McpAuditEntry`-Spalten, abgestufte
  Detailtiefe über `PayloadJson`, Feld-Deltas als eigene Tabelle
  (Drilling), Hash-Verkettung (`SequenceNo`/`PrevHash`/`EntryHash`),
  Retention 12/36 Monate + `RetentionPurged`, Export + Integritäts-
  prüfung in der UI, Admin-Sicht ohne Fachnutzlast. ✔

## Festgelegte Einzelentscheidungen (für Folgeschritte verbindlich)

- Betriebsform: in-process `FinanceManager.Web`, Route `/mcp`;
  getrennte Auth-Pfade `/api` (JWT) vs. `/mcp` (PAT, kein Cookie).
- Transport: Streamable HTTP; SSE-Upgrade unterstützt, primär für
  Agenten-Szenarien (Phase 5); SDK-Einsatz mit Eigenbau-Fallback.
- Pipeline: 10 Positionen verbindlich; `McpAuth` als eigenes
  Authentication-Schema der `/mcp`-Gruppe; `IMcpAccessContext` als
  `ICurrentUserService`-Äquivalent.
- Audit-Schreibpfad: transaktional an schreibende Wirkungen gekoppelt;
  lesende Aufrufe dürfen bei Audit-Fehler nicht blockieren.
- `McpAccessScope`-Zeilen nur bei Stufe „Individuell" (Standardstufen
  leiten das Profil zur Laufzeit ab – Stufenprofile wachsen mit dem
  Katalog mit).
- `McpAccess`: physisches Löschen optional, Widerruf (`Revoked`) als
  Standardpfad; `AccessKeyHint` global unique.
- `AiActionRequest`: `IdempotencyKey` unique; Soft-Referenz auf den
  Zugang + `McpAccessName`-Snapshot; Claim via bedingtem UPDATE.
- Backup: `McpAccess`-Konfiguration wird gesichert, `TokenHash`/
  `AccessKeyHint` **nicht** – Restore erfordert Token-Rotation.
- `Mcp:Enabled` Default = `false` (Opt-in-Aktivierung; Migration ≠
  Aktivierung).
- Admin-Sicht zeigt Schalter-Zustand; Umschaltung bleibt
  `appsettings`-Hoheit (kein UI-Schalter).

## Offene Punkte

- Keine blockierenden. Bewusst an Schritt 5 delegiert:
  Phasenzuordnung der UI-/Architektur-/Persistenzbausteine
  (Roadmap), Aufwandsschätzung und Backlog-Features.
- Umsetzungsoffen (nicht konzeptrelevant): konkrete SDK-Wahl für den
  MCP-Transport (C#-SDK vs. Eigenbau), exakte Optionsklassen-Namen,
  Detailgestaltung der Wirkungsvorschau-Renderer je Aktionstyp.
