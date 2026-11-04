# Plan – Schritt 3: Sicherheits-, Audit- und Approval-Konzept

## Lieferdokumente (alle unter `Docs/planung/mcp-integration/`)

### `sicherheitskonzept.md`

Gliederung:

1. Verbindliche Grundsätze (eine Vertrauensgrenze, nur einschränken,
   serverseitige Sofortprüfung, kein Informationsleck, Defense in Depth,
   Wiederverwendung bestehender Mechanik)
2. Bedrohungsmodell B1–B7 (Token-Diebstahl, Rechteüberschreitung,
   Prompt-Injection, unkontrollierte Schreibzugriffe, Brute-Force/Missbrauch,
   DoS, Fremdzugriff)
3. Authentifizierung: PAT-Format `fm_mcp_<kürzel>_<secret>`,
   Einmalanzeige, Einweg-Hash `pbkdf2|iters|salt|key` nach
   `Pbkdf2IdentityPasswordHasher`-Vorbild (FixedTimeEquals),
   Abgrenzung zu `dp:v1:`-Reversibilität, Lebensdauer/Rotation/Widerruf mit
   Sofortwirkung (OnTokenValidated-Prinzip), Transport nur
   Authorization-Header (nie URL), Trennung vom Identity-JWT,
   Token→OwnerUserId-Auflösung als ICurrentUserService-Äquivalent
4. Autorisierung: operative Durchsetzung der Prüfstufen 0–3 aus
   berechtigungsmodell.md (Benutzerrecht, Scope, Filter, Deckel),
   Laufzeitauflösung, datensatzscharfe Prüfung, Monotonie
5. Prompt-Injection-Schutz: untrusted-content-Katalog (Buchungstexte,
   Kontakt-/Notiz-Freitexte, `Attachment.Content` byte[] in DB, importierte
   Dokumente, Rückfluss vom Client), Maßnahmen (Kennzeichnung/
   Inhaltstrennung, Output-Policy/Whitelist, typisierte Parameter,
   Approval-Snapshot statt Prosa, strukturelle Wirkbegrenzung über Rechte/
   Approval/Audit), ehrliche Grenzen LLM-seitiger Injektion
6. Notfallmaßnahmen: `Mcp:Enabled` (Kill-Switch), `Mcp:WriteAccess:Enabled`
   (Schreib-Stopp mit Lese-Fallback), beides nach
   `appsettings`-Feature-Flag-Konvention (`BackgroundTasks:Enabled`-Vorbild);
   Anomalieerkennung (Fehlversuchsserien, Scope-Verweigerungs-Muster,
   Rate-Limit-Muster) mit gestuften Reaktionen (`INotificationWriter`
   SystemAlert/EventDriven, `IIpBlockService`/IpBlockMiddleware,
   Zugangsdeaktivierung)
7. Rate Limiting: `AddRateLimiter`-Mechanik übernommen, Partition pro
   Token/Zugang statt pro IP + IP-Partition für unauthentifizierten Pfad;
   Abstufung je Sicherheitsklasse (lesend/vorschlagend/schreibvorbereitend/
   genehmigungspflichtig/Meta), 429-Verhalten, Platzierung vor Scope-Prüfung
8. HTTP-only-LAN: TLS als Zielbetrieb (Reverse Proxy,
   Forwarded-Headers `ReverseProxy:KnownProxies|KnownNetworks`),
   HTTP-Fallback mit dokumentiertem Restrisiko und Zusatzmaßnahmen
   (kurze Ablaufdaten, enge Profile, optionale IP-Bindung, UI-Hinweis);
   verworfene Alternativen (TLS-Pflicht, Request-Signing, mTLS)
9. Konsistenzanforderungen an Schritt 4

### `approval-konzept.md`

Gliederung:

1. Zielbild und Grundsätze (Benutzer entscheidet über Wirkung, Snapshot-
   Treue, Persistenz statt Speicher, vorab-genehmigt = dokumentierte
   Genehmigung, kein neuer fachlicher Pfad)
2. Katalog genehmigungspflichtiger Aktionen: immer genehmigungspflichtig
   (`fm_statement_draft_book`, `fm_statement_draft_discard`,
   `fm_posting_reverse`), genehmigungspflichtige Teilmengen der Pflege-Tools
   T22–T29 (Merge/Löschen/Archivieren), bedingt genehmigungspflichtig nach
   Genehmigungsmodus (Sachbearbeiter je Batch, Buchhalter direkt),
   pre-approved-Scope-Behandlung, Freigabestufen-Konsistenz
3. `AiActionRequest`: fachliche Felder (Ids, Zugang, Tool+Katalogversion,
   ParameterSnapshot, ReasonText untrusted, Status, Zeitstempel,
   Entscheider, Ergebnisreferenz, CorrelationId, IdempotencyKey);
   Zustandsmodell Pending/Approved/Rejected/Executed/Failed + dokumentierter
   Zusatz `Expired` mit Begründung; Zustandsdiagramm; verbindliche
   Übergangstabelle inkl. verbotener Übergänge; Zeitlimit (Vorschlag 72 h)
4. Fachlicher Ablauf: Anforderung → Prüfstufen → Pending +
   `INotificationWriter` (EventDriven) → synchrone `AiActionRequestId` →
   Genehmigungscenter → persistente Queue → Executed/Failed →
   `fm_task_status`-Abruf + Ergebnisbenachrichtigung
5. Ausführung nach Freigabe: `AiActionRequest`-Tabelle als persistente
   Queue (Begründung: In-Memory-`IBackgroundTaskManager` verliert
   Genehmigungen bei Neustart/Auto-Update; Executor nach
   `BackgroundTaskRunner`-Muster); Idempotenz/Doppelausführungsschutz
   (Single-Shot-Zustand, fachliche Guard-Re-Prüfung,
   `StatementDraftBookingGuard`, IdempotencyKey, atomare Batches)
6. Fehlerbehandlung: Fehlerbilder je Phase, `Err_*`-klassifizierte
   FailureReasons, keine Auto-Retries, Widerrufs-Szenarien (Zugang weg
   während Pending/Approved → systemseitig Rejected; kein Client-Widerruf)
7. Genehmigungscenter-UX (fachlich): Eingangskorb, Wirkungsvorschau je
   Aktionstyp, Freigeben/Ablehnen mit `IConfirmationService`, Batch-Bedienung,
   Historie, Benachrichtigungsregeln
8. Konsistenzanforderungen an Folgeschritte

### `audit-konzept.md`

Gliederung:

1. Zielbild und Grundsätze (Append-only, Zurechenbarkeit ohne
   Token-Material, Verknüpfung statt Volltext-Duplikation, realistische
   Revisionssicherheit, Protokoll selbst schützenswert)
2. Abgrenzungstabelle zum Ist-Logging (`RequestLoggingMiddleware`,
   `FileLoggerProvider`, `INotificationWriter`, `IpBlock`,
   `IBackgroundTaskManager`) → neuer persistenter Audit-Speicher
3. Ereigniskatalog in fünf Kategorien: Toolaufrufe (inkl. lesende),
   Änderungsdaten (Feld-Delta Vorher/Nachher), Authentifizierungs-/
   Zugangsereignisse (inkl. Scope-/Filter-Änderungen durch den Besitzer),
   Approval-Entscheidungen (vollständige Zustandskette inkl. pre-approved),
   Sicherheitsereignisse (Schalter, IP-Sperren, Anomalien, aggregierte
   Rate-Limits)
4. Detailtiefe: Grundgerüst je Eintrag + abgestufte Tiefe (lesend ohne
   Nutzlast, schreibend voller Snapshot, Binärinhalte nur Metadaten+Sha256,
   Freitext-Längenlimit, niemals Token), Datenschutzfestlegung
5. Aufbewahrung: gestufte Fristen (12 Monate lesend, 36 Monate schreibend/
   Sicherheit), konfigurierbar `Mcp:Audit:*`, Bereinigungsjob als einziger
   Löschpfad, Protokoll überlebt Zugangslöschung, `*BackupDto`-Einbindung
6. Auswertbarkeit: Benutzer-Audit-Ansicht (Timeline, Filter, Drilling,
   Nutzungsübersicht), Admin-Sicherheitssicht (Metadaten, keine
   Fremdinhalte), Export JSON/CSV, Verknüpfungskette ToolCall→
   AiActionRequest→Änderungen→Ergebnisentitäten, Katalogversion im Eintrag
7. WORM-/Integritätsaspekte: realistische Zielsetzung auf SQLite,
   Hash-Verkettung (Vorgänger-Hash) als Manipulationserkennung inkl.
   ehrlicher Begrenzung, fortlaufende Eintragsnummern/Lückenlosigkeit,
   atomare Ereignisbündel
8. Konsistenzanforderungen an Folgeschritte

## Vorgehen

1. Eingaben lesen: project-plan.md Schritt 3, requirement.md F8–F10,
   inventory.md + inventory/entitaeten.md
2. Vorgängerdokumente lesen und Kanten übernehmen: fachkonzept.md,
   berechtigungsmodell.md, scope-und-toolkatalog.md (4 Sicherheitsklassen,
   Übersichtstabelle, Meta-Scope)
3. Code-Anker verifizieren: `Pbkdf2IdentityPasswordHasher`,
   `IAlphaVantageSecretProtector` (`dp:v1:`), `IpBlockMiddleware`/
   `IIpBlockService`, `RateLimitPolicies.AuthSensitive`,
   `RequestLoggingMiddleware` (token-Redaction), `INotificationWriter`
   (SystemAlert/EventDriven), `IBackgroundTaskManager`,
   `StatementDraftBookingGuard`, JWT/`OnTokenValidated`, appsettings-Flags
4. Drei Dokumente schreiben (deutsch, Markdown)
5. Lifecycle-Artefakte anlegen; Selbstprüfung gegen Akzeptanzkriterien;
   Commit ohne Push
