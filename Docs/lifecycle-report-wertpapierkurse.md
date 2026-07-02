# Lifecycle Report: Wertpapierkurse

## Planung
- Requirements: `Docs/requirements/wertpapierkurse-ing-requirements.md`
- Architektur-Blueprint: `Docs/architecture/architecture-blueprint-wertpapierkurse-ing.md`
- ERM: `Docs/architecture/entity-relationship-model-wertpapierkurse-ing.md`
- Architecture-Review: `Docs/improvements/review-architecture-wertpapierkurse-ing.md`
- Konsolidierte Planung: `Docs/planning/planning-wertpapierkurse-ing.md`

## Umsetzung
- ING-CSV-Import für Wertpapierkurse umgesetzt (Parser, Factory-Auflösung, Upsert mit insert/update/unchanged).
- API-Endpoint ergänzt: `POST /api/securities/{id}/prices/import`.
- Shared DTOs und API-Client für den Import ergänzt.
- UI erweitert (Detailseite/Ribbon-Aktion + Import-Panel inkl. i18n-Ressourcen).

## Tests
- Testlückenanalyse: `Docs/tests/wertpapierkurse-ing-testluecken.md`
- Testplan: `Docs/tests/wertpapierkurse-ing-testplan.md`
- Neue/erweiterte Tests für Integration, Shared-API-Client, Infrastructure-Upsert/Parser sowie Web-ViewModel/Component umgesetzt.
- Integrationstests für `ApiClientSecuritiesTests` sind grün; gesamtes Unit-Testprojekt bleibt durch bestehende, fachfremde Build-Fehler blockiert.

## Dokumentation
- Flow-Dokumentation: `Docs/flows/security-price-import-ing.md`
- Business-Feature-Dokumentation: `Docs/business/features/F007-wertpapierpreise-ing-csv-import.md`
- Aktualisierungen in API-, Flow-, Feature-, Planung- und README-Dokumenten durchgeführt.
- Dokumentationsabschluss: `Docs/documentation-plan.md`

## Offene Punkte / Hinweise
- Projektweite Build-Blocker in bestehenden Budget-Tests (`IsReversed`-Signaturabweichungen) verhindern aktuell einen vollständig grünen Lauf von `FinanceManager.Tests`.
