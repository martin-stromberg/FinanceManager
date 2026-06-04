# Architektur-Review: Budget Impact Visibility während Buchung

> **Repository:** `martin-stromberg/FinanceManager`  
> **Review-Artefakte:**  
> - `docs/requirements/requirements-budget-impact-booking.md`  
> - `docs/architecture/architecture-blueprint-budget-impact-booking.md`  
> - `docs/architecture/entity-relationship-model-budget-impact-booking.md`  
> **Datum:** 2026-05-31  
> **Reviewer:** review-architecture  
> **Review-Status:** **Amber** (tragfähig, aber mit kritischen Architektur-Lücken vor Umsetzung)

---

## 1) Executive Summary

Der Blueprint passt grundsätzlich zur bestehenden FinanceManager-Architektur (Web → Application → Domain → Infrastructure, EF Core, bestehende StatementDraft-Endpunkte).  
Die Kernidee – **eine serverseitige Budget-Impact-Berechnung für Hint + Summary** – ist korrekt.

Die Umsetzungsreife ist jedoch noch nicht vollständig erreicht. Kritisch sind insbesondere:

1. **Fehlendes formales Konsistenzmodell** zwischen Echtzeit-Hinweis und finaler Summary (**Blocker**).
2. **Daten-/Domänenlücke** (`BudgetRuleImpactProfile` im ERM, aber nicht abgesichert im Architekturpfad als verbindliche Einführung).
3. **Nicht spezifiziertes Trigger-, Race- und Fallback-Verhalten** im UI/API-Zusammenspiel.

---

## 2) Architektur-Review (Layer, Module, Interfaces)

### 2.1 Positive Bewertung
- Schichtenmodell ist sauber und repo-konform (keine neue Plattform, keine fremde Runtime).
- Integrationspunkte nutzen bestehende Prozesse:
  - `POST /api/statement-drafts/{draftId}/entries/{entryId}/contact`
  - `POST /api/statement-drafts/{draftId}/entries/{entryId}/savingsplan`
  - `POST /api/statement-drafts/{draftId}/entries/{entryId}/save-all`
  - `POST /api/statement-drafts/{draftId}/entries/{entryId}/book`
  - `POST /api/statement-drafts/{draftId}/book`
- Geplantes Interface `IBudgetImpactEvaluationService` ist als Application-Use-Case-Schnittstelle sinnvoll.

### 2.2 Kritische Punkte
- **F-01 (Blocker):** Hint und Summary werden in unterschiedlichen Zeitpunkten erzeugt, aber ohne konsistenzsichernde Klammer (Snapshot/Fingerprint/Versionierung).
- **F-03 (Major):** Triggerverhalten (`contact`, `savingsplan`, `save-all`) ist funktional beschrieben, aber ohne verbindliches „latest-wins“-Protokoll.
- **F-04 (Major):** Unklar, ob `BudgetImpactEvaluation` transient oder persistiert geführt wird. Das beeinflusst API-Vertrag, Reproduzierbarkeit und Telemetrie.
- **F-09 (Major):** Optionaler `GET .../budget-impact` ist nicht klar gegenüber den bestehenden Mutations-Endpunkten abgegrenzt (Gefahr doppelter Read-Pfade).

---

## 3) Technologieentscheidungen und Alternativen

### 3.1 Bewertete Entscheidungen
- **Keine externe Rules Engine:** Für aktuelle Komplexität angemessen.
- **Serverseitige Bewertung:** Richtig bzgl. NFR-2 (Konsistenz), Security (`ownerUserId`) und Testbarkeit.
- **DTOs im Shared-Layer:** Passend zur Solution-Struktur mit `FinanceManager.Shared*`.

### 3.2 Lücken & praktikable Alternativen
- **F-02 (Major):** `BudgetRuleImpactProfile` ist modelliert, aber nicht als Implementierungsentscheidung abgesichert.
  - **Alternative A (bevorzugt):** Direkte Entität + Migration + EF-Config.
  - **Alternative B (interim):** Konfigurationswerte je Kategorie mit DB-Override; später Migration.
- **F-05 (Major):** `<500ms p95` ohne konkretisierten DB-/Index-Plan.
  - **Alternative:** Frühzeitiger Query-/Index-Plan (`Postings`, `BudgetPurpose(SourceType, SourceId)`, `BudgetRule`-Schlüssel).
- **F-08 (Minor):** Observability erwähnt, aber ohne messbaren Eventvertrag.
  - **Alternative:** Standardisierte Events/Metriken je Evaluation-Aufruf.

---

## 4) UI/UX-Review (Informationsarchitektur & Nutzbarkeit)

### 4.1 Stärken
- Inline-Hints direkt am Entry-Kontext sind fachlich stimmig.
- Abschluss-Summary mit Vorher/Nachher/Delta unterstützt FR-3 klar.

### 4.2 Risiken
- **F-03 (Major):** Kein klarer Umgang mit konkurrierenden Requests (alte Antwort überschreibt neue).
- **F-06 (Major):** Fehlender verbindlicher Neutral-/Fallback-Status bei unvollständigen Daten (NFR-5-Risiko).
- **F-07 (Minor):** Label „StronglyChanged“ ist potenziell nicht selbsterklärend (NFR-3-Risiko).

---

## 5) Qualitätsziele: Vollständigkeit und Trade-offs

### 5.1 Positiv
NFR-1 bis NFR-5 sind vollständig benannt und grundsätzlich auf Architekturmaßnahmen gemappt.

### 5.2 Zielkonflikte
- **NFR-1 (Latenz)** vs. **NFR-2 (Konsistenz)**: aggressive Optimierung kann zu divergierenden Ergebnissen führen.
- **NFR-1** vs. **NFR-5 (Fehlwarnrate)**: schnelle, aber unvollständige Berechnung kann falsche Warnungen erzeugen.
- **NFR-3 (Verständlichkeit)** vs. **FR-1.2 (Mehrzweckbewertung)**: mehr Details erhöhen kognitive Last.

### 5.3 Fehlende Konkretisierung
- Keine expliziten SLOs für Timeout-/Fehlerpfade.
- Kein verbindlicher Lasttest-Nachweis für p95-Ziel.
- Kein verpflichtender Telemetrievertrag für spätere Qualitätssicherung.

---

## 6) Priorisierte Findings und Verbesserungen

| ID | Priorität | Finding | Risiko | Konkrete Verbesserung |
|---|---|---|---|---|
| F-01 | **Blocker** | Konsistenzmodell Hint vs. Summary nicht formalisiert. | Verletzung NFR-2, widersprüchliche UI-Aussagen. | Zentrale deterministische Rechenfunktion + Snapshot/Fingerprint in Hint und Summary; Drift erkennbar markieren. |
| F-02 | **Major** | `BudgetRuleImpactProfile` nicht als verbindlicher Implementierungspfad abgesichert. | FR-2.1/NFR-4 nicht robust umsetzbar. | Entität + Migration + Service-Integration in derselben Iteration liefern. |
| F-03 | **Major** | Trigger-/Debounce-/Race-Verhalten nicht verbindlich. | Veraltete Hints, Request-Spitzen, UI-Flackern. | Trigger-Matrix + cancel/replace + request-id + latest-wins festlegen. |
| F-04 | **Major** | Persistenzstrategie für Evaluation/Summary unklar. | Später Refactor, unklare Auditfähigkeit. | ADR mit klarer Entscheidung (Phase 1 transient, Phase 2 optional persistiert). |
| F-05 | **Major** | Performanceziel <500ms ohne Query-/Index-Nachweis. | NFR-1 verfehlt bei realem Volumen. | Vorab Queryplan, notwendige Indizes, Benchmark-Tests in CI. |
| F-06 | **Major** | Kein verbindlicher Neutralstatus bei unvollständigen Daten. | Fehlwarnungen, NFR-5-Verletzung. | Standardisierter `Neutral`-Pfad mit reason-code und erklärendem UX-Text. |
| F-07 | **Minor** | Begriffsrisiko bei „StronglyChanged“. | Missverständnisse (NFR-3). | Wording-Review + UX-Testfälle mit 85%-Zielquote. |
| F-08 | **Minor** | Observability nicht konkretisiert. | Schlechter Nachweis/Fehleranalyse. | Metrikvertrag (`evaluation_duration_ms`, `affected_purposes`, `highest_severity`, `neutral_reason`). |
| F-09 | **Major** | Optionaler GET-Impact-Pfad ohne klare Verantwortungsgrenze. | Doppelpfade, inkonsistente Ergebnisse. | Entweder streichen oder strikt als Read-Projection mit identischem Servicekern definieren. |

---

## 7) Traceability-Matrix (Major/Blocker ↔ FR/NFR)

| Finding | FR-Bezug | NFR-Bezug | Begründung |
|---|---|---|---|
| F-01 | FR-1, FR-3 | **NFR-2** | Konsistente Werte über Echtzeit-Hinweis und Abschluss hinweg. |
| F-02 | FR-2, FR-2.1 | **NFR-4** | Regelkategorien benötigen wartbare, zentrale Schwellenwerte. |
| F-03 | FR-1.1, FR-1.2 | NFR-1, NFR-5 | Triggerqualität steuert Latenz und Fehlwarnrate. |
| F-04 | FR-3, FR-4 | NFR-2, NFR-4 | Persistenzentscheidung beeinflusst Reproduzierbarkeit und Wartbarkeit. |
| F-05 | FR-1, FR-2 | **NFR-1** | Performanceziel ohne technische Maßnahmen nicht erreichbar. |
| F-06 | FR-2, FR-4 | **NFR-5**, NFR-3 | Fehlende Daten dürfen keine irreführende Warnung erzeugen. |
| F-09 | FR-1, FR-3 | NFR-2, NFR-4 | Doppelte API-Pfade erhöhen Inkonsistenz- und Wartungsrisiko. |

---

## 8) Empfohlene Implementierungssequenz (Risikomitigation)

### Phase A – Architekturentscheidungen schließen (Blocker-first)
1. ADR: Konsistenzmodell Hint/Summary (F-01).
2. ADR: Persistenzstrategie (F-04).
3. Servicevertrag finalisieren (`IBudgetImpactEvaluationService` als Single Source of Truth).

### Phase B – Domäne/Datenmodell umsetzbar machen
4. `BudgetRuleImpactProfile` (oder klar definierte Alternative) inkl. Migration (F-02).
5. DTO-/API-Verträge synchron zu ERM/Blueprint konsolidieren.

### Phase C – UX/API Laufzeitstabilität
6. Trigger-Matrix + Race-Handling + latest-wins (F-03).
7. Neutral-/Fallback-Pfade inkl. reason-codes implementieren (F-06).

### Phase D – Performance- und Qualitätsnachweis
8. Query-/Index-Optimierung + Benchmarking für `<500ms p95` (F-05).
9. Telemetrievertrag implementieren und auswertbar machen (F-08).

### Phase E – UX-Verständlichkeit absichern
10. Wording/Usability-Validierung für Kategorien (F-07).

---

## 9) Praktische .NET-Umsetzung für dieses Repo

- Controller bleiben dünn; Fachlogik in Application/Domain.
- Reuse der bestehenden Services (`BudgetPurposeService`, `BudgetPlanningService`, `BudgetRuleService`) statt Parallelpfade.
- Durchgehendes Owner-Scoping (`ownerUserId`) in allen Query-Pfaden.
- Tests entlang bestehender Testprojekte:
  - Domain-/Service-Tests: `FinanceManager.Tests`
  - API/Integrationspfade: `FinanceManager.Tests.Integration*`

---

## 10) Abschlussbewertung

Die Architektur ist **grundsätzlich geeignet**, aber noch nicht freigabereif.  
Vor Umsetzung müssen mindestens **F-01 (Blocker)** sowie die Major Findings **F-02, F-03, F-04, F-05, F-06 und F-09** verbindlich geschlossen werden.
