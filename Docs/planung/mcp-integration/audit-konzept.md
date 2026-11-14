# Audit-Konzept – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 9 der Anforderung; spezifiziert die
revisionssichere Protokollierung (F9) aller MCP-Aktivitäten. Setzt
Begriffe aus [fachkonzept.md](fachkonzept.md) (Nutzungsprotokollierung,
Abschnitt 6.5) und die Ereignisse aus
[sicherheitskonzept.md](sicherheitskonzept.md) sowie die
`AiActionRequest`-Zustände aus
[approval-konzept.md](approval-konzept.md) inhaltlich fort.

---

## 1. Zielbild und Grundsätze

Das Audit-Protokoll beantwortet für den Benutzer die verbindliche
fachliche Frage (Fachkonzept 6.5):

> *„Welches KI-System hat wann was mit meinen Daten getan oder
> versucht?"*

und für den Admin zusätzlich: *„Welche sicherheitsrelevanten
Ereignisse gab es rund um MCP?"*

**Verbindliche Grundsätze:**

1. **Append-only auf Anwendungsebene.** Audit-Einträge werden einmal
   geschrieben und von der Anwendung nie geändert oder gelöscht
   (einzige Ausnahme: fristgesteuerte Aufbewahrungsbereinigung,
   Abschnitt 5) – der einzige Schreibpfad ist das Anlegen.
2. **Zurechenbarkeit statt Anonymität.** Jeder Eintrag trägt
   Zeitpunkt, Zugang (`McpAccessId`), Besitzer und Ereignistyp; Token
   selbst werden nie protokolliert – auch nicht teilweise oder
   gehasht.
3. **Nachvollziehbarkeit durch Verknüpfung, nicht Volltext-Duplikation.**
   Die Wirkkette Toolaufruf → `AiActionRequest` → Ergebnisdatensätze
   wird über Ids (`CorrelationId`, Request-/Entitäts-Ids) rekonstruier-
   bar gemacht; es werden keine redundanten Datenstände kopiert.
4. **Revisionssicher heißt hier: lückenlos, unveränderbar erkennbar,
   benutzerlesbar.** Auf SQLite-/Selbsthosting-Ebene ist absolute
   Manipulationssicherheit physikalisch nicht erreichbar (die DB-Datei
   liegt beim Betreiber) – das Konzept definiert deshalb realistische
   Integritätsmerkmale (Abschnitt 7) statt regulatorischer Übertreibung.
5. **Das Protokoll ist selbst schützenswert.** Es enthält persönliche
   Finanzmetadaten; Zugriff folgt denselben Owner-Regeln wie die Daten
   selbst (Benutzer sieht sein Protokoll; Admin sieht Sicherheits-
   ereignisse, nicht fremde Finanzinhalte).

---

## 2. Abgrenzung zum vorhandenen technischen Logging

| Mechanismus (Ist) | Was er leistet | Warum er kein Audit-Trail ist |
|---|---|---|
| `RequestLoggingMiddleware` | Timing/Status je HTTP-Request (Debug/Warning), Redaction des Query-Parameters `token` | kein Wer/Was/auf-welche-Daten, kein Persistenzmodell für Auswertung, kein fachlicher Ereignisbegriff, Rotations-/Aufbewahrungssemantik eines Fehlerlogs |
| `FileLoggerProvider` | optionales Datei-Logging (`FileLogging:*`, rollierend) | technisches Diagnoselog; Dateirotation verwirft Einträge (`RetainedFileCountLimit`), kein benutzerbezogener Zugriff, keine Integritätsmerkmale |
| `Notification`/`INotificationWriter` | Benutzerhinweise (EventDriven, SystemAlert) | Kommunikationskanal, kein Protokoll: dismissable, editierbar, keine Pflichtvollständigkeit – kann Audit-Ereignisse *melden*, nicht *sein* |
| `IpBlock`-Fehlversuchszähler | IP-Sicherheitsstatus | punktueller Zähler, kein Ereignisprotokoll |
| `IBackgroundTaskManager`-Status | Task-Fortschritt (Queued/Running/Completed) | In-Memory, flüchtig, begrenzte Historie |

**Festlegung:** Für MCP wird ein **neuer persistenter Audit-Speicher**
(eigene Tabelle/Entität) konzipiert; das bestehende Logging bleibt
unverändert Diagnosekanal. Beide Kanäle dürfen sich ergänzen –
Diagnose-Logs enthalten weiterhin keine Token und keine fachlichen
Nutzlasten.

---

## 3. Ereigniskatalog: Was wird protokolliert

Einträge werden fachlich in fünf Kategorien geteilt. Jeder Eintrag hat
ein gemeinsames Grundgerüst (Abschnitt 4.1) plus kategorienspezifische
Daten.

### 3.1 Toolaufrufe (immer, auch lesend)

- welcher **Zugang** (McpAccessId + Zugangsname zum Zeitpunkt),
- wann (`Utc`), welches **Tool** (`fm_*`) in welcher
  **Katalogversion** (`katalog-v<n>`),
- Aufrufparameter (Detailgrad je Klasse, Abschnitt 4.2),
- **Ergebnisstatus:** Erfolg / Scope-Verweigerung / Authentifizierungs-
  fehler / Rate-Limit / Validierungsfehler / interner Fehler /
  zu `AiActionRequest` umgeleitet,
- Dauer, betroffene Datensatzanzahl (bei Listen/Updates) bzw.
  Ergebnis-Referenzen,
- `CorrelationId` (verknüpft Folgeaufrufe, Requests und
  Ausführungsereignisse),
- Quell-IP des Clients (sicherheitsrelevante Attribution).

### 3.2 Änderungsdaten (schreibende und ausgeführte Aktionen)

- **Welcher Datensatz** (Entitätstyp + Id) wurde angefasst,
- **Vorher/Nachher-Werte der geänderten Felder** (Feld-Delta, nicht
  Vollsnapshot – z. B. `ContactId: null → 3f9…`, `Status: Open →
  Announced`, `ArchivedUtc: null → 2026-…`),
- Verknüpfung zum auslösenden Toolaufruf und – falls vorhanden –
  `AiActionRequestId`,
- Ergebnis-Referenzen bei Erzeugung neuer Datensätze: `Posting`-Ids,
  `StatementImportId`, `StatementEntry`-Ids, neue Stammdaten-Ids.

### 3.3 Authentifizierungs- und Zugangsereignisse

- Zugang angelegt / aktiviert / deaktiviert / gelöscht,
- Token erzeugt / rotiert / widerrufen (nie Token-Material selbst),
- Ablaufdatum erreicht (Zugang verfällt),
- **Konfigurationsänderungen des Zugangs:** Freigabestufen-Wechsel,
  Scope-/Filter-Änderungen (Vorher/Nachher als Scope-Delta),
  Genehmigungsmodus-Änderung, IP-Bindungs-Änderung – diese
  Verwaltungsaktionen werden protokolliert, auch wenn sie der
  Besitzer selbst in der UI vornimmt (Vollständigkeit der
  Rechtehistorie),
- fehlgeschlagene Authentifizierungsversuche (aggregiert
  zusätzlich im Anomalie-Kontext, einzeln bei Sperr-relevanten
  Serien).

### 3.4 Approval-Entscheidungen

Je `AiActionRequest` eine lückenlose Zustandskette:

- Request angelegt (Tool, Parameter-Snapshot-Referenz, Ablauffrist),
- Entscheidung: freigegeben / abgelehnt / abgelaufen (`Expired`) /
  systemseitig zurückgezogen – mit `DecidedByUserId` und
  `DecisionNote`,
- Ausführung: gestartet, `Executed` mit `ExecutionResult`, oder
  `Failed` mit `FailureReason`,
- vorab-genehmigte Ausführungen (pre-approved Scope) werden als
  vollständige Kette protokolliert – die Genehmigungsinstanz bleibt
  dokumentiert (Approval-Konzept 3.4).

### 3.5 Sicherheitsereignisse

- Aktivierung/Deaktivierung der Notfallschalter (`Mcp:Enabled`,
  `Mcp:WriteAccess:Enabled`) inkl. auslösender Stelle (Konfiguration,
  Admin),
- IP-Sperrungen mit MCP-Bezug und wiederholte Authentifizierungs-
  fehlschläge je IP/Zugangskürzel,
- Anomaliealarme (massenhafte Scope-Verweigerungen, Rate-Limit-
  Muster) und ausgelöste Maßnahmen (Zugangsdeaktivierung,
  Benachrichtigung an Admins via `SystemAlert`),
- Rate-Limit-Ereignisse: nicht jede 429 einzeln, sondern
  **aggregierte** Einträge (Zeitfenster + Anzahl + Zugang/IP) – sonst
  würde der Angriff sein eigenes Protokoll fluten.

---

## 4. Detailtiefe und Datenschutzabwägung

### 4.1 Grundgerüst je Eintrag (immer vollständig)

| Feld | Inhalt |
|---|---|
| `Id`, `Utc` | Eintragsidentität, Zeitpunkt (UTC) |
| `Kategorie` + `Ereignistyp` | eine der fünf Kategorien + stabiler Typcode (z. B. `ToolCall`, `ScopeDenied`, `TokenRotated`, `ApprovalDecided`, `SwitchToggled`) |
| `OwnerUserId` | betroffener Besitzer (bei globalen Ereignissen: auslösender Kontext bzw. leer) |
| `McpAccessId` + `Zugangsname-Snapshot` | Zurechnung; der Name wird als Snapshot mitgeführt, damit Einträge nach Zugangslöschung/-umbenennung lesbar bleiben |
| `ToolName`/`Kontext` | Tool oder Verwaltungskontext (UI, System/Job) |
| `Ergebnisstatus` | Erfolg/Verweigerung/Fehler |
| `CorrelationId` | Verknüpfungsschlüssel (Abschnitt 6) |
| `Quell-IP` | bei Client-Aufrufen; bei Systemereignissen leer |
| `Vorgänger-Hash` | Integritätsanker (Abschnitt 7.2) |

### 4.2 Abgestufte Detailtiefe (Datenschutz- vs. Nachvollziehbarkeits-Abwägung)

Persönliche Finanzdaten im Protokoll sind legitimes Risiko
(Selbsthosting-Mitverantwortung des Benutzers) und zugleich Kern der
Nachvollziehbarkeit – deshalb abgestuft:

| Ereignis | Detailtiefe | Begründung |
|---|---|---|
| Lesende Toolaufrufe | Parameter **ohne** Nutzlast-Dumps: Ziel-Ids, Filterwerte, Paging, Zeitraum; **keine** zurückgelieferten Datensatzinhalte (nur Anzahl/Referenz-Ids) | Rekonstruktion „was wurde angesehen" ohne doppelte Datenablage; die gelesenen Werte stehen ohnehin in der DB |
| Vorschlagende Aufrufe | wie lesend + Verweis auf die Vorschlagsstruktur (Ziel-Entität/Feld), nicht deren Volltext | Vorschläge sind flüchtig; wichtig ist, dass sie erzeugt wurden |
| Schreibvorbereitende Aufrufe | **voller Parameter-Snapshot** der geschriebenen Werte + Feld-Delta Vorher/Nachher | nur so ist „was wurde geändert" beweisbar |
| Genehmigungspflichtige Ausführungen | voller Snapshot (bereits im `AiActionRequest`) + Ergebnis-Referenzen + Feld-Deltas | höchste Wirkung → höchste Tiefe |
| Freitexte | Längenbegrenzung (z. B. 500 Zeichen je Feld) mit Kürzungsvermerk; `Subject`/`RecipientName` vollständig nur, wenn sie selbst Wirkparameter sind | Protokollvolumen vs. Beweiswert |
| Binärinhalte (`Attachment.Content`) | **niemals** im Protokoll – nur Metadaten (FileName, ContentType, SizeBytes, `Sha256`) | `Sha256` reicht zur Identifikation; Inhalte blähen das Protokoll ohne Beweis-Mehrwert |
| Token/Secrets | **niemals** – auch nicht gehasht oder gekürzt; IP-Adressen werden gespeichert (Sicherheitsattribution, LAN-Realität) | Leak-Schutz des Protokolls selbst |
| Prompt-/Reason-Texte des Clients | gekennzeichnet als untrusted content, längenbegrenzt | Nachvollziehbarkeit der Client-Absicht vs. Protokollgröße |

**Datenschutzfestlegung:** Da FinanceManager persönliche Finanzdaten des
Benutzers selbst hostet, liegt die Haupt-Exposition beim
Zugangsbesitzer selbst; das Audit erweitert die Exposition nicht über
die Daten hinaus, die der Zugang ohnehin lesen durfte. Die
Aufbewahrungsfristen (Abschnitt 5) begrenzen die zeitliche Exposition;
der Zugriff auf das Protokoll folgt der Stufe-0-Regel (Owner sieht
seine Einträge; Admin sieht nur Sicherheitsereignisse ohne fachliche
Nutzlasten).

---

## 5. Aufbewahrungsdauer und Bereinigung

Abgestuftes Retentionsmodell (Vorschlag, konfigurierbar über
`Mcp:Audit:*`):

| Ereignisklasse | Standard-Aufbewahrung | Begründung |
|---|---|---|
| Lesende/vorschlagende Toolaufrufe (hochfrequent) | **12 Monate** | beantwortet „was hat die KI zuletzt gesehen"; begrenzt Protokollvolumen |
| Schreibende Toolaufrufe, Änderungsdaten, Approval-Ketten | **36 Monate** | fachliche Beweiswirkung für getätigte Wirkungen (Verbuchungen, Stornos, Stammdaten-Änderungen) |
| Authentifizierungs-/Zugangs- und Sicherheitsereignisse | **36 Monate** bzw. mindestens Lebensdauer des Zugangs + 12 Monate | Rechtehistorie und Sicherheitsvorfälle müssen den Zugang überdauern |
| Einträge gelöschter Zugänge | bleiben erhalten (Zugangsname-Snapshot) bis Fristablauf | Widerruf darf die Spur nicht löschen – das Protokoll überlebt den Zugang (Fachkonzept 6.2: „Protokoll bleibt revisionsfest erhalten") |

- **Bereinigung:** Hintergrundlauf (Konvention der vorhandenen
  Executoren/Scheduler), der fristüberschreitende Einträge entfernt –
  der **einzige** Löschpfad der Anwendung auf der Audit-Tabelle; die
  Bereinigung selbst wird als Systemereignis protokolliert.
- **Export/Archivierung vor Bereinigung:** optionaler Export
  (Abschnitt 6.3) erlaubt dem Benutzer langfristige Eigenarchivierung.
- **Backup-Einbindung:** die Audit-Tabelle folgt der
  `*BackupDto`-Konvention und wird in die bestehende Backup/Restore-
  Mechanik aufgenommen (Datenmodell, Schritt 4) – Protokoll und Daten
  bleiben konsistent wiederherstellbar.

---

## 6. Auswertbarkeit und Nachvollziehbarkeit von KI-Entscheidungen

### 6.1 Benutzer-Audit-Ansicht (fachlich)

Je MCP-Zugang eine Timeline-Ansicht mit:

- Filtern: Zeitraum, Tool, Ergebnisstatus (insb. „nur Verweigertes"),
  Ereigniskategorie, Request-Zustand;
- Drilling: Toolaufruf → Änderungsliste (Feld-Deltas) →
  `AiActionRequest` mit Entscheidung und Ausführung;
- Nutzungsübersicht des Zugangs (`LastUsedUtc`, Aufrufzahlen,
  Fehlschlagquoten – fachliche Anforderung aus Fachkonzept 6.5);
- Verständliche Darstellung: Ereignisse in de/en lokalisiert (resx-
  Konvention), keine Roh-JSON-Pflicht für Standardfälle – Detail-
  expandierung für Experten.

### 6.2 Admin-/Sicherheitsauswertung

- Admin sieht **sicherheitsrelevante Metadaten instanzweit**
  (Anomaliealarme, Schalter-Aktivierungen, Fehlversuchsserien,
  Rate-Limit-Aggregate) – **nicht** die fachlichen Finanzinhalte
  fremder Benutzer (Konsistenz: Admin ist kein Datentürsteher,
  Fachkonzept 5.4). SystemAlert-Benachrichtigungen verlinken in diese
  Sicht.

### 6.3 Export

- Export des eigenen Protokolls als JSON (vollständig) und CSV
  (Kurzfassung) über die Audit-Ansicht – für Eigenarchivierung und
  externe Auswertung; Export selbst ist ein Audit-Ereignis.

### 6.4 Nachvollziehbarkeit von KI-Entscheidungen (Verknüpfungskette)

Jede KI-bedingte Wirkung ist über eine explizite Id-Kette
rekonstruierbar:

```text
ToolCall-Eintrag (CorrelationId)
   └─ AiActionRequest (ParameterSnapshot, Entscheidung, Ausführung)
        └─ Änderungseinträge (Feld-Deltas je Datensatz)
             └─ Ergebnis-Entitäten (Posting-Ids, StatementImportId, …)
```

- **Warum-Frage beantwortbar:** ParameterSnapshot + `ReasonText`
  (gekennzeichnet) + Vorschlags-Context des Aufrufs zeigen, was der
  Client beabsichtigte; die Feld-Deltas zeigen, was tatsächlich
  passierte.
- **Verantwortungszuordnung eindeutig:** Zugang → Client-System;
  Besitzer als Genehmigender; System als Executor – jede der drei
  Rollen ist pro Wirkung im Protokoll benannt.
- **Katalogversion im Eintrag** (3.1) macht das Verhalten des Tools
  zum Aufrufzeitpunkt bestimmbar, auch wenn der Katalog später
  weiterentwickelt wird.

---

## 7. WORM-/Integritätsaspekte auf SQLite-Niveau

### 7.1 Realistische Zielsetzung

Auf einer SQLite-Datei im Selbsthosting-Betrieb ist physische
Unveränderlichkeit nicht erreichbar – wer Datei- und Prozesszugang
hat, kann theoretisch alles ändern. Das Konzept zielt deshalb auf:

1. **kein Anwendungs-Änderungspfad** (Append-only-Konvention, §1.1),
2. **Erkennbarkeit nachträglicher Veränderung** (Hash-Verkettung),
3. **Lückenlosigkeit** (fortlaufende Ordnung),
4. **Backup-Konsistenz** (Protokoll reist mit dem Datenbestand).

### 7.2 Integritätsanker: Hash-Verkettung (konzipiert)

- Jeder Audit-Eintrag enthält `Vorgänger-Hash` = Hash des
  kanonisierten Vorgängereintrags (je Besitzer- bzw. globaler
  Schreibreihenfolge – eine Kette je Schreiber-Kontext, Festlegung im
  Datenmodell) plus `Eigen-Hash` über den Eintragsinhalt.
- **Wirkung:** nachträgliches Ändern/Löschen eines Eintrags oder
  Einschießen falscher Einträge bricht die Kette erkennbar
  (Verifikationsfunktion in der Audit-Ansicht: „Integrität prüfen").
- **Begrenzung ehrlich benannt:** Wer DB-Zugriff hat, kann die Kette
  auch komplett neu aufbauen – der Anker schützt primär vor
  punktueller, inkrementeller Manipulation (App-Bugs, neugierige
  Ad-hoc-Edits, Teillöschungen) und gibt dem Benutzer ein
  Prüfinstrument; er ist kein forensischer Beweis gegen den
  DB-Administrator.
- **SQLite-Umsetzungshinweis (fachlich):** Schreiben der Einträge in
  einer einzigen Transaktion je Ereignisbündel (Toolaufruf +
  Feld-Deltas); `WAL`-Betrieb des Systems bleibt unverändert; Indizes
  auf `OwnerUserId`+`Utc` und `CorrelationId` für Auswertbarkeit.

### 7.3 Lückenlosigkeit und Ordnung

- Fortlaufende, monoton vergebene Eintragsnummern je Kette (neben der
  `Guid`-Id) machen nachträgliche Löschungen als Lücke erkennbar.
- Zeitstempel ausschließlich serverseitig (`UtcNow` der Anwendung) –
  Client-gelieferte Zeitangaben werden nie übernommen.
- Reihenfolge-Garantie: Bei Bündeln (Aufruf + mehrere Feld-Deltas)
  bilden sie einen zusammenhängenden, atomar geschriebenen Block.

### 7.4 Was das Audit **nicht** leisten soll

- Kein Ersatz für fachliche Invarianten (Storno statt Löschung gilt
  unabhängig vom Protokoll),
- kein Echtzeit-Alarm-System (Alarme laufen über
  `INotificationWriter`; das Audit ist deren Beleg),
- kein Volltext-Replay der Daten (Vorher/Nachher bleibt Feld-Delta,
  kein Zeitreise-Datenstand).

---

## 8. Konsistenzanforderungen an Folgeartefakte

- **Datenmodell (Schritt 4):** persistente Audit-Entität mit den
  Feldern aus 4.1 + kategorienspezifischen Nutzlasten (JSON-Spalten
  zulässig), Indizes (`OwnerUserId`+`Utc`, `CorrelationId`,
  `McpAccessId`, `Ereignistyp`), Retention-Felder,
  `*BackupDto`-Aufnahme; `AiActionRequest` trägt `CorrelationId`.
- **Architekturkonzept (Schritt 4):** Schreibpfad des Audits
  (transaktional gekoppelt an Toolausführung vs. nachgelagerter
  Writer – Festlegung dort), Bereinigungs-Executor, Verkettungs-
  Reihenfolge unter SQLite-Schreibserialisierung.
- **UI-Konzept (Schritt 4):** Audit-Ansicht je Zugang + Admin-
  Sicherheitssicht + Export + Integritätsprüfung (Abschnitt 6).
- **Approval-Konzept:** liefert die Request-Zustandskette; dieses
  Dokument legt deren Protokollierung fest (3.4).
- **Sicherheitskonzept:** liefert die Sicherheitsereignisse und
  Anomalie-Erkennung; dieses Dokument legt deren
  Aufzeichnungs-/Aggregationsgrad fest (3.5).
