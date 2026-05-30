# Architektur-Review: Stock-Price-Fetch-Error-Recovery

> **Reviewtyp:** Strukturiertes Architektur-Review  
> **Reviewter Artefakt-Stand:** Anforderungen v0.1 / Blueprint v0.2 / ERM v1.0 / Plan v1.0  
> **Review-Datum:** 2026-05-30  
> **Reviewer:** Architektur-Review Agent (GitHub Copilot)  
> **Status:** ❌ Nicht freigegeben (Build-Blocker offen)

## Referenzen
- Anforderungen: [../requirements/stock-price-fetch-error-requirements.md](../requirements/stock-price-fetch-error-requirements.md)
- Blueprint: [../architecture/architecture-blueprint-stock-price-fetch.md](../architecture/architecture-blueprint-stock-price-fetch.md)
- ERM: [../architecture/entity-relationship-model-stock-prices.md](../architecture/entity-relationship-model-stock-prices.md)
- Plan: [../planning/stock-price-fetch-plan.md](../planning/stock-price-fetch-plan.md)

---

## 1. Summary

Die Artefaktkette ist jetzt vollständig und konsistent verlinkt (Anforderungen ↔ Blueprint ↔ ERM ↔ Plan ↔ Review).  
Architektur, Technologieentscheidungen und Recovery-Zielbild sind inhaltlich stimmig; insbesondere ist `HasPriceError` als domänenzentrales Steuerfeld korrekt verankert.

**Zentraler Blocker bleibt unverändert:**
- **CS1061** in `FinanceManager.Web/Services/SecurityPricesBackfillExecutor.cs(153,55)`
- Ursache: Nutzung von `s.HasPriceError` auf einer Projektion ohne entsprechendes Feld.

Damit ist das Feature weiterhin **nicht freigabefähig**.

---

## 2. Findings

| Priorität | Finding | Evidenz | Bewertung |
|---|---|---|---|
| **Blocker** | Build-Fehler CS1061 im Backfill-Executor | `SecurityPricesBackfillExecutor.cs(153,55)`, NFR-1, Plan M1 | Verhindert Lieferfähigkeit |
| **Major** | Architekturentscheidung „typisierte Projektion“ noch nicht durchgängig umgesetzt | Blueprint Kap. 6/7, Ist-Stand Build-Fehler | Erhöht Risiko für Property-Drift |
| **Major** | Datenintegritätsentscheidung zu `SecurityPrices.SecurityId` weiterhin offen | ERM Hinweis auf fehlende explizite DB-FK-Constraint | Mittelfristiges Integritäts-/Wartungsrisiko |
| **Minor** | UI/UX-Abnahmekriterien sind nur teilweise operationalisiert | Anforderungen ACs, Blueprint UI/UX-Konzept | Erschwert eindeutige Fachabnahme |

### Ergänzende Bewertung je Review-Dimension
- **Systemarchitektur:** Schichten, Komponenten und Integrationen sind nachvollziehbar und konsistent modelliert.
- **Technologieentscheidungen:** `IPriceProvider` + EF Core + typisierte Vertragsstrategie sind angemessen; der zentrale Compile-Fix fehlt aber im Ist.
- **UI/UX:** Dismiss-Entkopplung ist korrekt; konkrete UI-Abnahmechecks sollten präzisiert werden.
- **Qualitätsziele:** NFR-1 (Build-Stabilität) ist aktuell klar verletzt; NFR-2 bis NFR-5 sind konzeptionell adressiert, aber durch den Blocker nicht freigabefähig.

---

## 3. Maßnahmen

### M1 – CS1061 beheben (Blocker)
- **Maßnahme:** Backfill-Kandidatenprojektion als expliziten Typ/DTO mit `HasPriceError` führen.
- **Abnahme:**  
  - `dotnet build FinanceManager.sln --no-restore` erfolgreich  
  - Kein CS1061 in `SecurityPricesBackfillExecutor.cs(153,55)`

### M2 – Recovery-Logik absichern (Major)
- **Maßnahme:** Retry-/Skip-Logik (`toInclusive < fromInclusive`) mit `HasPriceError` testseitig absichern.
- **Abnahme:**  
  - Recovery-Tests (Worker/Backfill) grün  
  - Fehlerhafte Securities werden im Recovery-Pfad nicht fälschlich übersprungen

### M3 – Datenintegrität verbindlich entscheiden (Major)
- **Maßnahme:** FK-Strategie für `SecurityPrices.SecurityId` dokumentieren und ggf. technisch umsetzen.
- **Abnahme:**  
  - Architektur-/ERM-Dokumentation mit begründeter Entscheidung aktualisiert  
  - Bei FK-Einführung: Migration buildbar

### M4 – UI-Abnahmekriterien schärfen (Minor)
- **Maßnahme:** Konkrete UI-Checks für Fehlerindikator, Recovery-Anzeige und Notification-Verhalten ergänzen.
- **Abnahme:**  
  - Prüfliste in Anforderungen/Plan ergänzt  
  - Fachliche Abnahme eindeutig reproduzierbar

---

## 4. Freigabeempfehlung

**Freigabe: NO-GO (nicht freigeben).**

Freigabe erst nach:
1. Behebung des Blockers **CS1061** (`SecurityPricesBackfillExecutor.cs(153,55)`),
2. grünem Build und grünen Recovery-Tests,
3. dokumentierter Entscheidung zur FK-Strategie `SecurityPrices.SecurityId`.

---

## 5. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.1 | 2026-05-30 | Architektur-Review Agent (GitHub Copilot) | Review auf Basis vollständiger Artefakte (inkl. Plan v1.0) aktualisiert; veralteten Befund zum fehlenden Plan entfernt; Findings/Maßnahmen/Freigabeempfehlung konsolidiert; CS1061 in `SecurityPricesBackfillExecutor.cs(153,55)` als zentralen Blocker bestätigt. |
| 1.0 | 2026-05-30 | Architektur-Review Agent (GitHub Copilot) | Initiales strukturiertes Review erstellt: Executive Summary, Stärken/Schwächen, Risiken, priorisierte Findings, Konsistenzprüfung, Maßnahmenliste mit Abnahmekriterien, Freigabeempfehlung. |
