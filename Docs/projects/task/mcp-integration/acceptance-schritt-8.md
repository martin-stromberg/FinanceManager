# Abnahmeprüfung – Entwicklungsschritt 8 (EP-08)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine restlichen. Runde 1 (`acceptance-schritt-8.1.md`) meldete 2 Abweichungen:

- [x] A1: explizites `null` löschte nullable Felder nicht → behoben in
  `f4341d24`: `Has`/`HasNonNull`-Semantik getrennt, alle Update-Pfade
  T22–T29 leeren bei null korrekt, setCategory-Null-Pfad aktiv
- [x] A2: `iban`/`linkedIbans`/`identifier` außerhalb `untrusted_data`
  → jetzt im Wrapper (T01/T07-Konvention)

## Hinweise

- Alle Features F-0801–F-0803 erfüllt (8 Tools: direkte write-Actions mit
  DataChange-Kopplung, destruktive Actions → strukturierte
  `Err_Mcp_ApprovalRequired`-Antwort ohne Wirkung, Scope-Tupel kataloggemäß).
- Suite 1798/1798 grün. Approval-Queue-Anschluss folgt in EP-09
  (parameters-Snapshot bereits in den Antworten).
