# Test-Ergebnisse – Schritt 1: Fachkonzept und Berechtigungsmodell

## Ergebnis

**Status:** Entfällt: reine Dokumentationslieferung, keine ausführbaren Tests.

Es wurde kein Produktivcode geändert; Build, Unit-, Integrations- und
E2E-Tests sind für diesen Schritt nicht anwendbar und wurden nicht
ausgeführt.

## Manuelle Checkliste

| Prüfung | Ergebnis |
|---|---|
| `Docs/planung/mcp-integration/fachkonzept.md` vorhanden, deutsch, Markdown | ✔ |
| `Docs/planung/mcp-integration/berechtigungsmodell.md` vorhanden, deutsch, Markdown | ✔ |
| F3-Matrix vollständig gezählt: 18 Datentyp-Zeilen × 7 Rechteart-Spalten | ✔ (Zeilen maschinell gezählt: 18) |
| Mapping-Tabelle vollständig: 18 Anforderungsbegriffe → Entität/Enum | ✔ (18 Zeilen) |
| Jeder Matrix-Eintrag belegt (✓ / — / ◑ mit Fußnotenbegründung) | ✔ |
| Rechteableitung ohne OwnerUserId für alle genannten Entitäten dokumentiert | ✔ (Posting, PostingAggregate, StatementImport, StatementEntry, StatementDraftEntry, AccountLinkedIban, AliasName, SecurityPrice, Attachment) |
| F1-Zugänge: Anlage, Aktivieren/Deaktivieren, Ablaufdatum, Token-Lifecycle (Einmalanzeige, Hash), Widerruf, Nutzungsprotokoll | ✔ alle Teilaspekte vorhanden |
| F5-Stufen: alle 5 Stufen mit Aktionsmatrix und fachlicher Bedeutung | ✔ |
| F4-Filter: optional, Standard = alle, ReportFavorite-CSV-Bezug | ✔ |
| `AccountShare`-Obergrenze + Grundsatz „nur einschränken" | ✔ |
| Keine generischen Aussagen ohne Code-Bezug | ✔ Stichprobe: alle Entitäts-/Enum-/Service-Namen stammen aus `inventory.md`/`entitaeten.md` |
| Keine Codeänderung, kein Push, kein Branchwechsel | ✔ |
