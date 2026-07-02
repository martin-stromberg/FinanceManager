# Architektur-Review: Wertpapierkurse-Import (ING CSV)

> **Review-Datum:** 2026-07-02  
> **Artefaktstand:** Anforderungen v1.0 / Blueprint v1.0 / ERM v1.0  
> **Reviewer:** review-architecture  
> **Status:** ✅ Conditional Go

## Referenzen

- Anforderungen: [../requirements/wertpapierkurse-ing-requirements.md](../requirements/wertpapierkurse-ing-requirements.md)
- Blueprint: [../architecture/architecture-blueprint-wertpapierkurse-ing.md](../architecture/architecture-blueprint-wertpapierkurse-ing.md)
- ERM: [../architecture/entity-relationship-model-wertpapierkurse-ing.md](../architecture/entity-relationship-model-wertpapierkurse-ing.md)
- Planung: [../planning/planning-wertpapierkurse-ing.md](../planning/planning-wertpapierkurse-ing.md)

## 1. Summary

Die Architektur ist für den ersten Schritt tragfähig: providerbasierte Factory, klarer ING-Parser und Upsert auf bestehendem Datenmodell. Hauptstärken sind Erweiterbarkeit ohne Controller-Änderung und idempotente Re-Import-Regeln.

Freigabe ist **bedingt**, da einige Punkte vor Umsetzung präzisiert werden sollten (Semantik bei Teilfehlern, Idempotenz bei mehrfachen gleichen Datenzeilen).

## 2. Findings

| Priorität | Finding | Bewertung |
|---|---|---|
| **Major** | HTTP-Vertrag für Teilfehler (200 vs. 422) ist noch nicht final entschieden | Risiko für inkonsistente UI-Fehlerbehandlung |
| **Major** | Regel für Mehrfacheinträge selben Datums in einer Datei ist nur als Annahme dokumentiert | Risiko nicht-deterministischen Verhaltens |
| **Medium** | Kein persistentes Import-Audit vorgesehen | Eingeschränkte Revisionsfähigkeit |
| **Medium** | Performancegrenze für sehr große CSV-Dateien nicht explizit definiert | Potenzielles Timeout-Risiko |
| **Low** | Provider-Erkennung (explizit vs. heuristisch) benötigt klare Priorisierungsregel | Wartbarkeitsthema |

## 3. Verbesserungsmaßnahmen

### M1 – API-Fehlervertrag festziehen (Major)
- Definiere verbindlich: Wann `200` mit Fehlerliste, wann `422`.
- Ergänze ProblemDetails-Konvention für Zeilenfehler.

### M2 – Deduplizierungsregel standardisieren (Major)
- Entscheide und dokumentiere: `last line wins` oder `first line wins`.
- Decke Regel mit Unit-Tests ab.

### M3 – Optionales Import-Audit bewerten (Medium)
- Prüfe schlankes Audit (z. B. Importzeitpunkt, Provider, Summenzähler) für Supportfälle.

### M4 – Größen- und Laufzeitgrenzen operationalisieren (Medium)
- Definiere max. Dateigröße/Zeilenanzahl.
- Ergänze technische Guards und UI-Hinweise.

## 4. Freigabeempfehlung

**Conditional Go**  
Umsetzung kann starten, wenn M1 und M2 vor Implementierungsbeginn präzisiert und als Testkriterien verankert werden.

## 5. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0 | 2026-07-02 | review-architecture | Initiales strukturiertes Review für ING-Wertpapierkursimport |
