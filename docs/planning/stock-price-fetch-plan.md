# Planungsübersicht: Stock-Price-Fetch-Error-Recovery

> **Primäranforderung:** [`../../06102d51-0369-438d-b08a-8cd5f738ab23.copilot-task.md`](../../06102d51-0369-438d-b08a-8cd5f738ab23.copilot-task.md)  
> **Status:** 🔄 In Arbeit (Umsetzung begonnen, Abschluss ausstehend)  
> **Version:** 1.0  
> **Datum:** 2026-05-30  
> **Autor:** Planning-Orchestrator

---

## 1. Zweck dieser Übersicht

Dieses Dokument konsolidiert die Ergebnisse des vollständigen Planning-Orchestrator-Workflows und verlinkt die zugehörigen Detailartefakte.

- Anforderungen: [`../requirements/stock-price-fetch-error-requirements.md`](../requirements/stock-price-fetch-error-requirements.md)
- Architektur-Blueprint: [`../architecture/architecture-blueprint-stock-price-fetch.md`](../architecture/architecture-blueprint-stock-price-fetch.md)
- ERM: [`../architecture/entity-relationship-model-stock-prices.md`](../architecture/entity-relationship-model-stock-prices.md)
- Architektur-Review: [`../improvements/review-stock-price-fetch-error.md`](../improvements/review-stock-price-fetch-error.md)

---

## 2. Konsolidierter Ist-Stand

- Die Primäranforderung fordert den Abschluss einer begonnenen, aber unvollständigen Implementierung.
- Der aktuelle technische Blocker ist dokumentiert und reproduzierbar:
  - **CS1061** in `FinanceManager.Web/Services/SecurityPricesBackfillExecutor.cs(153,55)`
  - Ursache: Zugriff auf `s.HasPriceError` bei anonymer Projektion ohne diese Property.
- Das Feature „Stock-Price-Fetch-Error-Recovery“ ist daher aktuell **nicht releasefähig**.

---

## 3. Ergebnis je Orchestrator-Schritt

| Schritt | Artefakt | Ergebnis |
|---|---|---|
| 1 | Anforderungen | Funktionale/Nicht-funktionale Anforderungen, ACs, Soll/Ist und Build-Befund vollständig dokumentiert |
| 2 | Architektur-Blueprint | Zielarchitektur, Komponenten, Datenfluss, Fehlerbehandlung, Qualitätsziele und Umsetzungssequenz beschrieben |
| 3 | ERM | Persistierte Entitäten, Fehlerstatus-Felder, Beziehungen, Constraints und DTO-/Projection-Regeln dokumentiert |
| 4 | Architektur-Review | Priorisierte Findings inkl. Blocker (CS1061) und Maßnahmenkatalog erstellt |
| 5 | Planungskonsolidierung | Dieses Dokument verlinkt und synchronisiert alle Ergebnisse |

---

## 4. Umsetzungsplan (aus den Artefakten abgeleitet)

### Meilenstein M1 – Compile-Fix und Vertragsstabilisierung
- Backfill-Projektion auf typisierten Contract umstellen (inkl. `HasPriceError`).
- `SecurityPricesBackfillExecutor` kompilierbar machen.
- **Abnahme:** `dotnet build FinanceManager.sln --no-restore` ohne CS1061.

### Meilenstein M2 – Recovery-Fluss absichern
- Skip-Logik nur bei `toInclusive < fromInclusive` **und** `HasPriceError == false`.
- Erfolgreiche Abrufe müssen `ClearPriceErrorAsync` auslösen.
- Fehlerfälle müssen `SetPriceErrorAsync` zuverlässig setzen.
- **Abnahme:** relevante Recovery-Tests grün.

### Meilenstein M3 – Qualitäts- und Review-Abschluss
- Review-Findings Blocker/Major abarbeiten.
- Dokumentationskette konsistent halten (Anforderungen ↔ Blueprint ↔ ERM ↔ Review ↔ Plan).
- **Abnahme:** Freigabeempfehlung im Review auf „freigabefähig“ aktualisierbar.

---

## 5. Verbindliche Abnahmekriterien (planungsseitig)

- [ ] Build ist grün, insbesondere ohne CS1061 in `SecurityPricesBackfillExecutor.cs(153,55)`.
- [ ] Recovery-Verhalten für Worker und Backfill ist testseitig nachgewiesen.
- [ ] `HasPriceError` bleibt bis zum erfolgreichen Fetch fachlich korrekt gesetzt.
- [ ] Alle fünf Artefakte sind vorhanden, gegenseitig verlinkt und konsistent.

---

## 6. Risiken und Gegenmaßnahmen

| Risiko | Auswirkung | Gegenmaßnahme |
|---|---|---|
| Weitere Property-Drifts in anonymen Projektionen | Neue Compile- oder Laufzeitfehler | Typisierte Projection-Contracts/DTOs als Standard |
| Hohe Warning-Last im Build | Erschwerte Wartbarkeit | Priorisierter Warning-Abbau außerhalb dieses Features |
| Unklare DB-Constraint-Strategie bei `SecurityPrices.SecurityId` | Datenintegritätsrisiko | FK-Entscheidung verbindlich dokumentieren und ggf. migrieren |

---

## 7. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0 | 2026-05-30 | Planning-Orchestrator | Initiale konsolidierte Planungsübersicht erstellt und mit allen Artefakten verlinkt |

