# Abnahmeprüfung – Entwicklungsschritt 2

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

- Keine. Beide Abweichungen aus der 1. Runde
  (`acceptance-schritt-2.1.md`) sind behoben.

## Hinweise

**Nachprüfung der gemeldeten Abweichungen (1. Runde → 2. Runde):**

- *Meta-Scope T32/T33 (behoben, konsistent an allen Stellen):* In
  `scope-und-toolkatalog.md` ist der Meta-Scope jetzt als festgelegte
  Regel definiert: Grundsatz 2 (Zeilen 25–29) deklariert die
  „Festgelegte Ausnahme" explizit; Abschnitt 3.6 (Zeilen 281–290)
  enthält eine eigene Scope-Regel mit vollständiger Definition
  (geschlossener Scope-Typ, kein Fachdatentyp, kein Filter, nicht
  übertragbar, jedem Zugang inhärent, Absicherung über Besitzprüfung);
  die Tool-Tabelle (Zeilen 294–295) trägt „Meta-Scope `Zugangsprofil`"
  bzw. „Meta-Scope `Ausführungsstatus`" statt des früheren Freitexts
  „Meta (kein Fachdatentyp)"; Abschnitt 5.2 (Zeilen 502–507) benennt den
  Meta-Scope als „einzige Ausnahme" der Tupel-Nomenklatur; die
  Übersichtstabelle (Zeilen 605–606) führt die gleichen Meta-Scope-Namen.
  Grundsatz 2 („Ein Tool ohne Scope-Zuordnung ist nicht zulässig"),
  Abschnitt 5.2 und `berechtigungsmodell.md` §9 stehen damit nicht mehr
  im Widerspruch zum Katalog – die in Runde 1 geforderte explizite
  Ausnahmeregel ist gesetzt.
- *T08-Contract-Lücke (behoben, konsistent an allen Stellen):* Zeile 103
  referenziert nun `IStatementImportQueryService` als „neu zu
  konzipierenden Lese-Contract" (explizit: existiert heute nicht);
  die neue Nebenbedingung „Offene Referenz (T08)" (Zeilen 126–135)
  benennt die Lücke vollständig – Belege liegen nur als Entities bzw.
  DTO-Felder vor, der Contract ist als Baustein in Schritt 4
  einzuplanen, der Adapter greift nicht auf Repositories/DB zu, T08 ist
  bis zur Bereitstellung nicht implementierbar; Abschnitt 5.1
  (Zeile 478) wiederholt den Vermerk. Die in Runde 1 beanstandete
  nicht deklarierte Lücke ist damit als bewusste, dokumentierte
  Umsetzungsvoraussetzung festgelegt.

**Kurzcheck Gesamtdokument (keine neuen Inkonsistenzen):**

- Übersichtstabelle §7 ↔ Detailtabellen §3: Meta-Scope-Namen
  (`Zugangsprofil`, `Ausführungsstatus`), Klassen und Scopes stimmen
  überein; Summenangabe 33 Tools weiterhin korrekt
  (10+2+9+8+2+2).
- Grundsatz-Formulierungen: Die Ausnahmen in Grundsatz 2 und §5.2 sind
  als solche markiert und erzeugen keine Selbstwidersprüche; Grundsatz 3
  bleibt für T08 verbindlich formuliert (Adapter ruft Contract, kein
  Repository-/DB-Zugriff).
- Akzeptanzkriterien (project-plan.md, Schritt 2) weiterhin erfüllt:
  Dokument deutsch unter `Docs/planung/mcp-integration/`; jedes Tool
  mit Geschäftsprozess, Scope(-Regel), Sicherheitsklasse, Ein-/Ausgaben
  auf DTO-Niveau; F7-Workflow vollständig inkl. Abbildung auf
  `IStatementDraftService`/`IMassImportOrchestrator` (Tabelle 4.5) und
  explizitem Bankanbindungs-Ausschluss; Versionierungsstrategie in §6;
  kein generischer CRUD-/DB-Zugriff, Storno als einziger Rücknahmeweg.

**Umfang des Diffs (Basis `...-planung-mcp-integration-fuer-f` → HEAD):**
2 Commits (`e7d43fc0` Lieferung, `f4114138` Korrektur), 6 Dateien,
rein dokumentarisch: `Docs/planung/mcp-integration/scope-und-toolkatalog.md`
(628 Zeilen) plus Lifecycle-Artefakte unter
`Docs/features/task/...-schritt-2-toolkatalog/` (plan.md,
requirement.md, review.md, test-results.md, todo.md). Der
Korrektur-Commit änderte nur `scope-und-toolkatalog.md` (+52/−10) und
`review.md` (+15, „Korrekturen Abnahme 1" dokumentiert beide Fixes).
Keine Code-Änderungen, keine Fremdbereiche – Umfang regelkonform.

**Beobachtungen ohne Abweichungscharakter:**

- Grundsatz 3 (Zeilen 30–34) spricht wörtlich von „denselben
  `I*Service`-Contracts, die auch die BFF-REST-API nutzt"; der neue
  `IStatementImportQueryService` ist formal noch kein solcher Contract.
  Das Dokument adressiert dies explizit („Grundsatz 3 bleibt
  verbindlich", Contract als Baustein der gemeinsamen Service-Schicht)
  – inhaltlich aufgelöst, eine Verschärfung der Grundsatz-Formulierung
  wäre optional möglich.
- Der Meta-Scope ist nur im Toolkatalog definiert; `berechtigungsmodell.md`
  (Schritt 1) kennt den Scope-Typ nicht. Runde 1 ließ „Ausnahmeregel
  oder Schritt-1-Erweiterung" als Behebungsoptionen – die Ausnahmeregel
  wurde gewählt und ist konsistent festgelegt; ein späterer
  Rückverweis im Berechtigungsmodell wäre denkbar, ist aber nicht Teil
  dieses Schritts.

**Verweis:** 1. Abnahmerunde siehe `acceptance-schritt-2.1.md` im
selben Verzeichnis (dortige Stichproben zu Code-Referenzen und
Schritt-1-Konsistenz bleiben unverändert gültig, da die Korrektur ausschließlich
die beiden beanstandeten Stellen plus review.md berührte).
