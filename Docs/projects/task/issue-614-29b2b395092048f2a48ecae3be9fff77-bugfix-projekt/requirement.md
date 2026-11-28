# Anforderung – Bugfix-Projekt (Issue #614)

## Ziel

Meta-Aufgabe: Die zwölf Bug-Issues #599–#610 sollen gemeinsam als ein Bugfix-Projekt auf dem
Basisbranch `task/issue-614-29b2b395092048f2a48ecae3be9fff77-bugfix-projekt` abgearbeitet und
anschließend per Pull Request gegen `staging` ausgeliefert werden. Das Projekt gilt als
abgeschlossen, wenn alle zwölf Teil-Issues erledigt sind.

Alle Teil-Issues betreffen die Blazor-Web-UI (`FinanceManager.Web`) — Liste-/Kartenansichten,
Ribbon-Aktionen, Navigation und ein SVG-Chart. Keine Datenbank-Schemaänderungen.

## Fachliche Anforderungen (Teil-Issues)

### #599 — Sparplanübersicht: Suche und Datumsfilter ohne Wirkung

- `/list/savings-plans` (`SavingsPlansListViewModel`): `Search`, `RangeFrom`, `RangeTo` werden
  nicht ausgewertet; `LoadPageAsync` lädt immer `SavingsPlans_ListAsync(OnlyActive)`.
- Textsuche: Teilstring auf `Name` (Vorschlag/Issue-Empfehlung: zusätzlich `ContractNumber`
  und Kategoriename), Groß-/Kleinschreibung ignorierend; leere Suche = volle Liste.
- Datumsfilter: einmalige/offene Pläne nach `TargetDate` im inklusiven Zeitraum; wiederkehrende
  Pläne (`Type = Recurring`) Treffer, wenn mindestens eine Fälligkeit (aus `TargetDate` ± n×`Interval`
  mit Monatsend-Semantik `SavingsPlan.AddIntervalWithMonthEndRule`, in beide Zeitrichtungen) im
  Zeitraum liegt; Pläne ohne `TargetDate` bei gesetztem Filter ausblenden; nur eine Grenze gesetzt
  = „ab Von"/„bis Bis"; Von > Bis → Grenzen tauschen; UND-Verknüpfung mit Suche und `OnlyActive`.
- Filterung vor dem Erzeugen der `Items` anwenden (Analyse-Aufrufe/Caches konsistent).

### #600 — Zurück-Button: Sparplandetail → Kategoriedetail kehrt nicht zur Herkunft zurück

- Ribbon-Aktion „Kategorie" in `SavingsPlanCardViewModel` feuert `OpenCategory` ohne `?back=`.
- Erwartung: `?back={aktuelle Karten-URL}` anhängen (etabliertes Muster: `OpenBankContact`/
  `OpenAccountDetails` in `CardPage.razor`, `BaseViewModel.RequestOpenLinkedCard`), inkl.
  Weitergabe eines vorhandenen `back`-Parameters (Navigationsketten).
- Direktaufruf der Kategoriekarte ohne `back` → Fallback Kategorieübersicht bleibt.

### #601 — Wertpapierübersicht: Suche ohne Wirkung; Datumsfilter entfernen

- `SecuritiesListViewModel`: `AllowRangeFiltering => false` (Konvention bei Listen ohne
  Datumsbezug); `Search` clientseitig auswerten (Liste ungepagt) — Vorschlag: Name, Identifier
  (ISIN/WKN/Ticker), AlphaVantage-Code, Kategoriename, Groß-/Kleinschreibung ignorierend.

### #602 — Budgetzweckregeldetail: Zurück-Button erzeugt ungültige Route

- `BudgetRuleCardViewModel` sendet Back-Payloads als app-relative Pfade ohne führenden Slash
  (`card/budget/purposes/{id}`); `CardPage` wertet nur `/`-/`http`-Payloads als URL, sonst
  Legacy `list/{Kind}/{payload}` → ungültige Route.
- Bevorzugte Lösung (Issue): `CardPage` akzeptiert app-relative Pfade (`card/…`, `list/…`)
  zentral als Direktziel — behebt gleichzeitig #606/#607/#608 und die weiteren genannten
  Stellen (`BudgetPurposeCardViewModel` „Category", `BudgetCategoryListViewModel` Back).
- Keine Regression bei `card,{id}`, einteiligem Subtyp, `?back=`, leerem Payload.

### #603 — Analyse-Buttons Budgetübersicht und Budgetbericht vereinheitlichen

- Beide Ansichten: identische fünf Buttons „Vormonat", „Aktueller Monat", „Nächster Monat",
  „Vorjahr", „Nächstes Jahr" in gleicher Reihenfolge, semantik identisch (relativ zur aktuellen
  Ansicht verschieben; „Aktueller Monat" = laufender Kalendermonat; wiederholbar).
- `BudgetPurposeListViewModel` (bisher absolut auf heute, ohne „Nächster …") und
  `BudgetReportViewModel` (relativ, fehlt nichts) an die verbindliche Semantik angleichen.
- Offene Punkte des Issues (Entscheidung im Projektplan zu dokumentieren): „Aktuelles Jahr"
  in der Übersicht, Zeitraumbreite bei Jahresnavigation, nicht monatsalignierte Zeiträume.

### #604 — Benutzerübersicht: Suche ohne Wirkung; Datumsfilter entfernen

- `UserListViewModel`: `Search` clientseitig auf `Username` (Teilstring, Case-insensitive)
  auswerten; `AllowRangeFiltering => false`. Andere Listen unverändert.

### #605 — KI-Zugänge: „Neu" bei deaktivierter KI-Integration sperren

- `/list/mcp-accesses` (`McpAccessListViewModel`): bei `IsMcpDisabled` darf der Anlege-Wizard
  nicht starten — Empfehlung: Ribbon-Aktion „Neu" `Disabled` + Guard im Wizard
  (`McpAccessCreateWizard` prüft Status, zeigt `Mcp_Disabled_Hint` statt Schritte; Defense in depth).
- Hinweis in der Liste bleibt; aktiviert: unverändertes Verhalten inkl. Mobile-Shortcut.
- Nicht-Ziel: serverseitige Durchsetzung des Schalters in `POST api/mcp-accesses` (Folge-Issue).

### #606/#607/#608 — MCP-Ansichten: Zurück-Button erzeugt ungültige Route

- `McpAuditViewModel`, `McpSecurityOverviewViewModel`, `McpApprovalCenterViewModel`,
  `McpAccessCardViewModel` senden `Back` mit Payload `"card/setup"` → `list/{kind}/card/setup`
  (kein Provider → „Laden …" für immer).
- Ziel: Zurück navigiert nach `/card/setup` bzw. zum `?back=`-Ziel; keine `/list/<kind>/card/setup`
  mehr. Wird über die zentrale Behebung in `CardPage` (#602) mitabgedeckt.

### #609 — Budgetzweckregelliste: „Neu" wirft Null-Referenz bei ungespeichertem Budgetzweck

- `BudgetRuleListViewModel` Ribbon-Callback `NewRule`: `BudgetCategoryId!.Value` wirft, wenn
  `BudgetPurposeId == Guid.Empty` und `BudgetCategoryId == null`.
- Empfehlung: Aktion `Disabled` solange weder gültige `BudgetPurposeId` noch `BudgetCategoryId`
  vorliegt; Payload null-sicher formulieren. Bei gespeichertem Kontext unverändert
  (`purpose,{id}`/`category,{id}` → `card/budget/rules/new?prefill=…&back=…`).

### #610 — Budgetbericht: letzte X-Achsenbeschriftung am Kartenrand abgeschnitten

- `BudgetLineChart.razor` (`viewBox="0 0 1000 325"`, Zeichenfläche endet x=980): erste/letzte
  X-Labels werden beschnitten. Fix im SVG-Koordinatensystem; Desktop+Mobile, Light+Dark,
  1/2/>12 Perioden. bUnit-Test für Rand-Labels (Komponente bisher ohne Tests).
- Offene Punkte: identisches Muster in Schwester-Chart ggf. mitbeheben; vertikale Knappheit
  (Basislinie 320/325) im selben Fix prüfen.

## Betroffene Bereiche

- `FinanceManager.Web/Components/Pages/CardPage.razor`, `ListPage.razor`, `GenericListPage`
- `FinanceManager.Web/ViewModels/…` (Savings/SavingsPlan, Securities, Admin/Users, Budget,
  Mcp), `Components/Shared/BudgetLineChart.razor`, Ribbon-Definitionen, `Resources/*.resx`
- Tests: `FinanceManager.Tests/ViewModels`, `FinanceManager.Tests/Components`, ggf. E2E

## Akzeptanzkriterien (Projekt)

- Alle zwölf Teil-Issues sind erfüllt (jeweils eigene Akzeptanzkriterien im Issue-Text).
- Keine Regressionen: bestehende Tests grün; neue Verhalten durch Tests belegt; Format-
  und Warnungs-Gates der CI erfüllt.
- PR gegen `staging` mit Closing-Bezügen zu #614 sowie den Teil-Issues.

## Explizite Nicht-Ziele

- Keine serverseitige Feature-Schalter-Durchsetzung der MCP-Verwaltungs-API (#605, Folge-Issue).
- Kein genereller Umbau des Listen-/Navigationsframeworks über die genannten Defekte hinaus
  (z. B. `?back=` auf ListPage, Hinweis bei ungespeicherten Änderungen #600).
- Kein UI-Redesign (#590 läuft parallel; Fixes bleiben designunabhängig).
- Keine API-Schemaänderungen, soweit clientseitige Filterung ausreicht (#599/#601/#604:
  Empfehlung clientseitig, da Listen ungepagt vollständig geladen werden).

## Offene Fragen (aus den Issues, vorab entschieden — Begründung im Projektplan)

- #603: Schicksal von „Aktuelles Jahr" in der Übersicht; Verschieberegel freier Zeiträume.
- #609: Deaktivieren vs. Ausblenden der „Neu"-Aktion.
- #605: Deaktivieren vs. Ausblenden vs. Wizard-Hinweis.
- #602: Ziel der Zurück-Navigation der Regelkarte (Eltern-Karte vs. Regelliste).
