# Review – Schritt 5: Roadmap, Aufwandsschätzung und Product-Backlog

**Status:** Abgeschlossen – Selbstprüfung gegen die Akzeptanzkriterien
des Projektplans bestanden. Vier deutschsprachige Dokumente unter
`Docs/planung/mcp-integration/` geliefert:

- `roadmap.md` (~556 Zeilen)
- `aufwandsschaetzung.md` (~139 Zeilen)
- `product-backlog.md` (~227 Zeilen)
- `README.md` (~64 Zeilen)

## Prüfung der Akzeptanzkriterien

|| Kriterium | Ergebnis |
|---|---|---|
|| Alle Dokumente als deutschsprachige Markdown-Dateien unter `Docs/planung/mcp-integration/` | ✔ vier neue Dateien, konsistent zur Repo-Docs-Konvention |
|| Roadmap: exakt die fünf vorgegebenen Phasen (F13) | ✔ Abschnitte 2–6: Read-Only Analyse, Assistenz-/Vorschlagsfunktionen, Kontoauszugs-Assistent, Genehmigungspflichtige Aktionen, Agentengestützte Workflows – keine Umbenennung, keine Zusatzphase |
|| Roadmap: Funktionsumfang je Phase | ✔ je Phase Tool-Tabelle mit Tool-IDs (T01–T33), Entitäten, Contracts, UI-Teile, `Mcp:*`-Flags; „bewusst nicht enthalten"-Liste je Frühphase |
|| Roadmap: Sicherheitsrisiken je Phase | ✔ je Phase Tabelle Risiko → Mitigation mit Konzept-Verweis (Bedrohungen B1–B7 des Sicherheitskonzepts abgedeckt) |
|| Roadmap: technische Voraussetzungen je Phase | ✔ Entitäten/Migrationen, Contracts, Pipeline-/UI-Bausteine je Phase; inkrementell, kein Schema-Bruch (Datenmodell §12) |
|| Roadmap: Erfolgskriterien je Phase | ✔ je Phase nummerierte, prüfbare Kriterien (5–6 Stück) |
|| Roadmap: Priorisierung begründet + Abhängigkeiten | ✔ Abschnitt 1: 5 Gründe + Abhängigkeitstabelle |
|| Aufwandsschätzung deckt alle Phasen ab | ✔ Phasentabelle P1–P5 (77–112 / 22–31 / 15–22 / 31–46 / 12–20 PT), Summe 157–231 PT ≈ 8–11,5 PM |
|| Aufwandsschätzung: Methodik, Annahmen, Unsicherheiten transparent | ✔ Abschnitte 1–2: PT-Band + Planwert, Annahmen A1–A5, Baustein-Tabelle B1–B19 mit Treibern, Risikozuschläge Abschnitt 4, explizite Nicht-Bestandteile Abschnitt 5 |
|| Backlog: Epics entlang Phasen und Querschnitte | ✔ 11 Epics EP-01–EP-11 (Fundament, Read-Only-Katalog, Audit-Basis, Vorschläge, Rechte/Filter, Audit-Ansicht, Assistent, Stammdaten, Queue, Center/finale Aktionen/Admin, Agenten) |
|| Backlog: Features mit Beschreibung, AK-Ansatz, Schätzung, Bezug zu Phase+Konzeptdokument | ✔ 41 Features F-0101–F-1105; je Feature PT-Band, AK-Skizze, Baustein-/Doku-/Tool-ID-Bezug |
|| Backlog: alle Konzeptinhalte abbildbar | ✔ 33 Tools T01–T33 in EP-02/04/07/08/10 verteilt (inkl. Meta T32/T33); Entitäten, Pipeline-Bausteine, UI-Komponenten, `Mcp:*`-Flags, Retention, Katalogpflege abgedeckt – siehe Zuordnung unten |
|| README verlinkt alle zwölf Ergebnisdokumente | ✔ Artefakt-Tabelle 1–12 mit Links + Lesereihenfolgen |

## Konsistenz zu Vorgängerdokumenten

- **Phasenzuordnung der Tools** folgt mechanisch der Übersichtstabelle
  (toolkatalog §7): lesend → P1 (T01–T10, T13, T14, T30, T32),
  vorschlagend → P2 (T11, T12, T15, T19), schreibvorbereitend → P3
  (T16–T18, direkte Teilmengen T22–T29), genehmigungspflichtig → P4
  (T20, T21, T31 + finale Teilmengen), Meta T33 voll ab P4. ✔
- **`AiActionRequest` erst Phase 4:** konsistent zu architekturkonzept
  §13 („Approval-Gate/Executor/Center erst ab Phase 4") und
  datenmodell §12 (inkrementelle Migrationen ohne Schema-Bruch). ✔
- **Phase-3-Genehmigungsmodus:** schreibvorbereitend läuft dort in
  Direktausführung; `PerAction` (Sachbearbeiter-Standard) wird mit
  `AiActionRequest` in P4 abbildbar – begründete Festlegung in der
  Roadmap, konsistent zu approval-konzept §2.3. ✔
- **Konzeptnamen:** `McpAccess`, `McpAccessScope`, `AiActionRequest`
  (6 Zustände inkl. `Expired`), `McpAuditEntry(+Change)`,
  `IStatementImportQueryService`, `IMcpAccessContext`, Prüfstufen 0–3,
  `katalog-v<n>`, `fm_mcp_*`, `pbkdf2|…`, `Err_Mcp_*`,
  `Mcp:Enabled`/`Mcp:WriteAccess:Enabled`/`Mcp:Approval:*`/
  `Mcp:Audit:*`/`Mcp:RateLimiting:*`/`Mcp:TokenHash:Iterations`,
  `RateLimitPolicies.Mcp*`, `StatementDraftBookingGuard`,
  `untrusted_data` – überall mit Vorgänger-Nomenklatur. ✔
- **UI-Komponenten:** `McpAccessListViewModel`, `McpAccessCardViewModel`,
  `McpApprovalCenterViewModel`, `McpAuditViewModel`,
  `McpSecurityOverviewViewModel`, Routen `/list|card/mcp-*` –
  phasenkorrekt zugeordnet (Genehmigungscenter erst P4, Matrix-Editor/
  Filter P2 – wie ui-konzept §8 vorgeschlagen). ✔
- **Schätzungskonsistenz:** Backlog-Feature-Bänder summieren je Epic
  auf die Baustein-Bänder B1–B19 der aufwandsschaetzung.md
  (EP-01 51–75 = B1-Teil+B2–B6+B13+B14; EP-02 11–15 = B7;
  EP-07+EP-08 12–17 = B9; EP-09 12–18 = B1-Teil+B10;
  EP-10 15–22 = B11+B17+B18; EP-11 10–17 = B19+B12-Teil). ✔

## Festgelegte Einzelentscheidungen (für Folgeschritte verbindlich)

- Phase 1 liefert **15 Tools** (lesend + Meta) bei vollständigem
  Sicherheitsfundament; `fm_task_status` (T33) nur in Basisform –
  Vollausbau mit Phase 4.
- `IStatementImportQueryService` (offener Baustein T08) wird in Phase 1
  umgesetzt; bei Termindruck innerhalb P1 nachrangig verschiebbar.
- Genehmigungsmodus-Auswahl in der UI ab Phase 2 persistierbar,
  Ausführungswirkung ab Phase 4 (oder Auswahl erst dann freigegeben –
  Umsetzungsentscheid, beide Varianten konzeptkonform).
- Admin-Sicherheitsübersicht in Phase 4 (erste sicherheitsrelevante
  Aggregate); SSE ausschließlich Phase 5.
- Budget-Kommunikation: Planwert ≈194 PT, konservativ 200–230 PT.

## Offene Punkte

- Keine blockierenden. Umsetzungsoffen (nicht konzeptrelevant):
  konkrete SDK-Wahl für Streamable HTTP (A1 der Schätzung),
  Detailgestaltung der Wirkungsvorschau-Renderer je Aktionstyp,
  exakte Agenten-Runbook-Ausprägung in Phase 5.
- Damit liegen alle zwölf geforderten Ergebnisartefakte der Anforderung
  unter `Docs/planung/mcp-integration/` vor (Index: `README.md`).
