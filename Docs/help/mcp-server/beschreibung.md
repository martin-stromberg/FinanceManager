← [Zurück zur Übersicht](index.md)

# MCP-Server — Beschreibung

## Zweck

Der MCP-Server öffnet FinanceManager für externe KI-Systeme (z. B. ChatGPT, Claude, Copilot oder lokale LLMs). Über das Protokoll MCP (Model Context Protocol) greift ein KI-System kontrolliert auf die Finanzdaten seines Benutzers zu — immer im Namen und unterhalb der Rechte des Benutzers, niemals darüber hinaus.

Der Zugriff erfolgt ausschließlich über fachliche Werkzeuge (Tools) auf denselben Anwendungsdiensten, die auch die Weboberfläche nutzt. Es gibt keinen direkten Datenbankzugriff und keinen alternativen Schreibpfad: Buchungen entstehen weiterhin nur durch die Verbuchung von Kontoauszugsentwürfen, Rücknahmen nur als Storno.

## Funktionsweise

### Aktivierung durch den Administrator

Die MCP-Integration ist standardmäßig deaktiviert (Opt-in). Ein Administrator schaltet sie auf der Seite **MCP-Sicherheit** (`/card/mcp-security`) über zwei getrennte Schalter frei:

- **MCP aktiviert**: Der Endpunkt `/mcp` nimmt Anfragen an; deaktiviert antwortet er mit `503`.
- **Schreibzugriff aktiviert**: Werkzeuge mit Schreibwirkung werden zugelassen; deaktiviert bleiben Lese-, Vorschlags- und Meta-Werkzeuge nutzbar.

Die Seite zeigt Administratoren außerdem eine Sicherheitsübersicht über alle Zugänge der Installation — ausschließlich Metadaten (Status, Stufe, Nutzungszeitpunkte, Sicherheitsereignisse), keine Fachdaten und keine Tokeninhalte anderer Benutzer.

### MCP-Zugang anlegen

Jeder Benutzer verwaltet seine eigenen Zugänge auf der Seite **MCP-Zugänge** (`/list/mcp-accesses`). Ein Zugang ist ein benutzerbezogenes Berechtigungsobjekt mit eigenem Token — fachlich ein „API-Benutzerkonto im Namen des Benutzers". Mehrere Zugänge pro Benutzer sind vorgesehen, etwa getrennt nach KI-System oder Zweck, damit Widerruf und Rechte granular bleiben.

Der Anlage-Assistent führt in drei Schritten:

1. **Stammdaten**: Name (eindeutig je Benutzer), optionale Beschreibung sowie in den erweiterten Angaben ein optionales Ablaufdatum und eine IP-Bindung (kommagetrennte Liste erlaubter Client-Adressen und -Bereiche).
2. **Freigabestufe**: Eine der fünf Stufen sowie der Genehmigungsmodus — genehmigungspflichtige Aktionen werden je Aktion freigegeben oder als dauerhaft vorab genehmigt behandelt.
3. **Token**: Das erzeugte Zugriffstoken wird genau einmal im Klartext angezeigt und muss sofort in die Konfiguration des KI-Systems übernommen werden. Ohne TLS-Verschlüsselung weist die Oberfläche auf das Risiko der Klartextübertragung hin.

Die fünf Freigabestufen im Überblick:

| Stufe | Wirkung |
|-------|---------|
| **Analyse** | Reines Auswertungsprofil: Leserechte auf allen Datentypen, keine Schreibwirkung. |
| **Assistent** | Lesen plus Vorschläge (z. B. Kategorisierungs- und Regelvorschläge); Vorschläge werden nicht gespeichert. |
| **Sachbearbeiter** | Lesen, Vorschlagen sowie Vervollständigen, Ändern und Erstellen auf Kontoauszugsentwürfen und vervollständigenden Stammdaten; keine Buchung, kein Löschen. |
| **Buchhalter** | Volles Prozessrecht inklusive Verbuchung und Storno — stets an die Genehmigungsregeln gebunden. |
| **Individuell** | Frei zusammengesetztes Rechteprofil aus Scope-Zeilen (Datentyp × Rechteart × Filter). |

Bei Stufe **Individuell** pflegt der Benutzer das Rechteprofil im Rechte-Editor: Je Zeile stehen ein Datentyp, eine Rechteart und optionale Filter (Kontotypen sowie Kontakt-, Wertpapier-, Sparplan- und Anhangkategorien) zur Verfügung.

### Zugang verwalten

In der Detailkarte eines Zugangs (`/card/mcp-accesses/{id}`) stehen bereit:

- **Aktivieren/Deaktivieren**: sofort wirksame Umschaltung; ein deaktiviertes Token wird ab der nächsten Anfrage abgelehnt.
- **Token rotieren**: erzeugt ein neues Token (wieder einmalige Klartextanzeige) und macht das alte sofort ungültig. Die Rotation ist zugleich der Reaktivierungsweg eines widerrufenen Zugangs.
- **Widerrufen**: endgültige Sperrung mit dokumentiertem Grund; das Protokoll des Zugangs bleibt erhalten.
- **Nutzung**: Aufrufzähler, Erfolgs- und Fehlerquoten, häufigste Werkzeuge und die letzten Aufrufe aus dem Protokoll, mit Verweis in die Audit-Timeline.
- **Monitoring**: Warteschlangentiefe, Alter der ältesten offenen Anforderung und Ergebniszähler der Anforderungen des Zugangs.

### Genehmigungscenter

Werkzeugaufrufe mit hoher Wirkung — etwa Verbuchung, Storno oder das Verwerfen von Entwürfen — erzeugen je nach Stufe und Genehmigungsmodus eine **Anforderung**. Diese erscheint im Genehmigungscenter (`/card/mcp-approvals`), erreichbar über die Zugangsliste oder das Badge des Navigationseintrags (Anzahl offener Anforderungen).

Jede Anforderung enthält Werkzeugname, den eingefrorenen Parametersatz zum Zeitpunkt der Anfrage, eine serverseitige Auswirkungsvorschau sowie die optionale Begründung des KI-Systems — ausdrücklich als ungeprüften Inhalt gekennzeichnet. Der Benutzer entscheidet einzeln oder im Stapel (bis zu 50 Anforderungen) über **Genehmigen** oder **Ablehnen**; eine eigene noch offene Anforderung lässt sich zurückziehen. Genehmigte Anforderungen gehen in eine Warteschlange und werden genau einmal über den regulären Anwendungsdienst ausgeführt; Erfolg, Fehlschlag oder Ablauf sind im Protokoll nachverfolgbar. Das KI-System erfragt den Status über das Werkzeug `fm_task_status` oder erhält Statusmeldungen über den Server-Ereignisstrom des `/mcp`-Endpunkts.

### Audit-Timeline

Die Audit-Timeline (`/card/mcp-audit`) zeigt das revisionssichere Protokoll aller MCP-Ereignisse des eigenen Benutzers: Werkzeugaufrufe, Datenänderungen inklusive Feld-Vorher/Nachher-Werten, Zugangsereignisse (Anlage, Rotation, Widerruf), Genehmigungsentscheidungen und Sicherheitsereignisse. Filter stehen für Zeitraum, Kategorie, Zugang, Werkzeug, Ergebnis und Korrelations-ID zur Verfügung; die Korrelations-ID verknüpft Werkzeugaufruf, Anforderung und Protokolleinträge. Über die Kettenprüfung wird die Integrität der Hash-Kette verifiziert; das Protokoll kann als CSV- oder JSON-Datei exportiert werden.

## Beispiele

- Ein Benutzer legt den Zugang „ChatGPT Analyse" auf Stufe **Analyse** an, kopiert das einmalig angezeigte Token in die ChatGPT-Konfiguration und lässt Ausgaben auswerten — ohne jede Schreibmöglichkeit.
- Ein lokales LLM erhält einen Zugang auf Stufe **Sachbearbeiter** mit Ablaufdatum in 30 Tagen und IP-Bindung an das Heimnetz; es vervollständigt Kontoauszugsentwürfe, darf aber nicht buchen.
- Ein **Buchhalter**-Zugang im Modus „je Aktion genehmigen" löst eine Verbuchung aus; der Benutzer prüft die eingefrorenen Parameter im Genehmigungscenter und gibt frei — die Ausführung erfolgt danach aus der Warteschlange.
- Ein Benutzer hat ein Token verloren und rotiert es; das alte Token ist ab sofort ungültig.
- Ein Administrator bemerkt auffällige Sicherheitsereignisse und deaktiviert über `/card/mcp-security` den Schreibzugriff für die gesamte Installation, während die Lesezugriffe weiterlaufen.

## Einschränkungen

- Die MCP-Integration ist ein Opt-in-Feature: Solange die Schalter nicht gesetzt sind, ist `/mcp` nicht erreichbar und die zugehörigen Menüeinträge werden ausgeblendet.
- Ein Zugang kann nie mehr als sein Besitzer: Der Zugriff auf fremde oder nur eingeschränkt geteilte Daten ist durch das Benutzerrecht und die Rolle der Kontofreigabe gedeckelt; auch die Admin-Rolle gewährt keinen Fremdzugriff.
- Das Token wird genau einmal angezeigt; es lässt sich nicht erneut abrufen, nur rotieren. Gespeichert wird ausschließlich ein Hash.
- Kritische Wirkungen (Verbuchung, Storno, Löschung) sind nie „still": Sie erfordern mindestens eine dokumentierte Genehmigung — je Aktion oder als vorab erteilte Dauerfreigabe des Zugangs.
- Rate-Limits begrenzen die Anfragelast je Werkzeugklasse; bei Überschreitung antwortet der Server mit `429`.
- Backups enthalten kein Tokenmaterial; ein wiederhergestellter Zugang gilt als widerrufen und wird erst durch eine Token-Rotation wieder nutzbar.
- Eine Anomalieerkennung meldet auffällige Häufungen von Berechtigungsverweigerungen und Ausführungsfehlern als Benachrichtigung an den Besitzer und die Administratoren.
- Das Protokoll wird nach gestaffelten Aufbewahrungsfristen bereinigt; die Bereinigung ist selbst in der Hash-Kette dokumentiert.
