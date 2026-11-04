# Priorisierte Umsetzungsroadmap – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 10 der Anforderung; priorisierte Umsetzungsroadmap
entlang der fünf vorgegebenen Phasen (F13). Konsolidiert alle
Vorgängerartefakte: Toolkatalog T01–T33 und Sicherheitsklassen aus
[scope-und-toolkatalog.md](scope-und-toolkatalog.md), Prüfstufen 0–3 und
Freigabestufen aus [berechtigungsmodell.md](berechtigungsmodell.md) bzw.
[fachkonzept.md](fachkonzept.md), PAT-/Notfall-/Rate-Limit-Festlegungen aus
[sicherheitskonzept.md](sicherheitskonzept.md), `AiActionRequest` aus
[approval-konzept.md](approval-konzept.md), Audit-Ereignisse aus
[audit-konzept.md](audit-konzept.md), Komponenten/Pipeline aus
[architekturkonzept.md](architekturkonzept.md), Entitäten aus
[datenmodell-erweiterungen.md](datenmodell-erweiterungen.md) und
UI-Bausteine aus [ui-konzept.md](ui-konzept.md). Die Aufwandsschätzung je
Phase steht in [aufwandsschaetzung.md](aufwandsschaetzung.md), die
Ableitung in Epics/Features in [product-backlog.md](product-backlog.md).

---

## 1. Phasenübersicht und Priorisierungsbegründung

Die fünf Phasen sind durch die Anforderung (F13) vorgegeben. Ihre Reihenfolge
ist zugleich die fachlich und technisch begründete Priorisierung:

```text
Phase 1                Phase 2              Phase 3              Phase 4                    Phase 5
Read-Only              Assistenz-/          Kontoauszugs-        Genehmigungs-              Agenten-
Analyse                Vorschlags-          Assistent            pflichtige                 gestützte
                       funktionen                                Aktionen                   Workflows
   │                      │                    │                    │                         │
   └─ Fundament ──────────┴─ Nutzwert ohne ────┴─ erster echter ────┴─ finale Wirkung ────────┴─ Autonomie
     (unverzichtbar)        Schreibwirkung       Prozess-Mehrwert    unter Kontrolle           unter Kontrolle
```

**Begründung der Priorisierung:**

1. **Phase 1 trägt das gesamte Fundament.** Ohne `/mcp`-Endpunkt, PAT-Auth,
   ScopeEvaluator, Zugangsverwaltung und Audit-Schreibpfad existiert kein
   MCP-Zugriff – sie ist die unverhandelbare erste Phase. Gleichzeitig
   liefert sie bereits echten Nutzwert (Analyse-Szenario 3.1 des
   Fachkonzepts) bei **null Schreibwirkung**: das größte denkbare
   Risiko-Szenario (unkontrollierte Schreibzugriffe, Bedrohung B4) kann in
   Phase 1 strukturell nicht eintreten. Erster produktiver Mehrwert bei
   minimalem Risiko – darum zuerst.
2. **Phase 2 baut Risiko-frei weiter aus.** Vorschlagende Tools persistieren
   nichts; sie erhöhen den Nutzwert (Szenario 3.2), ohne das
   Schadens-Szenario zu verändern. Sie wird vor Phase 3 gezogen, weil die
   Vorschlagsfunktionen (`fm_contacts_suggest`, `fm_statement_draft_analyze`)
   fachliche Vorstufen des Kontoauszugs-Assistenten sind und mit Stufe
   „Individuell" erstmals `McpAccessScope`-Zeilen und Filter-Pflege
   produktiv werden – beides muss vor dem Schreibzugriff stehen.
3. **Phase 3 bringt den ersten Schreibzugriff – bewusst in der
   korrigierbaren Klasse.** Die schreibvorbereitenden Entwurfs-Tools
   verändern ausschließlich nicht-finale Daten (Entwurfsfelder, Zuordnungen);
   jede Wirkung ist korrigierbar und erzeugt keine Buchungs-/Belegdaten.
   Der Kontoauszugs-Assistent (F7) ist der zentrale Workflow der Anforderung
   und der Hauptnutzen der Integration – er wird so früh geliefert, wie es
   ohne die schwere Approval-Maschinerie möglich ist: In Phase 3 laufen
   schreibvorbereitende Aktionen im Genehmigungsmodus **Direktausführung**
   (entspricht dem Genehmigungsmodus „dauerhaft vorab genehmigt" für diese
   Klasse bzw. einer Zugangseinstellung mit Direktfreigabe).
4. **Phase 4 schaltet finale Wirkungen erst mit voller Kontrollinstanz
   frei.** Verbuchung, Storno, Löschung/Archivierung/Merge sowie der
   Genehmigungsmodus „je Aktion" für schreibvorbereitende Aktionen erfordern
   die `AiActionRequest`-Queue, den `AiActionExecutor`, das Genehmigungs-
   center und die Benachrichtigungskette. Diese Maschinerie früher zu bauen,
   würde Phase 3 künstlich verzögern; sie später zu bauen, würde finale
   Wirkungen ohne Benutzerentscheid zulassen – Phase 4 ist damit der
   früheste korrekte Platz für die Klasse „genehmigungspflichtig".
5. **Phase 5 ist bewusst zuletzt.** Agentenszenarien (Szenario 3.5) setzen
   alle vorherigen Ebenen voraus und profitieren von der dann erprobten
   Genehmigungs- und Audit-Infrastruktur (pre-approved Scopes,
   SSE-Benachrichtigungen, wiederkehrende Verarbeitung). Sie ist
   funktional optional und skaliert die Reife, nicht die Sicherheit.

**Abhängigkeiten zwischen den Phasen (verbindlich):**

|| Phase | hängt ab von | Begründung |
|---|---|---|---|
|| 1 | – | liefert Fundament |
|| 2 | 1 | Vorschlags-Tools laufen über denselben Sicherheits-Pfad; `Individuell`/Filter braucht Zugangsverwaltung + Evaluator |
|| 3 | 1, 2 | Vervollständigung setzt Vorschlags-/Analyse-Tools des Assistenten voraus; Entwurfs-Workflow baut auf lesenden Draft-Tools auf |
|| 4 | 1–3 | Genehmigungspflichtige Tools (T20/T21/T31, finale Teilmengen T22–T29) setzen die zugehörigen schreibvorbereitenden Pfade voraus; Queue braucht Audit + Benachrichtigung |
|| 5 | 4 | Pre-approved- und Agenten-Modi sind nur mit funktionierender Genehmigungsinfrastruktur fachlich sinnvoll |

**Inkrementell, aber als ein Modell geplant:** Entitäten, Indizes und
Konfigurationsschlüssel sind vollständig in
[datenmodell-erweiterungen.md](datenmodell-erweiterungen.md) geplant; die
Migrationen können je Phase aufgeteilt werden (Zugang+Scopes+Audit in Phase 1,
`AiActionRequest` in Phase 4) – ohne späteren Schema-Bruch (Datenmodell §12).

---

## 2. Phase 1 – Read-Only Analyse

**Zielbild:** Ein Benutzer legt einen MCP-Zugang der Stufe **Analyse** an,
übergibt den PAT an einen MCP-Client und kann die Finanzdaten lesend
auswerten lassen. Jeder Aufruf ist authentifiziert, scope-geprüft,
ratenlimitiert und revisionssicher protokolliert. Keine Schreibwirkung –
`Mcp:WriteAccess:Enabled` könnte in dieser Phase dauerhaft `false` bleiben.

### 2.1 Funktionsumfang

**Tools (15 von 33):**

|| Bereich | Tools |
|---|---|---|
|| Analyse/Reporting (lesend) | T01 `fm_accounts_overview`, T02 `fm_postings_query`, T03 `fm_reporting_aggregate`, T04 `fm_contacts_lookup`, T05 `fm_budget_overview`, T06 `fm_savings_plan_analysis`, T07 `fm_securities_analysis`, T08 `fm_statement_imports_review`, T09 `fm_attachments_read`, T10 `fm_configuration_read` |
|| Entwurfs-/Storno-Sicht (lesend) | T13 `fm_statement_drafts_read`, T14 `fm_statement_draft_validate`, T30 `fm_posting_reversal_check` |
|| Meta | T32 `fm_capabilities_describe` (T33 `fm_task_status` wird in Phase 4 voll relevant; Grundform kann bereits mit Phase 1 mitgeliefert werden, solange sie nur „keine Requests"-Semantik liefert) |

Die Tool-Sichtbarkeit bleibt katalogvollständig (`katalog-v1` liefert alle
deklarierten Tools aus); Aufrufe nicht-freigeschalteter Tools scheitern am
Scope (Scope-Fehler als Zustand, Toolkatalog §6.3) – der Katalog wird also
nicht je Phase verschnitten, nur die Ausführbarkeit wächst.

**Verwaltungsoberfläche/API:**

- `McpAccessesController` vollständig für Standardstufen: Liste, Detail,
  Anlage (einmaliger Klartext-Token), Aktivieren/Deaktivieren, Stufe/Modus/
  Ablaufdatum/IP-Bindung ändern, Token-Rotation, Widerruf/Löschen,
  `GET /levels`, `GET /options` (UI-Konzept 6.1).
- UI: Nav-Eintrag „KI-Zugänge (MCP)", Zugangsliste `/list/mcp-accesses`
  (`McpAccessListViewModel`), Zugangskarte mit Sections Stammdaten +
  Token + Nutzung & Verlauf, dreistufiger Anlage-Assistent mit
  Einmalanzeige-Dialog und Pflicht-Bestätigung, TLS-/HTTP-Warnhinweis
  (UI-Konzept 4.1/4.2).
- Freigabestufe „Analyse" produktiv; „Assistent" anlegbar (Vorschlags-
  Tools folgen in Phase 2, Aufrufe scheitern sauber am Scope); „Sach-
  bearbeiter"/„Buchhalter"/„Individuell" in der UI als „folgt"
  gekennzeichnet bzw. anlegbar ohne wirksame Tools – Festlegung in der
  Umsetzung, Konzept empfiehlt: anlegbar, Scope-System ist generisch.

**Server/Architektur:**

- `/mcp`-Endpunkt (Streamable HTTP, Request/Response; SSE-Upgrade erst
  Phase 5), Pipeline-Positionen 1–7 + 9–10 der 10-Stufen-Pipeline
  (Position 8, `McpApprovalGate`, entfällt in Phase 1 – es existiert
  keine genehmigungspflichtige Ausführung).
- `McpToolRegistry` (`katalog-v1`, `IMcpToolDescriptor`), `McpToolDispatcher`,
  `McpScopeEvaluator` **vollständig** inkl. Filter-CSV-Auswertung,
  Ableitungstabelle und `AccountShare`-Deckel (der Evaluator wird nicht
  pro Phase nachgebaut – er ist der Sicherheitskern und wird einmal
  vollständig gebaut).
- `McpAccessService`/`IMcpAccessContext` (Token→`OwnerUserId`),
  `McpAuthHandler` (eigenes Authentication-Schema der `/mcp`-Gruppe).
- **Neuer Service-Baustein:** `IStatementImportQueryService` (Voraussetzung
  für T08; offener Baustein aus Toolkatalog §3.1).
- Audit-Schreibpfad (`McpAuditWriter`) mit den Kategorien `ToolCall`,
  `AccessEvent`, `SecurityEvent` inkl. Hash-Kette
  (`SequenceNo`/`PrevHash`/`EntryHash`) – von Anfang an, damit auch die
  ersten Lesezugriffe nachvollziehbar sind.
- Konfiguration: `Mcp:Enabled` (Default `false`, Opt-in),
  `Mcp:WriteAccess:Enabled`, `Mcp:RateLimiting:*` (lesend/meta aktiv,
  übrige Klassen konfigurierbar), `Mcp:TokenHash:Iterations`,
  `Mcp:Audit:RetentionDays:*`.
- Entitäten/Migration: `McpAccess`, `McpAccessScope`, `McpAuditEntry`,
  `McpAuditEntryChange` inkl. `*BackupDto`-Einbindung (Token-Hash aus-
  genommen).

**Bewusst nicht in Phase 1:** alle schreibenden Tool-Klassen,
`AiActionRequest`, Genehmigungscenter, `AiActionExecutor`, Scope-Matrix-
Editor/Filter-Pflege (Expertenmodus, Phase 2), Admin-Sicherheitsübersicht
(Phase 4/5), SSE (Phase 5).

### 2.2 Sicherheitsrisiken und Mitigationen

|| Risiko | Mitigation (Konzept-Verweis) |
|---|---|---|
|| Token-Diebstahl bei HTTP-only-LAN (B1) | Einmalanzeige, pbkdf2-Hash, kurze `ExpiresUtc`-Empfehlung, IP-Bindung je Zugang, UI-Warnhinweis, TLS-Zielbetrieb dokumentiert (Sicherheitskonzept 3, 8) |
|| Datenabfluss über Analyse-Workload (B6, B1-Folge) | Rate-Limit-Partition je Zugangskürzel + IP-Partition unauthentifiziert; Paging-Clamp ≤ 200; Ausgabemengen-Limits (Sicherheitskonzept 7) |
|| Prompt-Injection über ausgelieferte Inhalte (B3) | `untrusted_data`-Kennzeichnung, Whitelist-Ausgabe, strukturierte DTOs statt Prosa; Anhänge-Scope nur bei Bedarf vergeben (Empfehlung) (Sicherheitskonzept 5) |
|| Übergriff auf fremde Daten / Rechteausweitung (B2, B7) | Prüfstufen 0–3 datensatzscharf, `AccountShare`-Deckel, 404-Semantik, kein Client-gesteuertes `ownerUserId` (Berechtigungsmodell) |
|| Brute-Force auf `/mcp` (B5) | IP-Rate-Limit unauthentifizierter Pfad, `IpBlock`-Anbindung, Fehlversuchszähler, Anomalie-Benachrichtigung (Sicherheitskonzept 6.3) |
|| Fehlkonfiguration (zu weite Stufe) | Stufen-Auswahlkarten mit Klartext; effektives Profil je Zugang einsehbar; Audit zeigt tatsächliche Nutzung (UI-Konzept 4.3) |

### 2.3 Technische Voraussetzungen

- Neue Entitäten: `McpAccess`, `McpAccessScope` (Struktur vollständig –
  Zeilen entstehen erst mit „Individuell"/Filtern), `McpAuditEntry`,
  `McpAuditEntryChange`; Enums `McpAccessLevel`, `McpDataType`,
  `McpRightKind`, `McpApprovalMode`, `McpAccessStatus`,
  `McpAuditCategory`; Migration via `ApplyMigrationsAndSeed`.
- Contracts: `IMcpAccessService`, `IMcpAccessContext`,
  `IMcpToolRegistry`/`IMcpToolDescriptor`, `IMcpScopeEvaluator`,
  `IMcpAuditWriter`, `IStatementImportQueryService` (neu);
  `IApiClient`-Partial `ApiClient.Mcp.cs` (Zugangsverwaltung).
- Pipeline-Bausteine: `McpAuthHandler`, `McpSessionHub`,
  `McpToolDispatcher`, `McpScopeEvaluator`, `McpAuditWriter`,
  Rate-Limit-Policy `RateLimitPolicies.Mcp*`, `Mcp:*`-Optionsbindung.
- Transport-Entscheidung: MCP-C#-SDK (Streamable HTTP) oder Eigenbau-
  Fallback – Umsetzungsoffen, fachlich neutral (Architekturkonzept 3.2).

### 2.4 Erfolgskriterien

1. Ein Benutzer kann einen Zugang der Stufe „Analyse" anlegen, erhält
   den Token genau einmal im Klartext und findet ihn danach nur noch als
   Status/Metadaten vor.
2. Ein MCP-Client authentifiziert sich per `Authorization: Bearer
   fm_mcp_…` an `/mcp` und kann T01–T10, T13/T14, T30, T32 aufrufen –
   ausschließlich mit Daten des Zugangsbesitzers.
3. Scope-Verstoß (z. B. `fm_contacts_manage` auf Analyse-Zugang) wird
   mit definierbarem Scope-Fehler abgelehnt; fremde/gefilterte
   Datensätze verhalten sich wie nicht vorhanden.
4. Widerruf/Deaktivierung wirkt ab der nächsten Anfrage (401);
   `Mcp:Enabled=false` lehnt alle `/mcp`-Anfragen ab.
5. Jeder Aufruf (Erfolg, Verweigerung, Rate-Limit) steht als
   `McpAuditEntry` mit `CorrelationId` und Hash-Kette; die Kette ist
   verifizierbar.
6. Kein `/mcp`-Aufruf verändert Fachdaten (Code-Review/Test:
   schreibende Tools nicht ausführbar).

---

## 3. Phase 2 – Assistenz- und Vorschlagsfunktionen

**Zielbild:** Stufe **Assistent** wird produktiv: Der Client erzeugt
strukturierte fachliche Vorschläge ohne Persistenz von Benutzerdaten.
Zusätzlich werden die Stufe **Individuell** (Scope-Matrix-Editor),
die Filterverwaltung und die Benutzer-Audit-Ansicht freigeschaltet.

### 3.1 Funktionsumfang

**Tools (4):** T11 `fm_contacts_suggest`, T12 `fm_budget_rules_suggest`,
T15 `fm_statement_draft_analyze` (vorschlagend – inkl. `ClassifyAsync`-
Pipeline-Flags), T19 `fm_statement_draft_prepare_booking`.

**Rechte-/Filterverwaltung:**

- Stufe „Individuell" inkl. „Von Vorlage anpassen": `McpAccessScope`-
  Zeilen werden angelegt/persistiert; serverseitige Validierung gegen
  die F3-Matrix („nur ✓-Zellen") und Deckel (`Err_Mcp_InvalidScope`).
- Scope-Matrix-Editor (18 Datentypen × 7 Rechtearten, deaktivierte
  „—"-Zellen mit Kurzbegründung, Implikationshilfe „Änderung ohne Lesen"),
  Endpunkt `PUT /api/mcp-accesses/{id}/scopes`.
- Filterverwaltung: `PUT /api/mcp-accesses/{id}/filters`, fünf
  Filterdimensionen mit Kategorielisten aus Bestands-Endpunkten,
  Semantik-Hinweise (unklassifiziert nicht erfasst, UND über Dimensionen).
- Genehmigungsmodus-Anzeige/-Auswahl in der Rechte-Section
  (UI-Konzept 4.3.3); wirksam wird „je Aktion" erst mit Phase 4 – bis
  dahin ist Direktausführung die einzige Ausführungsform und die
  Auswahl wird entsprechend geführt (Auswahl speicherbar, Wirkung ab
  Phase 4 – oder Auswahl erst mit Phase 4 freigegeben; Festlegung in
  der Umsetzung, beide Varianten konzeptkonform).

**Audit-Ansicht (Benutzer):** `McpAuditViewModel` + `McpAuditController`
(`GET /api/mcp-audit`, Detail, Export JSON/CSV, `POST /verify`) –
Timeline, Filterleiste, Drilling auf Feld-Deltas und Request-Bezüge,
„Integrität prüfen". Nutzungsübersicht der Zugangskarte (Aufrufzahlen,
Fehlschlagquote) wird audit-gespeist.

**Bewusst nicht in Phase 2:** schreibende Wirkungen, `AiActionRequest`,
SSE, Agenten-Modi.

### 3.2 Sicherheitsrisiken und Mitigationen

|| Risiko | Mitigation |
|---|---|---|
|| Vorschläge „durchleuchten" gefilterte Daten (Indirekte Offenlegung) | Vorschläge nur auf Datensätzen des effektiven Scope/Filters; Vorschlagsziele ebenfalls scope-geprüft (Toolkatalog §3.2 Nebenbedingung, Evaluator datensatzscharf) |
|| `ClassifyAsync`-Nebenwirkung (Pipeline-Flags) als verdeckte Schreibwirkung | Präzisierte Klassifizierung „vorschlagend" dokumentiert (Toolkatalog §2); `fm_statement_draft_analyze` bleibt ohne fachliche Persistenz – Prüfung im Code-Review und über Audit (`DataChange`-Einträge dürften nicht entstehen) |
|| Fehlkonfiguration individueller Profile (zu weit) | Server-Validierung gegen F3-Matrix; UI-Implikationshilfe; Effektiv-Matrix zeigt Ergebnis; Audit protokolliert jede Scope-/Filter-Änderung (AccessEvent mit Vorher/Nachher-Delta) |
|| Filter-Missverständnis (Benutzer glaubt geschützt, unklassifizierte Datensätze sind „ausgenommen") | Verbindliche Semantik „gesetzter Filter erfasst Unklassifiziertes nicht" ist restriktiv, nicht ausweitend; UI-Kurzinfos an jeder Auswahl (UI-Konzept 4.4) |

### 3.3 Technische Voraussetzungen

- Phase-1-Fundament; `McpAccessScope`-Persistenzpfad aktiviert
  (Laufzeit-Ableitung der Standardstufen bleibt – Zeilen nur bei
  „Individuell"/materialisierter Vorlage, Datenmodell §4).
- Serverseitige Stufen-/F3-Validierung für `PUT …/scopes`.
- `McpAuditController` + `McpAuditViewModel`; `IApiClient`-Erweiterung.
- Keine neuen Entitäten; keine neuen `Mcp:*`-Schlüssel.

### 3.4 Erfolgskriterien

1. Ein Assistent-Zugang kann T11/T12/T15/T19 aufrufen; Ergebnisse sind
   strukturierte Vorschläge ohne Persistenz (kein `DataChange`-Audit-
   Eintrag, keine geänderten Felder).
2. Stufe „Individuell" lässt Scope-Tupel nur innerhalb der F3-„✓"-Zellen
   setzen; unsinnige Kombinationen werden serverseitig abgelehnt.
3. Ein gesetzter Kategoriefilter begrenzt Listen- und Vorschlags-
   ergebnisse datensatzscharf; unklassifizierte Datensätze erscheinen
   nicht.
4. Die Audit-Ansicht zeigt Toolaufrufe, Verweigerungen und
   Zugangsereignisse in Klartext; Export und Integritätsprüfung
   funktionieren.

---

## 4. Phase 3 – Kontoauszugs-Assistent

**Zielbild:** Der F7-Workflow wird produktiv: Der Client importiert
Kontoauszugs-Dateien über die vorhandenen Mechanismen, analysiert
Entwürfe, vervollständigt fehlende Zuordnungen und bereitet die
Verbuchung vor. Alle Aktionen sind **schreibvorbereitend** – also
korrigierbar und ohne Belegwirkung. Stufe **Sachbearbeiter** wird
produktiv. In Phase 3 laufen schreibvorbereitende Aktionen im
Genehmigungsmodus **Direktausführung**; der Modus „je Aktion" wird mit
Phase 4 aktivierbar (Konsistenz: Sachbearbeiter-Standard „je Batch"
ist erst mit `AiActionRequest` abbildbar – Benutzer, die den Assistenten
vor Phase 4 betreiben, wählen implizit die Direktausführung; die UI
kennzeichnet diesen Zustand).

### 4.1 Funktionsumfang

**Tools:**

|| Bereich | Tools |
|---|---|---|
|| Kontoauszugs-Workflow | T16 `fm_statement_draft_import` (nur Datei-Import/`MassImportOrchestrator` – kein Bankabruf), T17 `fm_statement_draft_complete` (nur Lücken), T18 `fm_statement_draft_edit` (Korrektur inkl. `ResetDuplicateEntryAsync`) |
|| Stammdaten-Pflege (direkte Teilmengen) | T22 `fm_contacts_manage` (Anlegen/Ändern/Aliase – ohne Merge/Löschen), T23 `fm_accounts_manage` (ohne Löschen), T24 `fm_savings_plans_manage` (ohne Archiv), T25 `fm_securities_manage` (ohne Archiv), T26 `fm_budgets_manage` (ohne Löschen), T27 `fm_categories_manage` (ohne Löschen), T28 `fm_attachments_manage` (ohne Löschen), T29 `fm_reports_configuration_manage` (ohne Löschen) |

Die genehmigungspflichtigen Teilmengen (Löschen/Archivieren/Merge) der
Pflege-Tools bleiben in Phase 3 **nicht ausführbar** – sie werden erst
mit dem Approval-Gate verdrahtet (Phase 4); ein Aufruf dieser
Teiloperationen schlägt mit definierbarem Fehler fehl (bzw. wird vom
Tool-Deskriptor als „erfordert Genehmigungsinfrastruktur" abgelehnt).

**Fachliche Ergänzungen:** fachliche Zusatzregeln des Tools
`fm_statement_draft_validate` (Zuordnungsvollständigkeit, Betrags-/
Vorzeichenlogik, Datumslogik, Split-Konsistenz – Toolkatalog §4.4);
Batch-Atomarität (`ApplyBatchEntryUpdatesAsync` ganz-oder-gar-nicht);
Upload-/Batch-Limits (20 MB, ≤ 100 Entries je Aufruf).

**UI:** keine neuen Komponenten nötig; die Nutzungsübersicht und
Audit-Ansicht zeigen schreibvorbereitende `DataChange`-Einträge.

### 4.2 Sicherheitsrisiken und Mitigationen

|| Risiko | Mitigation |
|---|---|---|
|| Fehlerhafte/manipulierte KI verunstaltet Entwürfe/Stammdaten (B4, erste Schreibwirkung) | Wirkung bleibt nicht-final und korrigierbar; Feld-Deltas im Audit (`McpAuditEntryChange`) machen jede Änderung rekonstruierbar; Rate-Limits der Klasse schreibvorbereitend (30/Min, Batch ≤ 100); Schreib-Stopp `Mcp:WriteAccess:Enabled` kapppt jederzeit |
|| Unbeabsichtigte Massenimporte/-änderungen | `fm_statement_draft_import` ausschließlich über Bestandsmechanik mit denselben Limits; atomare Batches; Pending-/Upload-Restriktionen der Pipeline unverändert |
|| Injektion über Tool-Parameter in Datenbankinhalte (Rückfluss, B3) | ausschließlich typisierte `*Request`-Validierung wie API-Pfad; keine Freitext-Kommandos; Freitext-Längenbegrenzung (Sicherheitskonzept 5.3) |
|| Entwurfs-Schreibzugriff ohne Buchung wirkt „halbfertig" | fachlich gewollt: Phase 3 endet bewusst vor Belegwirkung; `fm_statement_draft_book`/`_discard` sind Scope-technisch erst in Phase 4 ausführbar |

### 4.3 Technische Voraussetzungen

- Phase 1 + 2; `Mcp:WriteAccess:Enabled` wird produktiv relevant
  (Default `true`, wirkt als Deckel ab Pipeline-Position 8 – in
  Phase 3 als Direktausführungs-Freigabe modelliert).
- Adapter für `IStatementDraftService` (Set*/Save*/Batch/Core-Update/
  Create-Methoden), `IMassImportOrchestrator`, `IContactService`,
  `IAccountService`, `ISavingsPlanService`, `ISecurityService`,
  `IBudget*Service`, Kategorien-, `IAttachmentService`,
  `IReportFavoriteService`/`IHomeKpiService` (Toolkatalog §3.3/3.4).
- Keine neuen Entitäten; Audit erfasst `DataChange`-Einträge mit
  Feld-Deltas (vorhandene Tabellen nutzen).

### 4.4 Erfolgskriterien

1. Ein Sachbearbeiter-Zugang kann den vollständigen Workflow
   `import → read → analyze → complete → validate → prepare_booking`
   ausführen; `fm_statement_draft_book` schlägt kontrolliert fehl.
2. Vervollständigungen schreiben nur in Lücken (kein Überschreiben);
   Batch-Updates sind atomar; verweigerte Elemente scheitern den
   Batch, statt still übersprungen zu werden.
3. Jede Änderung erzeugt `DataChange`-Audit-Einträge mit Vorher/Nachher-
   Deltas, rekonstruierbar über `CorrelationId`.
4. `Mcp:WriteAccess:Enabled=false` degradet den Server sofort auf
   lesend+vorschlagend, ohne Fehlerzustände.
5. Fehlzuordnungen der KI sind über die Audit-Ansicht identifizierbar
   und in der UI korrigierbar (kein Datenverlust, keine Belegwirkung).

---

## 5. Phase 4 – Genehmigungspflichtige Aktionen

**Zielbild:** Stufe **Buchhalter** und der Genehmigungsmodus „je
Aktion"/„dauerhaft vorab genehmigt" werden produktiv: Finale Wirkungen
(Verbuchung, Entwurf verwerfen, Storno, Löschen/Archivieren/Merge von
Stammdaten) laufen ausschließlich als `AiActionRequest` über das
Genehmigungscenter. Der Benutzer entscheidet über die eingefrorene
Wirkungsvorschau; die Ausführung erfolgt einmalig, neustartsicher
und voll auditiert.

### 5.1 Funktionsumfang

**Tools:**

- Immer genehmigungspflichtig: T20 `fm_statement_draft_book`,
  T21 `fm_statement_draft_discard`, T31 `fm_posting_reverse`.
- Finale Teilmengen der Pflege-Tools T22–T29 (Merge, Löschen,
  Archivierung) werden freigeschaltet.
- T33 `fm_task_status` vollständig (Request-Zustände Pending/
  Approved/Rejected/Executed/Failed/Expired + Task-Fortschritt).
- Genehmigungsmodus `PerAction` wird für schreibvorbereitende Tools
  aktivierbar (Sachbearbeiter-Standard „je Aktion/Batch" – erst jetzt
  mit `AiActionRequest` abbildbar); `PreApproved` für vertrauens-
  würdige Zugänge inkl. dokumentierter Genehmigungsinstanz.

**Genehmigungsinfrastruktur:**

- Entität `AiActionRequest` + Migration (inkrementell, Datenmodell §5);
  `McpApprovalGate` an Pipeline-Position 8; `AiActionExecutor` (Hosted
  Service, `BackgroundTaskRunner`-Muster) mit atomarem SQLite-Claim
  (`Approved → laufend` via `ClaimToken`/`ClaimedUtc`), fachlicher
  Guard-Re-Prüfung (`CanReverseAsync`, `AlreadyBooked`, Besitz,
  Stufen 0–3) und Expired-Job.
- `AiActionRequestsController` (`/api/ai-action-requests`: Liste, Count
  für Nav-Badge, Detail mit serverseitig aufbereiteter Wirkungsvorschau,
  approve/reject, batch) + `IAiActionRequestService`.
- Genehmigungscenter `/card/mcp-approvals`
  (`McpApprovalCenterViewModel`): Eingangskorb, Wirkungsvorschau je
  Aktionstyp (Buchungsplan, Storno-Gruppe, Lösch-/Archiv-/Merge-
  Konsequenzen, Batch-Deltas), `ReasonText` als Client-Nachricht
  gekennzeichnet, Mehrfachauswahl, Historie, `IConfirmationService`
  für finale Wirkungen.
- Benachrichtigungen: `INotificationWriter` EventDriven bei neuer
  Anfrage und Ausführungsergebnis; Zähler-Badge am Nav-Eintrag.
- `IdempotencyKey`-Deduplizierung, Pending-Limit
  (`Mcp:Approval:MaxPendingPerAccess`), `Mcp:Approval:DefaultExpiryHours`.
- Admin-Sicherheitsübersicht (`McpSecurityOverviewViewModel`,
  `GET /api/mcp-audit/security-overview`): Schalter-Zustand,
  Anomalien, IP-Sperren – ohne Fremd-Fachdaten.

### 5.2 Sicherheitsrisiken und Mitigationen

|| Risiko | Mitigation |
|---|---|---|
|| Finale Wirkung ohne Kenntnis des Benutzers (B4) | `AiActionRequest` Pflicht, Wirkungsvorschau aus `ParameterSnapshot` (nicht Client-Prosa), `IConfirmationService`, dokumentierte Genehmigung auch bei `PreApproved` (Approval-Konzept Grundsätze 1/4) |
|| Bait-and-Switch (genehmigte ≠ ausgeführte Aktion) | Snapshot-Treue: Parameter eingefroren, Benutzer kann nur freigeben/ablehnen; Guard-Re-Prüfung bei Ausführung; veraltete Vorbedingungen → `Failed` mit `Err_*`-Code (Approval-Konzept 3.1/5.2/6) |
|| Doppelausführung (Retry, Neustart, UI+Queue parallel) | `IdempotencyKey`-Unique, atomarer Claim, Single-Shot-Zustände, `StatementDraftBookingGuard` im Ausführungspfad |
|| Approval-Spam (Client flutet Eingangskorb) | Pending-Limit je Zugang + niedrigstes Rate-Limit der Klasse; Batch-Entscheidung in der UI; Zugangsdeaktivierung als Eskalation |
|| Verwaiste/veraltete Requests | `ExpiresUtc` + Expired-Job; systemseitiges `Rejected` bei Widerruf/Deckel-Änderung; Requests überleben Neustarts persistent |
|| Admin-Einblick in Fremddaten | Admin-Sicht ausschließlich Sicherheitsmetadaten (Audit-Konzept 6.2); kein Fremdzugriff |

### 5.3 Technische Voraussetzungen

- Phasen 1–3; Entität `AiActionRequest` inkl. aller Indizes
  (Idempotency-Unique, Queue-Poll `(Status, ExpiresUtc)`,
  Genehmigungscenter `(OwnerUserId, Status, RequestedUtc)`,
  `CorrelationId`, `McpAccessId`-Soft-Referenz); `*BackupDto`-
  Einbindung (offene Requests überleben Restore).
- `McpApprovalGate`, `AiActionExecutor` + Claim-Logik,
  Expired-Auswertungslauf, `INotificationWriter`-Anbindung,
  `Mcp:Approval:*`-Schlüssel.
- `AiActionRequestsController` + Genehmigungscenter-UI + Nav-Badge;
  `fm_task_status`-Vollausbau.
- Keine Änderungen an Bestandsentitäten; Guard-Integration in
  `IStatementDraftService`-/`IPostingReversalService`-Aufrufpfade.

### 5.4 Erfolgskriterien

1. `fm_statement_draft_book` eines Buchhalter-Zugangs erzeugt einen
   Pending-`AiActionRequest` inkl. synchroner Request-Id; die
   synchrone Antwort enthält keine Ausführungswirkung.
2. Der Besitzer sieht die Anfrage als Notification und im
   Genehmigungscenter mit korrekter Wirkungsvorschau (Buchungsplan
   aus dem Snapshot); Freigabe führt genau einmal zur Ausführung,
   Ablehnung/Ablauf zu `Rejected`/`Expired`.
3. Ausgeführte Wirkungen erzeugen `Posting`/`StatementImport` nur über
   den regulären Service-Pfad; `ExecutionResult` verknüpft Request ↔
   Audit ↔ Ergebnis-Ids (`CorrelationId`-Kette).
4. Prozess-Neustart zwischen Freigabe und Ausführung verliert keinen
   Approved-Request; `IdempotencyKey`-Retry liefert denselben Request.
5. Widerruf eines Zugangs zieht offene Requests systemseitig auf
   `Rejected`; `Mcp:WriteAccess:Enabled=false` stoppt die Queue, ohne
   Daten zu verlieren.
6. Vorab-genehmigte Ausführungen schreiben dieselbe Request-/Audit-
   Spur (kein Pfad an der Genehmigungsinstanz vorbei).

---

## 6. Phase 5 – Agentengestützte Workflows

**Zielbild:** Wiederkehrende, mehrstufige Aufgaben (Szenario 3.5 des
Fachkonzepts) werden unterstützt: Ein Agenten-Client verarbeitet z. B.
neue Kontoauszugs-Entwürfe eigenständig bis zur Buchungsreife und meldet
offene Fälle – unter denselben Scopes, Filtern, Genehmigungspflichten
und Limits. **Vollautonomie bleibt Nicht-Ziel**; Agenten laufen über
denselben Zugangsmechanismus mit engeren Empfehlungen.

### 6.1 Funktionsumfang

- **SSE-Upgrade des Streamable-HTTP-Endpunkts** für serverinitiierte
  Benachrichtigungen (Architekturkonzept 3.1: primär für Agenten-
  Szenarien reserviert) – z. B. Statusänderungen von Requests ohne
  Polling; `fm_task_status` bleibt kompatibler Fallback.
- **Pre-approved-Agenten-Modus produktiv:** „dauerhaft vorab
  genehmigte" Scopes für vertrauenswürdige Agenten-Zugänge – mit
  dokumentierter Genehmigungsinstanz, `IConfirmationService`-
  Bestätigung bei der Einrichtung und empfohlenen Zusatzdeckeln
  (IP-Bindung, enge Filter, kurze `ExpiresUtc`, Pending-/Rate-Limits).
- **Wiederkehrende Verarbeitungsmuster** (fachliche Ausbaustufe, kein
  neuer Rechtekreis): konfigurierbare Agenten-Runbooks auf Tool-Ebene
  (z. B. „neue Entwürfe: analyze → complete → prepare_booking →
  Request"), Betriebs-/Monitoring-Sichten auf Request-Mengen,
  Fehlschlagquoten und Ausführungszeiten (aus `McpAuditEntries`
  aggregiert).
- **Betriebshärtung:** Retention-Executor produktiv
  (`Mcp:Audit:RetentionDays:*`, `RetentionPurged`-Ereignis),
  Anomalieerkennung-Ausbaustufe (Trends über Agenten-Workloads,
  gestufte Zugangsdeaktivierung), Deprecation-/Katalogsprung-
  Verfahren erstmals exerziert (`katalog-v`-Hauptversion bei
  Änderungsbedarf).
- Optional nach Bedarf: Legacy-HTTP+SSE-Kompatibilität für Alt-Clients
  (Architekturkonzept 3.1, als Erweiterung dokumentiert), weitere
  Tools nur als neuer Geschäftsprozess (Versionierungsregeln –
  „kein Tool für Einzelfälle").

### 6.2 Sicherheitsrisiken und Mitigationen

|| Risiko | Mitigation |
|---|---|---|
|| Autonom wirkender Agent mit weitem Profil (B4, erhöhte Fläche) | Gleiche Prüfstufen 0–3 + Genehmigungspflicht für finale Aktionen; Pre-approved nur nach bewusster `IConfirmationService`-Bestätigung; Empfehlung enger Filter/IP-Bindung/kurzer Ablaufdaten |
|| Lange laufende Verbindungen/SSE als Angriffs- oder Ressourcen-Fläche | Rate-Limits unverändert je Zugang; Session ist reiner Transportzustand (sicherheitsneutral); Proxy-Timeouts dokumentiert (Architekturkonzept 11) |
|| Alarm-Müdigkeit bei Agenten-Workloads (Benachrichtigungsflut) | EventDriven statt Modal, Aggregation; Pending-Limits; Nutzungs-/Fehlschlag-Dashboards aus Audit (Approval-Konzept 7.6, Sicherheitskonzept 6.3) |
|| Protokollwachstum durch Dauerbetrieb | Retention-Executor (12/36-Monats-Stufen), Export vor Bereinigung, `RetentionPurged` als Kettenanfang |

### 6.3 Technische Voraussetzungen

- Phase 4 vollständig; `McpSessionHub`-SSE-Pfad; Executor/
  Scheduler-Erweiterungen im `MonthlyReminderScheduler`- bzw.
  `BackgroundTaskRunner`-Muster; Audit-Aggregationen für Monitoring.
- Keine neuen Entitäten erforderlich; ggf. Konfigurationsschlüssel für
  Agenten-Profile (Festlegung in der Umsetzung, `Mcp:*`-Konvention).

### 6.4 Erfolgskriterien

1. Ein Agenten-Client kann einen dokumentierten wiederkehrenden
   Workflow (z. B. Entwurfs-Bearbeitung bis Buchungsreife) über den
   regulären Tool-/Approval-Pfad ausführen – ohne Sonderrechte.
2. Pre-approved-Zugänge erzeugen vollständige Request-/Audit-Ketten;
   die dokumentierte Genehmigungsinstanz ist aus dem Audit ableitbar.
3. SSE-Benachrichtigungen erreichen den Client; ohne SSE bleibt
   `fm_task_status`-Polling funktionsfähig.
4. Retention-Lauf entfernt fristüberschreitende Einträge nach den
   Stufen und hinterlässt `RetentionPurged` als verifizierbaren
   Kettenanfang.
5. Jederzeit greifbar: Kill-Switch, Schreib-Stopp, Zugangs-Widerruf –
   ein Agent ist sekundenschnell abschaltbar.

---

## 7. Übergreifende Querschnitts-Themen je Phase

|| Thema | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Phase 5 |
|---|---|---|---|---|---|
|| Neue Entitäten | `McpAccess`, `McpAccessScope`, `McpAuditEntry(+Change)` | – | – | `AiActionRequest` | – |
|| Freigabestufen produktiv | Analyse (+ Assistent anlegbar) | Assistent, Individuell | Sachbearbeiter | Buchhalter, Genehmigungsmodus `PerAction`/`PreApproved` | Agenten-Profile |
|| Tool-Klassen aktiv | lesend (+ Meta) | + vorschlagend | + schreibvorbereitend (direkt) | + genehmigungspflichtig (+ `PerAction` für schreibvorbereitend) | unverändert |
|| `Mcp:*`-Schlüssel neu | `Enabled`, `WriteAccess`, `RateLimiting`, `TokenHash`, `Audit` | – | – | `Approval:*` | ggf. Agenten-Optionen |
|| UI-Neulieferungen | Zugangsliste/-karte, Anlage-Assistent, Token-Section | Matrix-Editor, Filter, Audit-Ansicht | – | Genehmigungscenter, Badge, Admin-Übersicht | Monitoring-Sichten |
|| Katalogversion | `katalog-v1` | `katalog-v1.x` (kompatibel) | `katalog-v1.x` | `katalog-v1.x` | ggf. erste Hauptversion |

## 8. Abgrenzung

- Die Roadmap legt **Reihenfolge und Umfang** fest, keine Kalendertermine;
  Aufwände stehen in [aufwandsschaetzung.md](aufwandsschaetzung.md),
  die Umsetzungs-Features in [product-backlog.md](product-backlog.md).
- Die Phasen sind lieferfähige Stufen: Jede Phase endet mit einem
  betreibbaren, in sich konsistenten Zustand (`Mcp:Enabled=false` bleibt
  jederzeit der sichere Default; Migration ≠ Aktivierung).
- Keine Roadmap-Phase führt Nicht-Ziele ein: kein Bankabruf, keine
  generischen CRUD-/Query-Tools, kein DB-Zugriff, keine Vollautonomie.
