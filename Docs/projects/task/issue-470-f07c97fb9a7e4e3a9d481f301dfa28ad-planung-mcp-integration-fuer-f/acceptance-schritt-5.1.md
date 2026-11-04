# Abnahmeprüfung – Entwicklungsschritt 5

Geprüft: Roadmap, Aufwandsschätzung, Product-Backlog und README-Index unter
`Docs/planung/mcp-integration/` gegen die Akzeptanzkriterien von Schritt 5
(`project-plan.md` §Schritt 5) sowie gegen F13 und die Vorgängerkonzepte
(Schritte 1–4). Diff-Basis:
`task/…-planung-mcp-integration-fuer-f` → HEAD (`a11efd3c`).

## Ergebnis

**Status:** Abweichungen gefunden (1 geringfügige, nicht blockierend)

Alle verbindlichen Akzeptanzkriterien sind erfüllt; die einzige Abweichung
ist eine kleine Rechenungenauigkeit in der Planwert-Summe der
Aufwandsschätzung.

## Abweichungen

- [ ] `aufwandsschaetzung.md` (Zeile 70): Die Summe der Baustein-Planwerte
  B1–B19 beträgt rechnerisch **165 PT** (10+8+6+9+13+5+13+6+15+12+6+10+5+9+9+7+8+3+11),
  angegeben ist „Planwert ≈ **168 PT**" – Abweichung ≈3 PT. Analog ist der
  Phasen-Planwert für P4 (Zeile 84) mit „≈39" ca. 2 PT über der Summe der
  zugeordneten Planwerte (B1-Teil ~3 + B10 12 + B11 6 + B17 8 + B18 3 +
  Testblock ~5 ≈ 37). Da beide Angaben mit „≈" als Näherung deklariert sind,
  handelt es sich um eine kosmetische Ungenauigkeit; die Band-Summen
  stimmen dagegen exakt (Bausteine 136–200, Phasen 157–231 PT).

## Hinweise

**Nachweise je Prüfpunkt:**

1. **Dateien/Sprache:** `roadmap.md` (556 Z.), `aufwandsschaetzung.md`
   (139 Z.), `product-backlog.md` (227 Z.), `README.md` (64 Z.) liegen als
   deutschsprachige Markdown-Dateien unter `Docs/planung/mcp-integration/`
   vor (AK 1 erfüllt).
2. **Roadmap – 5 vorgegebene Phasen, Reihenfolge exakt:** §2 Read-Only
   Analyse, §3 Assistenz- und Vorschlagsfunktionen, §4 Kontoauszugs-
   Assistent, §5 Genehmigungspflichtige Aktionen, §6 Agentengestützte
   Workflows. Je Phase alle vier geforderten Elemente: Funktionsumfang
   (§x.1), Sicherheitsrisiken inkl. Mitigation mit Konzept-Verweisen
   (§x.2), technische Voraussetzungen als inkrementelle Ausbaustufen
   (§x.3), Erfolgskriterien (§x.4). Priorisierungsbegründung und
   verbindliche Phasen-Abhängigkeiten in §1 inkl. Abhängigkeitstabelle.
   (AK 2 erfüllt.)
3. **Aufwandsschätzung:** transparente Methodik (§1: PT-Bandbreite +
   Planwert, bausteinbezogene Schätzung, Referenzrahmen, Annahmen
   A1–A5 inkl. Unsicherheiten), Aufschlüsselung in 19 Bausteine B1–B19
   (decken Backend/Domain, Sicherheitslayer, UI, Migration ab) plus
   separater Testblock je Phase (A4), Phasenschätzung (§3) und
   Risikozuschläge (§4). Alle Phasen/Epics abgedeckt. Band-Summen exakt
   verifiziert; Planwert-Ungenauigkeit siehe Abweichung oben. Eine
   explizit aggregierte Bereichs-Summe (Backend/UI/Migration/Test) fehlt;
   die Baustein-Granularität bildet die Bereiche jedoch vollständig ab –
   als erfüllt gewertet. (AK 3 erfüllt.)
4. **Product-Backlog:** 11 Epics (EP-01–EP-11) entlang der Phasen plus
   Querschnitte (Zugangsverwaltung, Scope-Engine, Audit, Admin/Notfall,
   Betriebshärtung); 36 priorisierte Features (Priorität = Reihenfolge
   laut Konvention, Z. 13–14) mit Kurzbeschreibung, AK-Skizze, PT-Band,
   Baustein- (B1–B19) und Konzeptdokument-Bezug. **Alle 33 Tools
   abgedeckt:** T01–T10/T13/T14/T30/T32 (+T33-Basis) in EP-02, T11/T12/
   T15/T19 in EP-04, T16–T18 in EP-07, T22–T29 (direkte Teilmengen) in
   EP-08, T20/T21/T31/T33 + finale Teilmengen in EP-09/EP-10, T33-
   Vollausbau F-0905. Alle Konzeptbereiche aus Schritten 1–4 sind einem
   Feature zuordenbar (Zugänge→EP-01, Scopes/Filter→EP-05, Audit→EP-03/
   EP-06, Approval→EP-09/EP-10, Admin→F-1005, Betrieb→EP-11). Epic-Bänder
   decken sich mit den Bausteinen exkl. Testblöcke; die Epic-Untergrenze
   (137 PT) liegt 1 PT über der Baustein-Untergrenze (136 PT) – erklärbar
   durch Zuordnung von F-0501 (3–4 PT) zu B4/B5 zusätzlich zu B15 bei
   gleichzeitig nicht separat geführtem B12-Teil P3 (1–2 PT); als
   konsistent gewertet. (AK 4 erfüllt.)
5. **README-Index:** `README.md` §„Artefakte" verlinkt alle zwölf
   Ergebnisdokumente; alle Link-Dateinamen stimmen mit den
   tatsächlichen Dateien überein (Verzeichnisabgleich durchgeführt).
   Zusätzlich Lesereihenfolgen und Zentralfestlegungen. (AK 5 erfüllt.)
6. **Konsistenz zu Schritten 1–4 – keine erfundenen Konzepte:**
   - Tool-IDs/Namen T01–T33 identisch zum `scope-und-toolkatalog.md`
     (Zeilen 96–295, vollständiger Abgleich).
   - Sicherheitsklassen→Phasen-Mapping konsistent: lesend+Meta→P1,
     vorschlagend→P2, schreibvorbereitend→P3, genehmigungspflichtig→P4
     (Klassendefinition toolkatalog §2, Z. 65–77).
   - `AiActionRequest` ausschließlich ab Phase 4 (Entität, Gate an
     Pipeline-Position 8, Executor); 6 Zustände inkl. `Expired` konsistent
     zu approval-konzept §3.2 (Z. 148–179).
   - Entitätsnamen (`McpAccess`, `McpAccessScope`, `AiActionRequest`,
     `McpAuditEntry`, `McpAuditEntryChange`) und Enums konsistent zu
     `datenmodell-erweiterungen.md` §2.
   - `Mcp:*`-Flags (`Enabled`, `WriteAccess:Enabled`, `RateLimiting:*`,
     `TokenHash:Iterations`, `Audit:RetentionDays:*`, `Approval:*`)
     konsistent zu sicherheitskonzept/datenmodell.
   - Bedrohungs-IDs B1–B7 in den Risiko-Tabellen existieren in
     `sicherheitskonzept.md` (Z. 46–52); §-Verweise auf Vorgängerdokumente
     (ui-konzept §4.1–4.6/§5.2/§6.1–6.3, architektur §3.1/§6.2,
     approval §2.2/§5/§7, audit §5/§6.2) sind existent.
   - Freigabestufen (Analyse/Assistent/Sachbearbeiter/Buchhalter/
     Individuell) und Genehmigungsmodi (`PerAction`/`PreApproved`) gemäß
     F5/approval-konzept; Nicht-Ziele werden in Roadmap §8 explizit
     eingehalten.
7. **Gesamtabgleich 12 Artefakte:** Alle zwölf geforderten
   Ergebnisdokumente liegen vor: fachkonzept.md, sicherheitskonzept.md,
   berechtigungsmodell.md, scope-und-toolkatalog.md, ui-konzept.md,
   architekturkonzept.md, datenmodell-erweiterungen.md,
   approval-konzept.md, audit-konzept.md, roadmap.md,
   aufwandsschaetzung.md, product-backlog.md.
8. **Diff-Umfang:** `git diff` Basis…HEAD enthält nur Additions: die vier
   Lieferdokumente sowie Prozessartefakte unter
   `Docs/features/task/…-schritt-5-roadmap/` (plan/requirement/review/
   test-results/todo). Keine Änderungen an Vorgängerdokumenten.
9. **Randnotiz:** Backlog-Features nennen im Beschreibungstext Service-/
   Contract-Namen (z. B. `IStatementDraftService`-Methoden). Das ist
   technisch über der reinen „Was"-Ebene, entspricht aber dem
   Referenzstil des Toolkatalogs und dient der geforderten
   Rückverfolgbarkeit zu Phase/Konzeptdokument – nicht als Abweichung
   gewertet.

**Empfehlung:** Abnahme möglich; die Planwert-Ungenauigkeit (165 statt
≈168 PT) kann bei Gelegenheit redaktionell nachgezogen werden.
