# Architekturkonzept – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 6 der Anforderung; legt die technische
Zielarchitektur (F12) des MCP-Servers fest. Baut auf allen
Vorgängerartefakten auf: Scope-Tupel und Prüfstufen 0–3 aus
[berechtigungsmodell.md](berechtigungsmodell.md), Toolkatalog T01–T33
und vier Sicherheitsklassen aus
[scope-und-toolkatalog.md](scope-und-toolkatalog.md), PAT-/Notfall-/
Rate-Limit-Festlegungen aus
[sicherheitskonzept.md](sicherheitskonzept.md),
`AiActionRequest`-Queue aus [approval-konzept.md](approval-konzept.md)
und Audit-Schreibpfad aus [audit-konzept.md](audit-konzept.md).
Persistenzdetails: [datenmodell-erweiterungen.md](datenmodell-erweiterungen.md).

---

## 1. Zielbild und verbindliche Grundsätze

Der MCP-Server ist eine **parallele Zugriffsschicht** auf denselben
Service-Layer (`I*Service`-Contracts), den auch die BFF-REST-API für
die Blazor-UI nutzt. Er ist bewusst **kein** zweiter fachlicher Pfad:

1. **Ein Service-Layer, zwei Eingänge.** Jeder MCP-Tool ist ein
   dünner Adapter auf vorhandene `I*Service`-Contracts mit der
   `ownerUserId` des Zugangsbesitzers – identisch zum Controller-Pfad,
   kein privilegierter Kontext, kein eigener Datenbestand.
2. **Defense in Depth als Pipeline.** Kein einzelner Mechanismus
   trägt die Sicherheit: Token-Auth → Scope-/Filter-Prüfung →
   Sicherheitsklasse → Approval-Gate → Adapter → Audit
   (Sicherheitskonzept, Grundsatz 5).
3. **Single-Instance-Konformität.** Self-hosted Kestrel + SQLite +
   Auto-Update (`msTools.Updater`) bleiben unverändert; alles Neue
   muss Prozess-Neustarts überleben (persistente Queue statt
   In-Memory) und braucht kein verteiltes Locking.
4. **Bestehende Mechanik wiederverwenden.** `I*Service`-Contracts,
   `IpBlockMiddleware`, `AddRateLimiter`, `INotificationWriter`,
   `IBackgroundTaskManager`-Pattern, `StatementDraftBookingGuard`,
   `appsettings`-Feature-Flags.
5. **Keine Nicht-Ziele einführen.** Der Server exponiert
   ausschließlich den deklarierten Toolkatalog – kein generischer
   Datenzugriff, keine CRUD-Fläche, keine Bankanbindung.

---

## 2. Betriebsform (Entscheidung: in-process in `FinanceManager.Web`)

**Festlegung (final, behandelt Projektplan „Offene Punkte" Nr. 1):**
Der MCP-Server wird **in-process** in `FinanceManager.Web`
integriert und unter dem eigenen Route-Pfad **`/mcp`** bedient. Er
teilt Prozess, DI-Container, `AppDbContext`, Service-Layer und
Konfiguration mit der bestehenden Anwendung.

### 2.1 Begründung

- **Single-Instance-Deployment:** Es existiert genau ein Prozess und
  eine SQLite-Datei; ein zweiter Prozess brächte einen zweiten
  Schreibzugriff auf dieselbe DB-Datei – SQLite-serialisiert ohnehin,
  ein Verteiltheitsgewinn existiert nicht.
- **Auto-Update-Verträglichkeit:** `msTools.Updater` aktualisiert
  genau einen Dienst; ein separater MCP-Prozess müsste eigenständig
  aktualisiert, versioniert und im Update-Fenster koordiniert werden
  (Versionsskew zwischen API und MCP-Layer = reale Gefahr).
- **Dieselbe Vertrauensgrenze:** Tools brauchen die
  `I*Service`-Implementierungen in derselben Assembly-Kette
  (Infrastructure) – ein entfernter Prozess müsste sie duplizieren
  oder über einen neuen RPC-Pfad anbinden.
- **Ops-Einfachheit:** ein Port, eine TLS-/Proxy-Konfiguration, ein
  Logging, ein Backup, ein Migrationsschritt.

### 2.2 Geprüfte und verworfene Alternativen

|| Alternative | Warum verworfen |
|---|---|---|
| **Separater Prozess/Dienst** (eigener Kestrel-Host, z. B. `FinanceManager.Mcp`) | Zweiter DB-Zugriff auf dieselbe SQLite-Datei (Schreibserialisierung, WAL-Konkurrenz); zweiter Update-Pfad; kein Mehrwert – Isolation ist hier nicht gefordert, da der MCP-Layer bewusst auf denselben Services sitzt und keine eigenen Privilegien hat. Skalierungs-/Isolationsargumente entfallen im Single-Instance-Selbsthosting. |
| **Separates Deployment auf entferntem Host** | Erfordert verteilten Zugriff auf Fachdaten (neuer Remote-Contract = faktisch zweite API) – bricht Grundsatz 1 und erzeugt exakt die generische Zugriffsfläche, die das Konzept vermeidet. |
| **Nur stdio-Betrieb** (lokaler MCP-Client-Prozess) | Externe KI-Systeme (ChatGPT, Claude, Copilot) sind Remote-Clients; stdio bedient nur lokale Prozesse und würde den LAN-/Remote-Zugriff der Anforderung ausschließen. |

### 2.3 Konsequenz der Route-Trennung

`/mcp` und `/api` koexistieren im selben Host mit **getrennten
Authentifizierungspfaden**: `/api` bleibt JWT/Cookie-BFF für die
Blazor-UI, `/mcp` authentifiziert ausschließlich per PAT
(`Authorization: Bearer fm_mcp_…`). Die Cookie-Auth der UI wirkt
nicht auf `/mcp` (kein `FinanceManager.Auth`-Fallback im MCP-Pfad;
CSRF-Antiforgery-Middleware bleibt dem UI-Pfad vorbehalten –
Header-Auth ist CSRF-immun, Sicherheitskonzept 3.4).

---

## 3. Transport-Entscheidung: Streamable HTTP

**Festlegung:** Der MCP-Endpunkt implementiert den
**Streamable-HTTP-Transport** der MCP-Spezifikation (Annahme A4 der
Anforderung, hiermit verbindlich).

- **Mechanik:** Ein einziger HTTP-Endpunkt `/mcp` für `POST`
  (JSON-RPC-Nachrichten) und `GET` (optionale SSE-Upgrade für
  serverinitiierte Nachrichten/Streaming innerhalb desselben
  Endpunkts). Session-Zuordnung über den vom Server vergebenen
  Session-Header; der Client pollt/subskribiert über denselben Pfad.
- **Begründung:** Streamable HTTP ist der aktuelle
  Standard-Remote-Transport von MCP, deckt Request/Response und
  Streaming in einem Endpunkt ab, ist reverse-proxy-freundlich
  (gewöhnliches HTTP/S), und benötigt keine parallele
  Zwei-Endpunkt-Topologie.

### 3.1 Alternativen (geprüft, nicht gewählt)

|| Alternative | Entscheidung |
|---|---|---|
| Legacy **HTTP+SSE** (separate `/sse`- und `/messages`-Endpunkte, MCP-Version 2024-11-05) | Nicht gewählt – durch Streamable HTTP abgelöst; zusätzliche Endpunkte und komplexere Session-Verwaltung ohne Nutzen. Als **Kompatibilitätshinweis** dokumentiert: sollte ein Ziel-Client nur Legacy-SSE können, ist die Alt-Topologie als spätere Erweiterung möglich, aber nicht Teil des Konzepts. |
| **stdio** | Verworfen (siehe 2.2) – nur lokale Clients. |
| **WebSocket** | Nicht gewählt – außerhalb der MCP-Transport-Spezifikation; Streamable HTTP deckt Streaming ab, WebSocket brächte proprietäres Protokoll und Proxy-Komplexität ohne Mehrwert. |
| **Reines Request/Response ohne SSE** | Reduktion auf `POST`-only ist im Streamable-HTTP-Modell erlaubt (stateless-Betrieb), schwächt aber lange laufende Rückmeldungen. **Festlegung:** SSE-Upgrade wird unterstützt, primär für serverseitige Benachrichtigungen in Agenten-Szenarien (Roadmap-Phase 5); Frühphasen kommen mit Request/Response + `fm_task_status`-Polling aus. |

### 3.2 Framework-Einbindung

Die Implementierung soll auf dem offiziellen
**MCP-C#-SDK** (`ModelContextProtocol`-Bibliothek, ASP.NET-Core-
Integration) aufsetzen, sofern es zum Umsetzungszeitpunkt den
Streamable-HTTP-Transport serverseitig unterstützt. Fallback (wenn
nicht verfügbar): eigenständige Implementierung des Transports als
ASP.NET-Core-Endpoint-Gruppe – die fachlichen Festlegungen dieses
Dokuments (Sicherheitslayer, Tool-Registry, Adapter) sind vom
Transport-Framework unabhängig und bleiben in beiden Fällen gültig.

---

## 4. Struktur des MCP-Server-Layers

### 4.1 Komponentenübersicht

```text
FinanceManager.Web (in-process, ein Prozess)
│
├── Route /api/…            → BFF-Controller (bestehend, JWT) ─────────┐
│                                                                     │ gemeinsamer
├── Route /mcp              → MCP-Endpunkt (NEU, PAT)                  │ Service-Layer
│   └── McpServerLayer (NEU)                                          │
│        ├── McpAuthHandler        Token → McpAccess → OwnerUserId   │
│        ├── McpSessionHub         Streamable-HTTP-Sessions           │
│        ├── McpToolRegistry       Katalog katalog-v<n>, T01–T33     │
│        ├── McpToolDispatcher     Routing, Parameter-Bindung         │
│        ├── McpScopeEvaluator     Prüfstufen 0–3 (datensatzscharf)  │
│        ├── McpApprovalGate       → AiActionRequest statt Direktweg  │
│        ├── McpAuditWriter        Ereignisprotokoll (async, sicher)  │
│        ├── McpAccessService      Zugangsverwaltung (auch via /api)  │
│        └── AiActionExecutor      persistente Queue-Ausführung       │
│                                                                     ▼
│                                             I*Service-Contracts (Application)
│                                                                     │
│                                              Implementierungen (Infrastructure)
│                                                                     │
│                                              AppDbContext (SQLite) ─┘
│
└── Blazor Server UI ──(IApiClient über /api)──┘
```

### 4.2 Platzierung im Projektaufbau

|| Schicht | Ort | Inhalt |
|---|---|---|---|
| Contracts | `FinanceManager.Application` (neu: `Application/Mcp/`) | `IMcpAccessService`, `IMcpToolRegistry`/`IMcpToolDescriptor`, `IMcpScopeEvaluator`, `IAiActionRequestService`, `IMcpAuditWriter`, DTO-Grundgerüste |
| Domänenmodell | `FinanceManager.Domain` (neu: `Domain/Mcp/`, `Domain/Audit/`) | Entitäten `McpAccess`, `McpAccessScope`, `AiActionRequest`, `McpAuditEntry`(+Detail), MCP-Enums (Datenmodell-Dokument) |
| Implementierungen | `FinanceManager.Infrastructure` (`Infrastructure/Mcp/`) | `McpAccessService` (Token-Generierung/Hashing), `McpScopeEvaluator` (EF-basierte datensatzscharfe Prüfung), `AiActionExecutor` (Queue), `McpAuditWriter`, neuer `IStatementImportQueryService` (offener Baustein aus Toolkatalog T08) |
| Transport/Host | `FinanceManager.Web` (`Web/Mcp/`) | Endpoint-Mapping `/mcp`, `McpAuthHandler`, Session-Handling, Tool-Dispatcher, Rate-Limit-Policy-Partitionen, `Mcp:*`-Optionsbindung |
| Verwaltungs-API | `FinanceManager.Web/Controllers/` | `McpAccessesController`, `AiActionRequestsController`, `McpAuditController` (Skizze im UI-Konzept, Abschnitt 6) |

Begründung der Verteilung: Entitäten/Contracts wandern in
Domain/Application, weil die Verwaltungs-API (Web) und der
MCP-Laufzeitpfad (Web) sowie die Infrastructure-Implementierungen sie
gemeinsam nutzen – die bestehende Trennung bleibt gewahrt; nichts an
der MCP-Logik gehört in `Shared` (Shared ist UI↔API-Vertrag; die
MCP-Tools sind ein Server-interner Vertrag).

### 4.3 Service-Layer-Anbindung (Adapter-Prinzip)

Jeder Tool-Adapter ruft exakt die `I*Service`-Contracts mit
`ownerUserId = McpAccess.OwnerUserId` auf – derselbe Parameterweg
wie die Controller (`_current.UserId` → Service). Die Zuordnung ist
im Toolkatalog je Tool referenziert (`IAccountService`,
`IPostingsQueryService`, `IStatementDraftService`,
`IContactService`, `IBudgetPurposeService`, `ISavingsPlanService`,
`ISecurityService`, `IAttachmentService`, `IReportFavoriteService`,
`IHomeKpiService`, `IPostingReversalService`,
`IMassImportOrchestrator`, `IBackgroundTaskManager` für `book-all`
…).

**Offener Baustein (aus Schritt 2 übernommen):** T08
(`fm_statement_imports_review`) benötigt den neuen
`IStatementImportQueryService` (lesender Contract auf
`StatementImport`/`StatementEntry`). Er wird in Infrastructure neu
konzipiert und wie jeder andere Service mit `ownerUserId`-Signatur
gebaut; bis zu seiner Umsetzung bleibt T08 nicht implementierbar
(verbindliche Kante des Toolkatalogs, hier bestätigt).

**Kein neuer privilegierter Pfad:** Der Adapter-Pfad darf keine
`ownerUserId`-lose Variante, keinen Repository-/DbContext-Zugriff
und keine Service-Methoden außerhalb der deklarierten Liste nutzen.
Damit erbt der MCP-Layer automatisch das bestehende
Besitzmodell (EF-`Where(OwnerUserId == …)`, 404-Semantik) als
Prüfstufe 0.

---

## 5. Sicherheitslayer

### 5.1 Pipeline-Reihenfolge (verbindlich)

Die verbindliche Reihenfolge aus Sicherheitskonzept Abschnitt 9 wird
wie folgt operationalisiert – im selben `WebApplication`-Middleware-
Stack, aber pfadbeschränkt auf `/mcp`:

```text
HTTP-Request /mcp
  ├─ 1. RequestLoggingMiddleware        (bestehend; Diagnose, kein Audit)
  ├─ 2. IpBlockMiddleware               (bestehend; 403 bei IsBlocked)
  ├─ 3. UseRateLimiter / Mcp-Policy     (Partition: Token-Kürzel bzw.
  │      IP beim unauthentifizierten Pfad; Limits je Sicherheitsklasse)
  ├─ 4. Mcp:Enabled-Flag                (Kill-Switch → einheitlich 503/401)
  ├─ 5. McpAuthHandler                  (Bearer fm_mcp_* → Hash-Verify
  │      → Zugangs-Status/Ablauf/IP-Bindung → McpAccess-Principal;
  │      kein JWT, kein Cookie)
  ├─ 6. McpToolDispatcher               (Tool bekannt? Katalogversion?)
  ├─ 7. McpScopeEvaluator               (Prüfstufen 1–3 des
  │      Berechtigungsmodells; Stufe 0 läuft implizit über
  │      ownerUserId im Service-Aufruf)
  ├─ 8. McpApprovalGate                 (Sicherheitsklasse +
  │      Genehmigungsmodus + Mcp:WriteAccess-Deckel →
  │      Direktausführung ODER AiActionRequest anlegen)
  ├─ 9. Tool-Adapter                    (I*Service(ownerUserId,…);
  │      datensatzscharfe Prüfung in der EF-Query = Stufe 0+2)
  └─10. McpAuditWriter                  (Ereignisbündel transaktional
         an die Ausführung gekoppelt, Abschnitt 8)
```

- **Position 3:** `UseRateLimiter` steht im bestehenden Stack bereits
  vor `UseAuthentication`; die MCP-Policy wird als benannte Policy
  (`RateLimitPolicies.Mcp*`) auf die `/mcp`-Endpoint-Gruppe gebunden –
  Partitionierung pro Zugangskürzel bzw. pro IP für den
  unauthentifizierten Pfad (Sicherheitskonzept 7.1), Limits je
  Sicherheitsklasse (7.2) aus `Mcp:RateLimiting:*`.
- **Position 5:** `McpAuthHandler` ist ein eigenes Authentication-
  Schema/Endpoint-Filter der `/mcp`-Gruppe – kein JwtBearer. Er
  extrahiert das Zugangskürzel, lädt den `McpAccess`-Datensatz,
  verifiziert das Secret (`Pbkdf2IdentityPasswordHasher`-Format,
  `FixedTimeEquals`) und prüft Status (`Aktiv`), `ExpiresUtc`,
  `RevokedUtc` und optionale IP-Bindung – **bei jeder Anfrage**
  (serverseitige Sofortprüfung, OnTokenValidated-Prinzip). Ergebnis
  ist ein MCP-Principal: `McpAccessId` + `OwnerUserId`.
- **Token→UserId-Auflösung:** ein `IMcpAccessContext` (analog
  `ICurrentUserService`) liefert `OwnerUserId` des authentifizierten
  Zugangs; Tool-Adapter lesen ausschließlich daraus – nie aus
  Parametern des Clients (kein Client-gesteuertes `ownerUserId`,
  keine Rechteausweitung).
- **Position 7 (Details):** der Evaluator prüft zuerst das
  Tool-Scope-Tupel gegen das effektive Rechteprofil (Stufenprofil
  bzw. `McpAccessScope`-Zeilen), dann datensatzscharf: Filter-
  Ergänzung der EF-Abfragen (Whitelist-CSV-Auswertung,
  `ReportFavorite`-Muster, UND über Dimensionen/ODER über Scopes)
  plus indirekte Besitzauflösung der Ableitungstabelle
  (`Posting` je `Kind`, `StatementEntry` über `AccountId`,
  `AliasName` über `ContactId`, `Attachment`-Zieldecke,
  `AccountShare`-Deckel Read/Write/Admin). Meta-Tools
  (`fm_capabilities_describe`, `fm_task_status`) prüfen nur die
  Besitzbindung (Meta-Scope-Regel).
- **Position 8:** das Gate implementiert die Sicherheitsklassen:
  lesend/vorschlagend → direkt; schreibvorbereitend → direkt oder
  `AiActionRequest` je Genehmigungsmodus; genehmigungspflichtig →
  stets `AiActionRequest` (direkt nur bei pre-approved Scope, mit
  derselben Request-Spur). Der globale Schreib-Stopp
  (`Mcp:WriteAccess:Enabled=false`) ist hier der Stufe-3-Deckel, der
  alle schreibenden Klassen kappt.
- **Fehlerbilder:** 401 (Auth), 429 (Rate Limit), definierter
  Scope-Fehler auf Tool-Ebene (vom Client als Zustand behandelbar),
  404-Semantik auf Datensatzebene, 503/401 bei Kill-Switch –
  `ApiErrorDto`-/`Err_*`-Konvention bzw. das strukturierte
  MCP-Fehlerformat mit denselben Codes (Sicherheitskonzept 3.4).

### 5.2 Sessions und Zustandslosigkeit

Der Server hält **keinen** fachlichen Zustand in der Session: Das
effektive Rechteprofil wird je Toolaufruf neu bewertet
(Laufzeitauflösung, Berechtigungsmodell 3.3). Die Streamable-HTTP-
Session ist nur Transportzustand (Nachrichten-Zuordnung); ihre
Lebensdauer ist an die Circuit-/Keepalive-Mechanik des Hosts
gebunden und sicherheitsneutral – ein neuer Connect erfordert keine
Neuauthentifizierung über den Bearer-Header hinaus.

---

## 6. Tool-Registrierung und Ausführungsmodell

### 6.1 Registry und Katalog

- **`McpToolRegistry`** hält die statische Katalogdefinition
  `katalog-v<n>` (Toolkatalog, Abschnitt 6): je Tool ein
  `IMcpToolDescriptor` mit stabilem Namen (`fm_*`),
  Scope-Deklaration (Datentyp × Rechteart bzw. Meta-Scope),
  Sicherheitsklasse, JSON-Schema der Eingabe (Whitelist-Parameter,
  keine Freitext-Kommandos), Ausgabe-Form und
  Deprecation-Status.
- **Implementierungsform (Vorschlag):** attributbasierte
  Deskriptor-Klassen je Tool (Name, Scopes, Klasse, Adapter-Methode) –
  die Registry ist kompilierter Code, nicht konfigurierbar;
  „kein Tool ohne Scope" ist damit zur Build-Zeit erzwingbar
  (Toolkatalog-Grundsatz 2).
- **Client-Sicht:** Tool-Listing über den
  MCP-Standardmechanismus; Beschreibungstexte führen Zweck,
  Scope-Anforderung, Deprecation. `fm_capabilities_describe` meldet
  die aktive Katalogversion zurück.
- **Filter der Sichtbarkeit:** der Katalog wird vollständig
  ausgeliefert; die Rechteentscheidung bleibt aufrufsbezogen
  (Scope-Fehler als Zustand, Katalog-Versionierungsregel) –
  kein clientabhängiges Listing, das Rückschlüsse auf fremde
  Rechte gäbe.

### 6.2 Ausführungsmodell

|| Weg | Wann | Ablauf |
|---|---|---|---|
| **Direkt** | lesend, vorschlagend, schreibvorbereitend ohne Genehmigung | Adapter-Aufruf synchron im Request; Ergebnis direkt; Audit-Eintrag |
| **Request-Anlage** | genehmigungspflichtig oder Genehmigungsmodus „je Aktion" | `AiActionRequest` anlegen (Pending, `ParameterSnapshot`, `ExpiresUtc`, `CorrelationId`, `IdempotencyKey`) → `INotificationWriter` EventDriven → synchrone Antwort `{ aiActionRequestId, Pending }`; Ergebnis später via `fm_task_status` |
| **Queue-Ausführung** | Approved-Requests | `AiActionExecutor` (Hosted Service nach `BackgroundTaskRunner`-Muster) pollt die `AiActionRequest`-Tabelle = persistente Queue (Approval-Konzept 5.1); atomarer Claim via Status-Update `Approved → (läuft)` in einer SQLite-Transaktion; danach fachliche Guard-Re-Prüfung (`CanReverseAsync`, `AlreadyBooked`, Besitz) + erneute Prüfstufen 0–3 → Adapter-Ausführung → `Executed`/`Failed`; lange Ausführungen (z. B. `book-all`) delegieren an `IBackgroundTaskManager`-Tasks für Fortschritt, der Request bleibt führender Datensatz |
| **Ablauf-Job** | überfällige Pending | derselbe Runner (oder Scheduled-Executor im `MonthlyReminderScheduler`-Muster) markiert `Pending → Expired` bei `ExpiresUtc`-Überschreitung |

**SQLite-Besonderheiten (verbindlich):** Ein Executor-Prozess reicht
(Single-Instance); der Claim erfolgt als bedingtes UPDATE
(`WHERE Status = Approved`) in einer Transaktion – dasselbe
Serialisierungsprinzip wie `StatementDraftBookingGuard`
(`LockToken` + `ExpiresUtc`), dessen Muster für Bereichssperren
(z. B. gleichzeitige Draft-Verbuchung über UI und Queue)
weiterverwendet wird. Kein verteiltes Locking, kein zweiter
Queue-Speicher.

### 6.3 Idempotenz und Wirkungsschutz

- `IdempotencyKey` (aus Zugangs-Id + Client-Schlüssel; Unique-Index):
  Retry eines Toolaufrufs liefert denselben Request zurück statt
  eines Duplikats (Approval-Konzept 5.2).
- Single-Shot-Zustandsübergang: nur `Approved → Executed/Failed`;
  Terminalzustände unveränderlich.
- Fachliche Vorprüfungen bei Ausführung (Datenlage kann sich seit
  Freigabe geändert haben) → `Failed` mit `Err_*`-FailureReason.
- Atomarität der Wirkung: Batches ganz-oder-gar-nicht
  (`ApplyBatchEntryUpdatesAsync`-Semantik); Verbuchung unter
  `StatementDraftBookingGuard`; Storno unter
  `CanReverseAsync`-Vorprüfung in derselben Ausführung.
- Kein automatischer Retry von `Failed` (bewusste Client-Aktion;
  Approval-Konzept 6.1).

---

## 7. Logging- und Audit-Anbindung

### 7.1 Schreibpfad (Festlegung)

Der Audit-Schreibpfad ist **transaktional an die Toolausführung
gekoppelt**: `McpAuditWriter` schreibt das Ereignisbündel
(Toolaufruf-Eintrag + Feld-Delta-Details + evtl.
Approval-Zustandskette) in **derselben `AppDbContext`-
Transaktion** wie die fachliche Wirkung bzw. unmittelbar danach im
selben Request-Scope.

**Begründung der Festlegung (Alternativen geprüft):**

|| Alternative | Entscheidung |
|---|---|---|
| Transaktional gekoppelt (gewählt) | Wirkung und Beleg sind atomar – „kein Audit ohne Wirkung, keine Wirkung ohne Audit". SQLite-Schreibserialisierung macht dies kostengünstig; Bündel bleiben zusammenhängend (Hash-Kette, Lückenlosigkeit, Audit-Konzept 7.3). |
| Nachgelagerter Async-Writer (Queue/Channel) | Verworfen als **alleiniger** Pfad: bei Prozessabbruch ginge der Beleg der letzten Wirkung verloren – Revisionssicherheit gäbe es nur best-effort. Zulässig als *Ergänzung* für hochfrequente lesende Aufrufe ist eine gebündelte Nachverarbeitung, sofern der Beleg vor der Client-Antwort zumindest entgegengenommen ist; die fachliche Festlegung lautet dennoch: schreibende Wirkungen und Sicherheitsereignisse immer synchron. |
| Feuer-und-vergessen (`Task.Run`) | Verworfen – verliert Einträge bei Neustart, bricht Lückenlosigkeit. |

**Fehlerbehandlung:** Schlägt der Audit-Schreibpfad bei einer
schreibenden Wirkung fehl, schlägt die Ausführung fehl
(`Failed`/`Err_Internal`) – eine Wirkung ohne Beleg ist
inakzeptabel (Konsistenz zur Append-only-Regel). Für rein lesende
Aufrufe darf ein Audit-Schreibfehler den Toolaufruf **nicht**
blockieren (Diagnose-Log + Zähler), damit der Analysepfad nicht an
der Protokollschicht hängt; das Fehlen wird über die
Lückenlosigkeits-Prüfung sichtbar.

### 7.2 Weitere Anbindungen

- **Verkettung:** `CorrelationId` wird im Dispatcher erzeugt und
  durch Toolaufruf → `AiActionRequest` → Änderungseinträge
  durchgereicht (Audit-Konzept 6.4).
- **Hash-Kette:** Schreibreihenfolge je Schreiber-Kontext über die
  serialisierte SQLite-Transaktion; `PrevHash`/`EntryHash` und
  fortlaufende `SequenceNo` je Kette (Felder: Datenmodell).
- **Bereinigung:** Retention-Executor (Hintergrundlauf im Executor-
  Muster) entfernt fristüberschreitende Einträge nach den Stufen des
  Audit-Konzepts (12/36 Monate) und protokolliert die Bereinigung
  selbst als Systemereignis; einziger Löschpfad der Anwendung auf
  der Audit-Tabelle.
- **Diagnose-Logging bleibt getrennt:**
  `RequestLoggingMiddleware`/`FileLoggerProvider` unverändert –
  technischer Kanal ohne Token/Nutzlasten.

---

## 8. Persistenzmodell-Übersicht

(Normative Details: `datenmodell-erweiterungen.md`; hier die
architektonische Einordnung.)

- **Neue DbSets im `AppDbContext`:** `McpAccesses`,
  `McpAccessScopes`, `AiActionRequests`, `McpAuditEntries` (+
  Detail-Tabelle) – `DbSet`-Property + Konfigurationsblock in
  `OnModelCreating` (Indizes, Unique-Constraints, DeleteBehavior,
  Enum→short-Konvertierung) + EF-Code-Migration; Anwendung beim
  Start über `ApplyMigrationsAndSeed` (`db.Database.Migrate()`),
  kein `SchemaPatcher`-Bedarf für neue Tabellen.
- **SQLite-Schreibserialisierung:** alle Ausführungs- und Audit-Pfade
  sind einfache serielle Transaktionen; kein Sperrkonzept über den
  `StatementDraftBookingGuard`-Mechanismus hinaus.
- **Backup-Einbindung:** neue Entitäten folgen der
  `*BackupDto`-Konvention (`ToBackupDto`/`AssignBackupDto` in der
  Entität + `IBackupService`-Erweiterung) – Zugänge/Scopes und
  Audit-Bestand reisen mit dem Benutzer-Backup (Zuordnung je
  Entität im Datenmodell-Dokument; Token-Hashes werden **nicht**
  in Backups exportiert – Wiederherstellung erfordert Rotation,
  siehe Datenmodell).
- **Keine Änderung an Bestands-Entitäten:** die indirekte
  Owner-Ableitung (`Posting`, `StatementImport` …) bleibt wie im
  Berechtigungsmodell dokumentiert; kein neues `OwnerUserId` auf
  Fachentitäten.

---

## 9. Fehler- und Notfallschalter

### 9.1 Konfiguration (`appsettings`-Konvention)

Neue Sektion `Mcp` nach dem `BackgroundTasks:Enabled`-Vorbild
(Options-Bindung, reloadbar – Zielbild: ohne Deployment steuerbar,
Sicherheitskonzept 6):

|| Schlüssel | Wirkung | Default |
|---|---|---|---|
| `Mcp:Enabled` | `false` = `/mcp` lehnt alle Anfragen ab (Kill-Switch); Queue-Executor stoppt | `false` (Opt-in: MCP ist im Auslieferungszustand aus – Aktivierung ist bewusste Betreiberentscheidung) |
| `Mcp:WriteAccess:Enabled` | `false` = Klassen schreibvorbereitend + genehmigungspflichtig abgelehnt; Lese-Fallback aktiv | `true` |
| `Mcp:RateLimiting:*` | Limits je Sicherheitsklasse (Sicherheitskonzept 7.2) | Tabelle dort |
| `Mcp:TokenHash:Iterations` | PBKDF2-Iterationen für PAT-Hashing | Vorbildwert |
| `Mcp:Audit:RetentionDays:*` | Aufbewahrungsfristen je Ereignisklasse | 12/36 Monate |
| `Mcp:Approval:DefaultExpiryHours` | Standard-Entscheidungsfrist | 72 |
| `Mcp:Approval:MaxPendingPerAccess` | Pending-Limit je Zugang | 25 |
| `Mcp:CatalogVersion` | aktive `katalog-v<n>` (aus Registry abgeleitet, nicht frei editierbar – Anzeige/Diagnose) | – |

### 9.2 Verhalten der Schalter im System

- **Kill-Switch:** Endpoint-Level-Ablehnung vor jeder Auth-Arbeit
  (frühe, billige Prüfung in der Pipeline-Position 4); Pending-/
  Approved-Requests bleiben persistent und altern per `ExpiresUtc`
  (Approval-Konzept 6.1 – Approved bleibt stehen, Executor stoppt).
- **Schreib-Stopp:** Deckel in Pipeline-Position 8 (Approval-Gate);
  lesende/vorschlagende Tools und Meta-Tools laufen weiter.
- **Zugangs-Deckel:** deaktivierte/widerrufene/abgelaufene Zugänge
  greifen in Position 5 (Auth) – sofort, ohne Restlebensdauer.
- **Anomalie-Reaktionen:** Fehlversuchszähler → `IpBlock`-
  Infrastruktur (Position 2, instanzweit wirksam) bzw.
  Zugangsdeaktivierung; Benachrichtigungen via `INotificationWriter`
  (`SystemAlert` → Admins, `EventDriven` → Besitzer).

### 9.3 Anzeige der Schalter

Der Schalter-Zustand wird in der Verwaltungs-API für die
Admin-Sicherheitsübersicht exposiert (UI-Konzept 5.2); die
Schalter selbst bleiben Konfiguration (kein UI-Umschalter –
Betreiber-Hoheit).

---

## 10. Request-Pipeline-Skizze: ein Toolaufruf

```text
MCP-Client                    FinanceManager.Web /mcp                    Service-Layer        DB
    │   POST /mcp                     │                                        │               │
    │   Authorization: Bearer fm_mcp_*│                                        │               │
    │   tools/call fm_statement_draft_book                                     │               │
    │   {draftId, entryIds?}          │                                        │               │
    ├────────────────────────────────►│                                        │               │
    │                                 │ 1 RequestLogging (Diagnose)            │               │
    │                                 │ 2 IpBlock? → 403                       │               │
    │                                 │ 3 RateLimit (Token-Kürzel) → 429       │               │
    │                                 │ 4 Mcp:Enabled? → 503                   │               │
    │                                 │ 5 McpAuth: Kürzel→Access→Hash-Verify   │               │
    │                                 │    Status/Expires/Revoked/IP → 401     │               │
    │                                 │ 6 Dispatcher: Tool+Katalog ok?         │               │
    │                                 │ 7 ScopeEvaluator: (Kontoauszugs-       │               │
    │                                 │    entwürfe, Buchung) im Profil?       │               │
    │                                 │    + Filter/Share-Deckel → verweigert  │               │
    │                                 │ 8 ApprovalGate: Klasse =               │               │
    │                                 │    genehmigungspflichtig ──────────────┼──► AiActionRequest
    │                                 │    (Pending, Snapshot, Expires,        │    INSERT     │►
    │                                 │     CorrelationId, IdempotencyKey)     │               │
    │                                 │    → INotificationWriter EventDriven ──► Notification  │►
    │◄────────────────────────────────┤    (Eingangskorb des Besitzers)        │               │
    │ { aiActionRequestId, Pending }  │                                        │               │
    │                                 │                                        │               │
    │ [Benutzer] ──/api/ai-action-requests/{id}/approve (JWT-UI-Weg) ─────────►│ Approved      │►
    │                                 │                                        │               │
    │                                 │  AiActionExecutor (Hosted):            │               │
    │                                 │   atomarer Claim Approved→laufend      │──────────────►│
    │                                 │   Re-Prüfung Stufen 0–3 + Guards       │               │
    │                                 │   Adapter→IStatementDraftService       │──────────────►│
    │                                 │     .BookAsync(ownerUserId,…)          │  Posting/     │
    │                                 │   + StatementDraftBookingGuard         │  StatementImport
    │                                 │   + McpAuditWriter (gleiche Tx)        │  + Audit      │
    │                                 │   → Executed + ExecutionResult         │               │
    │   fm_task_status ──────────────►│                                        │               │
    │◄────────────────────────────────┤ { Executed, ExecutionResult }          │               │
```

Verkürzte Variante bei lesenden/vorschlagenden Tools: Position 8
entscheidet „direkt" → Adapter synchron → Audit → Ergebnis in der
Antwort (kein Request, keine Queue).

---

## 11. Deployment- und Betriebssicht

|| Szenario | Festlegung |
|---|---|---|
| **HTTP-only-LAN** | Unterstützter Fallback (Sicherheitskonzept 8.2): PAT im Klartext abhörbar – dokumentiertes Restrisiko; Zusatzmaßnahmen: kurze `ExpiresUtc`-Empfehlung, enge Profile, optionale IP-Bindung je Zugang, Rate Limits, UI-Hinweispflicht. `/mcp` verhält sich wie `/api` bzgl. `AllowedHosts`. |
| **Reverse Proxy / TLS (Zielbetrieb)** | TLS-Terminierung am Proxy; `ReverseProxy:KnownProxies`/`KnownNetworks` **Pflicht** für korrekte `RemoteIpAddress` (Rate-Limit-Partition, IpBlock, Audit-IP-Attribution, IP-Bindung). `SecurityHeadersPolicy`/HSTS greifen; SSE innerhalb von Streamable HTTP ist proxy-freundlich (gewöhnliches HTTP-Streaming; Timeouts des Proxys ggf. erhöhen). |
| **Auto-Update** | `msTools.Updater` aktualisiert den einen Prozess inkl. MCP-Layer und Migrationen; laufende MCP-Sessions brechen mit dem Prozess (Client-Reconnect); offene Requests überleben dank persistenter Queue. |
| **Neustart-Verhalten** | `ApplyMigrationsAndSeed` migriert neue Tabellen; Executor nimmt Approved-Requests wieder auf; `Expired`-Job läuft beim nächsten Intervall nach. |
| **Single-/Multi-User** | Zugänge sind strikt `OwnerUserId`-gebunden; kein instanzweiter Fremdzugriff; Admin sieht nur Sicherheitsmetadaten. |
| **Konfigurationsübersicht** | siehe Abschnitt 9.1; alle `Mcp:*`-Schlüssel sind opt-in mit sicheren Defaults. |

---

## 12. Abgrenzung und Nicht-Ziele (architektonisch)

- Kein eigener fachlicher Datenpfad, kein zweiter DbContext, keine
  zweite Datenbank – Adapter auf `I*Service` nur.
- Kein generischer CRUD-/Query-Endpunkt unter `/mcp`; der
  Toolkatalog ist die einzige Vertragsfläche.
- Keine Bankanbindung – `fm_statement_draft_import` nutzt
  ausschließlich die vorhandenen Datei-Importmechanismen.
- Kein Fremdzugriff: weder Admin-MCP-Zugänge noch ein
  instanzweites „Service-Token" existieren.
- Kein `OwnerUserId` auf `Posting` o. ä. – indirekte Ableitung
  bleibt (Kante aus Schritt 1/Projektplan).

---

## 13. Konsistenzanforderungen an Folgeartefakte

- **Datenmodell (dieser Schritt):** Entitäten und Indizes müssen die
  Pipeline-Anforderungen tragen: schneller Token-Lookup über
  Zugangskürzel (Index), `IdempotencyKey`-Unique, Queue-Claim
  (`OwnerUserId`+`Status`, `ExpiresUtc`-Index), Audit-Kette
  (`PrevHash`, `SequenceNo`).
- **UI-Konzept (dieser Schritt):** Verwaltungs-Endpunkte `/api/mcp-*`
  und `/api/ai-action-requests` sind die UI-Seite desselben
  `IMcpAccessService`/`IAiActionRequestService`-Bestands – dieselbe
  Service-Schicht, JWT-Kanal.
- **Roadmap (Schritt 5):** inkrementelle Ausbaustufen: Phase 1
  (Read-Only) benötigt Registry + Auth + Evaluator + Audit + Zugangs-
  verwaltung; Approval-Gate/Executor/Center erst ab Phase 4 –
  Architektur und Datenmodell sind aber jetzt vollständig geplant,
  damit keine spätere Migration nötig wird.
- **Umsetzung:** Tool-Deklarationen (Name, Scopes, Klasse) aus dem
  Katalog `katalog-v<n>` sind 1:1 zu übernehmen; Abweichungen erfordern
  Katalog-Hauptversion (Versionierungsregeln Schritt 2).
