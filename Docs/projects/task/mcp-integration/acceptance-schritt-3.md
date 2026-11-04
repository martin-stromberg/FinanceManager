# Abnahmeprüfung – Entwicklungsschritt 3 (EP-03)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine restlichen. Runde 1 (archiviert in `acceptance-schritt-3.1.md`) meldete 1 Abweichung:

- [x] A-01: AccessEvents ohne Vorher/Nachher-Scope-Delta → behoben in
  `3605a8ab`: `ScopesChanged`/`FiltersChanged`/`AccessUpdated` schreiben jetzt
  `McpAuditEntryChange`-Zeilen (Tupel-/Filter-Delta Vorher→Nachher, ohne
  Secrets); 4 neue Tests inkl. Ketten-Verifikation.

## Hinweise

- Alle Features F-0301/F-0302 aus Runde 1 als erfüllt bestätigt: persistenter
  `McpAuditWriter`, transaktionale Kopplung (Stage+SaveChanges-Runde),
  lesende Aufrufe audit-fehlertolerant, abgestufte Detailtiefe mit
  Secret-Redaction, `SequenceNo`/`PrevHash`/`EntryHash`-Kette + `VerifyAsync`,
  gefilterter Unique-Index für die globale Kette, CorrelationId durchgängig.
- Suite 1640/1640 grün. Hinweise aus Runde 1 bleiben dokumentiert
  (DataChange/ApprovalEvent-Callsites ab EP-06/09, Retention-Kürzung EP-11).
