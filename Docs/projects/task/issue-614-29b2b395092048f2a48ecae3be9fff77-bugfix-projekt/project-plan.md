# Projektplan – Bugfix-Projekt (Issue #614)

## Übersicht

- **Projekttitel:** Bugfix-Projekt UI-Defekte (#599–#610)
- **Gesamtziel:** Zwölf UI-Bugs in FinanceManager.Web beheben — Listenfilter, Back-Navigation,
  Ribbon-Absicherungen, Analyse-Buttons, Chart-Beschriftung — jeweils mit Testabsicherung,
  anschließend ein PR gegen `staging`.
- **Betroffene Bereiche:** `CardPage.razor`, `ListPage`/`GenericListPage`-Provider
  (SavingsPlans/Securities/Users-ListViewModels), Budget-ViewModels (Purpose-Liste, Regel-Liste,
  Bericht), MCP-ViewModels + Create-Wizard, Chart-Komponenten, resx-Ressourcen, Tests.

## Grobe Vorgehensentscheidungen

- **Zentrale statt Punkt-Fixe bei der Back-Navigation:** `CardPage` akzeptiert app-relative
  Pfade als Direktziel — **ausschließlich über eine geschlossene Präfix-Whitelist**
  (`card/`, `list/`, `reports/`). Keine generelle „enthält `/`"-Regel: einteilige Legacy-
  Payloads wie `account/{guid}` (PostingsCardViewModel) enthalten `/` und müssen weiterhin
  in den Subtyp-Branch `list/{Kind}/{payload}` laufen. Die Whitelist-Prüfung steht **nach**
  der `?back=`-Auswertung (explizites Rücksprungziel gewinnt vor dem VM-Fallback-Payload,
  wie von #606–#608 gefordert) und vor dem GUID-/Legacy-Teil; der Query-Teil des Payloads
  (z. B. eigenes `?back=` in `InitBack`) bleibt unverändert Bestandteil des Ziels.
  Behebt #602, #606, #607, #608 und die gleichartige Stelle in `BudgetPurposeCardViewModel`
  („Category"-Aktion) in einem Eingriff; bewahrt alle Bestandsformate (`card,{id}`,
  einteiliger Subtyp, `?back=`, leerer Payload, GUID-Sonderfall). Relative `NavigateTo`-Ziele
  bleiben PathBase-konform (`RazorRootAbsolutePathTests`).
- **Clientseitige Filterung** für #599/#601/#604: alle drei Listen laden ungepagt vollständig;
  API-Erweiterung entfällt (Nicht-Ziel). Filterung vor Projektion auf `Items`.
- **Recurring-Fälligkeiten (#599):** Monatsend-Semantik aus `SavingsPlan.AddIntervalWithMonthEndRule`
  wiederverwenden — Methode um einen Richtungsparameter verallgemeinern (oder eine
  `SubtractIntervalWithMonthEndRule`-Variante ergänzen) und sichtbar machen (`internal`
  bzw. `public static`, Domain-seitig dokumentiert), statt Logik im VM zu duplizieren.
  Match-Regel konkret: Fälligkeiten = `TargetDate ± n·Intervall`; ein Recurring-Plan ist
  Treffer, wenn eine Fälligkeit im effektiven Zeitraum liegt — Iteration rückwärts von
  `TargetDate`, solange Fälligkeit ≥ `RangeFrom` **und** ≥ `CreatedUtc` des Plans (ältere
  Fälligkeiten existieren nicht); Iteration vorwärts, solange Fälligkeit ≤ `RangeTo`.
  Ohne `RangeFrom` gilt `CreatedUtc` als Untergrenze, ohne `RangeTo` +∞ als Obergrenze.
  Monatsend-Clamping-Mehrdeutigkeit bei Rückrechnung wird akzeptiert (Fälligkeit = das,
  was die Domain-Regel rückwärts ergibt).
- **„Neu"-Aktionen absichern statt verstecken** (#605/#609): `UiRibbonAction.Disabled` +
  Tooltip (neue resx-Keys de/en/neutral) — Blockade bleibt für den Anwender erklärbar;
  zusätzlich Guard im Wizard (Defense in depth) bzw. null-sichere Payload.
- **Schrittfolge nach Kopplung:** Zuerst der zentrale Navigationsfix (größte Wirkung, eine
  gemeinsame Ursache für 5 Issues), dann Listenfilter, dann die übrigen Einzelthemen.

### Getroffene Entscheidungen zu offenen Punkten (Begründung, ohne Nutzerbestätigung gewählt)

- **#603 „Aktuelles Jahr":** entfällt in der Budgetübersicht — die verbindliche Semantik
  verlangt identische fünf Buttons in beiden Ansichten; der Jahres-Schnellfilter ist über den
  freien Von/Bis-Filter weiterhin erreichbar. Reihenfolge: Vormonat, Aktueller Monat,
  Nächster Monat, Vorjahr, Nächstes Jahr (vorhandene resx-Keys `Ribbon_PrevMonth`,
  `Ribbon_ThisMonth`, `Ribbon_NextMonth`, `Ribbon_PrevYear`, `Ribbon_NextYear`).
- **#603 Jahresnavigation Übersicht:** Zeitraumbreite bleibt erhalten, Verschiebung um ±1 Jahr
  (naheliegende Lesart der verbindlichen Semantik).
- **#603 freie Zeiträume:** „Vormonat"/„Nächster Monat" verschieben beide Grenzen exakt um
  einen Kalendermonat (`AddMonths`-Semantik mit Tages-Clamping), „Vorjahr"/„Nächstes Jahr"
  analog `AddYears`. Kein Snapping auf Kalendermonate — erhält manuell gesetzte Bereiche.
- **#603 Bericht „Aktueller Monat":** `AsOfDate` = Monatsende laufender Monat, `Settings.Months`
  bleibt erhalten (Ist-Verhalten, semantisch korrekt).
- **#609:** Aktion `Disabled` (statt `Hidden`/`EmbeddedList=null`) + Tooltip „zuerst speichern";
  bleibt sichtbar, Blockade wird erklärt. Payload wird zusätzlich null-sicher.
- **#605:** Ribbon „Neu" `Disabled` bei `IsMcpDisabled` (bleibt sichtbar + Tooltip via
  `Mcp_Disabled_Hint`) **plus** Guard in `McpAccessCreateWizard.OnInitializedAsync`
  (`Mcp_GetStatusAsync` → Hinweis statt Schritte) — deckt auch den `SetupMcpTab`-Pfad und
  Zustandswechsel bei geöffneter Seite ab.
- **#602:** Back-Ziel der Regelkarte bleibt die Eltern-Karte (Implementierungsabsicht des
  Bestandscodes, fachlich sinnvoll: Kontext des zugehörigen Zwecks/der Kategorie).
- **#599:** Suchfelder `Name` + `ContractNumber` + Kategoriename (Issue-Vorschlag); Fälligkeiten
  beider Zeitrichtungen; Pläne ohne `TargetDate` bei gesetztem Datumsfilter ausblenden.
- **#601:** Suchfelder `Name`, `Identifier`, `AlphaVantageCode`, `CategoryName` (Issue-Vorschlag).
- **#610:** Schwester-Charts mit identischem Muster werden im selben Schritt mitkorrigiert —
  konkret `PortfolioBarLineChart`; `PortfolioGroupedBarChart` ist slot-zentriert (Label-Mitte
  erreicht den viewBox-Rand nie) und bleibt bewusst unverändert (Entscheidung in Schritt-5-Plan).

## Entwicklungsschritte

### Schritt 1: Back-Navigation zentral reparieren und Rücksprungziele verknüpfter Karten setzen

Behebt #600, #602, #606, #607, #608.

- `CardPage.razor` `case "Back"`: Neuer Prüfschritt **nach** der bestehenden `?back=`-
  Auswertung und **vor** GUID-/Komma-/Legacy-Auswertung: Payloads mit Präfix `card/`,
  `list/` oder `reports/` (geschlossene Whitelist, Ordinal, ohne `:`-Schema und ohne
  `//`-Start) werden direkt an `NavigationManager.NavigateTo(payload)` übergeben —
  inklusive im Payload enthaltenem Query-Teil. Payloads `card/setup`,
  `card/budget/purposes/{id}`, `card/budget/categories/{id}` landen damit korrekt;
  einteilige Subtyp-Payloads (z. B. `account/{guid}` der Postings-Karte) laufen
  unverändert in den Legacy-Branch `list/{Kind}/{payload}`.
- `SavingsPlanCardViewModel` Ribbon „Kategorie": Payload um `?back={aktuelle Karten-URL}`
  ergänzen — Muster `OpenBankContact`/`BuildCardPath` inkl. Weitergabe vorhandenen `back`-
  Kontexts (Navigationsketten). Umsetzung entweder im `case "OpenCategory"` (CardPage hängt
  `?back=` an, wie `OpenBankContact`) oder im VM-Payload — Entscheidung im Lifecycle-Plan,
  bevorzugt CardPage-seitig (VM bleibt schlank, etabliertes Muster).
- Verifikation `BudgetCategoryListViewModel` Back-Payload `list/budget/purposes`: wird von
  `ListPage` (ignoriert Payloads) nicht betroffen — dokumentieren, kein Eingriff.
- **Akzeptanzkriterien:** Zurück auf Regelkarte → Purpose-/Kategorie-Karte (auch nach Delete);
  alle vier MCP-Ansichten → `/card/setup` (bzw. `?back=`-Ziel, falls gesetzt — `?back=` gewinnt
  vor dem Payload); keine `list/<kind>/card/…`-Route mehr; Sparplankarte → Kategorie → Zurück →
  Sparplankarte inkl. Ketten; kein Regression bei `card,{id}`, einteiligem Subtyp
  (**explizit:** `account/{guid}` → `list/postings/account/{guid}`), `?back=`, leerem Payload,
  GUID-Sonderfall. bUnit-/VM-Tests für alte und neue Formate.
- **Abhängigkeiten:** Keine.
- **Bereiche:** `CardPage.razor`, `SavingsPlanCardViewModel`, `CardPageTests`/VM-Tests.

### Schritt 2: Suche und Datumsfilter in Sparplan-, Wertpapier- und Benutzerübersicht

Behebt #599, #601, #604.

- `SavingsPlansListViewModel.LoadPageAsync`: `Search` (Name + ContractNumber + Kategoriename,
  Teilstring, Case-insensitive) und Datumsbereich auf die DTO-Liste anwenden **vor** `Items`-
  Projektion/Analyse-Aufrufen. Datumsfilter: `TargetDate` ∈ [Von,Bis] für OneTime/Open mit
  Zieldatum; Recurring: Treffer bei ≥1 Fälligkeit im Zeitraum — Iteration rückwärts von
  `TargetDate` solange Fälligkeit ≥ `RangeFrom` und ≥ `CreatedUtc`, vorwärts solange ≤ `RangeTo`;
  ohne `RangeFrom` Untergrenze = `CreatedUtc`, ohne `RangeTo` Obergrenze = +∞; Monatsend-Semantik
  via verallgemeinerter `SavingsPlan.AddIntervalWithMonthEndRule` (Richtungsparameter) bzw.
  neuer `SubtractIntervalWithMonthEndRule`; Pläne ohne `TargetDate` bei gesetztem Filter
  ausblenden; eine Grenze = offene Seite; Von > Bis → tauschen; UND mit Suche/`OnlyActive`.
  `AddIntervalWithMonthEndRule` sichtbar machen.
- `SecuritiesListViewModel`: `AllowRangeFiltering => false`; `Search` clientseitig auf
  Name/Identifier/AlphaVantageCode/CategoryName.
- `UserListViewModel`: `AllowRangeFiltering => false`; `Search` clientseitig auf `Username`.
- **Akzeptanzkriterien:** jeweils Issue-Akzeptanzkriterien; VM-Tests für Suchfilterung,
  Datumsbereich (inkl. Recurring-Fälligkeiten, Grenzfälle, Tausch, Leer-Suche),
  `AllowRangeFiltering == false`.
- **Abhängigkeiten:** Keine (fachlich unabhängig; Schritt 1 berührt andere Dateien).
- **Bereiche:** drei ListViewModels + `SavingsPlan`-Domain (Sichtbarkeit), VM-Tests.

### Schritt 3: Analyse-Buttons Budgetübersicht und Budgetbericht vereinheitlichen

Behebt #603.

- Beide VMs: identische fünf Buttons (Reihenfolge: Vormonat, Aktueller Monat, Nächster Monat,
  Vorjahr, Nächstes Jahr), semantisch relativ zur aktuellen Ansicht.
- `BudgetPurposeListViewModel`: `PrevMonth`/`PrevYear` von absolut auf relativ umbauen;
  `ThisMonth` bleibt Sprung auf laufenden Monat; `NextMonth`/`NextYear` neu; `ThisYear` entfällt
  (s. Entscheidungen). Verschiebung der `RangeFrom`/`RangeTo`-Grenzen mit AddMonths/AddYears;
  Default ohne gesetzten Bereich = aktueller Monat als Anker.
- `BudgetReportViewModel`: Bestand relativ — Labels/Reihenfolge prüfen, ggf. angleichen;
  `Settings.Months` unverändert.
- **Akzeptanzkriterien:** Issue-Akzeptanzkriterien (fünf Buttons, relative Verschiebung ±1
  Monat/±1 Jahr, wiederholbar, resx-Labels); VM-Tests für Verschieberegeln inkl. Monatsend-/
  Clamping-Fälle (z. B. 31.3. → 28./29.2.).
- **Abhängigkeiten:** Keine.
- **Bereiche:** `BudgetPurposeListViewModel`, `BudgetReportViewModel`, VM-Tests.

### Schritt 4: „Neu"-Ribbon-Aktionen bei ungültigem Kontext absichern

Behebt #605, #609.

- `McpAccessListViewModel`: Ribbon `New` `Disabled = IsMcpDisabled` (+ Tooltip
  `Mcp_Disabled_Hint`); `McpAccessCreateWizard.OnInitializedAsync` prüft `Mcp_GetStatusAsync`
  und zeigt bei deaktiviertem Feature den Hinweis statt der Schritte (Defense in depth, deckt
  `SetupMcpTab`-Einstieg ab).
- `BudgetRuleListViewModel`: `NewRule`-Aktion `Disabled`, solange weder gültige
  `BudgetPurposeId` noch `BudgetCategoryId` vorliegt; Tooltip (neue resx-Keys de/en/neutral,
  z. B. „Budgetzweck bzw. Budgetkategorie zuerst speichern"); Payload-Berechnung null-sicher.
- **Akzeptanzkriterien:** jeweils Issue-Akzeptanzkriterien; kein Wizard-Start bei deaktiviertem
  MCP; kein Absturz bei `NewRule` im ungespeicherten Zustand; aktiviert/gespeichert unverändert;
  Tests (VM-/bUnit-Ebene: Disabled-Flag, Wizard-Guard, NewRule-Callback).
- **Abhängigkeiten:** Keine.
- **Bereiche:** `McpAccessListViewModel`, `McpAccessCreateWizard`, `BudgetRuleListViewModel`,
  resx, Tests.

### Schritt 5: X-Achsenbeschriftung der SVG-Charts vollständig darstellen

Behebt #610.

- `BudgetLineChart.razor`: Rand-Reserve in der SVG-Geometrie so erweitern, dass erste/letzte
  `text-anchor='middle'`-Labels vollständig im viewBox liegen (z. B. erweiterte
  viewBox-Min/Max- oder Plot-Konstanten); vertikale Knappheit (Label-Basislinie y=320 bei
  Höhe 325) im selben Zug prüfen/beheben.
- Gleiche Korrektur für `PortfolioBarLineChart.razor` (identisches Konstantenmuster).
  `PortfolioGroupedBarChart.razor` bleibt ausgenommen — slot-zentrierte Labels erreichen den
  viewBox-Rand geometrisch nie (Label-Mitte = Slot-Mitte).
- **Akzeptanzkriterien:** Issue-Akzeptanzkriterien (alle Labels sichtbar, Desktop+Mobile,
  Light+Dark, 1/2/>12 Perioden); bUnit-Tests der Rand-Label-Positionen für die betroffenen
  Charts (`BudgetLineChart` erstmalig testabgedeckt). Der E2E-Screenshot-Nachweis erfolgt
  gebündelt auf Projektebene für alle UI-Änderungen.
- **Abhängigkeiten:** Keine.
- **Bereiche:** Chart-Komponenten, bUnit-Tests.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Back-Navigation zentral + Rücksprungziele verknüpfter Karten (#600/#602/#606/#607/#608) | Keine | CardPage, SavingsPlanCardViewModel |
| 2 | Suche/Datumsfilter in Sparplan-, Wertpapier-, Benutzerliste (#599/#601/#604) | Keine | 3 ListViewModels, Domain-Sichtbarkeit |
| 3 | Analyse-Buttons vereinheitlichen (#603) | Keine | BudgetPurpose- und BudgetReportViewModel |
| 4 | „Neu"-Aktionen bei ungültigem Kontext absichern (#605/#609) | Keine | McpAccessList/Wizard, BudgetRuleList, resx |
| 5 | X-Achsenbeschriftung der SVG-Charts (#610) | Keine | 2 Chart-Komponenten, bUnit |

## Abschluss / Delivery (kein Entwicklungsschritt)

Nach Merge aller Schritte in den Basisbranch: gebündelte E2E-Abnahme mit Screenshots
(UI-Änderungen — Pflicht laut Aufgabenumfeld), `git push` des Basisbranches, PR gegen
`staging` mit `Closes #614` sowie `Closes #599`–`#610` im Body, PR-Checks überwachen
(fail-fast), Ergebnis dem Kanal melden.

## Offene Punkte

Keine. Alle in den Issues verbliebenen offenen Fragen wurden oben mit Begründung entschieden
(siehe „Getroffene Entscheidungen"); sie sind nicht scopekritisch und betreffen bevorzugte,
im Issue selbst empfohlene Varianten.
