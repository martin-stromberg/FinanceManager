# Inventar-Detail: Aggregations- und Filtersemantik

Diese Semantik muss die Posten-Abfrage exakt spiegeln, damit die Overlay-Summe dem Zellwert entspricht (Akzeptanzkriterium 3/5). Alle Zeilenangaben beziehen sich auf `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`, soweit nicht anders angegeben.

## GroupKey-Formate und Postenmengen

| GroupKey | Erzeugung | Postenmenge für Drill-down |
|----------|-----------|----------------------------|
| `Account:{id}` | Z. 501 | `Kind == Bank && AccountId == id` |
| `Contact:{id}` | Z. 506 | `Kind == Contact && ContactId == id` |
| `SavingsPlan:{id}` | Z. 516 | `Kind == SavingsPlan && SavingsPlanId == id` — bei `LinkedAccountPostings`-Quelle stattdessen Bank-Posten der verlinkten Konten (s. u.) |
| `Security:{id}` | Z. 531–532 | `Kind == Security && SecurityId == id` (alle Subtypen bzw. netted) |
| `Security:{id}:{subType}` | Z. 530–531 | zusätzlich `SecuritySubType == subType` — wichtig, da Security-Reihen pro Subtyp aufgesplittet werden |
| `Category:{Kind}:{guid}` | `CategoryKey()` Z. 467 | Posten des Kinds, deren Entitäts-Kategorie `guid` ist (Mapping über `Contacts/SavingsPlans/Securities.CategoryId`) |
| `Category:{Kind}:_none` | `CategoryKey()` Z. 467 | Posten des Kinds, deren Entität `CategoryId == null` hat |
| `Type:{Kind}` | `TypeKey()` Z. 466 | alle Posten des Kinds unter den aktiven Filtern |
| *(Total-Zeile, kein Key)* | `GetTotals()` im VM | alle Posten der Spaltenperiode über alle Top-Level-Gruppen |

`ParseKindFromKey` (Z. 1769–1792) leitet das Kind aus dem Key ab; `TryParseSecurityKey` (Z. 1750–1767) zeigt das Parsen des Security-Keys inkl. Subtyp-Suffix.

## Periodenfenster je Spalte

Anker: `analysis` = `AnalysisDate` normalisiert auf Monatsanfang, sonst `DateTime.UtcNow`-Monat (Z. 92–93). Angezeigte Zeile ist stets die neueste Periode der Gruppe (s. dataflow.md). Quellgranularität `sourcePeriod` (Z. 68–77): Month→Month, Quarter→Quarter, HalfYear→HalfYear, Year→Year, **Ytd→Month, AllHistory→Month**.

| Spalte | Fenster |
|--------|---------|
| Betrag (`Amount`, PeriodStart = letzte Periode der Gruppe) | `[PeriodStart, PeriodStart + Intervalllänge)` — Monat +1M, Quartal +3M, Halbjahr +6M, Jahr +12M |
| Vorperiode (`PreviousAmount`) | gleiches Fenster auf `PrevPeriod(PeriodStart)` (Z. 755–769): Month `AddMonths(-1)`, Quarter → Quartalsanfang −3M, HalfYear → HJ-Anfang −6M, Year/Ytd → `{jahr-1}-01-01` |
| Vorjahr (`YearAgoAmount`) | Fenster auf `PeriodStart.AddYears(-1)` (Z. 786–798) |
| **Ytd** | Betrag = `[{Jahr}-01-01, analysis + 1 Monat)` (Monate ≤ Analysismonat, Z. 633–656); Vorperiode/Ytd-Vorjahr = gleiches Fenster im Vorjahr |
| **AllHistory** | unbegrenzt (`rawAnon` komplett, Anker `2000-01-01`, Z. 658–695); kein Vorperiode/Vorjahr (UI deaktiviert Vergleiche) |

Hilfsfunktionen: `ComputeLatestPeriod` (Z. 698–719, Duplikat im Netting-Pfad Z. 1292–1313), `GetPeriodStart` in `PostingAggregateService.cs:30–40`, `ApplyDateWindow` (Z. 933–942: Valuta-Modus `ValutaDate == DateTime.MinValue → BookingDate`-Fallback, identisch zur In-Memory-Logik Z. 901–904).

**Stolperfalle Take:** `Take` begrenzt nur die sichtbaren Perioden (Trim Z. 800–811); die angezeigte Zelle ist aber immer die Analyse-/neueste Periode, daher ist das Fenster stabil. Posten jenseits der neuesten Periode werden verworfen (Z. 727).

## Filtersemantik (Z. 380–460)

- Entity-Filter `AccountIds/ContactIds/SavingsPlanIds/SecurityIds`: greifen je Kind, wenn gesetzt.
- Kategoriefilter `ContactCategoryIds/SavingsPlanCategoryIds/SecurityCategoryIds`: nur aktiv bei `IncludeCategory` (Z. 410, 419, 430) — ersetzen dann den Entitätsfilter dieser Kinds.
- `SecuritySubTypes` (int-Set): nur wenn Security-Kind Teil der Anfrage (Z. 392–394); Posten ohne Subtyp fallen raus.
- `IncludeDividendRelated`: nur Flag — wirkt über die Pfadwahl (s. u.).
- Textfilter `BankTextFilters/ContactTextFilters/SavingsPlanTextFilters/SecurityTextFilters`: `TextFilterList.Normalize` (Z. 104), OR-Match `BudgetRulePatternMatcher.MatchesPosting(Subject, Description, pat, useRegex: false)` (Z. 900). Kinds mit Textfilter werden **immer** aus rohen `Postings` aggregiert, nicht aus Aggregaten — die Drill-down-Selektion ist dort identisch.
- `Amount == 0` wird ausgeschlossen (Aggregat-Aufbau Z. 899; `PostingAggregateService` ignoriert Nullposten generell).
- Ownership: je Kind die Owner-Entity-Ids laden (Z. 191–199) und Posten über Entitäts-Referenz einschränken; Aggregat-Zeilen ohne besessene Entität werden ebenfalls verworfen (Z. 319–324).

## Sonderpfad 1: `SavingsPlanPostingSource.LinkedAccountPostings` (Z. 206–280)

- `SavingsPlanAccountLinks` (owner-gefiltert) liefern Plan→Konten-Mapping.
- Statt `SavingsPlan`-Posten des Plans werden die **Bank-Posten der verlinkten Konten** als `SavingsPlan`-Zeilen emittiert (Z. 265–278). Bei Sparplan-Textfilter werden die Bank-Rohposten mit dem Pattern gematcht (Z. 224–249).
- Drill-down-Konsequenz: Für `SavingsPlan:{id}`-Zeilen (und darüber liegende Category-/Type-/Total-Zeilen) sind die zugehörigen Bank-Posten der verlinkten Konten zu listen; Pläne ohne Link behalten ihre eigenen Plan-Posten.

## Sonderpfad 2: `QuerySecurityDividendsNetAsync` (Z. 951 ff.)

Aktiv, wenn ausschließlich `PostingKind.Security` **und** (`IncludeDividendRelated` oder `CompareProjection` oder Ytd+Dividend-Subtyp) (Z. 55–65, 83–89).

- Postenbasis: `Kind == Security`, `SecuritySubType != null`, owner-besessene Securities, Datumsfenster `[loadStartMonth, analysis+1M)` mit Vorjahres-/Projektions-Vorgriff (Z. 962–998).
- **Netting:** Posten werden nach `GroupId` gruppiert; Wert = Summe der Subtypen Dividend+Fee+Tax (Z. 1026–1046); Ankerdatum = Dividenden-Posten der Gruppe.
- Drill-down-Konsequenz: Der Zellwert ist eine **Gruppensumme**, kein Posten-Sum über einzelne Zeilen. Das Overlay muss entweder Gruppen (Dividenden-Ereignisse) mit deren Einzelposten anzeigen oder die Einzelposten der Gruppe auflisten — in beiden Fällen stimmt die Summe nur bei Vollständigkeit der Gruppe.
- Category- und Type-Keys in diesem Pfad: `Security:{id}` (ohne Subtyp-Suffix) und `Category:Security:{id|_none}` (Z. 1071, 1086, 1114); Type-Zeilen nur bei AllHistory (Z. 1161–1183).

## Vergleichsspalten-Besonderheiten

- `PreviousAmount`/`YearAgoAmount` sind `null`, wenn kein Vergleichspunkt existiert → Zelle zeigt `"-"`, kein Button (Anforderung: deaktiviert/kein Button).
- Ytd: Vorperiode = Vorjahr-Fenster (Jan–Analysismonat des Vorjahrs, Z. 766); Vorjahr identisch — beide Spalten können somit dieselbe Postenmenge liefern.
- Nach dem „leere neueste Gruppen entfernen"-Schritt (Z. 813–832) kann `Amount == 0` mit gefüllten Vergleichswerten auftreten → Betrag-Button an einer 0,00-Zelle liefert eine leere Liste (Leeranzeige) — korrekt, aber UX-relevant.

## Extraktionskandidaten (Duplikation vermeiden)

Private/nicht teilbare Logik, die der Posten-Service benötigt:

- `PrevPeriod`/`ComputeLatestPeriod` (Z. 698–719, 755–769, Duplikat Z. 1292–1346)
- `ApplyDateWindow` (Z. 933–942), `ParseKindFromKey` (Z. 1769), `TryParseSecurityKey` (Z. 1750)
- Owner-Entity-Id-Auflösung (Z. 191–199), Kategorie-Mapping-Aufbau (Z. 326–378), Filter-Switch (Z. 400–459)
- Linked-Account-Auflösung (Z. 210–280), Textfilter-Match (Z. 859–920)

Empfehlung: gemeinsame Rohposten-/Fenster-Helfer (z. B. internal static in `FinanceManager.Infrastructure/Reports/` oder als Shared-Service) statt Copy-paste; der Aggregatpfad kann inkrementell darauf umgestellt werden oder bleibt unverändert (Scope-Abgrenzung: keine Änderung der Aggregatberechnung).
