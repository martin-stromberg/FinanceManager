# Abnahmeprüfung – Entwicklungsschritt 3

## Ergebnis

**Status:** Abweichungen gefunden

## Abweichungen

- [ ] Meta-Tool `fm_task_status` liefert laut `scope-und-toolkatalog.md`
  (T33, Zeile 295) als `AiActionRequest`-Zustände
  „Pending/Approved/Rejected/Executed/Failed" – ohne `Expired`. Das
  Approval-Konzept (`approval-konzept.md`, Zeilen 150–179) führt `Expired`
  als dokumentierten sechsten Zustand ein; die Zustandsaufzählung des
  Meta-Tools im Vorgängerartefakt ist damit nicht mehr vollständig.
  **Schweregrad: gering** – fachlich unproblematisch (Zusatz ist durch
  „mindestens"-Formulierung der Anforderung und die
  „bedingt-kompatibel"-Regel „neuer Enum-Wert in Ausgabefeld" des
  Toolkatalogs, Abschnitt 6.2, abgedeckt; der Toolkatalog verweist für die
  `AiActionRequest`-Definition selbst auf Schritt 3), aber ein Implementer
  von `fm_task_status` nach Katalogtext würde `Expired` nicht ausliefern.
  Empfehlung: Aufzählung bei nächster Katalog-Revision synchronisieren
  bzw. im Approval-Konzept explizit als Erweiterung der T33-Ausgabe
  kennzeichnen.

## Hinweise

**Artefakte und Diff**

- `git diff` Basis→HEAD: 8 neue Dateien, 1610 Zeilen – die drei
  Lieferdokumente `sicherheitskonzept.md` (501 Z.),
  `approval-konzept.md` (424 Z.), `audit-konzept.md` (341 Z.) unter
  `Docs/planung/mcp-integration/` plus Projektartefakte des Schritts unter
  `Docs/features/task/…-schritt-3-sicherheit/` (plan/requirement/review/
  test-results/todo). Alle drei Konzepte deutschsprachiges Markdown.
  Akzeptanzkriterium 1 erfüllt.

**F8 – Sicherheitskonzept: vollständig abgedeckt**

- Authentifizierung (§3): PAT-Format `fm_mcp_<kürzel>_<secret>` (≥160 Bit,
  `RandomNumberGenerator`), Einmalanzeige, Einweg-Hashing nach
  `Pbkdf2IdentityPasswordHasher`-Vorbild, Abgrenzung zum reversiblen
  `dp:v1:`-Protector, `ExpiresUtc`, Rotation/Widerruf mit sofortiger
  serverseitiger Invalidierung (`OnTokenValidated`/Security-Stamp-
  Analogie), Header-Transport, Fehlerspektrum (401/404/429).
- Autorisierung (§4): Durchsetzung der Prüfstufen 0–3 identisch zu
  `berechtigungsmodell.md` (Stufen, Reihenfolge, Monotonie, UND/ODER-
  Filtersemantik, `CategoryId == null`-Regel = BM Zeilen 314–316);
  indirekte Owner-Ableitung deckt sich mit Fachkonzept 1.3 und BM
  Abschnitt 6 (inkl. `Attachment`-Zieldecke); Unterordnung unter
  Benutzerrecht/`AccountShare` explizit.
- Prompt-Injection (§5): alle geforderten untrusted-content-Kategorien
  genannt (Buchungstexte `Subject`/`RecipientName`/`BookingDescription`,
  `Contact.*`, `AliasName.Pattern`, Notizen, `Attachment.Content` als
  byte[] in SQLite, importierte Dokumente/`OriginalFileName`, Rückfluss);
  drei Maßnahmenebenen (Kennzeichnung/Isolation, Output-Policy/
  schreibende Restriktionen, Rechtekappen + Approval + Audit).
- Notfallmaßnahmen (§6): `Mcp:Enabled` (Kill-Switch),
  `Mcp:WriteAccess:Enabled` (Schreib-Stopp mit Lese-Fallback),
  Anomalieerkennung mit `IpBlock`-Anknüpfung und
  `INotificationWriter` (`SystemAlert`/`EventDriven`) – Feature-Flag-
  Konvention korrekt benannt.
- Rate Limiting (§7): Partition pro Token statt pro IP
  (`AuthSensitive`-Vorbild korrekt beschrieben), Abstufung je
  Sicherheitsklasse inkl. Pending-Kontingent.
- HTTP-/LAN-Betrieb (§8): als dokumentiertes Restrisiko mit TLS-
  Empfehlung, Forwarded-Headers-Pflicht und Zusatzmaßnahmen behandelt –
  Akzeptanzkriterium 2 erfüllt.

**F9 – Audit-Konzept: vollständig**

- Ereigniskatalog (§3) deckt alle geforderten Ereignisse ab: Toolaufrufe
  (wer/wann/welches Tool/Parameter/Status), durchgeführte **und**
  abgelehnte Änderungen (Feld-Deltas), Genehmigungsentscheidungen,
  Sicherheitsereignisse (Token-Lifecycle, Schalter, Anomalien).
- Detailtiefe mit expliziter Datenschutzabwägung (§4.2-Tabelle),
  Aufbewahrung abgestuft (§5), Auswertbarkeit Benutzer/Admin/Export (§6),
  Nachvollziehbarkeit via CorrelationId-Kette Toolaufruf ↔
  `AiActionRequest` ↔ Änderung ↔ Ergebnis-Ids (§6.4).
- Abgrenzung zum Bestands-Logging (§2) trifft zu:
  `RequestLoggingMiddleware`/`FileLoggerProvider`/`IpBlock`/
  `IBackgroundTaskManager` sind real und richtig charakterisiert; neuer
  persistenter Speicher ist gefordert und begründet.
  Akzeptanzkriterium 3 erfüllt.

**F10 – Approval-Konzept: vollständig**

- `AiActionRequest` mit allen fünf geforderten Zuständen +
  Zustandsdiagramm, erlaubten/verbotenen Übergängen, `ExpiresUtc`-
  Zeitlimit (72-h-Vorschlag) und Begründung für Zusatz-Zustand `Expired`
  (§3.2/3.3).
- Workflow: Anforderung → `EventDriven`-Benachrichtigung →
  Genehmigungscenter → persistente Queue → Ergebnis via `fm_task_status`;
  Fehlerbehandlung inkl. Widerruf während Pending, Timeout, Kill-Switch-
  Verhalten, `Err_*`-Klassifikation (§4/§6).
- Doppelausführungsschutz: Single-Shot-Übergänge, `IdempotencyKey`,
  atomare Batches, `StatementDraftBookingGuard`, Guard-Neu-Prüfung bei
  Ausführung (§5.2). Persistente Queue statt In-Memory-
  `IBackgroundTaskManager` korrekt begründet (§5.1).
- Genehmigungspflichtige Aktionen (§2): 3 Kern-Tools +
  Lösch-/Archiv-/Merge-Teilmengen T22–T29 sind **exakt konsistent** mit
  der Übersichtstabelle (`scope-und-toolkatalog.md` Abschnitt 7) und den
  Mindeststufen (§5.3) – Akzeptanzkriterien 4 und 5 erfüllt.

**Konsistenz zu Vorgängern**

- Scope-Nomenklatur (Datentyp × Rechteart), Datentypnamen
  („Kontoauszugsentwürfe" etc.) und Rechtearten stimmen überein;
  „Buchung nur auf Entwürfen" (kein Posting-Schreibpfad) und
  „Storno statt Löschen" (`fm_posting_reverse`, Gegenbuchung) werden
  respektiert; Genehmigungsmodus „dauerhaft vorab genehmigt" folgt
  Fachkonzept 5.1/6.1; Prüfstufen 0–3 und Genehmigungsinstanz als
  Stufe-3-Deckel folgen `berechtigungsmodell.md`.
- `katalog-v<n>`-Versionierung korrekt übernommen (Audit §3.1).

**Code-Stichproben (alle referenzierten Bausteine verifiziert)**

- `Pbkdf2IdentityPasswordHasher.cs` (`Infrastructure/Auth/`): Format
  `pbkdf2|<iter>|<salt>|<key>`, 100.000/16/32, `FixedTimeEquals`,
  `Verify` liefert `false` bei malformeden Hashes – Konzept exakt.
- `RateLimitPolicies.AuthSensitive` („auth-sensitive"),
  `AddRateLimiter` Fixed-Window per `RemoteIpAddress`,
  `RateLimiting:AuthSensitive:PermitLimit/WindowSeconds`, 429,
  `[EnableRateLimiting]` auf Auth-/UsersController – alle korrekt.
- `IpBlockMiddleware` (403), `IpBlock`-Entity (`IsBlocked`,
  `BlockReason`, `UnknownUserFailedAttempts`), `IIpBlockService` –
  vorhanden.
- `INotificationWriter` mit `CreateForUserAsync`/`CreateForAdminsAsync`;
  `NotificationType.SystemAlert`/`EventDriven`; `NotificationTarget`
  HomePage/Dashboard/Modal – alle vorhanden.
- `IBackgroundTaskManager` ist In-Memory (Singleton,
  `ConcurrentDictionary`, `Enqueue(type, userId, payload,
  allowDuplicate)`); `BackgroundTaskRunner`/`IBackgroundTaskExecutor`/
  `BookingTaskExecutor`/`BackgroundTaskType.BookAllDrafts` vorhanden.
- `StatementDraftBookingGuard` (LockToken + ExpiresUtc),
  `IPostingReversalService` (`CanReverseAsync`/`ReversePostingAsync`/
  `GetRelatedPostingsAsync`), `IStatementDraftService`
  (`BookAsync`/`CommitAsync`/`CancelAsync`/`DeleteAllAsync`/
  `ClassifyAsync`/`ApplyBatchEntryUpdatesAsync`, `forceWarnings`,
  `BookingResult`), `Posting.ReversalForPostingId`/
  `ReversedByPostingId`, `IBudgetImpactEvaluationService` – vorhanden.
- `Attachment.Content` (`byte[]?`, plus `Sha256`/`SizeBytes`/`FileName`),
  `Contact.Description`, `AliasName.Pattern`, `StatementDraftEntry.
  Subject/RecipientName/BookingDescription`, `StatementDraft.
  OriginalFileName/DetectedAccountId`, `ReportFavorite.PostingKindsCsv`
  (CSV-Muster) – vorhanden.
- Konfiguration: `AllowedHosts: "*"` mit LAN-Kommentar,
  `ReverseProxy:KnownProxies/KnownNetworks`,
  `Kestrel:Limits:MaxRequestBodySize`, `BackgroundTasks:Enabled`,
  `Workers:SecurityPriceWorker:Enabled`, `Auth:AllowSelfRegistration`,
  `FileLogging:Enabled` inkl. `RetainedFileCountLimit` – vorhanden;
  `UseHsts`/`UseHttpsRedirection` außerhalb Dev/E2E aktiv – wie
  beschrieben.
- `AiActionRequest` korrekt **nicht** im Code (leere Grep-Menge;
  Inventory Abschnitt 5 bestätigt); `Err_*`-Konvention,
  `ApiErrorDto`, `IConfirmationService`, `SecurityHeadersPolicy`,
  `msTools.Updater`, `*BackupDto`-Konvention (`AttachmentBackupDto`,
  `ContactBackupDto`), `Entity`-Basisklasse – alle vorhanden.

**Weitere Beobachtung (kein Kriterienverstoß):** Die Rate-Limit-Tabelle
(Sicherheitskonzept §7.2) führt „Meta" als eigene Limit-Zeile, obwohl die
Meta-Tools im Toolkatalog der Klasse „lesend" zugeordnet sind – als
Verfeinerung innerhalb „lesend" konsistent, nur formal keine fünfte
Klasse.
