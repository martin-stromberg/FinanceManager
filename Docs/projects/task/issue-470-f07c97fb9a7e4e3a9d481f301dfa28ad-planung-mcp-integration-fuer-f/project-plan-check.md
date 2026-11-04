# Planprüfung – MCP-Integration

## Ergebnis

**Status:** Projektplan vollständig

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung | Schritt(e) | Abgedeckt |
|---|---|---|
| F1 – MCP-Zugänge (mehrere Zugänge/User, Aktivieren/Deaktivieren, Ablaufdaten, Tokenverwaltung, Widerruf, Nutzungsprotokollierung) | 1 (fachliche Spezifikation), ergänzend 3 (technische Token-Details: Hashing, Lebensdauer, Widerruf) und 4 (Tokenverwaltungs-UI) | ja |
| F2 – Berechtigungsmodell (Scope-/Aktionsmodell, Vererbung, Konfliktverhalten, Standard-/individuelle Rechte, Grundsatz „nur einschränken") | 1 | ja |
| F3 – Datentypen-Klassifizierung (alle Datentypen × 7 Rechtearten) | 1 (vollständige Matrix inkl. Mapping auf reale Entitäten/Enums; Akzeptanzkriterium verlangt „kein unaufgelöster Begriff") | ja |
| F4 – Filterkonzept (optionale Gruppierungsfilter, Standard = alle Datensätze) | 1 | ja |
| F5 – Freigabestufen (Analyse, Assistent, Sachbearbeiter, Buchhalter, Individuell) | 1 | ja |
| F6 – MCP-Toolkonzept (Toolkatalog, Scope-Zuordnung, Sicherheitsklassifizierung, Berechtigungen, Ein-/Ausgaben, Versionierung, Geschäftsprozess-Grundsatz) | 2 | ja |
| F7 – Kontoauszugs-Assistent (keine Bankanbindung, Importmechanismen, Analyse, Vervollständigung, Dubletten, Verbuchungsvorschläge, Plausibilität) | 2 | ja |
| F8 – Sicherheitskonzept (PAT/Hashing/Lebensdauer/Widerruf, Autorisierung, Prompt-Injection-Schutz, Notfallmaßnahmen, Rate Limiting) | 3 (alle fünf Teilaspekte benannt; zusätzlich HTTP-/LAN-Transportrisiko) | ja |
| F9 – Audit- und Compliance-Konzept (Was, Detailtiefe, Aufbewahrung, Auswertbarkeit, Toolaufruf-/Änderungsspeicherung, KI-Nachvollziehbarkeit) | 3 | ja |
| F10 – Genehmigungsworkflow (genehmigungspflichtige Aktionen, Zustände, UI, Ausführung nach Freigabe, Fehlerbehandlung, `AiActionRequest` mit Pending/Approved/Rejected/Executed/Failed) | 3 (Workflow + Zustandsmodell), ergänzend 4 (Genehmigungscenter-UI) | ja |
| F11 – Benutzeroberfläche (6 Komponenten, einfache Führung, Expertenmodus, Verständlichkeit) | 4 (alle sechs Komponenten im Akzeptanzkriterium explizit) | ja |
| F12 – Technische Architektur (MCP-Server-Struktur, Service-Layer, Sicherheitslayer, Tool-Registrierung, Scope-Prüfung, Logging, Persistenzmodell) | 4 | ja |
| F13 – Roadmap (5 vorgegebene Phasen, je Phase Funktionsumfang/Sicherheitsrisiken/techn. Voraussetzungen/Erfolgskriterien) | 5 | ja |
| Artefakt 1 – Fachkonzept | 1 → `fachkonzept.md` | ja |
| Artefakt 2 – Sicherheitskonzept | 3 → `sicherheitskonzept.md` | ja |
| Artefakt 3 – Berechtigungsmodell | 1 → `berechtigungsmodell.md` | ja |
| Artefakt 4 – Scope- und Toolkatalog | 2 → `scope-und-toolkatalog.md` | ja |
| Artefakt 5 – UI-Konzept | 4 → `ui-konzept.md` | ja |
| Artefakt 6 – Architekturkonzept | 4 → `architekturkonzept.md` | ja |
| Artefakt 7 – Datenmodell-Erweiterungen | 4 → `datenmodell-erweiterungen.md` | ja |
| Artefakt 8 – Approval-Konzept | 3 → `approval-konzept.md` | ja |
| Artefakt 9 – Audit-Konzept | 3 → `audit-konzept.md` | ja |
| Artefakt 10 – Priorisierte Umsetzungsroadmap | 5 → `roadmap.md` | ja |
| Artefakt 11 – Aufwandsschätzung | 5 → `aufwandsschaetzung.md` | ja |
| Artefakt 12 – Product-Backlog mit Epics und Features | 5 → `product-backlog.md` | ja |

Alle 13 fachlichen Anforderungen und alle 12 Ergebnisartefakte sind mindestens einem
Entwicklungsschritt zugeordnet, mit benannten Zieldateien unter
`Docs/planung/mcp-integration/` (Annahme A1–A3 werden übernommen) und in den
Akzeptanzkriterien verankert.

## Abhängigkeitsprüfung

| Schritt | Abhängigkeiten | Bewertung |
|---|---|---|
| 1 – Fachkonzept und Berechtigungsmodell | keine | korrekt als Fundamentschritt |
| 2 – Scope- und Toolkatalog | 1 | korrekt: Toolkatalog benötigt Scope-/Rechtemodell aus Schritt 1 |
| 3 – Sicherheits-, Audit- und Approval-Konzept | 1, 2 | korrekt: Autorisierung setzt Rechtemodell (1) voraus, genehmigungspflichtige Aktionen werden aus der Sicherheitsklassifizierung des Toolkatalogs (2) abgeleitet |
| 4 – UI-, Architektur- und Datenmodellkonzept | 1, 2, 3 | korrekt: Architektur/Persistenz konsolidieren Rechte-, Tool- und Approval-Konzepte; UI baut auf allen fachlichen Konzepten auf |
| 5 – Roadmap, Aufwandsschätzung, Product-Backlog | 1, 2, 3, 4 | korrekt: Planungsebene konsolidiert alle Konzeptdokumente |

- Alle Abhängigkeiten verweisen ausschließlich auf frühere Schritte.
- Der Graph ist zyklenfrei (strikte Kette 1 → 2 → 3 → 4 → 5, in Übersichtstabelle und
  Schrittbeschreibungen konsistent).
- Die fachliche Begründung der Reihenfolge (Rechte-/Datenmodell → Toolkatalog →
  Querschnittskonzepte → technische Dimension → Planungsebene) ist plausibel und im
  Abschnitt „Grobe Vorgehensentscheidungen" begründet.

## Fehlende oder unvollständige Punkte

Keine fachlichen Lücken. Eine formale Ungenauigkeit:

- **Anzahl der Datentypen in F3:** Schritt 1 spricht von „allen 19 Datentypen der
  Anforderung" (Beschreibung und Akzeptanzkriterium). Die Anforderung F3 nennt
  tatsächlich 18 Datentypen – und der Plan listet exakt diese 18 Begriffe
  vollständig auf (Konten, Kontotypen, Kontoauszüge, Kontoauszugsentwürfe,
  Buchungen, Kontakte, Kontaktgruppen, Sparpläne, Sparplankategorien, Wertpapiere,
  Wertpapierkategorien, Budgets, Budgetkategorien, Budgetregeln, Reports,
  KPI-Konfigurationen, Anhänge, Anhangkategorien). Inhaltlich ist die Abdeckung
  damit lückenlos; die Zahl „19" ist falsch und sollte bei Ausführung des Schritts
  nicht als dritter, unbekannter Datentyp interpretiert werden.

## Hinweise

- **Nicht-Ziele:** Kein Widerspruch festgestellt. Der Plan schließt direkten
  DB-Zugriff (Tools als Adapter auf `I*Service`), Online-Banking-Anbindung
  (expliziter Ausschluss in Schritt 2, Akzeptanzkriterium), generische CRUD-APIs
  (Grundsatz „Geschäftsprozesse statt CRUD" in Vorgehensentscheidungen und
  Akzeptanzkriterien) und vollautonome Buchhaltung (Approval-Pflicht, F10)
  ausdrücklich aus. Alle Lieferergebnisse sind Markdown-Konzeptdokumente – keine
  Implementierungsschritte enthalten.
- **Schrittqualität:** Jeder der fünf Schritte ist eigenständig als
  Lifecycle-Eingabe verwendbar: Ziel und Inhalt beschrieben, Zieldateien mit
  Verzeichnis benannt, „Liefert Artefakte"-Zuordnung, messbare Akzeptanzkriterien,
  Abhängigkeiten und betroffene Bereiche vorhanden. Querverweise zwischen
  Schritten (z. B. Schritt 3 auf die Sicherheitsklassifizierung aus Schritt 2)
  sind durch die Abhängigkeitskette aufgelöst.
- **Konsistenz mit Ist-Stand:** Alle geprüften Verweise stimmen mit
  `inventory.md`/`inventory/entitaeten.md` überein: `AiActionRequest` ist korrekt
  als nicht vorhanden/neu zu konzipieren erkannt; `AccountShare` (Read/Write/Admin),
  fehlendes `OwnerUserId` auf `Posting`, indirekter Benutzerbezug von
  `StatementImport`/`StatementEntry`, `AccountType`-Enum statt Kontotyp-Entität,
  `IStatementDraftService`-/`IMassImportOrchestrator`-Oberfläche,
  `StatementDraftBookingGuard`, `IBackgroundTaskManager` (In-Memory),
  `PostingReversalService` (Storno statt Löschen), `Pbkdf2IdentityPasswordHasher`,
  `OnTokenValidated`/Security-Stamp, `IpBlock`, `RateLimitPolicies.AuthSensitive`,
  `NotificationWriter` (SystemAlert/EventDriven), `Attachment.Content` in SQLite,
  `IApiClient`/MVVM-Konvention, `appsettings`-Feature-Flags, `*BackupDto`- und
  EF-Migrations-Konventionen sowie HTTP-/LAN-Betrieb (`AllowedHosts: "*"`) sind
  alle belegt. Keine erfundenen Entitäten oder Infrastruktur gefunden.
- **Zusatzlieferung:** Schritt 5 liefert zusätzlich ein `README.md`-Indexdokument
  für `Docs/planung/mcp-integration/` – harmlose, sinnvolle Ergänzung, kein
  Konflikt mit den geforderten Artefakten.
- **Offene Punkte:** Die zwei im Plan geführten offenen Punkte (Betriebsform des
  MCP-Servers, Umgang mit HTTP-only-LAN) sind mit begründeten Vorschlägen als
  „behandelt" markiert und werden in den Konzeptdokumenten (Schritte 3 und 4)
  final entschieden – konsistent mit der Anforderung („keine blockierenden
  Fragen").
