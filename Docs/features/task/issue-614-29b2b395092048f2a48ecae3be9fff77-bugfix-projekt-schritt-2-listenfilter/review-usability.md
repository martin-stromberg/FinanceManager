# Usability-Review – Schritt 2: Listenfilter (#599/#601/#604)

**Gesamturteil des Reviews:** Solide, weitgehend konsistente Umsetzung. Die Filtermechanik folgt denselben Mustern wie die übrigen Listen; `AllowRangeFiltering = false` für Wertpapiere/Benutzer entspricht exakt der Bestandskonvention.

## Findings — Behandlung im Schritt-Kontext

### M1 [medium] Kein Debounce + Voll-Reload pro Tastenschlag → Flackern & Nachlauf

Jede Eingabe leert `Items` → „Loading"-Vollfläche; bei Sparplänen 2 + N API-Calls pro Tastenschlag.

**Disposition: Vertagt (Listen-weites Thema, kein Schritt-Scope).** Ein Debounce gehört in `GenericListPage.OnSearchInput` und beträfe **alle** Listen gleichermaßen — das ist eine eigenständige Verbesserung, kein Fehler dieser drei Issues. Die Issues #599/#601/#604 fordern funktionierende Filter; die Reaktivität entspricht dem Bestandsverhalten anderer Listen.

### M2 [medium] Ladefehler sieht aus wie „keine Treffer"

`catch {} → Items.Clear()` in allen drei VMs; `IListProvider` exponiert `LastError` nicht.

**Disposition: Vertagt (prä-existentes, listen-weites Muster).** Vor dem Schritt war das Verhalten identisch; die Filterung macht leere Treffer nur häufiger legitimiert. Ein `LastError`-Kanal über `IListProvider` + Rendering in `GenericListPage` wäre ein sinnvolles Folge-Issue.

### M3 [medium] Recurring-Match ohne UI-Erklärung — Datum außerhalb des Filters sichtbar

Die `Zieldatum`-Spalte zeigt das *nächste* Fälligkeitsdatum; ein Plan kann im gefilterten Zeitraum eine *andere* Fälligkeit haben als die angezeigte.

**Disposition: Akzeptiert als spezifizierte Semantik.** Issue #599 definiert explizit „beliebige Fälligkeit im Zeitraum" als Match-Kriterium — die Implementierung entspricht der Anforderung. Eine Match-Begründung (`ListRecord.Hint`, z. B. „Fälligkeit im Zeitraum") ist ein sinnvolles Follow-up, ändert aber das gerenderte Spaltenbild und sollte fachlich mit dem Nutzer abgestimmt werden.

### M4 [medium] „Von"-allein-Filter wirkungslos bei aktiven Recurring-Plänen

Early-Return `true` — aktive wiederkehrende Pläne haben bei offener Obergrenze immer unbegrenzte Zukunftsfälligkeiten.

**Disposition: Akzeptiert, semantisch korrekt.** Die Alternative („untere Schranke schneidet die Zukunft ab") würde Pläne ausblenden, die tatsächlich Fälligkeiten nach dem Von-Datum haben — falsch. Der Nutzer, der „ab Datum X" filtert, sieht korrekt alle Pläne mit Fälligkeiten ab X — bei aktiven Recurring-Plänen ist das praktisch alle.

### L1 [low] Kein Filter-Reset (Ribbon `ClearFilter`)

**Disposition: Vertagt.** Kontakte/BudgetPurpose bieten Ribbon-Reset; die drei Listen nicht — prä-existente Lücke, im Plan bewusst ausgenommen. Beide Datumsfelder lassen sich einzeln leeren; Suchfeld-Clear ist ein Klick.

### L2 [low] Sparplan-Suche matcht unsichtbare Felder (ContractNumber, Kategoriename)

**Disposition: Akzeptiert.** Issue #601 fordert explizit die Suche über Vertragsnummer; der Kategoriename ist die naheliegende Erweiterung (Kategoriespalte ist teilweise sichtbar). „Treffer ohne ersichtlichen Grund" ist der Preis einer geforderten Volltextsuche — übliche UX-Praxis (z. B. Suche über IDs in fast allen Admin-Oberflächen).

### L3 [low] Kategorie-Lookups pro Tastenschlag neu geladen

**Disposition: Akzeptiert (bewusst).** Die Kategorien werden je Load geholt, damit Umbenennungen sofort greifen; der Zusatzaufruf ist billig (kleine Tabelle). Caching würde Staleness einführen — Trade-off bewusst zugunsten Frische.

### L4 [low] Vertauschte Von/Bis-Grenzen still normalisiert

**Disposition: Akzeptiert.** Identisch zum etablierten `BudgetPurposeListViewModel`-Verhalten — Konsistenz schlägt zusätzliches Feedback.

### L5 [low] A11y: Range-Labels nicht per `for`/`aria-label` gebunden

**Disposition: Vertagt (Bestands-`GenericListPage`-Betreff).** Betrifft alle Listen, die Range-Filter rendern — generische Komponenten-Verbesserung, nicht schrittspezifisch.

### L6 [low] Leertext unterscheidet „keine Treffer" nicht von „leere Liste"

**Disposition: Vertagt (generischer Komponenten-Betreff).** `EmptyText` ist parametrisierbar; eine differenzierte Meldung wäre eine `GenericListPage`-Erweiterung für alle Listen.

## Positiv hervorzuheben (aus dem Review)

- Filterung **vor** Analyse-Projektion → N+1 nur für sichtbare Pläne (per Test abgesichert).
- Umfangreiche Testabdeckung inkl. Grenzfälle (14 Fälle in `SavingsPlansListFilterTests`).
- Akzeptierte Semantik-Einschränkung (Monatsende-Drift) transparent dokumentiert.
- Lokalisierung de+en vollständig, keine hartkodierten Strings.
- `AllowRangeFiltering => false`-Entscheidung konventionskonform.

## E2E-Abnahme

Vier Playwright-Abnahmetests verifizieren die Filter live inkl. Screenshots:

- `SavingsPlansList_Search_FiltersVisiblePlans` — Suche „alphaplan" → nur AlphaPlan sichtbar; Zurücksetzen → beide Pläne wieder da.
- `SavingsPlansList_DateRange_FiltersVisiblePlans` — Von/Bis 01.–30.06.2026 → nur Plan mit Zieldatum 15.06.2026 sichtbar.
- `SecuritiesList_Search_FiltersAndRangeHidden` — Suche „alphasec" → nur AlphaSec; **kein** Datumsfilter gerendert.
- `UsersList_Search_FiltersAndRangeHidden` — Suche Admin-Name → nur Admin-Zeile; **kein** Datumsfilter gerendert.

Hinweis zur Test-Härtung: Der Zeilen-Count berücksichtigt nur sichtbare DOM-Knoten (`offsetParent !== null`), da `GenericListPage` pro Datensatz `tbody tr` **und** `.generic-list-mobile-card` rendert (CSS blendet jeweils eines aus). Filteranwendung nutzt Retry mit Stabilitätsfenster, weil die Blazor-Server-Schaltstelle nach dem Prerender den Provider neu erzeugen und den gefilterten Zustand kurzzeitig überschreiben kann.
