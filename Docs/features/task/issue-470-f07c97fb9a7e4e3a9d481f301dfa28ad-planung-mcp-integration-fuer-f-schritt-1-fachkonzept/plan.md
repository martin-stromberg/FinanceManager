# Plan – Schritt 1: Fachkonzept und Berechtigungsmodell

## Lieferdokumente

### `Docs/planung/mcp-integration/fachkonzept.md`

Gliederung:

1. Zielbild (Hauptziele, Nicht-Ziele als Abgrenzung, fachliche Invarianten:
   Posting schreibgeschützt, Storno statt Löschen, Archivieren, Owner-Prinzip,
   indirekte Besitzverhältnisse, AccountShare)
2. Akteure und Rollen (datenbesitzender Benutzer, Admin, MCP-Client/KI-System)
3. Anwendungsszenarien (Analyse, Assistenz, Kontoauszugs-Verarbeitung F7,
   genehmigungspflichtige Aktionen, Agentenszenarien)
4. Rechtearten – fachliches Aktionsmodell (7 Rechtearten mit Code-Bezug)
5. Freigabestufen F5 (Aktionsmatrix Analyse/Assistent/Sachbearbeiter/
   Buchhalter/Individuell, fachliche Bedeutung, Obergrenzen)
6. MCP-Zugänge F1 (Eigenschaften, Anlage/Verwaltung, Token-Lifecycle mit
   einmaliger Anzeige + Hash, Rotation, Widerruf, Nutzungsprotokollierung)
7. Datentypen-Klassifizierung F3 (Begriffs-Mapping 18 Typen → Entitäten/Enums,
   Matrix 18 × 7 mit Fußnoten)
8. Filterkonzept F4 (Dimensionen, Semantik, ReportFavorite-CSV-Muster)
9. Abhängigkeiten/Folgeschritte

### `Docs/planung/mcp-integration/berechtigungsmodell.md`

Gliederung:

1. Modellüberblick (Prüfstufen 0–3, verbindliche Grundsätze)
2. Scope-Modell F2 (Scope-Tupel Datentyp × Rechteart × Filter, Aktionsmodell,
   Granularitätsgrenzen)
3. Rechtevererbung (Standardprofile je Stufe, individuelle Anpassung,
   Laufzeit-Auflösung)
4. Konfliktverhalten (einschränkend gewinnt, ODER/UND-Regeln, keine Deny-Scopes)
5. Grundsatz „nur einschränken" operationalisiert (Benutzerrechte,
   AccountShare-Deckel Read/Write/Admin, Approval-/Notfall-Deckel)
6. Rechteableitung ohne OwnerUserId (Posting je Kind, PostingAggregate,
   StatementImport/-Entry, StatementDraftEntry, AccountLinkedIban, AliasName,
   SecurityPrice, Attachment polymorph)
7. Normierte Rechteart-Semantik
8. Auswertungslogik (fachlicher Entscheidungsablauf)
9. Konsistenzanforderungen an Folgeartefakte

## Vorgehen

1. Inventory lesen (Projektverzeichnis) – alle Entitäts-/Enum-Referenzen.
2. Beide Lieferdokumente schreiben (deutsch, entscheidungsorientiert,
   code-referenziert).
3. Lifecycle-Artefakte (requirement.md, plan.md, review.md, test-results.md,
   todo.md) unter `Docs/features/task/<schritt-branch>/`.
4. Selbstprüfung gegen Akzeptanzkriterien (Matrix 18×7 vollständig, jedes
   Mapping aufgelöst, alle F-Anforderungen adressiert).
5. Ein Commit `docs: Schritt 1 - Fachkonzept und Berechtigungsmodell (MCP, Issue #470)`.
