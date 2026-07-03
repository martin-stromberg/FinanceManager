# Architektur-Review: Massenimport ING Wertpapierkurse

> **Status:** ✅ Findings nach Implementierung überprüft  
> **Stand:** 2026-07-03

## 1. Kontext & Review-Umfang

**Geprüfte Artefakte**
- `issue.md`
- `Docs/requirements/massenimport-ing-wertpapierkurse-requirements.md`
- `Docs/architecture/architecture-blueprint-massenimport-ing-wertpapierkurse.md`
- `Docs/architecture/entity-relationship-model-massenimport-ing-wertpapierkurse.md`

**Review-Ziel**  
Bewertung des bestehenden Blueprints hinsichtlich Architekturtragfähigkeit, Umsetzbarkeit und Risiken mit Fokus auf Dialogpflicht/Dialog-Skip, Nachvollziehbarkeit pro Datei, sichere Wertpapierzuordnung sowie Robustheit bei unbekannten Dateien und Teilimporten.

## 2. Strukturierte Bewertung von Schichten, Modulen und Schnittstellen

### 2.1 Schichten- und Modulzuschnitt

Positiv:
- Klare Orchestrierung über `MassImportOrchestrator` mit sinnvoller Trennung von Erkennung (`ImportFactory`), Vorbelegung (`SecurityGuessService`) und Ausführung.
- Dialogmodell (`RecognitionDialogModel`) deckt fachlich die geforderten Dateiattribute ab.
- Erweiterbarkeit über Service-Descriptor ist konsistent zu FR-5/NFR-5.

Bewertung:
- **Grundstruktur ist tragfähig**, aber zentrale Policies (Skip-Entscheidung, Import-Freigabe, Fehler-/Teilimportregeln) sind noch nicht hinreichend als verbindliche Entscheidungslogik formalisiert.

### 2.2 Schnittstellenqualität

Positiv:
- Relevante Interfaces sind benannt (`IImportFactory.Resolve`, `ISecurityGuessService.TryGuess`).
- Ergebnisobjekte enthalten fachlich notwendige Felder (`FileType`, `ServiceDisplayName`, `CanImport`).

Risiko:
- Es fehlen explizite Invarianten an den Schnittstellen (z. B. „Kursdatei ohne valide Security darf nie `CanImport=true` liefern“).
- Statusübergänge zwischen Dialogentscheidung und tatsächlicher Ausführung sind nicht als atomare Domänenregel beschrieben.

## 3. Bewertung der Technologieentscheidungen

Positiv:
- Factory-Pattern für Dateityp-/Service-Erkennung ist passend und zukunftssicher.
- Regelbasierte Dateinamen-Erkennung ist pragmatisch und transparent für einen ersten Ausbaustand.
- Zwei-Phasen-Flow (Voranalyse → Bestätigung/Ausführung) adressiert korrekt das Risiko ungewollter Imports.

Einschränkungen:
- Dateinamenbasierte Zuordnung ist fehleranfällig bei Mehrdeutigkeit; ohne zusätzliche Validierungsregeln steigt das Risiko falscher Zuordnungen.
- Logging-Ziel ist definiert, aber ohne klaren technischen Audit-Mechanismus (Korrelation pro Batch/Datei) bleibt Nachvollziehbarkeit lückenhaft.

## 4. UI/UX-Review

Positiv:
- Dialog enthält alle fachlich notwendigen Eingriffe: Ausschluss pro Datei, sichtbarer Dateityp, Service-Anzeigename, manuelle Wertpapierzuordnung.
- Einstellungsabhängiges Dialogverhalten ist nutzerzentriert (Kontrolle vs. Geschwindigkeit).

Verbesserungsbedarf:
- Für größere Batches fehlen konkrete UX-Regeln (Sortierung/Filter, Hervorhebung unvollständiger Zeilen, Sammelaktionen).
- Die Leitplanke „Dialogpflicht vs. Skip“ ist textlich vorhanden, aber nicht als eindeutig prüfbare Entscheidungs-Matrix spezifiziert.

## 5. Bewertung der Qualitätsziele

- **Sicherheit:** grundsätzlich adressiert (keine Inhaltslogs), aber Schutz vor inkonsistenter manueller Zuordnung ist noch nicht vollständig abgesichert.
- **Korrektheit:** Zielbild stimmt; kritische Invarianten müssen verbindlich in Orchestrator/Validation verankert werden.
- **Performance:** Zielwert (≤2s/50 Dateien) ist plausibel, jedoch ohne Mess- und Degradationsstrategie.
- **Erweiterbarkeit:** sehr gut vorbereitet durch Factory + ServiceDescriptor.
- **Zuverlässigkeit/Teilimport:** fachlich gefordert, aber bei Mischfehlern noch nicht vollständig operationalisiert (Retry-/Statusmodell).

## 6. Schwachstellen, Risiken und priorisierte Findings

| Priorität | Befund | Auswirkung | Maßnahme |
|---|---|---|---|
| **Major** | Dialog-Skip-Regeln sind nicht als harte Entscheidungslogik (Matrix/Invarianten) definiert, v. a. bei gemischten Batches und `UNKNOWN`-Dateien. | Risiko von falschem Auto-Import oder inkonsistentem Dialogverhalten; Verstoß gegen FR-4/FR-9 möglich. | Verbindliche Policy-Matrix definieren (pro Dateityp/Status/Einstellung), zentral im Orchestrator implementieren und mit Matrix-Tests absichern. |
| **Major** | Manuelle Wertpapierzuordnung ist nicht gegen Zustandsänderungen zwischen Dialog und Ausführung abgesichert (TOCTOU, inaktive/gelöschte Security). | Falsche Kurszuordnung oder Laufzeitfehler trotz vorheriger Bestätigung. | Re-Validierung direkt vor Persistierung je Datei; Import nur mit aktiver, eindeutiger Security-ID; sonst erzwungener Dialog/Re-Entscheidung. |
| **Medium** | Nachvollziehbarkeit pro Datei ist konzeptionell benannt, aber ohne verbindliches Audit-Schema (Batch-/Datei-Korrelation, Änderungsherkunft). | Erschwerte Fehleranalyse, geringe Revisionssicherheit bei Support/QA. | Einheitliches Audit-Event-Schema definieren: `batchId`, `fileId`, `fileType`, `serviceDisplayName`, `excluded`, `selectedSecurityId`, `decisionSource`, `executionStatus`, `traceId`. |
| **Medium** | Teilimport-Regeln für unbekannte/fehlerhafte Dateien sind nicht vollständig operational (Retry, Endstatus, Fehlerklassifikation). | Uneinheitliches Laufverhalten, potenzielle Doppelimporte oder unklare Nutzerkommunikation. | Zustandsautomat für `ImportBatchFile.execution_status` inkl. terminaler Zustände und Retry-Regeln festlegen; idempotente Importausführung sicherstellen. |
| **Low** | UX für große Dateimengen ist nicht spezifiziert (Filter, Sortierung, Fehlerfokus). | Höherer Bedienaufwand und erhöhte Fehlbedienung bei realen Batchgrößen. | Dialog um Filter „nur problematische Dateien“, Sortierung nach Dateityp/Status und visuelle Pflichtfeldmarker erweitern. |
| **Low** | Herkunft/Fallback von `service_display_name` ist nicht als Governance-Regel definiert. | Inkonsistente Anzeige oder leere Service-Namen im Dialog/Log. | Zentralen Service-Katalog mit Pflichtfeldvalidierung und Fallback-Strategie (technischer Key) etablieren. |

## 7. Explizite Annahmen und Unsicherheiten

**Annahmen**
- `ImportBatch`/`ImportBatchFile` bleiben primär Runtime-Modelle; Persistenz ist optional.
- Security-Stammdaten sind während Importlauf verfügbar und eindeutig adressierbar.
- Dialog-Skip ist nur zulässig, wenn alle Pflichtangaben pro importierbarer Datei vollständig und valide sind.

**Unsicherheiten**
- Unklar, ob Audit-/Historisierungspflichten (Compliance) dauerhafte Speicherung von Batch-Entscheidungen erfordern.
- Nicht final geklärt, wie Mehrdeutigkeiten bei Dateinamen-Matching quantitativ bewertet werden (Confidence/Schwellenwert).
- Nicht beschrieben, ob parallele Importe desselben Nutzers synchronisiert oder entkoppelt verarbeitet werden.

## 8. Freigabeempfehlung

**Empfehlung: Conditional Go**

Die Architektur ist in ihrer Grundstruktur solide und umsetzbar. Für eine belastbare Umsetzung vor Implementierungsstart müssen jedoch die beiden **Major-Findings** verbindlich geschlossen werden.

**Top-3 Maßnahmen vor Umsetzung**
1. Verbindliche Dialog-Skip-/Dialogpflicht-Policy als Entscheidungs-Matrix inkl. automatisierter Tests festlegen.
2. Harte Re-Validierung der Wertpapierzuordnung unmittelbar vor Importausführung implementieren.
3. Auditierbares, dateigenaues Event-/Logging-Schema mit Batch-/Datei-Korrelation definieren.

## 9. Umsetzungsabgleich nach Implementierung

| Ursprüngliches Finding | Implementierungsnachweis | Status |
|---|---|---|
| Skip-Matrix als harte Policy fehlt | `MassImportOrchestrator.IsDialogRequired(...)` implementiert `AlwaysConfirm` vs. `OnMissingInformation`; testet Unknown/CanImport/Security-Zuordnung. | ✅ Geschlossen |
| Re-Validierung vor Persistierung fehlt | `ImportSecurityPricesAsync(...)` prüft `SelectedSecurityId`, lädt Security erneut via `_securityService.GetAsync(...)` und erfordert `IsActive`. | ✅ Geschlossen |
| Auditierbares Dateilogging fehlte | `LogAudit(...)` protokolliert `batchId`, `fileId`, `fileName`, `fileType`, `serviceDisplayName`, `excluded`, `selectedSecurityId`, `decisionSource`, `executionStatus`, `traceId`. | ✅ Geschlossen |
| Teilimport-/Statusmodell unklar | `MassImportFileExecutionStatus` mit `Pending`, `Skipped`, `Imported`, `Failed` in DTO/Orchestrator umgesetzt. | ✅ Geschlossen |

## 10. Aktualisierte Freigabeempfehlung

**Empfehlung: Go**

Die zuvor als Major klassifizierten Kernpunkte sind im Code und in Tests umgesetzt. Offene Punkte betreffen aktuell keine blockierenden Architektur-Risiken für den dokumentierten Feature-Scope.
