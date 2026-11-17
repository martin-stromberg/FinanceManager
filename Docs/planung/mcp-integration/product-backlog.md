# Product-Backlog – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 12 der Anforderung; Epics und priorisierte
Features als fertiger Backlog. Jedes Feature referenziert Phase
([roadmap.md](roadmap.md)), Konzeptdokument und – wo zutreffend –
Tool-IDs T01–T33 aus [scope-und-toolkatalog.md](scope-und-toolkatalog.md).
Schätzungen in Personentagen (PT-Band, konsistent zu
[aufwandsschaetzung.md](aufwandsschaetzung.md) – Baustein-Bezüge
B1–B19 jeweils angegeben). Features beschreiben das fachliche „Was",
keine Implementierungsdetails.

**Konventionen:** Priorität innerhalb einer Phase = Reihenfolge der
Features. „AK" = Akzeptanzkriterien-Ansatz (Skizze, keine
Vertragstexte). Meta-Tools T32/T33 und die verbindlichen Grundsätze
(kein generischer Datenzugriff, Storno statt Löschung, nur
einschränkende Rechte) gelten allen Features als Querschnitts-
Akzeptanzkriterium.

---

## Übersicht: Epics nach Phasen

|| Epic | Phase | Kern | PT-Band |
|---|---|---|---|---|
|| EP-01 MCP-Fundament & Zugangsverwaltung | 1 | `/mcp`, PAT, Evaluator, Registry, Verwaltung | 51–75 |
|| EP-02 Read-Only-Toolkatalog | 1 | T01–T10, T13, T14, T30, T32/T33-Basis | 11–15 |
|| EP-03 Audit-Grundlage & Revisionssicherheit | 1 | `McpAuditEntry(+Change)`, Hash-Kette | 5–7 |
|| EP-04 Assistenz- & Vorschlags-Tools | 2 | T11, T12, T15, T19 | 5–7 |
|| EP-05 Individuelle Rechte & Filter | 2 | Stufe Individuell, Matrix-Editor, Filter | 10–13 |
|| EP-06 Audit-Ansicht & Nutzungsprotokoll | 2 | `McpAuditController`, Timeline/Drilling/Export | 6–9 |
|| EP-07 Kontoauszugs-Assistent | 3 | T16–T18, Workflow T13–T19 | 6–8 |
|| EP-08 Stammdaten-Pflege | 3 | T22–T29 (direkte Teilmengen) | 6–9 |
|| EP-09 Genehmigungsworkflow & Queue | 4 | `AiActionRequest`, Gate, Executor | 12–18 |
|| EP-10 Genehmigungscenter & finale Aktionen | 4 | T20, T21, T31 + finale Teilmengen, UI, Admin | 15–22 |
|| EP-11 Agenten-Workflows & Betriebshärtung | 5 | SSE, Pre-approved, Retention, Monitoring | 10–17 |

(Summen entsprechen den Bausteinen B1–B19 der Aufwandsschätzung exkl.
phasenbezogener Testblöcke.)

---

## EP-01 – MCP-Fundament & Zugangsverwaltung (Phase 1)

**Ziel:** Der `/mcp`-Pfad existiert als authentifizierter, scope-
geprüfter Zugriffskanal; Benutzer verwalten ihre MCP-Zugänge vollständig
über die UI. Referenzen: sicherheitskonzept.md §3/§6/§7,
berechtigungsmodell.md, architekturkonzept.md §2–§6,
datenmodell-erweiterungen.md §3/§4, ui-konzept.md §4.1/§4.2/§6.1.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0101 | Domain-Grundgerüst & Migration I | Neue Entitäten `McpAccess`, `McpAccessScope` + Enums (`McpAccessLevel`, `McpDataType`, `McpRightKind`, `McpApprovalMode`, `McpAccessStatus`) in `Domain/Mcp/`; DbSets, `OnModelCreating`-Konfiguration (Unique `AccessKeyHint`, `(OwnerUserId,Name)`, Indizes, Enum→short, Cascade) und EF-Migration; `*BackupDto` ohne Token-Hash | Migration läuft via `ApplyMigrationsAndSeed`; Zugang persistierbar; Backup enthält Konfiguration, aber kein `TokenHash`/`AccessKeyHint` | 6–8 | B1 · datenmodell §3/§4/§9/§10 |
|| F-0102 | PAT-Erzeugung & -Authentifizierung | Token-Format `fm_mcp_<kürzel>_<secret>` (≥160 Bit), `pbkdf2|…`-Hash-Speicherung, `McpAuthHandler` (Kürzel→Access→`FixedTimeEquals`-Verify→`Status`/`ExpiresUtc`/`RevokedUtc`/`AllowedIpRanges`), `IMcpAccessContext` (→`OwnerUserId`), einheitliches 401-Fehlerbild | Ungültiger/deaktivierter/abgelaufener/widerrufener Token → 401 je Anfrage; Klartext nie persistiert/geloggt; IP-Bindung greift | 5–8 | B3 · sicherheit §3 |
|| F-0103 | MCP-Endpunkt `/mcp` (Streamable HTTP) | Endpoint-Gruppe unter `/mcp` in `FinanceManager.Web` (in-process); POST-JSON-RPC + SSE-Grundlage; getrennter Auth-Pfad von `/api` (kein Cookie/JWT); `McpSessionHub` als Transportzustand | Client-Handshake + Tool-Listing funktioniert; `/api` unverändert; Kill-Switch lehnt vor Auth ab | 6–10 | B2 · architektur §2/§3/§5 |
|| F-0104 | Zugangs-Service & Verwaltungs-API | `IMcpAccessService`/`McpAccessService` (Anlage, Rotation mit Sofort-Invalidierung, Widerruf, Aktivieren/Deaktivieren, Stufe/Modus/Ablauf/IP); `McpAccessesController` (Liste/Detail/POST/PUT/scopes/filters/activate/deactivate/rotate/delete/levels/options); `ApiClient.Mcp.cs`; serverseitige Stufenmatrix | CRUD via `/api/mcp-accesses` unter JWT; Einmal-Klartext nur in Anlage-/Rotations-Antwort; `Err_Mcp_*`-Fehlerbilder; Änderungen als AccessEvent | 8–11 | B4 · ui-konzept §6.1 · fachkonzept §6 |
|| F-0105 | ScopeEvaluator (Prüfstufen 0–3) | `IMcpScopeEvaluator`: effektives Profil (Stufenprofil vs. `McpAccessScope`-Zeilen), Scope-Tupel-Prüfung, Filter-CSV-Auswertung (UND/ODER, unklassifiziert nicht erfasst), Ableitungstabelle (`Posting` je `Kind`, `StatementEntry`, `StatementDraftEntry`, `AccountLinkedIban`, `AliasName`, `SecurityPrice`, `Attachment`-Zieldecke), `AccountShare`-Deckel; Laufzeitauflösung je Aufruf | Scope-Fehler auf Tool-Ebene, 404-Semantik auf Datensatzebene; Share-Rolle deckelt datensatzweise; Profiländerung wirkt ab nächstem Aufruf; keine erweiternde Regel möglich | 10–15 | B5 · berechtigungsmodell §1/§4–§6/§8 |
|| F-0106 | Tool-Registry & Dispatcher | `McpToolRegistry` mit `IMcpToolDescriptor` je Tool (Name `fm_*`, Scope-Tupel/Meta-Scope, Sicherheitsklasse, JSON-Eingabeschema, Deprecation), `katalog-v1`; `McpToolDispatcher` (Routing, Parameter-Bindung, definierter Scope-Fehler) | „Kein Tool ohne Scope" build-erzwingbar; unbekanntes Tool → Fehler; `fm_capabilities_describe` meldet Katalogversion | 4–6 | B6 · toolkatalog §6 · architektur §6 |
|| F-0107 | Notfallschalter, Rate-Limiting, Anomalie-Basis | `Mcp:*`-Optionsbindung (`Enabled`, `WriteAccess`, `RateLimiting:*`, `TokenHash:Iterations`); `RateLimitPolicies.Mcp*` (Partition Zugangskürzel + IP unauthentifiziert; Limits je Klasse); Fehlversuchszähler→`IpBlock`; Anomalie-Benachrichtigung (`SystemAlert`/EventDriven) | `Mcp:Enabled=false` → 503/401 für alle; Schreib-Stopp deckelt; 429 bei Limit; Fehlversuchsserie → IP-Sperre + Alarm | 4–6 | B13 · sicherheit §6/§7 · architektur §9 |
|| F-0108 | UI Zugangsverwaltung & Token-Wizard | Nav-Eintrag „KI-Zugänge", `/list/mcp-accesses` + `/card/mcp-accesses/{id}` (Sections Stammdaten/Token/Nutzung), dreistufiger Anlage-Assistent mit Einmalanzeige + Pflicht-Bestätigung + TLS-Hinweis; `IConfirmationService` für Widerruf/Stufenwechsel; resx de/en | Token nie wieder abrufbar; Deaktivierung sofort wirksam; Stufen-Klartexte; keine UI-eigene Rechtesemantik | 8–11 | B14 · ui-konzept §3/§4.1/§4.2 |

---

## EP-02 – Read-Only-Toolkatalog (Phase 1)

**Ziel:** Alle lesenden Analyse-Tools sind über `/mcp` abrufbar –
vollständig scope-/filter-/deckelgeprüft und protokolliert.
Referenzen: toolkatalog §3.1/§3.5/§3.6, sicherheitskonzept §5.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0201 | Analyse-Tools Konto/Buchung/Report | Adapter für T01 `fm_accounts_overview`, T02 `fm_postings_query`, T03 `fm_reporting_aggregate`, T10 `fm_configuration_read` auf `IAccountService`, `IPostingsQueryService`, `IReportAggregationService`, `IReportFavoriteService`, `IHomeKpiService` u. a. | Ausgabe ausschließlich DTOs des Besitzers; Paging-Clamp ≤200; leere Menge statt Fehler bei Fremd-/Filterdaten | 2–3 | B7 · T01–T03, T10 |
|| F-0202 | Analyse-Tools Stammdaten/Budget/Bestand | Adapter für T04 `fm_contacts_lookup`, T05 `fm_budget_overview`, T06 `fm_savings_plan_analysis`, T07 `fm_securities_analysis` | Kategorie-Ids auflösbar; `SecurityPrice` nur lesend; Filter wirken durch Aggregate | 3–4 | B7 · T04–T07 |
|| F-0203 | Anhänge-Lesen (untrusted-safe) | Adapter T09 `fm_attachments_read`: Metadaten + Inhaltsstrom mit deklariertem `ContentType`, `Sha256`, `untrusted_data`-Kennzeichnung, Größen-/Rate-Limits | Binärinhalt als Dokumentdaten markiert; keine Prosa-Vermischung; Kategoriefilter wirken | 1–2 | B7 · T09 · sicherheit §5.2 |
|| F-0204 | Kontoauszug-Leseservice + T08 | Neuer Contract `IStatementImportQueryService` (lesend auf `StatementImport`/`StatementEntry`, Besitz via `AccountId`) + Adapter T08 `fm_statement_imports_review` | Belege nur lesend; kein Schreib-/Löschpfad; Share-Deckel via Konto | 3–4 | B7 · T08 · toolkatalog §3.1 |
|| F-0205 | Entwurfs-/Storno-Lesetools + Meta | Adapter T13 `fm_statement_drafts_read`, T14 `fm_statement_draft_validate`, T30 `fm_posting_reversal_check`; Meta T32 `fm_capabilities_describe` (+ T33-Basis ohne Request-Bezug) | Meta-Scope ohne Fachdatenzugriff, nur Besitzprüfung; Draft-Ausgaben vollständig wie UI-Sicht | 2–2 | B7 · T13/T14/T30/T32 |

---

## EP-03 – Audit-Grundlage & Revisionssicherheit (Phase 1)

**Ziel:** Jeder MCP-Vorgang ist vom ersten Aufruf an revisionssicher
protokolliert. Referenzen: audit-konzept.md, datenmodell §6–§8,
architektur §7.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0301 | Audit-Speicher & Schreibpfad | `McpAuditEntry` + `McpAuditEntryChange` (in F-0101-Migration enthalten) + `McpAuditWriter`: transaktional an schreibende Wirkung gekoppelt; lesende Aufrufe blockieren nicht bei Audit-Fehler; 5 `McpAuditCategory`-Werte; abgestufte `PayloadJson`-Detailtiefe (kein Token, keine Binärinhalte, Längenlimits) | Schreibende Wirkung ohne Beleg schlägt fehl; Lesender Aufruf trotz Audit-Fehler erfolgreich; Feld-Deltas als eigene Zeilen | 3–4 | B12 · audit §3/§4 · architektur §7 |
|| F-0302 | Hash-Kette & Lückenlosigkeit | `SequenceNo` je Kette (Besitzer/global), `PrevHash`/`EntryHash`-Verkettung über kanonisierte Inhalte; atomare Bündel (Aufruf + Deltas); serverseitige `UtcNow`-Zeiten | „Integrität prüfen" erkennt Bruchstelle/Lücke; Eintragsnummern monoton; append-only ohne App-Update-Pfad | 2–3 | B12 · audit §7 |

---

## EP-04 – Assistenz- & Vorschlags-Tools (Phase 2)

**Ziel:** Stufe Assistent wird produktiv: strukturierte Vorschläge ohne
Persistenz. Referenzen: toolkatalog §3.2/§3.3 (T15/T19), fachkonzept §3.2.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0401 | Kontakt-/Alias-Vorschläge | T11 `fm_contacts_suggest`: Kategorie-Zuordnungen, `AliasName.Pattern`-Vorschläge, Dubletten-Kandidaten – Ausgabe als Vorschlagsliste mit Begründung/Konfidenz, ohne Schreiben | Kein Persistenz-Nebeneffekt (kein `DataChange`-Eintrag); Vorschläge nur über scope-/filtererfasste Datensätze | 2–3 | B8 · T11 |
|| F-0402 | Budgetregel-Vorschläge | T12 `fm_budget_rules_suggest`: `PurposePattern`/`PurposePatternIsRegex` aus Buchungshistorie, Betrags-/Intervall-Vorschläge | Vorschlagsstruktur mit Ziel-Feld + Begründung; liest nur erlaubte Buchungen | 1–1 | B8 · T12 |
|| F-0403 | Entwurfs-Analyse & Buchungsplan | T15 `fm_statement_draft_analyze` (Klassifizierung, `AlreadyBooked`+Heuristik-Dubletten, fehlende Zuordnungen) und T19 `fm_statement_draft_prepare_booking` (buchbare/gesperrte Entries, `PostingKind`-Vorschau, `forceWarnings`-Empfehlung) | Analysebericht mit Zuordnungsvorschlägen; `AlreadyBooked`-Entries im Plan gesperrt; beliebig oft aufrufbar | 2–3 | B8 · T15/T19 · toolkatalog §4.3–§4.5 |

---

## EP-05 – Individuelle Rechte & Filter (Phase 2)

**Ziel:** Stufe Individuell mit Scope-Matrix-Editor und
Filterverwaltung. Referenzen: berechtigungsmodell §3.2/§4,
fachkonzept §8, ui-konzept §4.3/§4.4, datenmodell §4.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0501 | Individuelle Scope-Profile (Server) | `PUT /api/mcp-accesses/{id}/scopes`: `McpAccessScope`-Zeilen für Stufe „Individuell"/materialisierte Vorlage; Server-Validierung gegen F3-„✓"-Matrix + Deckel (`Err_Mcp_InvalidScope`); Standardstufen bleiben zeilenlos (Laufzeit-Ableitung) | Ungültige Tupel abgelehnt; effektives Profil korrekt auflösbar; Scope-Änderung als AccessEvent mit Delta | 3–4 | B4/B5 · datenmodell §4 |
|| F-0502 | Scope-Matrix-Editor (UI) | Matrix 18 Datentypen × 7 Rechtearten; „—"-Zellen deaktiviert mit Begründung; Implikationshilfe „ohne Lesen"; „Von Vorlage anpassen"; Effektiv-Anzeige bei Standardstufen | Nur gültige Kombinationen speicherbar; Stufenwechsel verwirft Anpassungen (Bestätigung); Deckel-Hinweistext | 4–5 | B15 · ui-konzept §4.3.2 |
|| F-0503 | Filterverwaltung | `PUT …/filters` + Section „Einschränkungen": 5 Dimensionen (Kontotyp, Kontakt-/Wertpapier-/Sparplan-/Anhangkategorien) mit Kategorielisten; Semantik-Hinweise (unklassifiziert nicht erfasst, UND, indirekte Wirkung) | Gesetzter Filter begrenzt Listen/Vorschläge datensatzscharf; leere Liste = unbeschränkt | 2–3 | B15 · ui-konzept §4.4 · fachkonzept §8.3 |
|| F-0504 | Genehmigungsmodus-Auswahl | Radio „je Aktion"/„dauerhaft vorab genehmigt" in Rechte-Section; `IConfirmationService` (high severity) für Pre-approved-Buchung/Löschung; wirksam ab Phase 4 (persistiert wird die Einstellung) | Auswahl persistiert; Sicherheitshinweise sichtbar; keine Ausführungswirkung vor P4 | 1–1 | B15 · ui-konzept §4.3.3 |

---

## EP-06 – Audit-Ansicht & Nutzungsprotokoll (Phase 2)

**Ziel:** „Welches KI-System hat wann was getan" wird für den Benutzer
beantwortbar. Referenzen: audit-konzept §6, ui-konzept §4.5/§6.3.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0601 | Audit-API | `McpAuditController`: `GET /api/mcp-audit` (Filter Zeitraum/Zugang/Tool/Kategorie/Status/`correlationId`, Paging), `GET /{id}` inkl. Feld-Deltas, `GET /export?format=json\|csv`, `POST /verify` | Nur eigene Einträge (ownerUserId); Export selbst Audit-Ereignis; Verify meldet intakt/Bruchstelle | 2–3 | B16 · ui-konzept §6.3 |
|| F-0602 | Audit-Timeline & Drilling (UI) | `McpAuditViewModel` `/card/mcp-audit` + Tab „Verlauf" der Zugangskarte: Kategorie-Badges, Klartext-Ereigniszeilen, Filterleiste, Expand-Details (Parameter, Vorher/Nachher-Tabelle), Links auf `AiActionRequest` und Ergebnis-Entitäten | Lokalisierte Klartexte de/en; Rohdaten hinter „Details"; Deep-Link mit Zugangsfilter | 3–4 | B16 · ui-konzept §4.5 |
|| F-0603 | Nutzungsübersicht je Zugang | Section „Nutzung & Verlauf": `LastUsedUtc`, Aufrufzahlen, Fehlschlagquote, zuletzt verwendete Tools – aus `McpAuditEntries` aggregiert | Kennzahlen stimmen mit Audit-Einträgen überein; Leerzustand freundlich | 1–2 | B16 · fachkonzept §6.5 |

---

## EP-07 – Kontoauszugs-Assistent (Phase 3)

**Ziel:** F7-Workflow produktiv – import, analyze, complete, edit,
validate, prepare_booking. Referenzen: toolkatalog §4, fachkonzept §3.3.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0701 | Entwurfs-Import via MCP | T16 `fm_statement_draft_import`: Datei-Bytes → `CreateDraftAsync`/`CreateEmptyDraftAsync`/`CreatePreliminaryDraftAsync` bzw. `IMassImportOrchestrator.ProcessAsync`; Upload-Limits (20 MB, `MassImport:*`) | Nur vorhandene Importmechanik; kein Bankabruf; erzeugte Drafts sichtbar | 2–3 | B9 · T16 |
|| F-0702 | Entwurfs-Vervollständigung | T17 `fm_statement_draft_complete`: Set*/Save*-Methoden + `ApplyBatchEntryUpdatesAsync` – **nur Lücken**, atomare Batches (≤100 Entries), `DataChange`-Audit mit Feld-Deltas | Vorhandene Werte nie überschrieben; Batch ganz-oder-gar-nicht; Scope auf referenzierte Ziele (Kontakte/Sparpläne/Wertpapiere/Konten) geprüft | 2–3 | B9 · T17 |
|| F-0703 | Entwurfs-Korrektur | T18 `fm_statement_draft_edit`: `UpdateEntryCoreAsync`, `AddEntryAsync`, `DeleteEntryAsync`, `ResetDuplicateEntryAsync` | Kernfelder korrigierbar; Dubletten-Rücksetzung protokolliert; Änderungsrecht (nicht Vervollständigung) erforderlich | 1–1 | B9 · T18 |
|| F-0704 | Fachliche Zusatzvalidierung | `fm_statement_draft_validate` um Zusatzregeln erweitern: Zuordnungsvollständigkeit, Betrags-/Vorzeichenlogik, Datumslogik, Split-Konsistenz, `ArchiveSavingsPlanOnBooking`-/`IsCostNeutral`-Nebenbedingungen | Befundliste = `ValidateAsync` + Zusatzregeln; gesperrte Entries im Buchungsplan | 1–1 | B9 · toolkatalog §4.4 |

---

## EP-08 – Stammdaten-Pflege (Phase 3)

**Ziel:** Schreibvorbereitende Teilmengen von T22–T29 direkt ausführbar;
finale Teilmengen (Löschen/Archiv/Merge) bleiben bis Phase 4 verwehrt.
Referenzen: toolkatalog §3.4, approval-konzept §2.2.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0801 | Kontakte & Kategorien pflegen | T22 `fm_contacts_manage` (Create/Update/Alias – ohne Merge/Löschen) + T27 `fm_categories_manage` (vier Kategorietypen, ohne Löschen) | Merge/Löschen-Aufrufe → definierter Fehler „erfordert Genehmigung" (bis P4); `(Owner,Name)`-Unique-Fehler als `Err_*` | 2–3 | B9 · T22/T27 |
|| F-0802 | Konten & Anlagen pflegen | T23 `fm_accounts_manage` (ohne Löschen) + T24 `fm_savings_plans_manage` + T25 `fm_securities_manage` (jeweils ohne Archiv/Löschen) | Unter-IBAN-Pflege; Archiv-Aufrufe blockiert bis P4; Feld-Deltas im Audit | 2–3 | B9 · T23–T25 |
|| F-0803 | Budgets, Anhänge, Konfiguration | T26 `fm_budgets_manage`, T28 `fm_attachments_manage` (Upload/Metadaten/Neuzuordnung, ohne Löschen), T29 `fm_reports_configuration_manage` (ohne Löschen) | Anhang-Upload mit `Sha256`/Metadaten; Lösch-Modi blockiert; `ReassignAsync`-Decke der Ziel-Entität | 2–3 | B9 · T26/T28/T29 |

---

## EP-09 – Genehmigungsworkflow & persistente Queue (Phase 4)

**Ziel:** `AiActionRequest`-Infrastruktur vom Anlegen bis zur einmaligen,
neustartsicheren Ausführung. Referenzen: approval-konzept.md,
architektur §6, datenmodell §5.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-0901 | `AiActionRequest`-Entität & Migration | Tabelle = persistente Queue: Felder inkl. `ParameterSnapshot`, `ExpiresUtc`, `CorrelationId`, `IdempotencyKey` (unique), `ClaimToken`/`ClaimedUtc`, `McpAccessName`-Snapshot + Soft-Referenz; Indizes (Queue-Poll, Center-Liste); `*BackupDto` (offene Requests überleben Restore) | 6 Zustände + verbindliche Übergänge; verbotene Übergänge technisch ausgeschlossen; Migration inkrementell | 2–4 | B1/B10 · datenmodell §5 · approval §3 |
|| F-0902 | Approval-Gate & Request-Anlage | `McpApprovalGate` an Pipeline-Pos. 8: Klassenlogik (lesend/vorschlagend direkt; schreibvorbereitend je `McpApprovalMode`; genehmigungspflichtig stets Request), `PreApproved` → sofort Approved mit dokumentierter Genehmigung, Pending-Limit, `INotificationWriter` EventDriven | Synchrone Antwort `{ aiActionRequestId, Pending }`; Duplikat (gleicher Key) → bestehender Request; Limit → 429 | 3–4 | B10 · approval §2/§3.4/§4 |
|| F-0903 | `AiActionExecutor` (Queue-Ausführung) | Hosted Service (`BackgroundTaskRunner`-Muster): atomarer Claim via bedingtem UPDATE, Guard-Re-Prüfung (`CanReverseAsync`, `AlreadyBooked`, Besitz, Stufen 0–3), Adapter-Ausführung, `Executed`/`Failed` + `Err_*`-FailureReason, Delegation langer Läufe an `IBackgroundTaskManager` | Genau-einmal-Ausführung; Neustart verliert keinen Approved-Request; `StatementDraftBookingGuard` greift; kein Auto-Retry | 4–5 | B10 · approval §5 · architektur §6.2 |
|| F-0904 | Expired-Job & System-Übergänge | Ablauf-Auswertung (`Pending → Expired` bei `ExpiresUtc`), systemseitiges `Rejected` bei Widerruf/Deckel-Änderung/Schreib-Stopp; `Mcp:Approval:*`-Konfiguration | Überfällige Requests verfallen; Widerruf zieht Pending/Approved zurück; Approved bleibt bei Kill-Switch stehen | 1–2 | B10 · approval §3.3/§6 |
|| F-0905 | Requests-API & `fm_task_status` | `AiActionRequestsController` (Liste/Count/Detail+Wirkungsvorschau serverseitig/approve/reject/batch); `fm_task_status`-Vollausbau (Request-Zustände + Task-Fortschritt, nur eigene) | Besitzer-Filter serverseitig; Client sieht nur eigene Requests; Batch meldet Teilerfolg | 2–3 | B10 · ui-konzept §6.2 · T33 |

---

## EP-10 – Genehmigungscenter & finale Aktionen (Phase 4)

**Ziel:** Der Benutzer entscheidet über Wirkung; die drei immer-
genehmigungspflichtigen Tools + finale Pflege-Teilmengen werden
freigeschaltet; Admin-Sicherheitsübersicht. Referenzen:
approval-konzept §7, ui-konzept §4.6/§5.2.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-1001 | Genehmigungscenter (UI) | `/card/mcp-approvals` `McpApprovalCenterViewModel`: Eingangskorb (Restlaufzeit, Wirkungsangabe), Detail mit Wirkungsvorschau je Aktionstyp (Buchungsplan, Storno-Gruppe, Lösch/Archiv/Merge, Batch-Deltas), `ReasonText` als Client-Nachricht markiert, Mehrfachauswahl, Historie, Nav-Badge, Notification-Links | Vorschau aus Snapshot (Server); Freigabe finaler Wirkung über `IConfirmationService`; jeder Request eigener Übergang | 6–8 | B17 · approval §7 · ui-konzept §4.6 |
|| F-1002 | Verbuchung & Verwerfen via Request | T20 `fm_statement_draft_book` + T21 `fm_statement_draft_discard` über Queue: `BookAsync`/`CommitAsync`/`book-all` (`StatementDraftBookingGuard`), `CancelAsync`/`DeleteAllAsync`; `ExecutionResult` mit Posting-/Import-Ids | Nur via `AiActionRequest`; Wirkung = regulärer Service-Pfad; Ergebnis via `fm_task_status`; `DataChange`+`ApprovalEvent`-Audit-Kette | 2–4 | B11 · T20/T21 |
|| F-1003 | Storno via Request | T31 `fm_posting_reverse`: `CanReverseAsync`-Vorprüfung in Anlage **und** Ausführung, Gegenbuchung inkl. Gruppenbuchungen, Begründungstext (Audit-Feld, untrusted-markiert) | Doppel-Storno ausgeschlossen; `Err_AlreadyReversed` bei geänderter Lage; Audit verknüpft Storno-Paar | 1–2 | B11 · T31 |
|| F-1004 | Finale Pflege-Teilmengen | Merge/Löschen/Archivieren von T22–T29 über den Request-Pfad (inkl. Konsequenzhinweis in der Vorschau) | Merge nur per Freigabe; Archiv statt Löschen für SavingsPlan/Security erzwungen; Snapshot listet betroffene Datensätze | 3–4 | B11 · T22–T29 |
|| F-1005 | Admin-Sicherheitsübersicht | `GET /api/mcp-audit/security-overview` (`Roles="Admin"`) + `McpSecurityOverviewViewModel`: Schalter-Zustand, Anomalien, IP-Sperren, Rate-Limit-Aggregate – ohne Fremd-Fachdaten | Admin sieht keine Finanzinhalte anderer Benutzer; SystemAlert verlinkt in die Sicht; Schalter bleiben appsettings-Hoheit | 3–4 | B18 · ui-konzept §5.2 · audit §6.2 |

---

## EP-11 – Agenten-Workflows & Betriebshärtung (Phase 5)

**Ziel:** Wiederkehrende Verarbeitung über denselben kontrollierten
Pfad; Betrieb (Retention, Monitoring, Katalogpflege) wird
produktionsreif. Referenzen: fachkonzept §3.5, architektur §3.1/§11,
audit §5.

|| ID | Feature | Fachliche Beschreibung | AK-Ansatz | PT | Bezug |
|---|---|---|---|---|---|---|
|| F-1101 | SSE-Server-Benachrichtigungen | SSE-Upgrade des `/mcp`-Endpunkts für Status-/Ergebnis-Benachrichtigungen (Request-Zustandswechsel) ohne Polling | Client erhält `Executed`-Meldung per SSE; `fm_task_status`-Polling bleibt Fallback; Session sicherheitsneutral | 3–5 | B19 · architektur §3.1 |
|| F-1102 | Pre-approved-Agenten-Modus | „Dauerhaft vorab genehmigt" produktiv für Agenten-Zugänge: dokumentierte Genehmigungsinstanz, Einrichtungs-Bestätigung (high severity), Zusatzdeckel-Empfehlungen (IP-Bindung, enge Filter, kurzes `ExpiresUtc`) | Jede Ausführung schreibt vollständige Request-/Audit-Spur; kein Pfad an der Genehmigung vorbei | 2–3 | B19 · approval §3.4 |
|| F-1103 | Wiederkehrende Verarbeitungsmuster | Fachliche Runbooks auf Tool-Ebene (z. B. „neue Entwürfe: analyze→complete→prepare→Request"), Monitoring-Sichten aus Audit-Aggregationen (Request-Mengen, Fehlschlagquoten, Laufzeiten) | Agent nutzt identischen Scope-/Approval-Pfad; keine Sonderrechte; Monitoring ohne Fremddaten | 2–4 | B19 · fachkonzept §3.5 |
|| F-1104 | Retention & Protokoll-Betrieb | Retention-Executor (`Mcp:Audit:RetentionDays:*`): 12/36-Monats-Stufen, `RetentionPurged`-Systemereignis als Kettenanfang, terminale Requests gleiche Frist; Export vor Bereinigung | Einziger Löschpfad auf Audit-Tabelle; Kette ab neuem Anfang verifizierbar; Bereinigung protokolliert | 2–3 | B12 · audit §5 · datenmodell §8 |
|| F-1105 | Katalogpflege & Deprecation | Erster `katalog-v`-Sprung exerziert (Deprecation-Markierung, Hauptversion bei brechender Änderung), Anomalie-Ausbaustufe (Trends über Agenten-Workloads, gestufte Deaktivierung) | Kompatibilitätsregeln eingehalten; Alt-Clients funktionieren eine Hauptversion weiter | 1–2 | B19 · toolkatalog §6 |

---

## Querschnitts-Anforderungen an alle Features

1. **Scope-Deklaration:** Kein Tool ohne deklariertes Scope-Tupel bzw.
   Meta-Scope (Toolkatalog-Grundsatz 2); `katalog-v<n>`-Benennung
   verbindlich.
2. **Audit:** Jeder Aufruf/Zustandswechsel/jede Verwaltungsaktion
   erzeugt Einträge; `CorrelationId` verknüpft die Wirkkette.
3. **Nicht-Ziele:** kein DB-Direktzugriff, kein generisches CRUD, keine
   Bankanbindung, keine Vollautonomie – als Review-Kriterium jedes
   Features.
4. **Konventionen:** `ownerUserId`-Pfad der Services, `ApiErrorDto`/
   `Err_Mcp_*`, `Entity`-Basisklasse, Enum→short, `IApiClient`-only-
   UI, resx de/en, `IConfirmationService` für kritische Aktionen.
5. **Untrusted content:** Ausgaben markiert (`untrusted_data`),
   Eingaben ausschließlich typisiert validiert; `ReasonText`/Client-
   Texte immer als Daten gekennzeichnet.
