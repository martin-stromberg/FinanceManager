# Review – Schritt 3: Sicherheits-, Audit- und Approval-Konzept

## Ergebnis

**Status:** Vollständig umgesetzt

## Prüfung gegen die Akzeptanzkriterien

| Kriterium | Prüfergebnis |
|---|---|
| Alle drei Dokumente liegen als deutschsprachige Markdown unter `Docs/planung/mcp-integration/` vor | ✔ `sicherheitskonzept.md` (501 Zeilen), `approval-konzept.md` (424 Zeilen), `audit-konzept.md` (341 Zeilen) |
| Sicherheitskonzept: Authentifizierung (PAT, Hashing, Lebensdauer, Widerruf) | ✔ Abschnitt 3: Format `fm_mcp_<kürzel>_<secret>`, Einmalanzeige, PBKDF2-Einweg-Hash im `pbkdf2\|iters\|salt\|key`-Format des `Pbkdf2IdentityPasswordHasher` (HMACSHA256, FixedTimeEquals, Verify-false-statt-Exception), Abgrenzung zu `dp:v1:`-Reversibilität mit Begründung, `ExpiresUtc`/Rotation mit Sofort-Invalidierung/Widerruf nach `OnTokenValidated`-Prinzip (Statusprüfung inhärent im Auth-Pfad), Bearer-Header-Transport, kein Cookie, keine URL-Übergabe |
| Sicherheitskonzept: Autorisierung (Scopes, Aktionen, Filter, Benutzerrechte) | ✔ Abschnitt 4: operative Durchsetzung der Prüfstufen 0–3 des berechtigungsmodell.md (Tabelle Stufe→Umsetzung), indirekte Owner-Ableitung referenziert, Laufzeitauflösung, datensatzscharfe Prüfung, Monotonie |
| Sicherheitskonzept: Prompt-Injection-Schutz | ✔ Abschnitt 5: vollständiger untrusted-content-Katalog (`Subject`/`RecipientName`, `Contact.Description`, `AliasName.Pattern`, `StatementDraft.Description`, `Attachment.Content` byte[] in SQLite, `OriginalFileName`/Parser-Ausgaben, Rückfluss vom Client); konkrete Maßnahmen: strukturierte Ausgabe, `untrusted_data`-Kennzeichnung, kein Inhaltsecho, Whitelist-Output, typisierte Parameter ohne Freitext-Kommandos, Approval-Snapshot statt Begleittext, Wirkbegrenzung über Rechte/Approval/Audit; ehrliche Grenzen |
| Sicherheitskonzept: Notfallmaßnahmen | ✔ Abschnitt 6: `Mcp:Enabled` (Kill-Switch), `Mcp:WriteAccess:Enabled` (Schreib-Stopp, Lese-Fallback) nach `appsettings`-Konvention (`BackgroundTasks:Enabled`-Vorbild), Queue-Verhalten der Schalter, Anomalieerkennung mit gestuften Reaktionen (`INotificationWriter` SystemAlert→Admins / EventDriven→Besitzer, `IIpBlockService`/`IpBlockMiddleware`, Zugangsdeaktivierung) |
| Sicherheitskonzept: Rate Limiting | ✔ Abschnitt 7: `AddRateLimiter`-Mechanik/`RateLimitPolicies.AuthSensitive` als Vorbild, Partition pro Token/Zugang + IP-Partition für unauthentifizierte Aufrufe, Abstufung je Sicherheitsklasse mit konkreten Vorschlägen, 429/`ApiErrorDto`, Platzierung vor Scope-Prüfung |
| Sicherheitskonzept: HTTP-/LAN-Betrieb als explizites Risiko | ✔ Abschnitt 8: `AllowedHosts:"*"`-Ausgangslage, TLS als Zielbetrieb (Reverse Proxy, `ReverseProxy:KnownProxies\|KnownNetworks`, UseHsts), HTTP-Fallback mit dokumentiertem Restrisiko + Zusatzmaßnahmen (kurze Lebensdauer, enge Profile, optionale IP-Bindung, UI-Hinweispflicht), verworfene Alternativen (TLS-Pflicht, HMAC-Signing, mTLS) – entspricht Annahme 2 „Offene Punkte → Behandlung" |
| Approval-Konzept: `AiActionRequest` mit Zuständen Pending/Approved/Rejected/Executed/Failed | ✔ Abschnitt 3: alle fünf vorgegebenen Zustände + dokumentierter Zusatz `Expired` (begründet: Ablauf ≠ Ablehnung/Fehler, Audit-Trennung), Zustandsdiagramm, verbindliche Übergangstabelle, verbotene Übergänge, Zeitlimit (Vorschlag 72 h, konfigurierbar) |
| Approval-Konzept: Workflow inkl. Fehlerbehandlung | ✔ Abschnitt 4: kompletter Ablauf (Prüfung → Pending + EventDriven-Benachrichtigung → Genehmigungscenter → Queue-Ausführung → `fm_task_status`-Abruf); Abschnitt 6: Fehlerbilder je Phase, `Err_*`-FailureReasons, kein Auto-Retry, Widerrufs-Szenarien |
| Approval-Konzept: Doppelausführungsschutz / Idempotenz | ✔ Abschnitt 5.2: Single-Shot-Zustandsübergang, fachliche Guard-Re-Prüfung bei Ausführung (`CanReverseAsync`, `AlreadyBooked`, Besitz), `StatementDraftBookingGuard`-Konkurrenzschutz, `IdempotencyKey` gegen Retry-Duplikate, atomare Batches |
| Approval-Konzept: persistente Queue begründet | ✔ Abschnitt 5.1: `AiActionRequest`-Tabelle als Queue; In-Memory-`IBackgroundTaskManager` überlebt Neustarts/Auto-Update nicht; Executor nach `BackgroundTaskRunner`-Muster |
| Genehmigungspflichtige Aktionen konsistent zum Toolkatalog | ✔ Abschnitt 2: 1:1 aus Übersichtstabelle Abschnitt 7 des Toolkatalogs – Kern (`fm_statement_draft_book`, `fm_statement_draft_discard`, `fm_posting_reverse`), Teilmengen T22–T29 (Merge/Löschen/Archivieren), Genehmigungsmodus-Abhängigkeit (Sachbearbeiter/Buchhalter/pre-approved), Stufen-Konsistenz |
| Audit-Konzept: Ereignisse, Detailtiefe, Aufbewahrung, Auswertbarkeit | ✔ Abschnitt 3: fünf Ereigniskategorien vollständig (Toolaufrufe, Änderungsdaten Vorher/Nachher als Feld-Delta, Auth-/Zugangsereignisse, Approval-Ketten, Sicherheitsereignisse); Abschnitt 4: Grundgerüst + abgestufte Detailtiefe + Datenschutzabwägung; Abschnitt 5: gestufte Fristen (12/36 Monate, `Mcp:Audit:*`), Bereinigung, Backup; Abschnitt 6: Benutzer-/Admin-Sicht, Export, Verknüpfungskette |
| Audit-Konzept: Abgrenzung zum vorhandenen Logging + WORM/Integrität | ✔ Abschnitt 2: Abgrenzungstabelle `RequestLoggingMiddleware`/`FileLoggerProvider`/`INotificationWriter`/`IpBlock`/In-Memory-Tasks → neuer persistenter Speicher; Abschnitt 7: Append-only, Hash-Verkettung (Vorgänger-Hash) mit ehrlicher Begrenzung auf SQLite-Niveau, Lückenlosigkeit, atomare Bündel |
| Audit-Konzept: Nachvollziehbarkeit von KI-Entscheidungen | ✔ Abschnitt 6.4: Id-Kette ToolCall→AiActionRequest→Änderungen→Ergebnisentitäten, ParameterSnapshot+ReasonText als Absicht, Feld-Deltas als Wirkung, Katalogversion je Aufruf |

## Konsistenz zu Vorgängerdokumenten

- **fachkonzept.md:** Token-Lifecycle 6.3 (Einmalanzeige, Rotation sofort,
  Widerruf Security-Stamp-Prinzip), Genehmigungsmodus 5.1/6.1,
  Nutzungsprotokoll 6.5, Freigabestufen-Logik, „Admin kein
  Datentürsteher" – konsistent übernommen. ✔
- **berechtigungsmodell.md:** Prüfstufen 0–3, Meta-Scope-Ausnahme,
  Filter-Semantik (unclassified nicht erfasst), Deckel-Hierarchie – das
  Sicherheitskonzept implementiert exakt dieses Verfahren. ✔
- **scope-und-toolkatalog.md:** 4 Sicherheitsklassen, Übersichtstabelle als
  Approval-Eingabe, `fm_task_status`/`fm_capabilities_describe` als
  Ergebnisabrufe, `IStatementImportQueryService` unberührt, `Err_*`-Konvention,
  Katalogversion im Protokoll. ✔
- **Code-Anker verifiziert:** `Pbkdf2IdentityPasswordHasher`
  (`FinanceManager.Infrastructure/Auth`, pbkdf2-Format, FixedTimeEquals),
  `DataProtectionAlphaVantageSecretProtector` (`dp:v1:`),
  `RateLimitPolicies.AuthSensitive` (FixedWindow, RemoteIp, 20/10 s),
  `IpBlockMiddleware` (403 bei `IsBlocked`), `RequestLoggingMiddleware`
  (`token`→`[REDACTED]`), `INotificationWriter` (CreateForUser/Admins/Global,
  `NotificationType.SystemAlert`/`EventDriven`), `IBackgroundTaskManager`
  (In-Memory), `OnTokenValidated` (Security-Stamp), `BackgroundTasks:Enabled`,
  `AllowedHosts:"*"`, `ReverseProxy:KnownProxies`. ✔

## Festgelegte Einzelentscheidungen (für Folgeschritte verbindlich)

- PAT-Format `fm_mcp_<zugangskürzel>_<secret>` – Kürzel ermöglicht
  effizientes Hash-Verify ohne Vollscan.
- Zusatz-Zustand `Expired` auf `AiActionRequest` (die fünf Vorgabezustände
  bleiben unverändert); Ablaufjob markiert überfällige Pending-Requests.
- `AiActionRequest`-Tabelle = persistente Queue; kein separater
  Queue-Speicher; Executor nach `BackgroundTaskRunner`-Muster.
- Vorab-genehmigte Ausführungen erzeugen dieselbe `AiActionRequest`-Spur
  („Approved (vorab)") – kein Pfad um die Genehmigungsinstanz herum.
- Neuer persistenter Audit-Speicher mit Hash-Verkettung
  (Vorgänger-Hash) als Integritätsanker; realistische
  Revisionssicherheit auf SQLite begründet dokumentiert.
- Gestufte Audit-Detailtiefe: lesende Aufrufe ohne Nutzlast-Dumps,
  Binärinhalte nur als `Sha256`-Referenz, niemals Token-Material.
- `Mcp:*`-Konfigurationssektion: `Enabled`, `WriteAccess:Enabled`,
  `RateLimiting:*`, `TokenHash:Iterations`, `Audit:*`.

## Offene Punkte

- Keine blockierenden. Bewusst an Folgeschritte delegiert:
  Entitäts-Feldlisten/Indizes (`AiActionRequest`, `McpAccess`,
  Audit-Eintrag) und `Mcp:*`-Optionsklassen (Schritt 4,
  `datenmodell-erweiterungen.md`), Queue-Claim-Atomarität unter SQLite
  (Schritt 4, `architekturkonzept.md`), Genehmigungscenter-/Audit-UI
  Details (Schritt 4, `ui-konzept.md`).
