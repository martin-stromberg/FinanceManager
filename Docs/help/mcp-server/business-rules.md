← [Zurück zur Übersicht](index.md)

# MCP-Server — Business Rules

## Token identifiziert Zugang und Besitzer

**Beschreibung:** Jeder Aufruf am MCP-Endpunkt authentifiziert sich über das Personal Access Token eines MCP-Zugangs; der handelnde Benutzer wird ausschließlich aus dem Zugang aufgelöst.

**Bedingungen:**
- Das Token hat das Format `fm_mcp_<hint>_<secret>` und ist einem aktiven, nicht abgelaufenen Zugang zugeordnet.
- Es existiert kein Parameter, mit dem ein Client eine andere Benutzer- oder Zugangs-ID wählen könnte.

**Verhalten:**
- Alle Rechte- und Sichtbarkeitsprüfungen laufen mit der `OwnerUserId` des Zugangsbesitzers.
- Ungültige, deaktivierte, widerrufene oder abgelaufene Tokens werden sofort mit `401` abgelehnt.

**Umsetzung:** `McpAuthHandler` (Authentifizierungsschema `McpPat`), `IMcpAccessContext`, `McpAccess.IsCurrentlyUsable`.

## Rechteprüfung ist monoton einschränkend

**Beschreibung:** Für jeden Werkzeugaufruf entscheiden vier aufeinander aufbauende Prüfstufen — Benutzerrecht, Scope-Recht, Filter, Kontext-Deckel. Jede Stufe kann die zulässige Menge nur verkleinern, nie erweitern.

**Bedingungen:**
- Stufe 0 (Benutzerrecht): Der Besitzer hat Zugriff auf den Datensatz (Besitz oder `AccountShare`-Freigabe).
- Stufe 1 (Scope-Recht): Das effektive Rechteprofil enthält einen Scope aus Datentyp × Rechteart.
- Stufe 2 (Filter): Ein auf dem Scope gesetzter Filter erfasst den Datensatz.
- Stufe 3 (Kontext-Deckel): Freigaberolle, Genehmigungsmodus und globale Schalter lassen die Aktion zu.

**Verhalten:**
- Scopes sind ausschließlich positiv (erlaubend); es gibt keine Deny-Scopes und keine Rechtevererbung zwischen Datentypen.
- Mehrere Scopes auf denselben Datentyp und dieselbe Rechteart vereinigen ihre Filtermengen (ODER); mehrere Filterdimensionen innerhalb eines Scopes schränken kumulativ ein (UND). Unklassifizierte Datensätze erfasst ein Kategoriefilter nicht.
- Verweigerte oder ausgefilterte Datensätze verhalten sich wie nicht vorhandene (kein Informationsleck).
- Die Auswertung erfolgt bei jedem Aufruf neu: Änderungen an Stufe, Profil, Filtern, Zugangsstatus oder Kontofreigaben greifen ab der nächsten Anfrage.

**Umsetzung:** `IMcpScopeEvaluator`, `McpScopeMatrix`, `McpRecordScopeFilter`.

## Kontofreigaben deckeln MCP-Rechte

**Beschreibung:** Für per `AccountShare` geteilte Konten bildet die Freigaberolle die absolute Obergrenze aller MCP-Rechte auf kontoabhängige Daten (Buchungen, Auszugsdaten, verknüpfte IBANs, zugeordnete Entwürfe).

**Bedingungen:**
- Der Datensatz hängt an einem Konto, das dem Besitzer nicht gehört, ihm aber geteilt ist.

**Verhalten:**
- Rolle `Read` erlaubt nur Lesen; `Write` zusätzlich Vorschlagen, Vervollständigen, Ändern, Erstellen und Buchen im kontoabhängigen Kontext; `Admin` zusätzlich Löschen und Storno.
- Ein die Rolle überschreitender Scope wird datensatzweise verweigert — eigene Konten des Besitzers bleiben vom selben Scope erfasst.
- Endet oder ändert sich die Freigabe, greift die neue Obergrenze sofort.

**Umsetzung:** `McpScopeEvaluator` über `AccountShare`/`AccountShareRole`.

## Sicherheitsklassen und Genehmigungspflicht

**Beschreibung:** Jedes Werkzeug gehört zu genau einer Sicherheitsklasse; die Klasse bestimmt Rate-Limit, Schreib-Stopp und Genehmigungsweg.

**Bedingungen:**
- Klassen: `Lesend`, `Vorschlagend`, `Schreibvorbereitend`, `Genehmigungspflichtig`.
- Der Genehmigungsmodus des Zugangs (`PerAction` oder `PreApproved`) steuert, ob eine Anforderung im Genehmigungscenter landet oder als vorab genehmigt direkt in die Warteschlange geht.

**Verhalten:**
- Genehmigungspflichtige Werkzeuge (Verbuchung, Storno, Verwerfen) erzeugen immer einen `AiActionRequest` — niemals eine „stille" Direktausführung.
- Im Modus `PreApproved` bleibt die Genehmigungsinstanz dokumentiert auf dem Datensatz; die Anforderung läuft ohne Wartephase in die Ausführungswarteschlange.
- Der Parametersatz einer Anforderung ist zum Anfragezeitpunkt eingefroren und unveränderlich; eine Genehmigung kann nur erteilen oder ablehnen, niemals editieren.
- Deaktiviert der Administrator den Schreibzugriff, werden die Klassen `Schreibvorbereitend` und `Genehmigungspflichtig` vollständig abgelehnt; genehmigte, aber zurückgehaltene Anforderungen können ablaufen.

**Umsetzung:** `McpToolSecurityClass`, `IMcpApprovalGate`, `McpApprovalMode`, `AiActionRequest.ParameterSnapshot`.

## Token-Sicherheit

**Beschreibung:** Das Zugriffstoken ist ein Personal Access Token mit striktem Lebenszyklus; der Klartext wird nie persistiert.

**Bedingungen:**
- Gespeichert wird ausschließlich ein PBKDF2-Hash (`pbkdf2|<iters>|<salt>|<key>`); der Klartext verlässt den Server nur in der Anlage- und Rotationsantwort.
- `AccessKeyHint` ist der nicht geheime, global eindeutige Suchschlüssel des Tokens.

**Verhalten:**
- Anlage und Rotation zeigen den Klartext genau einmal; eine erneute Abfrage ist nicht möglich.
- Rotation ersetzt `AccessKeyHint` und `TokenHash` ohne Karenzzeit und macht das alte Token sofort ungültig; sie ist zugleich der Reaktivierungsweg widerrufener Zugänge.
- `ExpiresUtc` begrenzt die Gültigkeit optional zeitlich; `AllowedIpRanges` (CSV aus CIDR-Bereichen oder Einzeladressen) bindet den Zugang an Client-Adressen.
- Deaktivierung und Widerruf wirken ab der nächsten Anfrage; der Widerruf ist ein Endzustand mit dokumentiertem Grund.
- Backups enthalten weder Hash noch Hint; wiederhergestellte Zugänge werden als widerrufen angelegt.

**Umsetzung:** `McpTokenIssuer`, `McpTokenHasher`, `McpIpRangeMatcher`, `McpAccess.RotateToken`/`Revoke`, `McpAccessBackupDto`.

## Ungeprüfte Inhalte bleiben Daten

**Beschreibung:** Alle vom KI-System gelieferten und an es ausgegebenen Inhalte gelten als ungeprüft (untrusted) — Schutz gegen Prompt Injection in beide Richtungen.

**Bedingungen:**
- Freitexte wie die Begründung einer Anforderung oder Name und Beschreibung eines Zugangs stammen vom Client beziehungsweise aus Benutzereingaben.

**Verhalten:**
- Begründungstexte werden im Genehmigungscenter als „ungeprüfter Inhalt" gekennzeichnet angezeigt.
- Werkzeugantworten trennen Freitexte in markierte `untrusted_data`-Wrapper, damit der Client sie als Daten und niemals als Anweisung behandelt.

**Umsetzung:** `untrusted_data`-Wrapper in Tool-Antworten (z. B. `FmCapabilitiesDescribeTool`), Kennzeichnung im `McpApprovalsPanel`.

## Audit ist append-only und verkettet

**Beschreibung:** Jede relevante Aktion erzeugt einen revisionssicheren Audit-Eintrag; die Einträge bilden je Besitzer (sowie einer globalen Kette) eine Hash-Kette.

**Bedingungen:**
- Jeder Eintrag trägt `SequenceNo`, `PrevHash` und `EntryHash` über den kanonisierten Inhalt.
- Nutzlasten enthalten niemals Tokenmaterial oder Binärinhalte, nur Metadaten und Hashes.

**Verhalten:**
- Die Anwendung ändert oder löscht Einträge niemals; allein der Retention-Job entfernt alte Einträge und dokumentiert die Kürzung durch `RetentionPurged`-Ankereinträge, sodass die Kette „ab diesem Eintrag" verifizierbar bleibt.
- `GET /api/mcp-audit/verify` prüft lückenlose Sequenz, Verkettung und Eintragshashes der eigenen Kette.
- Feldänderungen liegen als eigene `McpAuditEntryChange`-Zeilen vor (Vorher/Nachher, drillingfähig nach Entität).

**Umsetzung:** `IMcpAuditWriter`, `McpAuditQueryService.VerifyAsync`, `McpAuditRetentionExecutor`.

## Globale Schalter und Voreinstellung

**Beschreibung:** Zwei datenbankgestützte Schalter (`McpSettings`) kappen die gesamte MCP-Fläche beziehungsweise alle Schreibwirkung; beide stehen im Auslieferungszustand auf `false`.

**Bedingungen:**
- `Enabled` und `WriteAccessEnabled` werden von Administratoren auf `/card/mcp-security` gesetzt.

**Verhalten:**
- `Enabled = false`: `/mcp` lehnt alle Anfragen mit `503` ab, bevor Authentifizierung läuft; die Navigationseinträge werden ausgeblendet.
- `WriteAccessEnabled = false`: Werkzeuge der Klassen `Schreibvorbereitend` und `Genehmigungspflichtig` werden abgelehnt; Lese-, Vorschlags- und Meta-Werkzeuge bleiben nutzbar.
- Änderungen wirken sofort und werden als Sicherheitsereignis protokolliert.

**Umsetzung:** `McpSettingsService`, `McpDisabledMiddleware`, `IMcpApprovalGate`.

## Aufbewahrungsfristen des Protokolls

**Beschreibung:** Ein Retention-Job löscht Audit-Einträge gestaffelt nach Kategorie — der einzige Löschpfad auf der Protokolltabelle.

**Bedingungen:**
- Kurze Frist (Standard 12 Monate): `ToolCall`, `AccessEvent`, `SecurityEvent`.
- Lange Frist (Standard 36 Monate): `ApprovalEvent`, `DataChange`.

**Verhalten:**
- Der Lauf erfolgt periodisch (Standard alle 720 Minuten); die gelöschten Abschnitte bleiben durch `RetentionPurged`-Ankereinträge in der Hash-Kette dokumentiert.

**Umsetzung:** `McpAuditRetentionService`/`McpAuditRetentionExecutor`, `McpOptions.Retention`.
