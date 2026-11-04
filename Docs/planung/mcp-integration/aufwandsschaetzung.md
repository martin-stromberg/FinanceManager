# Aufwandsschätzung – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 11 der Anforderung; Aufwandsschätzung je Phase
und je größerem Baustein in Personentagen (PT). Grundlage sind die
konzipierten Bausteine aus [architekturkonzept.md](architekturkonzept.md),
[datenmodell-erweiterungen.md](datenmodell-erweiterungen.md), den
33 Tools aus [scope-und-toolkatalog.md](scope-und-toolkatalog.md) und die
Phasenzuordnung aus [roadmap.md](roadmap.md). Die Feature-Ebene steht in
[product-backlog.md](product-backlog.md).

---

## 1. Methodik und Annahmen

- **Einheit:** Personentage (PT) als **Bandbreite** (optimistisch–pessimistisch)
  plus Planwert. Schätzgefühl: bewusst konservativ, da es sich um
  sicherheitskritische Neuland-Funktionen handelt.
- **Schätzbasis:** kein Story-Pointing, sondern bausteinbezogene Aufwände
  aus der Konzeptstruktur (Entitäten, Contracts, Pipeline-Positionen,
  UI-Komponenten, Migration). Jeder Baustein nennt seine Haupttreiber.
- **Referenzrahmen:** Umsetzung durch 1–2 Entwickler, die das Repo kennen
  (Konventionen `I*Service`/`IApiClient`/MVVM/EF-Migrationen sind belegt);
  kein neuer Tech-Stack außer dem MCP-Transport.
- **Annahmen (Unsicherheiten, die die Bandbreite treiben):**
  - A1: Das MCP-C#-SDK unterstützt Streamable HTTP serverseitig ausreichend;
    andernfalls Eigenbau des Transports (B2 obere Grenze).
  - A2: Die datensatzscharfe Filter-/Deckel-Auswertung des `McpScopeEvaluator`
    lässt sich als EF-Query-Erweiterung umsetzen; aufwendige Fälle
    (polymorphe `Attachment`-Zieldecke, `Posting`-`Kind`-Ableitung) sind
    einkalkuliert, aber der größte Einzelunsicherheitsfaktor.
  - A3: UI-Aufwände folgen den bestehenden `BaseListViewModel`/
    `BaseCardViewModel`/Overlay-Mustern ohne konzeptionelle
    Neuerfindung; der Scope-Matrix-Editor ist das komplexeste neue
    UI-Element.
  - A4: Testaufwand wird als eigener Block je Phase geführt
    (Unit-/Integrations-/E2E-Tests, Migrations- und Sicherheits-
    durchspielung) – nicht in den Baustein-Aufwänden versteckt.
  - A5: Planung, Review und Dokumentation des *Umsetzungs*-Projekts sind
    nicht Bestandteil dieser Schätzung (reine Umsetzung).

---

## 2. Schätzung je Baustein

Phase-Zuordnung siehe [roadmap.md](roadmap.md); „P" = Roadmap-Phase.

|| # | Baustein | P | Inhalt | PT (Band) | Planwert | Treiber/Unschärfe |
|---|---|---|---|---|---|---|
|| B1 | Datenmodell & Migration | 1+4 | Entitäten `McpAccess`, `McpAccessScope`, `AiActionRequest`, `McpAuditEntry`, `McpAuditEntryChange`; Enums `McpAccessLevel`/`McpDataType`/`McpRightKind`/`McpApprovalMode`/`McpAccessStatus`/`AiActionRequestStatus`/`McpAuditCategory`; DbSets + `OnModelCreating` (Indizes, Unique, Enum→short); EF-Migration via `ApplyMigrationsAndSeed`; `*BackupDto`-Aufnahme (Token-Hash ausgenommen); Retention-Felder | 8–12 | 10 | gut strukturiert im Konzept; Aufwandsteilung P1 (≈6–8) / P4 (`AiActionRequest`, ≈2–4) |
|| B2 | MCP-Endpunkt `/mcp` (Transport) | 1 | Streamable-HTTP-Endpunkt (POST + SSE-Upgrade), Session-Zuordnung (`McpSessionHub`), Endpoint-Gruppe pfadbeschränkt, SDK-Integration bzw. Eigenbau-Fallback | 6–10 | 8 | größte Framework-Unsicherheit (A1); SSE selbst ist P5, hier nur Transport-Basis |
|| B3 | Token-Authentifizierung | 1 | PAT-Generierung `fm_mcp_<kürzel>_<secret>` (≥160 Bit), `pbkdf2|…`-Hash, `McpAuthHandler` (Kürzel→Access→Verify→Status/Ablauf/IP), `IMcpAccessContext` (Token→`OwnerUserId`), Fehlerbild 401 | 5–8 | 6 | bewährte Primitive (`Pbkdf2IdentityPasswordHasher`, `FixedTimeEquals`); IP-Bindung (`AllowedIpRanges`-CIDR-Auswertung) kleiner Zusatz |
|| B4 | Zugangsverwaltung Service + API | 1 | `IMcpAccessService`/`McpAccessService` (Anlage, Rotation, Widerruf, Status, Stufen-/Modus-/Ablauf-/IP-Pflege, serverseitige Stufenmatrix), `McpAccessesController` (11 Endpunkte), DTOs `Shared/Dtos/Mcp/`, `ApiClient.Mcp.cs`-Partial | 8–11 | 9 | viele kleine Endpunkte nach Bestandsmuster; Stufenmatrix serverseitig (`GET /levels`) |
|| B5 | ScopeEvaluator | 1 | `IMcpScopeEvaluator`/`McpScopeEvaluator`: Prüfstufen 0–3, effektives Profil (Stufenprofil vs. `McpAccessScope`-Zeilen), Filter-CSV-Auswertung (`ReportFavorite`-Muster, UND/ODER, unklassifiziert nicht erfasst), Ableitungstabelle (`Posting` je `Kind`, `StatementEntry`, `StatementDraftEntry`, `AccountLinkedIban`, `AliasName`, `SecurityPrice`, `Attachment`-Zieldecke), `AccountShare`-Deckel | 10–15 | 13 | komplexester Baustein (A2); datensatzscharfe EF-Filterung; ausführliche Testmatrix nötig |
|| B6 | Tool-Registry & Dispatcher | 1 | `McpToolRegistry` (`IMcpToolDescriptor` je Tool: Name, Scopes, Klasse, JSON-Schema, Deprecation), `katalog-v1`, `McpToolDispatcher` (Routing, Parameter-Bindung, Scope-Fehlerbild), `fm_capabilities_describe`-Auskunft | 4–6 | 5 | deskriptor-basiert, „kein Tool ohne Scope" build-erzwingbar |
|| B7 | Tool-Adapter lesend (14 Tools) | 1 | T01–T10, T13 `fm_statement_drafts_read`, T14 `fm_statement_draft_validate`, T30 `fm_posting_reversal_check`, T32; **inkl. neuem Contract `IStatementImportQueryService`** (T08-Voraussetzung) | 11–15 | 13 | dünne Adapter auf `I*Service`, aber 14 Stück + Whitelist-Parameter/Output-Konventionen (`untrusted_data`); `IStatementImportQueryService` ≈ 3–4 PT enthalten |
|| B8 | Tool-Adapter vorschlagend (4 Tools) | 2 | T11 `fm_contacts_suggest`, T12 `fm_budget_rules_suggest`, T15 `fm_statement_draft_analyze`, T19 `fm_statement_draft_prepare_booking` (Buchungsplan-Vorschau) | 5–7 | 6 | Vorschlagsstrukturen + „kein Persistenz-Nebeneffekt" absichern; `ClassifyAsync`-Einbindung |
|| B9 | Tool-Adapter schreibvorbereitend (11 Tools) | 3 | T16 `fm_statement_draft_import`, T17 `fm_statement_draft_complete`, T18 `fm_statement_draft_edit`, T22–T29 (direkte Teilmengen; finale Teilmengen bleiben abgelehnt bis P4); Batch-/Upload-Limits; `untrusted`-Eingabeverarbeitung | 12–17 | 15 | größte Adapter-Menge; fachliche Zusatzregeln `fm_statement_draft_validate` (§4.4); `ApplyBatchEntryUpdatesAsync`-Atomarität; Anhang-Upload-Pfad |
|| B10 | Approval-Queue | 4 | `AiActionRequest`-Fachlogik, `McpApprovalGate` (Pipeline-Pos. 8), `AiActionExecutor` (Hosted Service, atomarer Claim `Approved→laufend`, Guard-Re-Prüfung, `ExecutionResult`/`FailureReason`), Expired-Job, `IAiActionRequestService`, `INotificationWriter`-Einbindung, `IdempotencyKey` | 10–14 | 12 | persistente Queue statt In-Memory; SQLite-Claim-Atomarität; Fehlerbilder `Err_*` |
|| B11 | Tool-Adapter genehmigungspflichtig | 4 | T20 `fm_statement_draft_book`, T21 `fm_statement_draft_discard`, T31 `fm_posting_reverse` + finale Teilmengen von T22–T29 (Merge/Löschen/Archiv) über den Request-Pfad; Wirkungsvorschau-Aufbereitung je Aktionstyp (serverseitig) | 5–8 | 6 | Ausführung = Adapter + Guard-Re-Prüfung; Vorschau-Renderer je Typ |
|| B12 | Audit-Subsystem | 1–5 | `McpAuditWriter` (transaktional gekoppelt), 5 Ereigniskategorien, abgestufte `PayloadJson`-Detailtiefe, Feld-Delta-Erzeugung (`McpAuditEntryChange`), Hash-Kette (`SequenceNo`/`PrevHash`/`EntryHash`), Retention-Executor (`RetentionPurged`), Export/Verifikation-Service | 8–12 | 10 | Teilung: P1 ≈5–7 (Schreibpfad+Kette), P3 ≈1–2 (DataChange-Auswertung), P5 ≈2–3 (Retention, Export-Finish) |
|| B13 | Notfall, Rate-Limiting, Anomalie | 1 | `Mcp:*`-Optionsbindung + Kill-Switch/Schreib-Stopp an Pipeline-Positionen 4/8, `RateLimitPolicies.Mcp*` (Token-/IP-Partition, Limits je Klasse), Fehlversuchszähler → `IpBlock`, Anomalie-Benachrichtigungen (`SystemAlert`/EventDriven) | 4–6 | 5 | Wiederverwendung `AddRateLimiter`/`IpBlockMiddleware`/`INotificationWriter` |
|| B14 | UI: Zugangsverwaltung + Token | 1 | Nav-Eintrag + Badge, `/list/mcp-accesses` (`McpAccessListViewModel`), `/card/mcp-accesses/{id}` (`McpAccessCardViewModel`, Sections), Anlage-Assistent (3 Stufen, Einmalanzeige, Pflicht-Bestätigung, TLS-Hinweis), Stufenwechsel-/Widerruf-Dialoge, resx de/en | 8–11 | 9 | bewährte MVVM-Muster; Overlay-Dialoge `UiOverlaySpec`; `IConfirmationService` |
|| B15 | UI: Rechte-/Filterverwaltung | 2 | Stufen-Auswahlkarten mit Klartext, Scope-Matrix-Editor 18×7 (deaktivierte Zellen, Implikationshilfe, Effektiv-Anzeige), Filter-Section (5 Dimensionen, Kategorielisten, Semantik-Hinweise), Genehmigungsmodus-Auswahl | 8–11 | 9 | komplexestes neues UI-Element (A3); Matrix-Zustandslogik clientseitig, Wahrheit serverseitig |
|| B16 | UI: Audit-Ansicht | 2 | `McpAuditViewModel` + `McpAuditController`-Consumer: Timeline, Filterleiste, Drilling (Feld-Deltas, Request-/Entitäts-Links), Export JSON/CSV, „Integrität prüfen", Klartext-Lokalisierung | 6–9 | 7 | Listen-/Detail-Muster bekannt; Drilling-Verknüpfungen auf Bestandsrouten |
|| B17 | UI: Genehmigungscenter | 4 | `McpApprovalCenterViewModel` `/card/mcp-approvals`: Eingangskorb, Wirkungsvorschau-Typen (Buchungsplan/Storno/Lösch-Merge/Batch), Mehrfach-Entscheidung, Historie, Badge-Count, Notification-Links | 7–10 | 8 | Wirkungsvorschau-Darstellung je Aktionstyp; Teilerfolgs-Meldung bei Batch |
|| B18 | UI: Admin-Sicherheitsübersicht | 4 | `McpSecurityOverviewViewModel` + `GET /api/mcp-audit/security-overview`: Schalter-Zustand, Anomalien, IP-Sperren (keine Fremd-Fachdaten) | 3–4 | 3 | kleine Admin-Ansicht nach Bestandsmuster |
|| B19 | Agenten-Ausbau | 5 | SSE-Upgrade für Server-Benachrichtigungen, Pre-approved-Härtung (Deckel-Empfehlungen, Einrichtungs-Bestätigung), wiederkehrende Verarbeitungsmuster (Runbooks/Monitoring-Sichten aus Audit-Aggregationen), Katalog-Deprecation-Sprung | 8–14 | 11 | stärkste Unschärfe; Umfang hängt davon ab, welche Agenten-Muster konkret gefordert werden |

**Summe Bausteine:** **136–200 PT**, Planwert ≈ **168 PT**.

---

## 3. Schätzung je Phase

Baustein-Anteile je Phase (Splittungen gemäß Tabelle oben) plus
phasenbezogener Testblock (A4, ≈15 % der Phase, min. 3 PT):

|| Phase | Enthaltene Bausteine | PT (Band) | Planwert |
|---|---|---|---|---|
|| **P1 Read-Only Analyse** | B1-Teil (6–8), B2, B3, B4, B5, B6, B7, B12-Teil (5–7), B13, B14 + Testblock (10–15) | **77–112** | ≈ 94 |
|| **P2 Assistenz/Vorschläge** | B8, B15, B16 + Testblock (3–4) | **22–31** | ≈ 26 |
|| **P3 Kontoauszugs-Assistent** | B9, B12-Teil (1–2) + Testblock (2–3) | **15–22** | ≈ 19 |
|| **P4 Genehmigungspflichtige Aktionen** | B1-Teil (2–4), B10, B11, B17, B18 + Testblock (4–6) | **31–46** | ≈ 39 |
|| **P5 Agenten-Workflows** | B19, B12-Teil (2–3) + Testblock (2–3) | **12–20** | ≈ 16 |
|| **Summe** | | **157–231 PT** | **≈ 194 PT** |

In Personenmonaten (20 PT/PM): ≈ **8–11,5 PM** Gesamtband, Planwert
≈ 9,7 PM. Phase 1 trägt mit ≈ 48 % die Hälfte des Aufwands – sie
finanziert das komplette Fundament (Transport, Auth, Evaluator, Audit,
Verwaltung), von dem alle Folgephasen profitieren; die Folgephasen sind
deutlich schmaler, weil kein Infrastruktur-Baustein wiederholt wird.

### Phasenabhängige Bemerkungen

- **P1-Splitting möglich:** T08 und der `IStatementImportQueryService`
  (≈3–4 PT) sowie T30 sind innerhalb P1 nachrangig; bei Termindruck
  verschiebbar, ohne die Phase zu brechen (Tool-Scope bleibt deklariert,
  Aufruf schlägt sauber fehl).
- **P2/P3 sind koppelbar:** Der fachliche Nutzen entsteht primär durch
  die Kombination; bei knappen Kapazitäten kann P3 mit P2 fusioniert
  werden (≈37–53 PT), ohne Architekturbruch.
- **P4 ist der Vertrauens-Schritt:** Kürzungen hier (z. B. auf
  Guard-Re-Prüfung oder Idempotenz verzichten) wären Sicherheits-
  Abstriche – nicht empfohlen; der Aufwand ist durch das
  Approval-/Audit-Modell gut begrenzt.
- **P5 ist optional skalierbar:** Der Kern „Agenten laufen über denselben
  Pfad" benötigt faktisch nur SSE + Pre-approved-Betrieb (unteres
  Bandende); Monitoring/Runbooks sind Ausbau.

---

## 4. Risikozuschläge

|| Risiko | Betroffene Bausteine | Zuschlagsempfehlung |
|---|---|---|---|
|| MCP-C#-SDK trägt Streamable HTTP nicht ausreichend → Eigenbau | B2 | +4–8 PT (im Band bereits anteilig; bei Eigenbau volles obere Ende +4) |
|| ScopeEvaluator: datensatzscharfe Filter über alle Ableitungsfälle komplexer als gedacht (polymorphe `Attachment`-Decke, `Posting`-`Kind`) | B5 | +3–5 PT Puffer empfohlen |
|| Wirkungsvorschau-Renderer je Aktionstyp aufwendiger (fachliche Texte de/en, alle 8 Pflege-Typen) | B11, B17 | +2–3 PT |
|| Lokalisierung de/en über alle neuen Oberflächen/Tool-Texte | B14–B18 | +2–4 PT (resx-Umfang) |
|| E2E-/Sec-Durchspielung über mehrere Rollen/Zugänge | Testblöcke | in Testblöcken enthalten; +2–3 PT Reserve |

**Gesamtempfehlung:** Planwert ≈ **194 PT**; für Roadmap-/Budget-
Kommunikation konservativ **200–230 PT** (inkl. Zuschläge) ansagen –
das entspricht dem oberen Bandende und deckt die genannten Risiken
ohne weitere Pufferlogik ab.

---

## 5. Was die Schätzung bewusst nicht enthält

- Keine Implementierung heute: Die Schätzung ist Konzeptartefakt und
  Vorlage für das spätere Umsetzungsprojekt, kein Commitment.
- Kein Betrieb/Skalierungsaufwand (Single-Instance: Deployment,
  Auto-Update und Backup sind Bestandsmechanik; Zusatzaufwand
  vernachlässigbar, in B1/B12 anteilig).
- Kein MCP-Client-seitiger Aufwand (Konfiguration externer KI-Systeme
  liegt beim Benutzer).
- Keine Bankanbindung, kein generischer Datenzugriff – Nicht-Ziele.
