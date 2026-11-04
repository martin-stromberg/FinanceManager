# Review – Schritt 1: Fachkonzept und Berechtigungsmodell

## Ergebnis

**Status:** Vollständig umgesetzt

## Prüfung gegen die Akzeptanzkriterien

| Kriterium | Prüfergebnis |
|---|---|
| Beide Dokumente liegen als deutschsprachige Markdown unter `Docs/planung/mcp-integration/` vor | ✔ `fachkonzept.md` und `berechtigungsmodell.md` angelegt, deutschsprachig, konsequent entscheidungsorientiert (Tabellen/Festlegungen statt Prosa-Sammlung) |
| F3-Matrix enthält alle 18 Datentypen × 7 Rechtearten | ✔ Matrix (Abschnitt 7.2 des Fachkonzepts) enthält genau 18 Datenzeilen × 7 Rechteartespalten (Lesen, Vorschlag, Vervollständigung, Änderung, Erstellung, Buchung, Löschung); maschinell gezählt: 18 Zeilen |
| Jeder Anforderungsbegriff auf reale Entität/Enum gemappt | ✔ Mapping-Tabelle (Abschnitt 7.1) löst alle 18 Begriffe auf: `AccountType`-Enum statt Kontotyp-Entität, `ContactCategory` = Kontaktgruppen, `StatementImport`/`StatementEntry` = Kontoauszüge, `StatementDraft(+Entry)` = Entwürfe, `BudgetPurpose`/`BudgetRule`/`BudgetCategory`, `ReportFavorite`/`HomeKpi`/`PortfolioKpiConfiguration`, `Attachment(+Category)` u. a. – kein unaufgelöster Begriff |
| Scope- und Aktionsmodell vollständig (F2) | ✔ `berechtigungsmodell.md` Abschnitt 2: Scope-Tupel (Datentyp × Rechteart × Filter), Aktionsmodell mit codebelegten Aktionen je Rechteart, Granularitätsgrenzen |
| Rechtevererbung + Konfliktverhalten + Standard-/individuelle Rechte | ✔ Abschnitt 3 (Stufen-Standardprofile, zugangslokale individuelle Anpassung, Laufzeit-Auflösung) und Abschnitt 4 (einschränkend gewinnt, ODER über Scopes, UND über Filterdimensionen, keine Deny-Scopes) |
| Grundsatz „nur einschränken, niemals erweitern" inkl. `AccountShare` | ✔ Abschnitt 5: Benutzerrechte als Stufe-0-Deckel, `AccountShareRole` (Read/Write/Admin) als datensatzscharfe Obergrenze mit Rechtematrix je Rolle, sofortige Wirkung bei `RevokedUtc`/Rollenwechsel; Admin erweitert keine Datenrechte |
| Rechteableitung für Entitäten ohne `OwnerUserId` dokumentiert | ✔ Abschnitt 6 deckt alle Fälle ab: `Posting` (je `Kind` über `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId`), `PostingAggregate`, `StatementImport`/`StatementEntry` (über `AccountId`), `StatementDraftEntry` (über `DraftId`), `AccountLinkedIban`, `AliasName`, `SecurityPrice`, `Attachment` (eigenes OwnerUserId + polymorphe Ziel-Decke) |
| Fünf Freigabestufen je Stufe mit erlaubten Aktionen (F5) | ✔ Fachkonzept Abschnitt 5: Aktionsmatrix aller 7 Rechtearten × 5 Stufen inkl. Genehmigungspflicht-Markierung (◐) und fachlicher Bedeutung je Stufe |
| Filterkonzept (F4) vollständig | ✔ Fachkonzept Abschnitt 8: optional, Standard = alle Datensätze; 5 Filterdimensionen auf realen Gruppierungsstrukturen; `ReportFavorite`-CSV-Muster; UND-Verknüpfung; festgelegte Semantik für unklassifizierte Datensätze |
| MCP-Zugangsverwaltung (F1) vollständig | ✔ Fachkonzept Abschnitt 6: mehrere Zugänge/Benutzer, Aktivieren/Deaktivieren, Ablaufdatum, Token-Lifecycle (Erzeugen → einmalige Anzeige → Hash-Speicherung nach `Pbkdf2IdentityPasswordHasher`-Vorbild → Rotation → sofortiger Widerruf), Nutzungsprotokollierung |
| Konkreter FinanceManager-Bezug | ✔ Durchgängig reale Referenzen: `Account`, `Posting` ohne OwnerUserId, `StatementImport`/`StatementEntry`, `StatementDraft`, `AccountShare`/`AccountShareRole`, `AccountType`-Enum (2 Werte), `ContactCategory`, `Attachment.Content` als byte[], `IStatementDraftService`, `ICurrentUserService`, `PostingReversalService`, `ownerUserId`-Pattern |

## Festgelegte Einzelentscheidungen (für Folgeschritte verbindlich)

- Rechteart „Buchung" existiert nur auf Kontoauszugsentwürfen; „Löschung" von
  Buchungen = Storno; `SavingsPlan`/`Security` = Archivieren.
- Kontoauszüge (`StatementImport`/`StatementEntry`): kein Löschrecht
  (Belege); Erstellung nur indirekt via Buchungsrecht.
- Unklassifizierte Datensätze (`CategoryId == null`) fallen nicht in
  Kategoriefilter (ausschließende Semantik).
- Token-Rotation invalidiert das alte Token sofort (kein Doppelbetrieb).

## Offene Punkte

- Keine blockierenden. Bewusst an Folgeschritte delegiert: Tool-Zuordnung
  (Schritt 2), technisches Token-Hashing/Transport und `AiActionRequest`-
  Detailzustände (Schritt 3), Persistenzmodell und UI (Schritt 4).
