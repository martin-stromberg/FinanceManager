# Datenmodell-Erweiterungen – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 7 der Anforderung; legt die neuen
Persistenzstrukturen für die MCP-Integration auf Ebene der
EF-Core-/SQLite-Konventionen des Repos fest. Fachliche Feldfestlegungen
stammen aus [fachkonzept.md](fachkonzept.md) (MCP-Zugang, Abschnitt 6),
[berechtigungsmodell.md](berechtigungsmodell.md) (Scope-Tupel, Filter-
CSV-Muster), [approval-konzept.md](approval-konzept.md)
(`AiActionRequest`-Felder, Abschnitt 3), [audit-konzept.md](audit-konzept.md)
(Grundgerüst + Detailtiefe, Abschnitt 4) und
[sicherheitskonzept.md](sicherheitskonzept.md) (Token-Hash-Format,
IP-Bindung). Architektonische Einordnung:
[architekturkonzept.md](architekturkonzept.md).

---

## 1. Verbindliche Persistenz-Konventionen (Ist-Stand)

Alle neuen Entitäten folgen ohne Ausnahme den bestehenden Regeln:

|| Konvention | Quelle |
|---|---|---|
| Basisklasse `Entity` (`Guid Id` = `Guid.NewGuid()`, `CreatedUtc`, `ModifiedUtc`, `Touch()`); `IAggregateRoot` für Aggregate | `Domain/Entity.cs` |
| `OwnerUserId`-Bezug für benutzerbezogene Entitäten (Besitzprüfung im Service/EF-`Where`, **kein** Global Query Filter) | Repo-Konvention, inventory.md 1.3 |
| Registrierung: `DbSet`-Property + Konfigurationsblock in `OnModelCreating` (Indizes, Unique-Constraints, `DeleteBehavior`, Precisions) + EF-Code-Migration; Anwendung beim Start via `ApplyMigrationsAndSeed` (`db.Database.Migrate()`) | `Infrastructure/AppDbContext.cs`, `ProgramExtensions.ApplyMigrationsAndSeed` |
| Enums als `HasConversion<short>()` (bzw. `<int>` wo im Bestand so gehandhabt) | AppDbContext, z. B. `ImportSplitMode`, `AttachmentEntityKind` |
| `*BackupDto`-Record in der Entität + `ToBackupDto`/`AssignBackupDto` + `IBackupService`-Erweiterung für Backup-relevante Entitäten | z. B. `AccountBackupDto`, `ReportFavoriteBackupDto` |
| CSV-Id-Listen nach `ReportFavorite`-Muster (`AccountIdsCsv`, `*CategoryIdsCsv`) für Filter | `Domain/Reports/ReportFavorite.cs` |
| Keine Änderung bestehender Fachentitäten (insb. **kein** `OwnerUserId` auf `Posting` u. a. – indirekte Ableitung bleibt) | Projektplan, berechtigungsmodell.md 9 |
| `Err_*`-Fehlercodes, UTC-Zeitstempel (`*Utc`-Suffix) | API-/Domänenkonvention |

**Neuer Ordner:** `FinanceManager.Domain/Mcp/` (Entitäten + Enums)
bzw. `FinanceManager.Domain/Audit/` für den Audit-Bestand –
analog der bereichsweisen Ordnerkonvention der Domain.

---

## 2. Übersicht der neuen Entitäten und Enums

### 2.1 Entitäten

|| Entität | Zweck | Owner-Bezug | Neuer DbSet |
|---|---|---|---|---|
| `McpAccess` | MCP-Zugang: Token-Hash, Freigabestufe, Genehmigungsmodus, Filter-/Sicherheitseinstellungen, Lebenszyklus | `OwnerUserId` (Besitzer) | `McpAccesses` |
| `McpAccessScope` | Ein Scope-Tupel (Datentyp × Rechteart × Filter) des Rechteprofils eines Zugangs | über `McpAccessId` | `McpAccessScopes` |
| `AiActionRequest` | Genehmigungsanfrage + persistente Ausführungsqueue | `OwnerUserId`; `McpAccessId` (Soft-Referenz) | `AiActionRequests` |
| `McpAuditEntry` | Revisionssicherer Ereigniseintrag (5 Kategorien, Hash-Kette) | `OwnerUserId` (nullable für globale Ereignisse); `McpAccessId` (Soft-Referenz) | `McpAuditEntries` |
| `McpAuditEntryChange` | Feld-Delta (Vorher/Nachher) je geändertem Datensatz eines Eintrags | über `EntryId` | `McpAuditEntryChanges` |

**Bewusst nicht als Tabelle:** Globale Sicherheitsschalter
(`Mcp:Enabled`, `Mcp:WriteAccess:Enabled`, `Mcp:RateLimiting:*`,
`Mcp:Audit:*`, `Mcp:Approval:*`, `Mcp:TokenHash:Iterations`) bleiben
`appsettings`-Konfiguration (Sicherheitskonzept 6;
Feature-Flag-Vorbild `BackgroundTasks:Enabled`) – kein DB-Settings-
Modell nötig. Nutzungsstatistiken (Aufrufzahlen, Fehlschlagquoten)
werden aus `McpAuditEntries` aggregiert; `McpAccess` führt nur
`LastUsedUtc` als direktes Feld (Fachkonzept 6.5).

### 2.2 Enums (neu, `Domain/Mcp/`)

|| Enum | Werte | Persistenz |
|---|---|---|---|
| `McpAccessLevel` | `Analyse=0, Assistent=1, Sachbearbeiter=2, Buchhalter=3, Individuell=4` | `short` |
| `McpDataType` | die 19 Datentypen der F3-Matrix (`Accounts=0, AccountTypes=1, StatementImports=2, StatementDrafts=3, Postings=4, Contacts=5, ContactCategories=6, SavingsPlans=7, SavingsPlanCategories=8, Securities=9, SecurityCategories=10, Budgets=11, BudgetCategories=12, BudgetRules=13, Reports=14, KpiConfigurations=15, Attachments=16, AttachmentCategories=17, Administration=18` — `Administration` ergänzt mit den Admin-Tools T34–T38; installationsbezogen, kein Fachdatentyp, zusätzlich Admin-Gate auf der Besitzerrolle) | `short` |
| `McpRightKind` | `Read=0, Suggest=1, Complete=2, Modify=3, Create=4, Book=5, Delete=6` | `short` |
| `McpApprovalMode` | `PerAction=0` (je Aktion/Batch), `PreApproved=1` (dauerhaft vorab genehmigt) | `short` |
| `McpAccessStatus` | `Active=0, Deactivated=1, Revoked=2` (Ablauf ist berechnet aus `ExpiresUtc`, kein Status) | `short` |
| `AiActionRequestStatus` | `Pending=0, Approved=1, Rejected=2, Executed=3, Failed=4, Expired=5` | `short` |
| `McpAuditCategory` | `ToolCall=0, DataChange=1, AccessEvent=2, ApprovalEvent=3, SecurityEvent=4` | `short` |
| `AiActionRequestResult`-Semantik | keine eigene Entität – Ergebnis als JSON-Referenz im Request | – |

`McpDataType`/`McpRightKind` sind die Persistenzform der fachlichen
Begriffe der Schritte 1–2; die Abbildung Datentyp→Entität bleibt
dokumentiert (Fachkonzept 7.1), nicht redundant in der DB.

---

## 3. `McpAccess` – MCP-Zugang

Trägt die fachliche Spezifikation aus Fachkonzept 6.1 plus die
Sicherheitsfelder aus dem Sicherheitskonzept.

| Feld | Typ | Bedeutung / Regel |
|---|---|---|
| `Id` | `Guid` | `Entity`-Konvention |
| `OwnerUserId` | `Guid` (required) | Besitzer; alle Sichtbarkeit hängt daran |
| `Name` | `string` (required, max ~100) | Bezeichnung; **unique je (OwnerUserId, Name)** – wie Kategorie-Konvention |
| `Description` | `string?` | freie Notiz des Benutzers (Zweck/Client) |
| `AccessKeyHint` | `string` (required, ~8–12 Zeichen) | nicht-geheimes Zugangskürzel aus dem Token `fm_mcp_<kürzel>_<secret>` – Lookup-Key des Auth-Pfads; **unique global** |
| `TokenHash` | `string` (required) | Einweg-Hash im Format `pbkdf2|<iters>|<salt-b64>|<key-b64>` (`Pbkdf2IdentityPasswordHasher`-Vorbild); **niemals Klartext, niemals reversibel** – `dp:v1:`-Mechanismus explizit unzulässig |
| `Level` | `McpAccessLevel` (required) | Freigabestufe |
| `ApprovalMode` | `McpApprovalMode` (required) | Genehmigungsmodus (`PerAction` Standard; `PreApproved` dokumentiert vorab genehmigt) |
| `Status` | `McpAccessStatus` (required, default `Active`) | sofort wirksamer Zustand |
| `ExpiresUtc` | `DateTime?` | optionales Ablaufdatum; abgelaufen = verhält sich wie deaktiviert (berechnet, kein Status-Update nötig) |
| `AllowedIpRanges` | `string?` | optionale IP-Bindung (kommagetrennte CIDR-/Adressliste; Sicherheitskonzept 8.2 – HTTP-Zusatzmaßnahme) |
| `LastUsedUtc` | `DateTime?` | letzte erfolgreiche Authentifizierung (Update bei jeder Verwendung) |
| `TokenIssuedUtc` | `DateTime` | Ausstellungszeitpunkt des aktuellen Token (Rotations-/Altersinfo) |
| `RevokedUtc` / `RevocationReason` | `DateTime?` / `string?` | Widerruf-Zeitpunkt und fachlicher Grund (systemseitig: „Zugang deaktiviert durch Anomalie", benutzerseitig: Widerruf/Löschung) |
| `CreatedUtc`/`ModifiedUtc` | Basis | `Entity`-Konvention |

**EF-Konfiguration:**

- `HasIndex(x => x.AccessKeyHint).IsUnique()` – O(1)-Lookup des
  Authentifizierungspfads (Kürzel → Datensatz → Hash-Verify; kein
  Vollscan).
- `HasIndex(x => new { x.OwnerUserId, x.Name }).IsUnique()` –
  Namenskonvention wie bei Kategorien.
- `HasIndex(x => new { x.OwnerUserId, x.Status })` –
  Zugangsliste des Benutzers.
- `HasIndex(x => x.ExpiresUtc)` – Ablauf-Auswertung/
  Benachrichtigungs-Job.
- `DeleteBehavior`: Zugänge werden fachlich **widerrufen statt
  physisch gelöscht** (Fachkonzept: „Widerruf plus Entfernung der
  Konfiguration"; Referenzstabilität für `AiActionRequest`/Audit).
  Festlegung: `Status=Revoked` + `RevokedUtc` ist der Widerruf; ein
  physisches `DELETE` ist erlaubt, **aber** `McpAccessScope`-Zeilen
  hängen `Cascade` am Zugang, während `AiActionRequest`/`McpAuditEntry`
  über Soft-Referenzen (unten) lesbar bleiben. Empfehlung für die
  Umsetzung: Standardpfad „Löschen" = `Revoked`; hartes Löschen nur
  als optionale Bereinigung mit dokumentierter Referenzbehandlung.

---

## 4. `McpAccessScope` – Scope-Tupel und Filter

Das Rechteprofil eines Zugangs ist eine **Zeilenmenge** (ein Datensatz
je Scope-Tupel) – direkte Abbildung von Berechtigungsmodell 2.1/3.2
(„Liste von Scope-Tupeln … als Zeilenmenge je Zugang").

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | `Guid` | `Entity`-Konvention |
| `McpAccessId` | `Guid` (required, FK) | Zugang; `Cascade`-Löschung mit dem Zugang |
| `DataType` | `McpDataType` (required) | einer der 18 Datentypen |
| `Right` | `McpRightKind` (required) | eine der 7 Rechtearten |
| `AccountTypesCsv` | `string?` | Filterdimension Kontotypen: CSV der `AccountType`-Werte (`0`/`1`); `null` = kein Filter |
| `ContactCategoryIdsCsv` | `string?` | Filter Kontaktgruppen (CSV von `ContactCategory`-Ids) |
| `SecurityCategoryIdsCsv` | `string?` | Filter Wertpapierkategorien |
| `SavingsPlanCategoryIdsCsv` | `string?` | Filter Sparplankategorien |
| `AttachmentCategoryIdsCsv` | `string?` | Filter Anhangkategorien |
| `CreatedUtc`/`ModifiedUtc` | Basis | `Entity`-Konvention |

**EF-Konfiguration:**

- `HasIndex(x => new { x.McpAccessId, x.DataType, x.Right })` –
  Profil-Lookup; **nicht** unique (mehrere Scopes mit unterschied-
  lichen Filtern auf denselben Datentyp × Rechteart sind zulässig –
  ODER-Vereinigung, Berechtigungsmodell 4.2).
- `DeleteBehavior.Cascade` von `McpAccess` (Scopes sind
  Bestandteile des Zugangs, keine eigenen Aggregate).

**Regeln:**

- **Standardprofile werden nicht persistiert als Zeilen:** Ein Zugang
  mit `Level != Individuell` hat *keine* `McpAccessScope`-Zeilen – das
  effektive Profil wird zur Laufzeit aus der Stufenmatrix abgeleitet
  (serverseitige Definition, `GET /api/mcp-accesses/levels` für die
  UI). `McpAccessScope`-Zeilen existieren **nur** bei Stufe
  `Individuell` (bzw. „Von Vorlage anpassen" → dann wird die Vorlage
  materialisiert). Begründung: Stufenprofile sollen mit
  Katalog-Weiterentwicklung mitwachsen können; eingefrorene Kopien
  würden still veralten. Die Validierung „nur ✓-Zellen der
  F3-Matrix" ist Service-Logik, kein DB-Constraint.
- **Filter-Semantik:** leere/Null-CSV = unbeschränkt; gesetzte CSV =
  positive Whitelist; unklassifizierte Datensätze (`CategoryId==null`)
  nicht erfasst; UND über Dimensionen innerhalb einer Zeile, ODER
  über Zeilen desselben (DataType, Right).
- Meta-Scope der Tools T32/T33 wird **nicht** persistiert – er ist
  jedem Zugang inhärent (Toolkatalog 3.6).

---

## 5. `AiActionRequest` – Genehmigungsanfrage und Queue

Übernimmt die Feldfestlegung des Approval-Konzepts Abschnitt 3
(fachlich normativ) plus die Queue-/Claim-Felder des
Architekturkonzepts.

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | `Guid` | Request-Id, synchron an den Client zurückgegeben |
| `OwnerUserId` | `Guid` (required) | Besitzer = Besitzer des anfordernden Zugangs |
| `McpAccessId` | `Guid` (required) | anfordernder Zugang – **Soft-Referenz** (kein FK-Zwang, siehe unten) |
| `McpAccessName` | `string` (required, Snapshot) | Zugangsname zum Anforderungszeitpunkt (lesbar nach Zugangslöschung) |
| `ToolName` | `string` (required) | `fm_*`-Toolname |
| `CatalogVersion` | `string` (required) | `katalog-v<n>` zum Anforderungszeitpunkt |
| `ParameterSnapshot` | `string` (required, JSON) | eingefrorener Parametersatz – Grundlage von Wirkungsvorschau und Ausführung; unveränderlich |
| `ReasonText` | `string?` | optionaler Begleittext des Clients (untrusted content; Längenbegrenzung analog Audit-Konzept 4.2) |
| `Status` | `AiActionRequestStatus` (required) | 6 Zustände inkl. `Expired` |
| `RequestedUtc` | `DateTime` (required) | Anforderungszeitpunkt (serverseitig) |
| `ExpiresUtc` | `DateTime` (required) | Entscheidungsfrist (Default 72 h, `Mcp:Approval:DefaultExpiryHours`) |
| `DecidedUtc` / `DecidedByUserId` / `DecisionNote` | `DateTime?` / `Guid?` / `string?` | Entscheidung; bei `PreApproved` sofort gefüllt („Vorab-Genehmigung Zugangseinstellung", `DecidedByUserId` = Besitzer) |
| `ExecutedUtc` | `DateTime?` | Ausführungszeitpunkt |
| `ExecutionResult` | `string?` (JSON) | Ergebnisreferenzen (Posting-Ids, `StatementImportId`, `BookingResult`-Kurzfassung) – Verknüpfungspfad ins Audit |
| `FailureReason` | `string?` | klassifizierter `Err_*`-Code bei `Failed` |
| `CorrelationId` | `Guid` (required) | verknüpft Toolaufruf ↔ Request ↔ Audit-Einträge |
| `IdempotencyKey` | `string` (required) | Deduplizierung (`McpAccessId` + client-/serverschlüssel) |
| `ClaimedUtc` / `ClaimToken` | `DateTime?` / `Guid?` | Queue-Claim (atomarer Übergang `Approved → laufend` unter SQLite; Muster `StatementDraftBookingGuard`-`LockToken`) |
| `AttemptCount` | `int` (default 0) | Ausführungsversuche (fachlich: kein Auto-Retry; Zähler für Diagnose) |
| `CreatedUtc`/`ModifiedUtc` | Basis | `Entity`-Konvention |

**EF-Konfiguration:**

- `HasIndex(x => x.IdempotencyKey).IsUnique()` –
  Anforderungs-Deduplizierung (Retry → gleicher Request).
- `HasIndex(x => new { x.OwnerUserId, x.Status, x.RequestedUtc })` –
  Genehmigungscenter-Liste.
- `HasIndex(x => new { x.Status, x.ExpiresUtc })` – Queue-Poll
  (`Approved`) + Expired-Job (`Pending` + Frist).
- `HasIndex(x => x.CorrelationId)` – Audit-Verknüpfung.
- `HasIndex(x => x.McpAccessId)` – Zugangs-Drilling.
- **Soft-Referenz `McpAccessId`:** kein `Restrict`-/`Cascade`-FK-
  Verhalten gegen Zugangslöschung erforderlich – der Request bleibt
  als Audit-Substrat lesbar (`McpAccessName`-Snapshot). In der EF-
  Konfiguration als normale Spalte ohne Navigations-Constraint
  (oder `DeleteBehavior.NoAction`), damit `Revoked`-/Löschpfade
  die Request-Historie nicht kaskadieren.

**Queue-Eigenschaft:** Die Tabelle **ist** die persistente Queue –
kein separater Speicher (Approval-Konzept 5.1). Der Claim erfolgt
als bedingtes UPDATE (`WHERE Id=@id AND Status=Approved`) in einer
Transaktion; `ClaimToken`/`ClaimedUtc` markieren den laufenden
Versuch.

---

## 6. Audit-Tabellen

### 6.1 `McpAuditEntry` – Ereigniseintrag

Grundgerüst je Eintrag (Audit-Konzept 4.1) + Integritätsfelder (7.2):

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | `Guid` | Eintragsidentität |
| `Utc` | `DateTime` (required) | serverseitiger Zeitpunkt (`UtcNow`; Client-Zeiten nie) |
| `SequenceNo` | `long` (required) | fortlaufende, monotone Eintragsnummer **je Kette** (je `OwnerUserId`-Kontext bzw. globaler System-Kontext) – Lückenlosigkeits-Merkmal |
| `Category` | `McpAuditCategory` (required) | eine der 5 Ereigniskategorien |
| `EventType` | `string` (required) | stabiler Typcode (`ToolCall`, `ScopeDenied`, `TokenRotated`, `ApprovalDecided`, `SwitchToggled`, `AuthFailed`, `RetentionPurged` …) |
| `OwnerUserId` | `Guid?` | betroffener Besitzer; `null` bei rein globalen Ereignissen (z. B. `Mcp:Enabled`-Umschaltung ohne Benutzerkontext – dann auslösender Kontext in `Context`) |
| `McpAccessId` | `Guid?` | Zurechnung (Soft-Referenz; überlebt Zugangslöschung) |
| `McpAccessName` | `string?` | Zugangsname-Snapshot |
| `ToolName` | `string?` | `fm_*`-Tool bei Toolaufrufen |
| `CatalogVersion` | `string?` | `katalog-v<n>` zum Aufrufzeitpunkt |
| `Context` | `string?` | Verwaltungskontext (`Ui`, `McpEndpoint`, `System/Job`, `Admin`) |
| `ResultStatus` | `string` (required) | `Success`, `Denied`, `Failed`, `RateLimited`, `RedirectedToApproval` … (stabile Werteliste) |
| `CorrelationId` | `Guid?` | Verknüpfungsschlüssel der Wirkkette |
| `SourceIp` | `string?` | Quell-IP bei Client-Aufrufen |
| `DurationMs` | `int?` | Laufzeit des Toolaufrufs |
| `AffectedCount` | `int?` | betroffene Datensatzanzahl (Listen/Updates) |
| `PayloadJson` | `string?` | kategorienspezifische Nutzlast in abgestufter Detailtiefe (Audit-Konzept 4.2): Parameter ohne Nutzlast-Dumps bei lesend, voller Snapshot-Bezug bei schreibend, `Sha256`-Metadaten statt Binärinhalt; **niemals Token-Material** |
| `RelatedRequestId` | `Guid?` | Verknüpfung zum `AiActionRequest` |
| `PrevHash` | `string?` | Hash des kanonisierten Vorgängereintrags derselben Kette |
| `EntryHash` | `string` (required) | Hash über den Eintragsinhalt (Verkettungsanker) |
| `CreatedUtc` | Basis | Insert-Zeitpunkt (`ModifiedUtc` bleibt fachlich `null` – append-only) |

**EF-Konfiguration:**

- `HasIndex(x => new { x.OwnerUserId, x.Utc })` – Benutzer-Timeline.
- `HasIndex(x => x.CorrelationId)` – Wirkkette.
- `HasIndex(x => x.McpAccessId)` – Zugangsbezug.
- `HasIndex(x => new { x.Category, x.EventType })` –
  Filterauswertung/Admin-Sicht.
- `HasIndex(x => new { x.OwnerUserId, x.SequenceNo }).IsUnique()` –
  Kettendefinition (je Besitzer-Kette; globale Kette mit
  `OwnerUserId IS NULL` – unter SQLite als separater Auswertungszweig
  gehandhabt, da Unique mit NULLs mehrere Zeilen erlaubt; die Kette
  wird applikatorisch über `Context` unterschieden).
- Kein Update-/Delete-Pfad in der Anwendung (Append-only-Konvention;
  einzige Ausnahme: Retention-Lauf, Abschnitt 8).

### 6.2 `McpAuditEntryChange` – Feld-Deltas

Eigene Kindtabelle, damit die Drilling-Navigation („Toolaufruf →
Änderungsliste → Datensatz") als relationale Abfrage läuft statt in
JSON geparst zu werden:

| Feld | Typ | Bedeutung |
|---|---|---|
| `Id` | `Guid` | `Entity`-Konvention |
| `EntryId` | `Guid` (required, FK → `McpAuditEntry`, `Cascade`) | übergeordneter Eintrag |
| `EntityType` | `string` (required) | betroffener Entitätstyp (`Posting`, `Contact`, `StatementDraftEntry` …) |
| `EntityId` | `Guid` (required) | betroffene Datensatz-Id (Ergebnis-Referenz/Drilling-Ziel) |
| `Field` | `string` (required) | geändertes Feld |
| `OldValue`/`NewValue` | `string?` | Vorher/Nachher als serialisierter Wert (Längenbegrenzung mit Kürzungsvermerk, Audit-Konzept 4.2) |

**EF-Konfiguration:**

- `HasIndex(x => x.EntryId)` – Details je Eintrag.
- `HasIndex(x => new { x.EntityType, x.EntityId })` –
  „welche MCP-Aktionen berührten diesen Datensatz".
- `DeleteBehavior.Cascade` von `McpAuditEntry` (Retention entfernt
  Einträge inkl. Details atomar).

**Begründung der Zweitabellen-Entscheidung:** Audit-Konzept lässt
JSON-Nutzlasten zu; Feld-Deltas sind jedoch das primäre
Drilling-Objekt der UI (Vorher/Nachher-Tabelle, Entity-Drilling) –
als eigene Zeilen bleiben sie indizier- und filterbar, während
variable Nutzlasten (Parameter, Aggregate) kompakt in `PayloadJson`
bleiben.

---

## 7. Indizes- und Constraints-Übersicht

|| Tabelle | Index/Constraint | Zweck |
|---|---|---|---|
| `McpAccesses` | unique `AccessKeyHint` | Token-Lookup |
| `McpAccesses` | unique `(OwnerUserId, Name)` | Namenskonvention |
| `McpAccesses` | `(OwnerUserId, Status)`, `(ExpiresUtc)` | Liste, Ablauf-Job |
| `McpAccessScopes` | `(McpAccessId, DataType, Right)` | Profil-Auswertung; Cascade von Access |
| `AiActionRequests` | unique `IdempotencyKey` | Deduplizierung |
| `AiActionRequests` | `(OwnerUserId, Status, RequestedUtc)` | Genehmigungscenter |
| `AiActionRequests` | `(Status, ExpiresUtc)` | Queue-Poll + Expired-Job |
| `AiActionRequests` | `CorrelationId`, `McpAccessId` | Verknüpfung/Drilling |
| `McpAuditEntries` | `(OwnerUserId, Utc)` | Timeline |
| `McpAuditEntries` | `CorrelationId`, `McpAccessId`, `(Category, EventType)` | Kette/Filter |
| `McpAuditEntries` | unique `(OwnerUserId, SequenceNo)` (Kette) | Lückenlosigkeit |
| `McpAuditEntryChanges` | `EntryId`, `(EntityType, EntityId)` | Drilling; Cascade von Entry |

---

## 8. Retention- und Löschverhalten

|| Bestand | Verhalten |
|---|---|
| `McpAuditEntries` (+ `…Changes`) | Retention je Ereignisklasse (Audit-Konzept 5): lesende/vorschlagende `ToolCall` 12 Monate; `DataChange`/`ApprovalEvent` 36 Monate; `AccessEvent`/`SecurityEvent` 36 Monate bzw. Zugangsdauer + 12 Monate. Bereinigung über Hintergrundlauf (`Mcp:Audit:RetentionDays:*`), protokolliert als `RetentionPurged`-Systemereignis – einziger Löschpfad der Anwendung; Einträge gelöschter Zugänge bleiben bis Fristablauf. |
| `AiActionRequests` | **nicht** retention-gelöscht, solange nicht-terminal (`Pending`/`Approved` verfallen nur über `Expired`-Übergang). Terminal-Requests (`Executed`/`Failed`/`Rejected`/`Expired`) sind Audit-Substrat und folgen derselben 36-Monats-Frist wie Approval-Ereignisse (konfigurierbar, `Mcp:Audit:*`); die Löschung erfolgt im selben Retention-Lauf. |
| `McpAccess`/`McpAccessScope` | kein Retention-Lauf; Lebenszyklus über `Status`/`RevokedUtc`; optionale harte Löschung mit dokumentierter Referenzbehandlung (Soft-Referenzen bleiben lesbar). |

**Hash-Kette unter Retention:** Beim Entfernen des **ältesten**
Segments einer Kette bleibt der erste verbleibende Eintrag mit
seinem `PrevHash` bestehen – die Kette ist ab dann „ab diesem
Eintrag" verifizierbar; der Retention-Lauf hinterlegt das
`RetentionPurged`-Ereignis mit Anzahl/Zeitraum als neuen
Kettenanfang. (Ehrliche Begrenzung wie im Audit-Konzept: der
Anker schützt vor punktueller Manipulation, nicht vor komplettem
Neuaufbau.)

---

## 9. Migration-Strategie

1. **Neue Entitäten** in `FinanceManager.Domain/Mcp/` +
   `Domain/Audit/` (inkl. Enums und `*BackupDto`-Records, siehe
   Abschnitt 10).
2. **`AppDbContext`:** `DbSet`-Properties + `OnModelCreating`-
   Blöcke wie oben (Enum→`short`, `DeleteBehavior`, Indizes);
   Provider bleibt ausschließlich SQLite.
3. **Migration:** `dotnet ef migrations add` (EF-Design im Web-
   Projekt) → Anwendung beim Start über `ApplyMigrationsAndSeed`
   (`db.Database.Migrate()`); kein `SchemaPatcher`-Bedarf (neue,
   leere Tabellen, keine Bestandsdaten nachzuziehen).
4. **Bestandsdaten:** keine Migration nötig – es gibt keine
   Bestands-MCP-Daten; kein Rückfüllen von `OwnerUserId` oder
   ähnlichem auf Fachentitäten (verbindliche Kante).
5. **Rollback-/Downgrade-Betrachtung:** neue Tabellen sind
   additive Erweiterung; Down-Migration entfernt sie gefahrlos,
   solange keine Produktivdaten drin sind (Konzept stellt keine
   Abhängigkeit bestehender Tabellen auf die neuen her – nur
   Soft-Referenzen von neuen auf alte).
6. **Idempotenz des Starts:** wie bisher migrationsgesteuert –
   `ApplyMigrationsAndSeed` ist der einzige Anwendungspunkt; das
   MCP-Feature-Flag `Mcp:Enabled=false` ist der sichere Default,
   sodass Migration ≠ Aktivierung.

---

## 10. Backup-/Restore-Einbindung

|| Entität | Backup-Behandlung |
|---|---|
| `McpAccess` | **Backup ja, Token-Hash nein.** `McpAccessBackupDto` exportiert Konfiguration (Name, Description, Level, ApprovalMode, ExpiresUtc, AllowedIpRanges, Status→`Revoked`), **nicht** `TokenHash`/`AccessKeyHint` (sicherheitsfachlich: ein Backup darf keine wiederherstellbaren Zugangsgeheimnisse enthalten – Backup-Dateien sind weniger geschützt als die Laufzeit-DB). Bei Restore wird der Zugang als **widerrufen** rekonstruiert (`Status=Revoked`, neue `Id` erlaubt bzw. Bestands-Id); der Benutzer aktiviert ihn über Rotation → neues Token. Das entspricht dem Prinzip „kompromittiertes Token = sofort tot" und verhindert, dass ein Backup unbemerkt gültige Zugänge reaktiviert. |
| `McpAccessScope` | `McpAccessScopeBackupDto` je Zugang mit im Backup (Rechteprofil wiederherstellbar); Filter-CSVs unverändert (Ids sind benutzerstabil). |
| `AiActionRequest` | **ja** (`AiActionRequestBackupDto`): offene (nicht-terminale) Requests müssen einen Restore überleben (eine erteilte Freigabe ist verbindlich; Approval-Konzept); Snapshot/Status/CorrelationId vollständig. Terminal-Requests fahren mit, sind aber im Restore optional (Retention-kompatibel). |
| `McpAuditEntry`(+`Changes`) | **ja** (Audit-Konzept 5: „Backup-Einbindung … Protokoll und Daten bleiben konsistent wiederherstellbar"); `McpAuditEntryBackupDto` inkl. `SequenceNo`/`PrevHash`/`EntryHash`, damit die Kette nach Restore verifizierbar bleibt. |
| Konfiguration `Mcp:*` | kein Backup – `appsettings` ist Betreiber-Konfiguration, nicht Benutzerdaten (konsistent zur Behandlung anderer Flags). |

---

## 11. Auswirkungen auf Bestands-Tabellen

**Keine.** Sämtliche neuen Beziehungen zeigen von neuen Tabellen auf
Bestands-Konzepte (`OwnerUserId` → `User`, Filter-CSVs →
Kategorie-Ids), nicht umgekehrt:

- Kein `OwnerUserId`/`Mcp*`-Feld auf `Posting`, `StatementImport`,
  `Account` o. ä. – die indirekte Besitzableitung des
  Berechtigungsmodells bleibt unverändert in der Evaluator-Logik.
- `AccountShare` bleibt alleinige Fremdfreigabe-Quelle (Stufe-0/3).
- `Notification`-Nutzung für Genehmigungs-/Sicherheitsalarme ist
  reine Verwendung der Bestands-Entität (kein Schema-Eingriff;
  `NotificationType`/`NotificationTarget`-Bestandswerte reichen).
- `StatementDraftBookingGuard` bleibt das einzige fachspezifische
  Lock-Objekt; `AiActionRequest`-Claim nutzt eigene Felder.

---

## 12. Konsistenzanforderungen an Folgeartefakte

- **Architekturkonzept (dieser Schritt):** Pipeline-Anforderungen an
  die Indizes (Token-Lookup, Queue-Claim, Audit-Kette) sind hier
  normativ abgedeckt – bei Umsetzung keine zweite Zugriffstabelle
  (Token-Cache) erfinden.
- **UI-Konzept (dieser Schritt):** alle dargestellten Felder
  (Status-Badges, Nutzungsübersicht, Wirkungsvorschau, Timeline)
  sind durch die hier definierten Felder abbildbar.
- **Roadmap (Schritt 5):** inkrementelle Reihenfolge der
  Migrationen möglich (Zugang+Scopes zuerst, AiActionRequest mit
  Phase 4, Audit parallel zu Phase 1) – aber als **ein** Modell
  geplant, um Folge-Migrationen ohne Schema-Bruch zu halten.
- **Umsetzung:** `Err_Mcp_*`-Fehlercodes, `fm_mcp_*`-Token-Format,
  `pbkdf2|…`-Hash-Format und die Enum-Wertelisten dieses Dokuments
  sind verbindlich; Abweichungen nur über Änderung dieses Dokuments
  (Katalog-/Konzept-Versionierung).
