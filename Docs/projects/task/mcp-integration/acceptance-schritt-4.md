# Abnahmeprüfung – Entwicklungsschritt 4 (EP-04)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine restlichen. Runde 1 (`acceptance-schritt-4.1.md`) meldete 3 latente
Abweichungen im Record-Filter — alle in `098dfb48` behoben und testgesichert:

- [x] T11 filtert Primärdaten über `Vorschlag`-statt `Lesen`-Zeilen
  (`ApplyAsync`-Overload mit `McpRightKind`)
- [x] T12 injiziert `McpRecordScopeFilter`: Suggest-Zeilen auf Purposes,
  Lesen-Zeilen auf Posting-Historie
- [x] T19 eintragsweiser Record-Filter (wie T13/T15)

## Hinweise

- Alle Features F-0401–F-0403 erfüllt (4 Tools, keine Persistenz,
  Scope-Tupel kataloggemäß, untrusted_data, Paging-Clamp, Audit via Dispatcher).
- Suite 1659/1659 grün. Hinweise aus Runde 1 dokumentiert (ClassifyAsync-
  Verzicht = strengere Designwahl; EP-05-Profile machen feine Filter wirksam).
