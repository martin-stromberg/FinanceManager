# Planungsübersicht: Wertpapierkurse-Import (ING CSV)

> **Quelle:** [`../../issue.md`](../../issue.md)  
> **Status:** ✅ Umsetzung und Dokumentation abgeschlossen  
> **Version:** 1.0  
> **Datum:** 2026-07-02  
> **Koordination:** planning-orchestrator

## 1. Zweck

Diese Übersicht konsolidiert den vollständigen Orchestrator-Ablauf (Anforderungen → Architektur → ERM → Review) und verlinkt alle Artefakte.

## 2. Verlinkte Planungsartefakte

- Anforderungen: [`../requirements/wertpapierkurse-ing-requirements.md`](../requirements/wertpapierkurse-ing-requirements.md)
- Architektur-Blueprint: [`../architecture/architecture-blueprint-wertpapierkurse-ing.md`](../architecture/architecture-blueprint-wertpapierkurse-ing.md)
- ERM: [`../architecture/entity-relationship-model-wertpapierkurse-ing.md`](../architecture/entity-relationship-model-wertpapierkurse-ing.md)
- Architektur-Review: [`../improvements/review-architecture-wertpapierkurse-ing.md`](../improvements/review-architecture-wertpapierkurse-ing.md)

## 3. Orchestrator-Sequenz (vollständig durchgeführt)

1. **Anforderungsanalyse** erstellt (FR/NFR, ACs, Scope, Use Cases, Annahmen).
2. **Architektur-Blueprint** erstellt (Komponenten, API, Factory, Upload-Flow, Qualitätsziele).
3. **ERM** erstellt (bestehende Entitäten + prozessuale Import-Contracts, Upsert-Regeln).
4. **Architektur-Review** durchgeführt (Findings, Risiken, priorisierte Maßnahmen).
5. **Konsolidierung** in diesem Dokument abgeschlossen.

## 4. Konsolidierte Kernentscheidungen

1. Import erfolgt ausschließlich auf der **Wertpapier-Kursseite** (`/list/securities/prices/{id}`).
2. Erweiterbarkeit wird über **`ISecurityPriceImportService` + Factory** umgesetzt.
3. Erste Implementierung ist **ING CSV** gemäß `sample.csv`.
4. Persistenz nutzt **Upsert pro Tag** auf bestehender Tabelle `SecurityPrices`.
5. Ergebnisrückgabe enthält mindestens `inserted`, `updated`, `unchanged`, `skipped`, `errors`.

## 5. Umsetzungsreihenfolge (empfohlen)

1. DTOs/Contracts für Import-Request und Result finalisieren.
2. Factory + ING-Importservice implementieren.
3. `ISecurityPriceService` um Batch-Upsert erweitern.
4. API-Endpunkt `POST /api/securities/{id}/prices/import` integrieren.
5. Kursseiten-Aktion (`SecurityPricesListViewModel`) + Upload-UI anbinden.
6. Unit/Integration/UI-Tests gemäß Blueprint ergänzen.

## 6. Offene Entscheidungen (vor Implementierung klären)

- Teilfehler-HTTP-Semantik (`200` + Fehlerliste vs. `422`).
- Deduplizierungsregel bei mehrfachen Datumszeilen in einer Datei.
- Optional: persistentes Import-Audit.

## 7. Abgleich nach Umsetzung

- **Teilfehler-HTTP-Semantik:** entschieden als `200 OK` mit `errors[]` und `skipped`; `400 Err_Invalid_Import` bei vollständig invalider Datei.
- **Deduplizierung:** umgesetzt als `last row wins` pro Datum innerhalb einer Datei.
- **Import-Audit:** aktuell nicht persistent, weiterhin optionaler Ausbaupunkt.

### Konsistente Referenzen
- API: [`../api/SecuritiesController.md`](../api/SecuritiesController.md#post-apisecuritiesidpricesimport)
- Flow: [`../flows/security-price-import-ing.md`](../flows/security-price-import-ing.md)
- Business: [`../business/features/F007-wertpapierpreise-ing-csv-import.md`](../business/features/F007-wertpapierpreise-ing-csv-import.md)
- Requirements: [`../requirements/wertpapierkurse-ing-requirements.md`](../requirements/wertpapierkurse-ing-requirements.md)
- Architektur: [`../architecture/architecture-blueprint-wertpapierkurse-ing.md`](../architecture/architecture-blueprint-wertpapierkurse-ing.md)
- Tests: [`../tests/wertpapierkurse-ing-testplan.md`](../tests/wertpapierkurse-ing-testplan.md)

## 8. Änderungshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0 | 2026-07-02 | planning-orchestrator | Vollständige Planungskonsolidierung für ING-Wertpapierkursimport erstellt |
