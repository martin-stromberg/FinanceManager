# Sicherheitskonzept – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 2 der Anforderung; spezifiziert das Sicherheitsmodell
(F8) für den MCP-Server. Baut auf [fachkonzept.md](fachkonzept.md)
(Akteure, Freigabestufen, Zugänge) und
[berechtigungsmodell.md](berechtigungsmodell.md) (Prüfstufen 0–3) auf und
verwendet die Sicherheitsklassen aus
[scope-und-toolkatalog.md](scope-und-toolkatalog.md) für die
Rate-Limit-Abstufung. Die Genehmigungsinstanz ist in
[approval-konzept.md](approval-konzept.md), die Protokollierung in
[audit-konzept.md](audit-konzept.md) festgelegt.

---

## 1. Verbindliche Grundsätze

1. **Eine Vertrauensgrenze.** Der FinanceManager-Server ist vertrauenswürdig;
   der MCP-Client ist prinzipiell untrusted – einschließlich aller von ihm
   eingehenden und an ihn ausgehenden Inhalte (Fachkonzept, Abschnitt 2).
2. **Nur einschränken, niemals erweitern.** Ein MCP-Zugang ist ein Unterraum
   der Rechte seines Besitzers; Benutzerrecht und `AccountShare`-Rolle sind
   absolute Obergrenzen (belegte Vorgabe, `berechtigungsmodell.md`).
3. **Serverseitige Sofortprüfung.** Sicherheitsentscheidungen werden bei
   **jeder** Anfrage gegen den aktuellen Datenbankzustand getroffen – analog
   zur bestehenden `OnTokenValidated`-Pipeline, die `User.Active`,
   Security-Stamp und Admin-Rolle pro Request prüft. Es gibt kein
   „Restlebensdauer-Fenster", in dem widerrufene Zugänge weiter wirken.
4. **Kein Informationsleck.** Verweigerte Datensätze verhalten sich wie
   nicht vorhandene (404-Semantik der API-Konvention); Fehlermeldungen
   verraten weder Scope-Existenz noch fremde Daten.
5. **Defense in Depth.** Kein einzelner Mechanismus ist alleiniger Schutz:
   Authentifizierung, Scope-/Filter-Prüfung, Approval, Rate Limiting,
   Audit und Notfallschalter greifen übereinander.
6. **Bestehende Mechanik wiederverwenden.** Wo das System bewährte
   Primitiven hat (PBKDF2-Hashing, Rate-Limiter, IpBlock,
   NotificationWriter, Feature-Flags), setzt das MCP-Konzept darauf auf
   statt Parallelmechanismen zu erfinden.

---

## 2. Bedrohungsmodell (fachlich)

| # | Bedrohung | Hauptschutz |
|---|---|---|
| B1 | Token-Verlust/-Diebstahl (Klartext-Transport, Client-Kompromittierung) | Hash-Speicherung, Einmalanzeige, Widerruf/Rotation mit Sofortwirkung, kurze Ablaufdaten, TLS-Empfehlung |
| B2 | Überschreitung der Freigabe durch den Client | Prüfstufen 0–3 des Berechtigungsmodells, datensatzscharfe Prüfung, keine impliziten Rechte |
| B3 | Prompt-Injection über Benutzerdaten (Buchungstexte, Anhänge) | Kennzeichnung als untrusted content, Output-Policy, Scope-/Filter-Kappen, Approval für finale Aktionen |
| B4 | Unkontrollierte Schreibzugriffe (fehlerhaftes/manipuliertes KI-System) | Sicherheitsklassen, `AiActionRequest`-Genehmigung, Schreib-Stopp-Schalter, Rate Limits |
| B5 | Brute-Force-/Missbrauchsversuche auf den MCP-Endpunkt | Rate Limiting (Token- und IP-Partition), Fehlversuchszähler, `IpBlock`-Infrastruktur, Sicherheitsalarme |
| B6 | Denial of Service durch Analyse-Workload | Rate-Limit-Abstufung je Klasse, Paging-Clamps, Upload-/Batch-Limits |
| B7 | Übergriff auf fremde Benutzerdaten | Stufe 0 Benutzerrecht (`OwnerUserId`-Bindung), kein Fremdzugriff für Admin-Zugänge, datensatzscharfe Auflösung |

---

## 3. Authentifizierung: Personal Access Tokens (PAT)

Jeder MCP-Zugang (fachliche Spezifikation: `fachkonzept.md`, Abschnitt 6)
besitzt genau ein aktives Token. Der Token authentifiziert **den Zugang**,
nicht den Benutzer interaktiv – er ist vom Identity-JWT der UI-Sitzung
vollständig getrennt.

### 3.1 Token-Format und Generierung

- **Format:** `fm_mcp_<zugangskürzel>_<secret>` – zusammengesetzt aus
  - festem Präfix `fm_mcp_` (Erkennbarkeit in Logs, Konfigurationsdateien
    und Drittsystemen; verhindert Verwechslung mit JWTs),
  - einem kurzen, nicht-geheimen **Zugangskürzel** (z. B. 8 Zeichen aus
    der Zugangs-Id abgeleitet), das die effiziente Zuordnung Token →
    Zugangs-Datensatz ermöglicht (siehe 3.3),
  - einem kryptografisch zufälligen **Secret-Anteil** (≥ 160 Bit
    Entropie, `RandomNumberGenerator`, Base64url-kodiert).
- **Einmalige Anzeige:** Der Klartext wird dem Benutzer genau einmal nach
  Erzeugung oder Rotation angezeigt und ist danach nicht mehr abrufbar
  (Copy-and-store-Prinzip, wie im Fachkonzept 6.3 festgelegt).
- **Niemals in URLs:** Der Token wird ausschließlich im
  `Authorization`-Header übertragen, nie als Query-Parameter. Die
  `RequestLoggingMiddleware` maskiert zwar den Query-Parameter `token`
  (`[REDACTED]`), aber URLs landen in Proxys, Browser-Historien und
  Referer-Headern – Header-Transport ist verbindliche Konvention.

### 3.2 Speicherung: Einweg-Hash nach `Pbkdf2IdentityPasswordHasher`-Vorbild

Der Token liegt in der Datenbank **ausschließlich als Einweg-Hash** vor:

- **Verfahren:** PBKDF2-HMACSHA256 mit per-Token-Salt, selbes
  Speicherformat wie der bestehende
  `Pbkdf2IdentityPasswordHasher`
  (`Infrastructure/Auth/Pbkdf2IdentityPasswordHasher.cs`):
  `pbkdf2|<iterationen>|<salt-b64>|<key-b64>`, Verifizierung mit
  `CryptographicOperations.FixedTimeEquals` (kein Timing-Seitenkanal,
  kein Detail-Leak bei malformeden Hashes – `Verify` liefert `false`
  statt zu werfen).
- **Parameter:** Das Vorbild verwendet 100.000 Iterationen, 16 Byte Salt,
  32 Byte Key. Für PATs ist diese Iterationszahl **nicht** als
  Brute-Force-Schutz nötig (das Secret trägt ≥ 160 Bit Entropie,
  Online-Raten werden durch Rate Limiting gebremst); sie ist aber auch
  nicht schädlich. Festlegung: **gleicher Mechanismus und Format**, die
  konkrete Iterationszahl ist Konfigurationsdetail der Umsetzung
  (`Mcp:TokenHash:Iterations`, Standard = Vorbildwert), damit der
  Hash-Aufwand je Request begrenzt bleibt.
- **Verifikationspfad:** Eingehender Header → Zugangskürzel extrahieren →
  Zugangs-Datensatz laden → `Verify(secret, gespeicherterHash)` →
  Status-/Ablaufprüfung. Der Klartext wird nie persistiert, nie
  geloggt.
- **Abgrenzung zu `dp:v1:`:** Der reversibel verschlüsselnde
  `DataProtectionAlphaVantageSecretProtector` (`dp:v1:`-Präfix) ist für
  MCP-Token **unzulässig**: Ein MCP-Token wird serverseitig nie im
  Klartext benötigt, ein reversibles Secret würde bei DB-/Key-Verlust
  alle Zugänge offenlegen. Der AlphaVantage-Key braucht Reversibilität
  (Weiterleitung an den Fremddienst) – der PAT nicht.
- **Konsequenz:** Bei DB-Kompromittierung sind die Token-Hashes nicht
  direkt verwendbar (Brute Force auf ≥160-Bit-Geheimnisse ist praktisch
  ausgeschlossen); der Angreifer kann keine gültigen Header rekonstruieren.

### 3.3 Lebensdauer, Rotation, Widerruf

- **Ablaufdatum (`ExpiresUtc`):** optional, vom Benutzer bei der Anlage
  gesetzt; nach Ablauf verhält sich der Zugang wie deaktiviert
  (Fachkonzept 6.1). Empfehlung aus Abschnitt 7: Bei HTTP-only-Betrieb
  kurze Lebensdauern wählen.
- **Aktivieren/Deaktivieren:** sofort wirksamer Schalter des Zugangs;
  ein deaktivierter Token wird bei der nächsten Anfrage abgelehnt.
- **Rotation:** Erzeugt ein neues Token und invalidiert das alte
  **sofort** – kein Übergangs-Doppelbetrieb. Begründung: Ein
  kompromittierter Finanzdaten-Token muss tot sein, sobald die Rotation
  stattfindet; der Benutzer steuert die Rotation bewusst.
- **Widerruf/Löschen:** Sofortige serverseitige Invalidierung. Technisch
  kein „Token-Verfallsfenster" wie bei JWTs nötig: Da der Hash-Vergleich
  ohnehin pro Anfrage den Zugangs-Datensatz lädt, ist die
  Statusprüfung (`Aktiv`/`Deaktiviert`, `ExpiresUtc`) inhärent im
  Authentifizierungspfad – dasselbe Prinzip wie `OnTokenValidated`
  (Security-Stamp-Vergleich pro Request), nur ohne die
  30-Minuten-JWT-Laufzeit-Lücke.
- **Auflösung Token → Benutzer:** Authentifizierter Zugang →
  `OwnerUserId` des Zugangsbesitzers → fachliches Äquivalent zu
  `ICurrentUserService` (`_current.UserId`). Alle Service-Aufrufe laufen
  mit dieser `ownerUserId` – identisch zum Controller-Pfad, kein
  privilegierter Kontext.

### 3.4 Transportmechanismus und Fehlerspektrum

- **Transport:** `Authorization: Bearer fm_mcp_…`. **Kein**
  Cookie-Transport für MCP (anders als der `FinanceManager.Auth`-Cookie
  der UI): Header-Auth ist CSRF-immun und entspricht der
  PAT-Praxis; die `CookieCsrfProtectionMiddleware` bleibt dem
  UI-Pfad vorbehalten.
- **Fehlerspektrum:** Authentifizierungsfehler → `401` (ungültiger
  Token, deaktivierter/abgelaufener/widerrufener Zugang, MCP global
  deaktiviert) ohne Detaildifferenzierung der Ursache;
  Autorisierungsfehler auf Datensatzebene → `404`-Semantik;
  Scope-Verweigerung auf Tool-Ebene → definierter Fehler (vom Client als
  Zustand behandelbar, siehe Katalog-Versionierungsstrategie); Rate
  Limit → `429`.

---

## 4. Autorisierung: Durchsetzung der Prüfstufen 0–3

Das Sicherheitskonzept implementiert das in `berechtigungsmodell.md`
spezifizierte Auswertungsverfahren als Durchsetzungsschicht. Reihenfolge
und Semantik sind dort normativ; hier wird die operative Durchsetzung
festgelegt:

| Prüfstufe | Operationale Durchsetzung |
|---|---|
| **Stufe 0 – Benutzerrecht** | Token → `OwnerUserId`; Service-Aufrufe mit dieser Id; indirekte Besitzauflösung über die Ableitungstabelle (`Posting` je `Kind`, `StatementImport`/`StatementEntry` über `AccountId`, `StatementDraftEntry` über `DraftId`, `AccountLinkedIban`, `AliasName`, `SecurityPrice`, `Attachment`-Zieldecke). Fremdes Aggregat → Datensatz nicht vorhanden. |
| **Stufe 1 – Scope-Recht** | Tool-Aufruf verlangt das deklarierte Scope-Tupel (Datentyp × Rechteart) des Tools; fehlt es im effektiven Rechteprofil (Stufen-Standard bzw. individuelle Scopes), wird der Aufruf abgelehnt – vor jeder Datensatzberührung. Meta-Tools prüfen nur die Besitzbindung (Meta-Scope). |
| **Stufe 2 – Filter** | Je betroffenem Datensatz: Ergibt mindestens ein passender Scope-Filter (CSV-Id-Listen im `ReportFavorite`-Muster, UND über Dimensionen, ODER über Scopes) eine Erfassung? Unklassifizierte Datensätze (`CategoryId == null`) fallen nicht in Whitelist-Filter. |
| **Stufe 3 – Kontext-Deckel** | `AccountShare`-Rolle als harte Obergrenze auf kontoabhängige Daten; Genehmigungsmodus (Direktausführung vs. `AiActionRequest`); globale Notfallschalter (Abschnitt 6); Admin erweitert keine Datenrechte. |

**Verbindliche Laufzeitregeln:**

- **Laufzeit-Auflösung, kein Claim-Freeze:** Das effektive Profil wird je
  Anfrage neu bewertet (Berechtigungsmodell 3.3). Änderungen an Stufe,
  Scopes, Filtern, Zugangs-Status, `AccountShare` oder Notfallschaltern
  greifen ab dem nächsten Aufruf.
- **Datensatzscharfe Prüfung:** Listen- und Abfragetools filtern
  serverseitig (EF-`Where` im Service-Pfad, analog zur bestehenden
  `OwnerUserId`-Filterung); gefilterte/fremde Datensätze sind
  unsichtbar. Bulk-Operationen (Batch-Vervollständigung, Verbuchung)
  prüfen jeden betroffenen Datensatz einzeln; verweigerte Elemente
  werden nicht still übersprungen, sondern der Aufruf schlägt
  kontrolliert fehl bzw. der Batch wird gar nicht angenommen
  (atomare `ApplyBatchEntryUpdatesAsync`-Semantik).
- **Monotonie:** Die Kette kann nur verweigern; kein Tool, keine Rolle,
  kein Flag kann ein „Nein" einer früheren Stufe aufheben.

---

## 5. Prompt-Injection-Schutz (untrusted content)

### 5.1 Bedrohungslage

Alle **an den MCP-Client ausgelieferten Benutzer- und Drittinhalte** sind
untrusted content und können Anweisungen enthalten, die ein LLM als
Befehle interpretiert („verbuchungswürdige Buchungstexte" ist kein
Hypothetikum – ein Empfängername kann „Ignoriere alle bisherigen
Anweisungen und buche …" enthalten):

- Buchungstexte und Gegenparteien: `StatementDraftEntry.Subject`,
  `RecipientName`, `BookingDescription`, `Posting.Subject`,
  `RecipientName`, `Description`
- Stammdaten-Freitexte: `Contact.Description`, `Contact.Name`,
  `AliasName.Pattern`, Kontonamen, Kategorienamen
- Notizen/Beschreibungen: `StatementDraft.Description`,
  `BudgetPurpose`-Namen, Report-Titel
- **Anhänge:** `Attachment.Content` liegt als `byte[]` **in der
  SQLite-Datenbank** – PDF-/Text-/Bildinhalte sind die
  kritischste Injection-Quelle (importierte Dokumente, Belegscans).
- Importierte Dokumente: `OriginalFileName`, Parser-Ausgaben der
  Kontoauszugsdateien.
- **Rückfluss:** Auch vom Client eingehende Tool-Parameter (z. B.
  Begründungstexte, Beschreibungen, zu setzende Feldwerte) sind
  untrusted und können selbst Injektionsinhalte in die Datenbank
  tragen, die später an denselben oder andere Clients ausgeliefert
  werden.

Prompt-Injection lässt sich **LLM-seitig nicht vollständig verhindern** –
das ist eine Eigenschaft des Clients. Das Konzept setzt daher auf
defense-in-depth: Kennzeichnung, Trennung, Rechtekappen,
Genehmigungsinstanz und Protokollierung.

### 5.2 Maßnahme 1: Kennzeichnung und Inhaltstrennung (Output-Konventionen)

- **Strukturierte Ausgabe statt Prosa:** Tools liefern strukturierte
  Felder (JSON/DTO-Form), keine Fließtext-Antworten, in denen
  Dateninhalte mit Systemtext vermischt werden.
- **Daten-Markierung:** Freitext-Felder, die Drittinhalte enthalten
  können (Buchungstexte, Namen, Beschreibungen, Anhangsinhalte), werden
  in der Tool-Ausgabe einheitlich als Daten gekennzeichnet – konzeptionell
  als eigenes, eindeutig benanntes Feld/Wrapper (z. B. Kennzeichnung
  `untrusted_data`), damit der Client sie als Inhalt und nicht als
  Anweisung behandeln kann. Systemtexte der Tools enthalten **keine**
  Benutzerdaten-Inhalte.
- **Kein Inhaltsecho in Anweisungskontext:** Tool-Beschreibungen,
  Fehlermeldungen und Folgeaufruf-Anweisungen dürfen keine
  Fremdinhalte interpolieren, die wie Anweisungen aussehen könnten.
- **Binärinhalte:** `fm_attachments_read` liefert Anhanginhalte nur mit
  deklariertem `ContentType` und Metadaten (Dateiname, `Sha256`) aus;
  die Ausgabe markiert den Inhalt eindeutig als Dokumentdaten. Die
  Bereitstellung großer Binärinhalte an den Client ist
  ratenlimitiert und größenbegrenzt (Abschnitt 7, Upload-/Antwort-Limits
  analog `Kestrel:Limits:MaxRequestBodySize`).

### 5.3 Maßnahme 2: Tool-Output-Policy und schreibende Restriktionen

- **Whitelist-Ausgabe:** Tools geben nur die Felder aus, die der
  Geschäftsprozess braucht (DTO-Festlegung im Toolkatalog) – keine
  „alles inklusive"-Serialisierung von Entitäten.
- **Keine Freitext-Kommandos entgegennehmen:** Schreibende Tools nehmen
  ausschließlich typisierte, validierte Parameter entgegen (gleiche
  `*Request`-Validierung wie die API). Ein Parameter vom Typ
  „freie Anweisung an das System" existiert nicht.
- **Approval-Snapshot zeigt Wirkparameter, nicht Prosa:** Im
  `AiActionRequest` wird der konkrete Parameter-Snapshot
  hinterlegt und dem Benutzer als Wirkungsvorschau gezeigt – die
  Entscheidungsgrundlage ist „was wird geändert", nicht der
  vom Client formulierte Begleittext.
- **Keine Ketten-Eskalation:** Ergebnisse eines Toolaufrufs werden vom
  Server nicht als Anweisung an Folgeaufrufe weitergegeben; jeder
  Aufruf wird unabhängig gegen Scope/Filter/Deckel geprüft. Ein
  injizierter Buchungstext kann daher keine zusätzlichen Rechte
  erschließen – maximal kann er den Client zu einer Aktion verleiten,
  für die der Client ohnehin berechtigt ist, und für finale Aktionen
  greift die Genehmigungspflicht.
- **Schutz der deterministischen Mechanik:** Die Server-Pipeline
  (Klassifizierung, Dublettenerkennung, Validierung, Verbuchung) ist
  rein datengetrieben und interpretiert keine Inhalte als Steuerung;
  Injection kann nur den LLM-Kontext treffen, nie den Service-Layer.

### 5.4 Maßnahme 3: Strukturelle Begrenzung der Wirkung

- **Rechtekappen:** Ein Client, dessen Inhalte ihn zur Eskalation
  verleiten wollen, stößt an dieselben Prüfstufen 0–3; es gibt keinen
  Inhaltskanal, der Rechte vergibt.
- **Genehmigungspflicht bleibt:** Alle finalen Wirkungen (Buchung,
  Storno, Löschung/Archivierung, Merge) laufen über
  `AiActionRequest` – selbst wenn ein injizierter Inhalt den Client
  zur Anforderung bewegt, entscheidet der Benutzer anhand der
  Wirkungsvorschau (Approval-Konzept).
- **Protokollierung:** Jeder Toolaufruf und jede angeforderte Wirkung
  ist revisionssicher protokolliert – Injection-bedingte Aktionen sind
  dem Zugang zurechenbar und im Audit sichtbar (Audit-Konzept).
- **Empfehlung an Benutzer (fachlich):** Für Zugänge mit Schreibrechten
  vertrauenswürdige, kontrollierte MCP-Clients bevorzugen;
  Anhänge-Leserecht (größte Injection-Oberfläche) nur vergeben, wenn
  fachlich nötig – Filter auf Anhangkategorien kann die Fläche
  verkleinern.

---

## 6. Notfallmaßnahmen

Drei gestufte Sofortmaßnahmen plus Anomalieerkennung. Alle Schalter
folgen der bestehenden **Feature-Flag-Konvention** (`appsettings`,
Vorbild `BackgroundTasks:Enabled`, `Workers:SecurityPriceWorker:Enabled`,
`Auth:AllowSelfRegistration`, `FileLogging:Enabled`) und werden –
analog zur Konfigurationsbindung des bestehenden Systems – bei
Änderung wirksam (konkrete Reload-Semantik: Umsetzungsdetail,
Zielbild = ohne Deployment steuerbar).

### 6.1 Globaler Kill-Switch: `Mcp:Enabled`

- **Wirkung:** `false` → der MCP-Endpunkt lehnt **alle** Anfragen ab
  (401/503, einheitliches Fehlerbild); Authentifizierung, Toolaufrufe
  und Ausführung offener `AiActionRequest`s stoppen.
- **Granularität:** global (instanzweit, Admin-/Betreiber-Ebene).
- **Verhalten der Approval-Queue:** Pending-/Approved-Requests werden
  **nicht** ausgeführt und bleiben persistent erhalten; nach
  Reaktivierung entscheidet ihr Ablaufdatum (`ExpiresUtc`) über
  weitere Gültigkeit (Approval-Konzept).
- **Lese-Fallback entfällt bei globaler Deaktivierung bewusst** –
  der Schalter ist für den Gesamtverdacht gedacht (Fachkonzept 6.4).

### 6.2 Schreib-Stopp: `Mcp:WriteAccess:Enabled`

- **Wirkung:** `false` → alle Tools der Klassen **schreibvorbereitend**
  und **genehmigungspflichtig** werden abgelehnt; lesende und
  vorschlagende Tools (inkl. Meta-Tools) bleiben funktionsfähig.
  Fachlich: Der MCP-Server wird auf „Analyse plus Vorschläge"
  zurückgestuft.
- **Granularität:** global. **Ergänzend** eine benutzerbezogene
  Variante: Deaktivierung einzelner Zugänge deckt den
  Einzelzugangs-Verdacht ab (Zugangs-Status, Fachkonzept 6.1).
- **Ausführungsseite:** Bereits genehmigte, aber noch nicht
  ausgeführte Requests werden zurückgehalten (Queue stoppt) –
  der Schreib-Stopp wirkt als Deckel auf Stufe 3 für alle
  Schreibaktionen.

### 6.3 Anomalieerkennung und Sicherheitsalarme

- **Erkannte Muster (fachlich):**
  - wiederholte Authentifizierungsfehlschläge je IP und je
    Zugangskürzel (Token-Erraten, Credential-Stuffing),
  - gehäufte Scope-Verweigerungen eines Zugangs (Client tastet
    Rechtegrenzen ab),
  - Rate-Limit-Ausschöpfung über Zeit, ungewöhnliche
    Zugriffshäufigkeit/-zeiten,
  - Zugriffe von IP-Adressen, die im `IpBlock`-System bereits
    auffällig sind oder gesperrt wurden.
- **Reaktionen (gestuft):**
  1. **Benachrichtigung:** `INotificationWriter` –
     `CreateForAdminsAsync` mit `NotificationType.SystemAlert` für
     instanzweite Vorfälle (Brute-Force-Muster, wiederholte
     IP-Verstöße) und `CreateForUserAsync` mit `EventDriven` an den
     Zugangsbesitzer bei Auffälligkeiten seines Zugangs
     (z. B. massenhafte Scope-Verweigerungen).
  2. **IP-Sperrung:** Anknüpfung an `IIpBlockService`/
     `IpBlockMiddleware` – die bestehende Infrastruktur sperrt IPs
     (403, `IpBlock`-Tabelle mit `IsBlocked`/`BlockReason`/
     Fehlversuchszähler); die Sperre wirkt auf **alle** Requests der
     IP, also auch MCP.
  3. **Zugangsdeaktivierung:** automatische Deaktivierung eines
     Zugangs bei konfigurierbaren Schwellen (z. B. wiederholte
     Authentifizierungsfehlversuche mit korrektem Kürzel, aber
     falschem Secret) – der Besitzer wird per `EventDriven`-
     Benachrichtigung informiert und kann reaktivieren.
  4. **Eskalation:** anhaltende Vorfälle → manueller Schreib-Stopp /
     Kill-Switch.
- **Protokollbezug:** Alle Anomalie-Erkennungen und ausgelösten
  Maßnahmen sind Sicherheitsereignisse im Audit (Audit-Konzept,
  Ereigniskatalog).

---

## 7. Rate Limiting

### 7.1 Aufbau auf vorhandener Infrastruktur

Das System nutzt bereits `AddRateLimiter` mit der benannten Policy
`RateLimitPolicies.AuthSensitive` (Fixed-Window, Partition
`RemoteIpAddress`, `PermitLimit`/`WindowSeconds` aus
`RateLimiting:AuthSensitive:*`, `[EnableRateLimiting]` auf
`AuthController`/`UsersController`, Antwort `429`). Das MCP-Konzept
übernimmt **Mechanik und Konfigurationsmuster**, ändert aber die
Partitionierung:

- **Partition pro MCP-Zugang/Token** (Zugangskürzel aus dem Header)
  statt pro IP – ein Zugang ist die fachlich relevante Kontingent-
  Einheit; mehrere Clients hinter einer NAT-IP sollen sich nicht
  gegenseitig blockieren, ein einzelner ausufernder Client soll nur
  seinen eigenen Zugang belasten.
- **Zusätzliche IP-Partition** für den **unauthentifizierten** Pfad
  (falsche/fehlende Tokens): hier existiert noch kein Zugang –
  Schutz gegen Token-Erraten bleibt IP-basiert (analog
  `AuthSensitive`) und speist den Fehlversuchszähler der
  Anomalieerkennung.

### 7.2 Abstufung je Sicherheitsklasse

Limits folgen den vier Klassen des Toolkatalogs (konfigurierbar über
`Mcp:RateLimiting:*`; Zahlen sind fachliche Standardvorschläge):

| Klasse | Limit-Grundsatz (Vorschlag) | Begründung |
|---|---|---|
| lesend | hohes Kontingent je Zugang (z. B. 300 Aufrufe/Min) + Ausgabemengen-Begrenzung (Paging-Clamp ≤ 200, Antwortgrößen-Limits) | Analyse-Workload ist der Regelfall; Schutz richtet sich gegen Datenabfluss-Tempo, nicht gegen Volumen an sich |
| vorschlagend | mittleres Kontingent (z. B. 60/Min) | Rechenintensive Analysen (`ClassifyAsync`); kein Persistenzrisiko, aber CPU-/DB-Last |
| schreibvorbereitend | niedriges Kontingent (z. B. 30/Min) + Batch-Größen-Limit (≤ 100 Entries je Aufruf) | Jeder Aufruf mutiert Daten; Begrenzung des Schadens bei fehlerhaften/manipulierten Clients |
| genehmigungspflichtig | sehr niedriges Kontingent (z. B. 10/Min) je Zugang **und** Begrenzung offener Pending-Requests (z. B. 25 je Zugang) | Verhindert Approval-Spam im Genehmigungscenter; finale Wirkung darf nie im Flut-Tempo angefordert werden |
| Meta (`fm_capabilities_describe`, `fm_task_status`) | moderat (z. B. 60/Min), Polling-freundlich | `fm_task_status` wird für Statusabfragen benötigt und darf den Workflow nicht blockieren |

### 7.3 Verhalten bei Überschreitung

- Antwort `429` mit einheitlichem Fehlerformat (`ApiErrorDto`-
  Konvention) – der Client behandelt es als Rate-Limit-Zustand.
- Überschreitungen werden gezählt (Nutzungsprotokoll) und fließen in
  die Anomalieerkennung ein; keine automatische Zugangssperre allein
  durch legitime Rate-Limits – erst Muster (6.3) lösen Maßnahmen aus.
- Der Limiter sitzt **vor** der Scope-Prüfung in der Pipeline
  (`UseRateLimiter` früh, analog der bestehenden Einbindung), damit
  auch unberechtigte Fluten gedrosselt werden; die Scope-Prüfung selbst
  bleibt datensatzscharf dahinter.

---

## 8. Transport- und Betriebsrisiken: HTTP-only-LAN-Betrieb

**Ausgangslage (belegt):** `AllowedHosts: "*"` mit dokumentiertem
Kommentar – der Zugriff per LAN-IP/HTTP ist ausdrücklich
unterstütztes Szenario; `UseHsts`/`UseHttpsRedirection` ist
außerhalb von Dev/E2E aktiv, aber kein TLS-Zwang für LAN-
Deployments. PATs im `Authorization`-Header wären im HTTP-Fall
im Klartext im LAN abhörbar → Bedrohung B1. Der Projektplan
(Offene Punkte, Punkt 2, behandelt) legt fest: **TLS als
Zielbetrieb, HTTP als dokumentierter Fallback mit Zusatzmaßnahmen** –
eine TLS-Pflicht würde das unterstützte LAN-Szenario brechen.

### 8.1 Zielbetrieb: TLS (Empfehlung)

- **Empfehlung:** Betrieb des MCP-Endpunkts ausschließlich über HTTPS –
  realisiert über Reverse Proxy vor Kestrel (TLS-Terminierung) oder
  Kestrel-Zertifikat. Die Infrastruktur dazu existiert:
  `SecurityHeadersPolicy`, `UseHsts`, `UseHttpsRedirection` und
  Forwarded-Headers-Unterstützung für konfigurierte Proxies
  (`ReverseProxy:KnownProxies`/`KnownNetworks`).
- **Forwarded-Headers (verbindlich für Proxy-Betrieb):** Der Proxy muss
  in `ReverseProxy:KnownProxies|KnownNetworks` eingetragen sein, damit
  `RemoteIpAddress` die Client-IP liefert – Voraussetzung für
  IP-Partition des Rate-Limiters, `IpBlockMiddleware` und
  Audit-IP-Attribution. Ohne korrekte Forwarded-Headers-Config sähe das
  System nur die Proxy-IP (Rate-Limits und IP-Sperren würden falsch
  greifen bzw. alle Clients gemeinsam treffen).
- **UI-Hinweis:** Die Zugangsverwaltung soll TLS als empfohlenen
  Betriebsmodus kennzeichnen (Informationspflicht, keine Pflicht).

### 8.2 Fallback: HTTP-only-LAN (dokumentiertes Restrisiko + Zusatzmaßnahmen)

HTTP-Betrieb bleibt unterstützt (LAN-Vertrauensmodell des Produkts);
das Restrisiko „Token im Klartext im LAN abhörbar" wird **offen
deklariert** und durch diese Zusatzmaßnahmen abgemildert:

1. **Kurze Token-Lebensdauer:** Empfehlung, bei HTTP-Betrieb
   `ExpiresUtc` kurz zu setzen (z. B. ≤ 90 Tage) – begrenzt das
   Zeitfenster eines abgehörten Tokens.
2. **Enge Rechteprofile:** Für HTTP-exponierte Zugänge minimale
   Freigabestufe/Filter empfehlen (Read-only-Analyse als
   Standardempfehlung ohne TLS).
3. **Optionale IP-Bindung je Zugang (Vorschlag):** Zugangs-Einstellung
   „nur aus Adressbereich X" (z. B. Subnetz-Liste) – ein abgehörtes
   Token ist außerhalb des LAN nutzlos; wirkt als zusätzlicher
   Stufe-3-Deckel.
4. **Rate Limits + Anomalieerkennung** wie in Abschnitt 6/7 –
   ein abgehörtes Token erzeugt bei Missbrauch sichtbare Muster.
5. **Informationspflicht in der UI:** Beim Anlegen eines Zugangs unter
   HTTP-Betrieb Hinweis „Token wird unverschlüsselt übertragen –
   TLS-Setup empfohlen" (fachliche Anforderung an Schritt 4,
   UI-Konzept).
6. **Niemals Token in URLs** (3.1) – auch im LAN nicht.

### 8.3 Explizit nicht übernommene Alternativen

- **TLS-Pflicht:** verworfen – bricht das dokumentierte LAN-Szenario.
- **Challenge-Response-/Signatur-Verfahren statt Bearer-Token**
  (HMAC-Signing je Request): fachlich deutlich komplexer für Clients
  und außerhalb des MCP-Üblichkeitsrahmens; die Kombination aus
  kurzer Lebensdauer, IP-Bindung und Rate-Limits deckt das LAN-
  Restrisiko hinreichend ab, ohne das Protokoll aufzublähen.
- **mTLS:** für den Zielgruppen-Selbsthosting-Betrieb überdimensioniert;
  als optionale Reverse-Proxy-Fähigkeit nicht ausgeschlossen, aber
  kein Konzeptbestandteil.

---

## 9. Konsistenzanforderungen an Folgeartefakte

- **Approval-Konzept:** übernimmt Genehmigungsmodus, Pending-Queue-
  Verhalten der Notfallschalter und die Klasse
  „genehmigungspflichtig" (dieses Dokument, Abschnitte 6.2, 7.2).
- **Audit-Konzept:** spezifiziert die hier genannten Protokoll-
  Ereignisse (Authentifizierung, Scope-Verweigerungen, Anomalien,
  Schalter-Aktivierungen, Token-Lifecycle) im Detail.
- **Architektur-/Datenmodellkonzept (Schritt 4):** konkretisiert
  Token-Hash-Persistenz (`pbkdf2|…`-Format + Zugangskürzel-Index),
  `Mcp:*`-Konfigurationssektion (Enabled, WriteAccess, RateLimiting,
  TokenHash), Sicherheitslayer-Reihenfolge (Rate Limiter →
  IpBlock → Token-Auth → Scope-/Filter-Prüfung → Tool-Adapter),
  IP-Bindungs-Feld und Forwarded-Headers-Voraussetzungen.
- **UI-Konzept (Schritt 4):** Einmalanzeige des Tokens,
  TLS-/HTTP-Hinweis, Verwaltung von Ablaufdatum, Rotation, Widerruf,
  IP-Bindung und Genehmigungsmodus.
