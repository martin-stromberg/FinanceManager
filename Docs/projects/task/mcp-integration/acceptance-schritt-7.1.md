# Abnahmeprüfung – Entwicklungsschritt 7 (EP-07)

Geprüft: `task/mcp-integration-schritt-7-kontoauszug` gegen `task/mcp-integration`
(13 Dateien, +3215/−14). Testlauf: `dotnet test FinanceManager.Tests --filter Mcp`
→ **222/222 grün** (darunter 22 neue `McpStatementWriteToolsTests`).

## Ergebnis

**Status:** Abweichungen gefunden

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-0701 T16 `fm_statement_draft_import` | erfüllt | `FmStatementDraftImportTool.cs`: 4 Modi `upload`/`empty`/`preliminary`/`massImport` (Z. 98–106, Inferenz Z. 108–129). Upload: base64 → `CreateDraftAsync`-Stream mit 20-MB-Deckel (`McpStatementDraftGuards.MaxUploadBytes`, Z. 136/440–465); `empty` → `CreateEmptyDraftAsync`; `preliminary` → `CreatePreliminaryDraftAsync` mit owner-geprüftem Konto (`RequireAccountAsync`, Z. 245); `massImport` → `IMassImportOrchestrator.ProcessAsync` mit `MassImportOptions`-Limits (≤50 Dateien/10 MB/100 MB, Z. 286–313), `ConfirmExecution=true`, `decisions`. Erkennungsstatus `recognized` (Z. 171/180), owner überall aus `context.Access.OwnerUserId`, `untrusted_data` auf `originalFileName`/`description`/Parser-Feldern (Z. 174, 225, 274, 425–430, 480–484). Audit `DraftImported` mit sha256+Metadaten statt Inhalt (Z. 138–148). |
| F-0702 T17 `fm_statement_draft_complete` | teilweise | `FmStatementDraftCompleteTool.cs`: Nur-Lücken-Semantik via `Gap<T>` (Z. 480–533): unbelegt→`fill`, gleich→`unchanged`, belegt→`skipped(alreadySet)`, Booleans nur false→true (`requiresEdit`). Apply über `SaveEntryAllAsync`/`SetEntrySplitDraftAsync` mit Effektivwerten (Z. 539–573) — gesetzte Werte werden mitübergeben statt überschrieben (Verifiziert gegen `StatementDraftService.SaveEntryAllAsync`, Z. 219–310: null würde löschen; Tool sendet Ist-Stand). `dryRun` returnt vor jeder Wirkung/Audit-Staging (Z. 221–234). Atomarität nur als Vor-Validierung+Apply (Z. 214–219 vor 285–308), **keine** Entry-übergreifende Transaktion → siehe Abweichung 2. Record-Scope auf Draft+Entry+Referenzziele (Z. 132–159, 579–621). `DataChange`-Audit `DraftCompleted` mit Feld-Deltas (Z. 163–177, 279–289). ≤100 Entries (Schema `maxItems`, `MaxEntryUpdates`). |
| F-0703 T18 `fm_statement_draft_edit` | erfüllt | `FmStatementDraftEditTool.cs`: `updateEntry` nur Whitelist-Felder (`CorrectableFields`, Z. 71–75; Fremdfeld→`Err_Mcp_InvalidArguments`, Z. 425–446), Merge über Ist-Stand (Z. 183–189) → `UpdateEntryCoreAsync`. `addEntry` → `AddEntryAsync` mit zusätzlichem (Kontoauszugsentwürfe, Erstellung)-Scope im Handler (Z. 245–251). `deleteEntry` → `DeleteEntryAsync`. `resetDuplicate` → `ResetDuplicateEntryAsync`, nur `AlreadyBooked`-Entries (Z. 361–364); umgekehrt sperrt `AlreadyBooked` update/delete (Z. 169–173, 313–317) — konsistent mit Batch-Konvention. Audit `DraftEdited` mit Feld-Deltas old→new (`AddChange`, Z. 196–202, 516–545) bzw. Add/Delete/Reset-Markern. |
| F-0704 Zusatzvalidierung | teilweise | `McpStatementDraftGuards.cs`: owner-scoped Referenzauflösung (fremd=fehlend→`Err_Mcp_InvalidArguments`), archivierte Sparpläne/inaktive Wertpapiere abgelehnt (Z. 51–116), `ValidateEntryCore` (Betrag≠0, Subject Pflicht, Textlängen, ISO-Currency, Valuta-Toleranz 7d, Z. 131–167), Pipeline-Nebenbedingungen 1:1 zu `SaveEntryAllAsync` gespiegelt (Z. 177–226), Split-Voraussetzungen (CompleteTool Z. 442–467) — alles **vor** Service-Wirkung. **Aber:** Backlog F-0704 fordert normativ die Erweiterung von `fm_statement_draft_validate` (T14) um Zusatzregeln als Befundliste + gesperrte Entries im Buchungsplan — T14 unverändert (`FmStatementDraftValidateTool.cs` liefert nur `ValidateAsync`). → Abweichung 1. |
| Kein `BookAsync`/`CommitAsync` | erfüllt | `grep` über `FinanceManager.Web/Mcp/`: kein Aufruf; Postings bleiben T20. Alle Schreibpfade ausschließlich über `IStatementDraftService`-Contracts/`IMassImportOrchestrator` (Adapter-Prinzip, architekturkonzept §7). |
| `DataChange` transaktional gekoppelt | erfüllt | `McpWriteAudit.StageDataChangeAsync` staged Eintrag+Deltas in den geteilten `AppDbContext`; der `SaveChangesAsync` des Services committet beides atomar (`McpAuditWriter.StageAsync`, Z. 115–127). `DiscardStaged` detached das Bündel bei Fehlschlag vor dem Save (Z. 67–89) — verhindert Audit ohne Wirkung. Audit-Kategorien korrekt: `ToolCall` im Dispatcher (`WriteAsync`), `DataChange` je fachlicher Wirkung. |

## Abweichungen

- [ ] **A1 (mittel) — F-0704 als Pre-Service-Guards statt T14-Erweiterung umgesetzt.**
  Backlog F-0704: „`fm_statement_draft_validate` um Zusatzregeln erweitern … Befundliste
  = `ValidateAsync` + Zusatzregeln; gesperrte Entries im Buchungsplan". Umgesetzt wurde
  `McpStatementDraftGuards`, das dieselbe Regelfamilie als **Fehler vor dem Schreiben**
  durchsetzt — sinnvoll und in `requirement.md`/review.md des Schritts dokumentiert,
  deckt aber nicht die normative Lieferung: T14 bleibt unverändert, es gibt keine
  Zusatzregel-Befundliste (lesend) und keinen Bezug zum T19-Buchungsplan; Regeln wie
  Betrags-/Vorzeichenlogik zum Kontotyp, Zeitraum-Überlappung und Split-Summen-
  Konsistenz fehlen ganz. Bewertung: inhaltlich vertretbare Auslegung (ungültige
  Writes werden verhindert statt nur gemeldet), normativ aber nicht erfüllt →
  Entscheidung nötig: als akzeptierte Scope-Entscheidung festschreiben (Backlog/
  Toolkatalog §4.4 anpassen) oder T14-Erweiterung als Folgepunkt aufnehmen.

- [ ] **A2 (niedrig) — T17-Batch nicht entry-übergreifend transaktional.**
  Toolkatalog/Backlog: „Batch-Updates atomar"/„ganz oder gar nicht". Die Apply-Phase
  ruft `SaveEntryAllAsync` je Entry mit eigenem `SaveChangesAsync` — bei einem
  Fehlschlag mitten im Apply bleiben bereits committete Entries bestehen **und**
  verlieren ihren `DataChange`-Beleg (gestagtes Bündel wird verworfen, Z. 297–307).
  Begründung plausibel und dokumentiert (`ApplyBatchEntryUpdatesAsync` deckt nur
  Kernfelder ab — verifiziert in `StatementDraftService.BatchUpdate.cs` Z. 137–291:
  keine ContactId/SavingsPlanId/SecurityId/IsCostNeutral/SplitDraftId), Restfenster
  real aber klein (Vor-Validierung deckt die erreichbaren Fachfehler ab; einziger
  nicht vorvalidierter Fall: `inUse`-Konflikt `SetEntrySplitDraftAsync` →
  `InvalidOperationException` → `Err_Mcp_InvalidState` mit evtl. Teil-Apply).
  Deskriptor-Text „Atomic all-or-error batches" ist damit leicht überzogen.

## Hinweise

- **Contract-Doc-Fix `IStatementDraftService`** (`SaveEntryAllAsync`, `SetEntrySecurityAsync`):
  Interface-Deklaration nannte `splitDraftId`/`contactId`/`price`/`fee`, während die
  Implementierung `contactId`/`savingsPlanId`/`feeAmount`/`taxAmount` bindet — Typsignatur
  identisch, daher kein Verhaltensbruch; alle Aufrufer positional (Controller Z. 1016–1029,
  DemoDataService Z. 553). Korrekte, notwendige Drift-Behebung; benannte-Aufrufe hätten
  vorher falsch gebunden.
- **T17 Zwei-Phasen-Apply:** Draft-Level-Fills (`SetAccountAsync`/`SetDescriptionAsync`)
  committen vor der Entry-Re-Evaluierung; schlägt die Neuplanung fehl, bleibt die
  Draft-Level-Wirkung — auditiert und fachlich eigenständig, aber der Call endet im
  Fehler mit Teileffekt (Edge Case, akzeptabel dokumentiert).
- **T16 `recognized=false`:** Audit-Record bleibt bewusst bestehen — die Pipeline
  persistiert die Datei als `StatementDetails` (`CreateDraftAsync` Z. 475–479);
  Begründung im Code korrekt.
- **SplitDraftId `inUse`-Prüfung** nicht über Contract-Fläche lesbar — verbleibt
  Service-seitig (dokumentiert in review.md).
- **Approval-Hülle:** `AiActionRequest`-Pfad existiert noch nicht; schreibvorbereitende
  Tools wirken direkt (katalogkonform, Genehmigungspflicht erst T20/T21/EP-10).
- Tests Moq-basiert auf Contract-Ebene (kein Integrationstest gegen die reale
  Pipeline) — für Adapter-Logik ausreichend, Abdeckung der genannten Pfade gut.
