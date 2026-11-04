# Todo – EP-05 Individuelle Rechte & Filter

- [x] Eingaben gelesen (product-backlog EP-05, berechtigungsmodell §3–§5, ui-konzept §4.3/§4.4/§7, approval-konzept §3/§5, inventory)
- [x] F-0501: `PUT …/scopes` verifiziert + Implikationsregel (non-Read ⇒ Read) in `ValidateScopes` nachgezogen; Owner-Isolation/Delta-Audit bestätigt
- [x] F-0501: Tests (invalid tuple, non-individuell, write-without-read, unknown enum, foreign owner; Bestands-Delta-Tests grün)
- [x] F-0502: `McpRightsEditor.razor` — 18×7-Matrix, F3-gesperrte Zellen mit Tooltip, Read-only für Standardstufen, „Von Vorlage anpassen", Auto-Read/Kaskade, Save via `IApiClient`, Warning-Confirm bei Erweiterung, Filter-Erhalt beim Save
- [x] F-0503: Filterverwaltung je persistierter Zeile (5 Dimensionen, Kategorien via Options-Listen, unbekannte Ids muted), PUT filters, AND/OR/Unklassifiziert/Indirekt-Klartext, Sperrlogik bei dirty Matrix
- [x] F-0504: Genehmigungsmodus-Klartext + Kill-Switch-Hinweis (Section + Wizard), Critical-Confirm PreApproved+Book/Delete in `SaveAsync`
- [x] resx de/en/neutral (24 neue Keys); translation-check/razor-l10n/razor-usage/enum-coverage/format OK
- [x] `dotnet build` grün; `dotnet test` 1659 grün (MCP: 137)
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [ ] Commit + Push `task/mcp-integration-schritt-5-rechte`

## Offene Punkte / Nachgelagert
- [ ] Multi-Zeilen-Editor (OR-Varianten je Tupel) — aktuell nur Erhalt beim Save; Erzeugung weiterhin über API/Create-Request.
- [ ] Genehmigungsmodus-Ausführungswirkung (AiActionRequest, Approval-Queue) — EP-08.
- [ ] Gruppierung der Matrixzeilen nach Fachbereich (ui-konzept §4.3.2 schlägt Konten&Auszüge/Stammdaten/Budget/Anhänge vor) — bewusst weggelassen (reine Präsentation, kein AK-Blocker).
- [ ] `McpAccessOptions`-Route liefert Kategorielisten; bei sehr vielen Kategorien ggf. später Paging/Suche in der Filter-Checkbox-Liste.
