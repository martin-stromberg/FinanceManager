# Umsetzungsplan: Hochrechnung für Posten im Bericht (Issue #530)

## Übersicht

Die Berichts-Hochrechnung (`CompareProjection`) wird von der bisherigen Security-Sonderlösung (Netto-Dividendenpfad `QuerySecurityDividendsNetAsync` mit lokaler Funktion `ApplyProjectionAmounts`) zu einer einzigen artübergreifenden Funktionalität verallgemeinert: Mustererkennung, Erwartungsberechnung und Punkt-Zuordnung werden in eine geteilte interne Komponente extrahiert; der Standardpfad `QueryAsync` lernt das Laden von Roh-Events für alle vier `PostingKind`s und ruft dieselbe Komponente auf. UI-Gates, Detail-DTO und MCP-Ausgabe werden artneutral gemacht. Der Auftraggeber evaluiert das Ergebnis fachlich — es werden keine artspezifischen Sonderlogiken erfunden.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein wiederkehrendes „Buchungs-Event" je Berichts-Entitätszeile. Event-Quelle je Postenart: **Bank** = jede `Posting`-Buchung des Kontos (`Amount != 0`); **Contact** = jede Buchung des Kontakts; **SavingsPlan** = jede Buchung des Plans, bei `SavingsPlanPostingSource.LinkedAccountPostings` die Bank-Buchungen der verknüpften Konten (dem Plan zugeordnet); **Security** = Netto-Dividendenereignis je `GroupId` (Dividend+Fee+Tax, unverändert). |
| Auslösende Ereignisse | `ReportAggregationQuery.CompareProjection == true` und `Interval != AllHistory` — ausgelöst über Dashboard-Checkbox (`Label_CompareProjection`), gespeicherte Favoriten (`ReportFavorite.CompareProjection`), KPI-Kacheln (`HomeKpiGrid`, reicht das Flag bereits durch) und MCP (`fm_reporting_aggregate`, `fm_reports_configuration_manage`). |
| Erwartete Wirkung | `ComparedProjection = true`, `ProjectionAmount` = gebuchter Betrag + erwartete unbestätigte Vorjahresbeträge je Berichtspunkt; Detail-Overlay mit erwarteten Einzelereignissen; Kategorie- und `Type:`-Summenzeilen aggregieren die Projektionswerte ihrer Kinder artneutral. |
| Umgang mit fehlenden Objekten | Entitäten ohne Vorjahres-Events im Referenzzeitraum: kein Muster, keine Erwartung (verwerfen, keine Objekterzeugung). Entitäten ohne aktuelle, aber mit unerfüllten Vorjahres-Events: Erwartung fließt ein — Leaf-Zeilen existieren im Standardpfad aus `PostingAggregates` über die gesamte Historie, die Latest-Period-Injektion liefert den aktuellen Ankerpunkt (gleiche Semantik wie im Netto-Dividendenpfad). Es werden keine neuen fachlichen Objekte, Konfigurationen oder Sitzungen benötigt. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Storno-Beziehungen (`ReversalForPostingId`/`ReversedByPostingId`), `ParentId`-Splits und `LinkedPostingId`-Verknüpfungen von Bank-/Contact-/Sparplan-Buchungen werden bei der Mustererkennung nicht gesondert behandelt; jede Buchung mit `Amount != 0` ist ein Event. | 1:1-Übernahme der dokumentierten Aggregatsemantik (`business-rules.md`: „keine Sonderbehandlung von Stornos oder vorläufigen Buchungen"); der Dividendenpfad filtert Stornos als Events ebenfalls nicht. Fachliche Verzerrung (z. B. Korrekturpaare als irreguläres Muster) ist Gegenstand der angestrebten Kundenevaluation. | bestätigt (2026-11-15, durch Anforderung „Logik 1:1 übernehmen" + angekündigte Kundenevaluation) |
| 2 | `SavingsPlanPostingSubType.ActualReport`-Buchungen (manuelle Istmeldungen ohne Kontobewegung) zählen als normale Events. | Keine artspezifische Sonderlogik; die Istmeldung ist für den Report eine reguläre Buchung (`business-rules.md` enthält keine Ausnahme). | bestätigt (2026-11-15, durch Anforderung „Logik 1:1 übernehmen" + angekündigte Kundenevaluation) |
| 3 | Die Hochrechnungsspalte bekommt weiterhin kein Posten-Drill-Down. | Bestehendes, dokumentiertes Verhalten (`business-rules.md`: Delta- und Hochrechnungsspalten besitzen keinen Posten-Button); nicht Teil der Anforderung. | bestätigt (2026-11-15, durch Anforderung „Logik 1:1 übernehmen" + angekündigte Kundenevaluation) |
| 4 | Für Wertpapiere werden Erwartungen ausschließlich aus Dividenden-Ereignissen erzeugt — auch bei Mehrfachauswahl (Multi-Kind), wo Security-Zeilen je Untertyp aufgesplittet sind, tragen nur `Security:{id}:Dividend`-Zeilen eine Hochrechnung. | Käufe/Verkäufe/Gebühren sind keine wiederkehrenden Ertragserwartungen; das ist die bestehende, fachlich verifizierte Security-Semantik, die unverändert gilt. | bestätigt (2026-11-15, durch Anforderung „Logik 1:1 übernehmen" + angekündigte Kundenevaluation) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Artübergreifende Projektionslogik | Neue `internal static` Komponente `ReportProjectionCalculator` (Service-Layer-/Specification-Muster) in `FinanceManager.Infrastructure/Reports/`, die die heutige lokale Funktion `ApplyProjectionAmounts` samt `DetectDividendPattern`, `FindExpectedDividends`, Spacing-Prüfungen und Leaf/Category/Type-Rollups übernimmt — verallgemeinert auf `(PostingKind, EntityId)` statt `SecurityId`. | „Eine einzige artübergreifende Funktionalität": Beide Aufrufpfade (Netto-Dividendenpfad und Standardpfad) rufen identische Semantik auf; die bisherige lokale Funktion ist wegen ihrer Closures (`query`, `analysis`, `startMonth`, `endExclusive`, `HasPositiveHolding`, `securityNames`) nur im Security-Pfad erreichbar. |
| Event-Modell | Neuer `internal` Record `ProjectionEvent(PostingKind Kind, Guid EntityId, DateTime Date, DateTime Month, decimal Amount)` ersetzt `DividendEvent`; `ProjectionCandidate` erhält `Kind`+`EntityId` statt `SecurityId`; `DividendPattern` wird `internal enum ProjectionPattern` (gleiche Member). | Minimalinvasive Verallgemeinerung der bestehenden Strukturen; die private Sichtbarkeit erlaubt den Umbau ohne API-Bruch. |
| Event-Ladeverantwortung | Neuer `internal` Loader `ReportProjectionEventLoader` (Konstruktor: `AppDbContext`) mit `LoadEventsAsync` je Art und `LoadSecurityDividendEventsAsync` für das `GroupId`-Netting; der Security-Netto-Pfad bezieht seine `dividendEvents` künftig aus derselben Methode. | Trennung DB-Zugriff (Loader) von reiner Berechnung (Calculator); verhindert Divergenz der Event-Semantik zwischen den beiden Pfaden — sonst müsste das `GroupId`-Netting dupliziert werden. |
| Bestands-/Aktivitätsprüfung je Art | Calculator nimmt ein optionales Delegat `Func<Guid, DateTime, bool>` (nur für `PostingKind.Security` belegt — die bisherige `HasPositiveHolding`-Closure); für andere Arten `null` = immer zulässig. | Domain-Prüfung ergab: `Account`, `Contact`, `SavingsPlan` besitzen keinerlei Aktivitäts-/Bestands-Flags — es existiert schlicht kein Pendant zu `HasPositiveHolding`, die Prüfung entfällt ersatzlos. |
| Mehrfachauswahl (Multi-Kind) | Die Hochrechnung gilt für jede aktive Auswahlkombination außer `AllHistory` — auch Multi-Kind. `Type:{kind}`-Zeilen aggregieren die Projektionen ihrer Art. | „Artübergreifende Funktionalität" wörtlich; das `Type:`-Rollup existiert in `ApplyProjectionAmounts` bereits (heute hartkodiert auf `Type:Security`) und wird verallgemeinert. Ein künstliches Multi-Kind-Verbot wäre eine Einschränkung ohne fachlichen Zwang. |
| Leaf-Zuordnung `Security:`-Keys | Im Standardpfad (Multi-Kind, subtype-gesplittete Keys `Security:{id}:{subType}`) werden Erwartungen nur auf `SecurityPostingSubType.Dividend`-Suffix-Zeilen gelegt; im Netto-Dividendenpfad bleiben die plain `Security:{id}`-Keys zulässig (dort stehen sie für Netto-Dividenden). Parser: vorhandenes `ReportQuerySemantics.TryParseGroupKey` (liefert `Entity`+`PostingKind`+`EntityId`+`SecuritySubType`), kein neuer Parser nötig. | `Security:{id}` ohne Suffix bedeutet im Standardpfad „Buchung ohne Untertyp" — dort Dividenden-Erwartungen zu addieren wäre falsch zugeordnet; im Netto-Pfad ist der Key dagegen definitionsgemäß die Dividendenzeile. |
| Detail-DTO | `ReportProjectionExpectedDividendDto(SecurityId, SecurityName, …)` wird durch `ReportProjectionExpectedItemDto(PostingKind Kind, Guid EntityId, string EntityName, DateTime ExpectedDate, DateTime PriorYearDate, decimal Amount)` ersetzt; `ReportAggregatePointDto.ProjectionExpectedDividends` → `ProjectionExpectedItems`. MCP-Ausgabe `expectedDividends`/`securityId`/`securityName` → `expectedItems`/`kind`/`entityId`/`entityName`. | Verallgemeinerung statt Parallel-DTOs: Eine property pro Sache; App, Web-UI und MCP werden gemeinsam ausgeliefert (interne API, kein externer Vertrag). Die Summenzeilen-Details mischen künftig Entitäten verschiedener Arten — `Kind` macht die Herkunft eindeutig. |
| UI-Gate | `CanCompareProjection` = `SelectedKinds.Count > 0 && Interval != AllHistory` (an beiden doppelt geführten Stellen: `ReportDashboardViewModel` Z. 223–229 und `ReportDashboard.razor` Z. 594–595). | Deckt sich mit dem Multi-Kind-Beschluss; `AllHistory` bleibt das einzige fachliche Ausschlusskriterium. |
| Overlay-Spaltenlabel | resx-Key `ProjectionDetails_Security` → `ProjectionDetails_Entity` (de „Entität", en „Entity"); `item.SecurityName` → `item.EntityName`. | Artneutrale Bezeichnung der Entitätsspalte im Detail-Overlay; resx-Pflicht de+en erfüllt. |
| `Type:`-Rollup-Verallgemeinerung | `Type:{K}`-Projektion = Summe der `ProjectionAmount` aller Nicht-`Type:`-Zeilen der Art `K`, deren `ParentGroupKey` `null` oder `Type:{K}` ist (Leafs ohne Kategorie + Kategoriezeilen direkt unter dem Typ); Leafs unter einer `Category:`-Zeile werden nur über deren Rollup gezählt. | Verhindert Doppelzählung bei `IncludeCategory` und schließt die heutige Lücke, dass Leafs mit `ParentGroupKey == "Type:…"` (im Multi-Kind-Modus, z. B. alle Bank-Leafs) nicht aggregiert würden — heute werden ausschließlich parent-lose Leafs summiert. |

## Programmabläufe

### Standardpfad `QueryAsync` mit Hochrechnung

1. Gates in `QueryAsync` (ab Z. 56): `projectionEnabled = query.CompareProjection && query.Interval != ReportInterval.AllHistory`; `securityProjectionEnabled = projectionEnabled && !hasContradictingSecuritySubTypes`. Routing-Condition **verhaltensgleich** umbauen: `onlySecurityKind && (includeDividendRelated || securityProjectionEnabled)` → `QuerySecurityDividendsNetAsync` (wichtig: das bisherige `compareProjection` enthielt `!hasContradictingSecuritySubTypes`; einfaches Entfernen des `onlySecurityKind`-Terms würde Security+Subtypfilter fälschlich in den Netto-Pfad leiten). Der zweite Routing-Block `onlySecurityKind && ytdMode && dividendSelected` → `QuerySecurityDividendsNetAsync` (YTD-Fallback, Z. 85–88) bleibt unverändert erhalten — der Gate-Umbau darf diesen Early-Return nicht entfernen oder verschieben.
2. Nach `analysis`-Normalisierung (Z. 92): Projektionsfenster analog dem Netto-Pfad bestimmen — `monthsBack = Take > 0 ? Take - 1 : 0`; `projectionWindowStart = Ytd ? new DateTime(analysis.Year - monthsBack, 1, 1) : analysis.AddMonths(-monthsBack)`; `endExclusive = analysis.AddMonths(1)`; `eventLoadStart = projectionWindowStart.AddYears(-2)` (2-Jahres-Referenzfenster).
3. `projectionEnabled` → `ReportProjectionEventLoader.LoadEventsAsync` je ausgewählter Art:
   - Owner-Entity-Ids laden (Accounts/Contacts/SavingsPlans/Securities je `OwnerUserId`, analog Textfilter-Block Z. 184–191).
   - **Bank:** `Posting`-Query `Kind == Bank`, `AccountId` ∈ owned ∩ `Filters.AccountIds` (falls gesetzt), `Amount != 0`, Effektivdatumsfenster `[eventLoadStart, endExclusive)`; bei gesetzten `BankTextFilters` Match auf `Subject`/`Description` je Buchung → `ProjectionEvent(Bank, AccountId, effective, month, Amount)`.
   - **Contact:** analog mit `ContactId`/`ContactTextFilters`.
   - **SavingsPlan (`PlanPostings` oder Plan ohne Links):** analog mit `SavingsPlanId`/`SavingsPlanTextFilters`. **`LinkedAccountPostings`:** `SavingsPlanAccountLinks` (owner-gefiltert) laden; für verknüpfte Pläne Events aus den Bank-Buchungen der verknüpften `AccountId`s (EntityId = PlanId, `SavingsPlanTextFilters` matchen auf die Quell-Bankbuchung — dokumentierte Textfilter-Semantik); unverknüpfte Pläne weiterhin eigene Plan-Buchungen.
   - **Security (nur `securityProjectionEnabled` und im Standardpfad faktisch nur Multi-Kind):** `LoadSecurityDividendEventsAsync` — der extrahierte `GroupId`-Netting-Code (Anker `SecuritySubType == Dividend`, `SecurityTextFilters` auf Anker-Posting, Netto = Dividend+Fee+Tax je Gruppe), gefiltert auf `allowedSecurities`/`allowedSecurityCats`.
   - `UseValutaDate` steuert das Effektivdatum aller Arten (`ValutaDate`-`MinValue`-Fallback), identisch zu `ApplyDateWindow`.
4. Enthält die Auswahl Security und `securityProjectionEnabled`: Holding-Transaktionen laden (extrahierte Logik aus Z. 1124–1166) und `HasPositiveHolding` als Delegat nur für `PostingKind.Security` an den Calculator übergeben.
5. Punkt-Aufbau unverändert (Aggregate-/Textfilter-/Linked-Derivation/Gruppen/Ytd-AllHistory-Transform).
6. Nach der Latest-Period-Injektion (Z. 691–706), vor den Vergleichsspalten: `ReportProjectionCalculator.ApplyProjectionAmounts(points, events, context, nameResolver, holdingCheck)` — deckt auch die injizierten Null-Punkte ab (im Netto-Pfad wird aus demselben Grund zweimal aufgerufen).
7. Leer-Latest-Bereinigung (Z. 754–772): `hasProjectionData`-Term ergänzen (`compareProjection && ProjectionAmount.HasValue && != 0`), wie im Netto-Pfad Z. 1283 — sonst würden nur-projizierte aktuelle Zeilen verworfen.
8. Rückgabe: `ComparedProjection = projectionEnabled && aktiveProjektionsArten.Any()` — aktive Arten = ausgewählte Arten minus Security bei `!securityProjectionEnabled`. Ergebnis: reine Security-Auswahl mit widersprechendem Subtypfilter → `false` (Verhalten wie heute); Bank+Security mit widersprechendem Filter → `true` (Bank projiziert).

### Netto-Dividendenpfad `QuerySecurityDividendsNetAsync` (Verhalten unverändert)

1. `dividendEvents` werden über `ReportProjectionEventLoader.LoadSecurityDividendEventsAsync` geladen (extrahierter, identischer Code) statt inline gebaut — `monthly`-Aggregation, Punkteaufbau und `TransformToInterval` bleiben.
2. `HasPositiveHolding` bleibt lokale Closure über `holdingTransactionsBySecurity`.
3. Beide `ApplyProjectionAmounts`-Aufrufe (Z. 1203, 1225) rufen `ReportProjectionCalculator` mit `Kind = Security`-Events, `securityNames`-Auflösung und dem Holding-Delegat auf.
4. Die lokale Funktion `ApplyProjectionAmounts` (Z. 1304–1622) und die privaten Typen `DividendEvent`, `ProjectionCandidate`, `DividendPattern` entfallen; `HoldingTransaction` bleibt privat.

### `ReportProjectionCalculator.ApplyProjectionAmounts` (verallgemeinerte Semantik)

1. `MapProjectionPeriod`/`GetProjectionPeriodEnd`/`projectionStart`/`projectionEnd`/`currentYearStart` — unverändert aus `ReportProjectionContext` (Interval, Analysis, StartMonth, EndExclusive).
2. `expectedDetails`: Gruppierung der Events nach `(Kind, EntityId)` statt `SecurityId`; `currentEvents` = Events des Analysejahres bis `endExclusive`; `DetectPattern` auf Referenzjahr `analysis.Year - 1`; Kandidaten = Vorjahres-Events im Fenster `[projectionStart-1J, projectionEnd-1J)` +1 Jahr; `MapProjectionPeriod != MinValue`; für `Kind == Security` zusätzlich `hasActiveEntity`-Delegat (`HasPositiveHolding`).
3. `FindExpectedItems` (umbenanntes `FindExpectedDividends`): Monthly/Quarterly/Annual/Irregular-Semantik unverändert.
4. Zuordnung zu `ReportProjectionExpectedItemDto` mit `nameResolver(kind, entityId)` (Konto-/Kontakt-/Plan-/Wertpapiername bzw. Id-Fallback `ToString("N")[..6]`).
5. Leaf-Zuordnung: `ReportQuerySemantics.TryParseGroupKey(point.GroupKey)` → `Entity`-Ziel liefert `(Kind, EntityId, SecuritySubType)`; Lookup `expectedByEntityPeriod[(kind, entityId, period)]`; `ProjectionAmount = Amount + expected`, `ProjectionExpectedItems = details`. Für `Kind == Security` gilt der callsite-spezifische Leaf-Filter (siehe Designentscheidung).
6. Category-Rollup unverändert generisch (Kind-Zeilen via `ParentGroupKey`); `Type:`-Rollup gemäß Designentscheidung (Summe über `ParentGroupKey ∈ {null, "Type:{K}"}` je Art, `ParseKindFromKey`).

### UI-Fluss Dashboard

1. Checkbox `Label_CompareProjection` ist aktiv, sobald mindestens eine Buchungsart gewählt ist und das Intervall nicht `AllHistory` ist — für Bank, Contact, SavingsPlan, Security und Mehrfachauswahl.
2. `ReloadAsync`/`ApplyFavorite`/`OnToggleKind`/`SubmitFavoriteDialogAsync` bleiben mechanismusgleich (Flag-Reset nur bei `!CanCompareProjection`, d. h. nur noch `AllHistory`); ein gespeicherter Favorit mit `CompareProjection` auf Bank-Auswahl lädt die Option nun aktiv.
3. Ergebnistabelle: `Th_Projection`-Spalte + `amt-projection`-Zellen wie bisher; Info-Button `ProjectionDetails_Open` öffnet Overlay mit `ProjectionExpectedItems`, Entitätsspalte `ProjectionDetails_Entity` (`EntityName`).

### MCP-Fluss

`fm_reporting_aggregate` mit `compareProjection=true` liefert für jede unterstützte Auswahl `comparedProjection` und `points[].projectionAmount`/`points[].expectedItems[]` (`kind`, `entityId`, `untrusted_data.entityName`, `expectedDate`, `priorYearDate`, `amount`); `fm_reports_configuration_manage` persistiert `compareProjection` weiterhin artunabhängig.

Beteiligte Klassen/Komponenten: `ReportAggregationService`, `ReportProjectionEventLoader` (neu), `ReportProjectionCalculator` (neu), `ReportQuerySemantics`, `ReportAggregatePointDto`, `ReportProjectionExpectedItemDto` (neu), `ReportDashboardViewModel`, `ReportDashboard.razor`, `FmReportingAggregateTool`, `FmReportsConfigurationManageTool`, resx de/en.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ReportProjectionCalculator` | `internal static` Klasse (`FinanceManager.Infrastructure/Reports/ReportProjectionCalculator.cs`) | Artübergreifende Hochrechnung: `ApplyProjectionAmounts`, `DetectPattern`, `FindExpectedItems`, `FindIrregularExpected`, `HasRegularMonthlySpacing`, `HasRegularSpacing`, `HasCurrentMatch`, `GetQuarter`, `MapProjectionPeriod`, `GetProjectionPeriodEnd` — übernommen aus der heutigen lokalen Funktion (Z. 1304–1622). |
| `ReportProjectionContext` | `internal` Record (in `ReportProjectionCalculator.cs`) | Parameterbündel: `ReportInterval Interval`, `DateTime Analysis`, `DateTime StartMonth`, `DateTime EndExclusive` (ersetzt die Closure-Variablen der bisherigen lokalen Funktion). |
| `ProjectionEvent` | `internal` Record | `(PostingKind Kind, Guid EntityId, DateTime Date, DateTime Month, decimal Amount)` — ersetzt `DividendEvent`. |
| `ProjectionCandidate` | `internal` Record | `(PostingKind Kind, Guid EntityId, DateTime ExpectedDate, DateTime PriorYearDate, decimal Amount)` — verallgemeinerter Ersatz des privaten Records. |
| `ProjectionPattern` | `internal` Enum | `Monthly, Quarterly, Annual, Irregular` — hebt das private `DividendPattern` an; Coverage in `EnumCoverageTests` (Reflection-Muster wie `ReportGroupKeyTargetKind_Values`). |
| `ReportProjectionEventLoader` | `internal` Klasse (`FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`, `AppDbContext` im Konstruktor) | `LoadEventsAsync(query, kinds, entityIds/Filterkontext, windowStart, windowEnd, ct)` je Art; `LoadSecurityDividendEventsAsync(securityIds, window, textPatterns, useValutaDate, ct)`; `LoadSecurityHoldingTransactionsAsync(securityIds, endInclusive, useValutaDate, ct)`. |
| `ReportProjectionExpectedItemDto` | `public` Record (`FinanceManager.Shared/Dtos/Reports/`) | `(PostingKind Kind, Guid EntityId, string EntityName, DateTime ExpectedDate, DateTime PriorYearDate, decimal Amount)` — ersetzt `ReportProjectionExpectedDividendDto`. |

## Änderungen an bestehenden Klassen

### `ReportAggregationService` (Klasse)

- **Geänderte Methoden:**
  - `QueryAsync` — Gate Z. 60 auf `projectionEnabled`/`securityProjectionEnabled` aufgespalten (Routing verhaltensgleich); Projektionsfenster-Variablen (`projectionWindowStart`, `endExclusive`, `eventLoadStart`); bei `projectionEnabled` Event-Loader- und ggf. Holding-Load-Aufruf; `ReportProjectionCalculator.ApplyProjectionAmounts`-Aufruf nach Latest-Period-Injektion (vor Vergleichsspalten Z. 709); `hasProjectionData` in Leer-Latest-Bereinigung (Z. 761–764); Rückgabe `ComparedProjection` statt hartkodiertem `false` (Z. 781).
  - `QuerySecurityDividendsNetAsync` — `dividendEvents` aus `LoadSecurityDividendEventsAsync` des Loaders; beide `ApplyProjectionAmounts`-Aufrufe (Z. 1203, 1225) auf den Calculator umgeleitet; Holding-Load ggf. an Loader delegiert; lokale Funktion `ApplyProjectionAmounts` (Z. 1304–1622) entfernt.
- **Entfernte Typen:** private `DividendEvent`, `ProjectionCandidate`, `DividendPattern` (Z. 1625–1637) → durch `internal`-Typen ersetzt; `HoldingTransaction` bleibt privat.

### `ReportAggregatePointDto` (Datenmodellklasse)

- **Geänderte Eigenschaften:** `ProjectionExpectedDividends` → `ProjectionExpectedItems` vom Typ `IReadOnlyList<ReportProjectionExpectedItemDto>?` (XML-Doku artneutral formulieren); `ProjectionAmount`-XML-Doku „for dividend reports" → artneutral.

### `ReportProjectionExpectedDividendDto` (Datenmodellklasse)

- Wird entfernt und durch `ReportProjectionExpectedItemDto` ersetzt (alle Referenzen: `ApplyProjectionAmounts`-Umzug, `ReportDashboard.razor`, `FmReportingAggregateTool`, Tests).

### `ReportQuerySemantics` (Klasse)

- Keine neue Methode erforderlich: `TryParseGroupKey` liefert bereits `Entity`-Ziel mit `PostingKind`, `EntityId` und `SecuritySubType`; `ParseKindFromKey` für das `Type:`-Rollup. `TryParseSecurityKey` bleibt für andere Verwendungen bestehen (Verwendung im Calculator entfällt zugunsten `TryParseGroupKey`).

### `ReportDashboardViewModel` (ViewModel)

- **Geänderte Eigenschaften:** `CanCompareProjection` (Z. 223–229) → `SelectedKinds.Count > 0 && (ReportInterval)Interval != ReportInterval.AllHistory`; `IsSecurityOnlySelection` (Z. 217) entfernen, sofern keine weitere Verwendung.
- **Geänderte Methoden:** keine weiteren — `ReloadAsync`, `ShowProjectionColumn`, `GetTotals`, `LoadAsync`, Favoritenpfade arbeiten artneutral weiter.

### `ReportDashboard.razor` (Seite)

- **Geänderte Member:** `CanCompareProjection` (Z. 595) → analog ViewModel; `IsSecurityOnlySelection` (Z. 594) entfernen; `GetTotalProjectionDetails`/`OpenProjectionDetails`/`CloseProjectionDetails`/`_projectionDetails` auf `ReportProjectionExpectedItemDto`/`ProjectionExpectedItems` umstellen; `item.SecurityName` → `item.EntityName` (Z. 153); Sortierung `ThenBy(EntityName)` (Z. 611, 622).
- **Markup:** `ProjectionDetails_Security` (Z. 143) → `ProjectionDetails_Entity`; `top.ProjectionExpectedDividends` (Z. 305, 307) → `ProjectionExpectedItems` (analog die weiteren Zeilenvarianten Z. 352–358, 394–400, 424–430).

### `FmReportingAggregateTool` / `FmReportingAggregateHandler` (MCP-Tool)

- Output `expectedDividends` (Z. 195–202) → `expectedItems` mit `kind` (`d.Kind.ToString()`), `entityId`, `expectedDate`, `priorYearDate`, `amount`, `untrusted_data.entityName`; Tool-`Description` (Z. 32) und `compareProjection`-Schema-Beschreibung artneutral formulieren („projection … for all posting kinds"); `McpAuditLinks` bleibt unverändert (mappt Argument-Feldnamen, nicht Ausgabefelder).

### `FmReportsConfigurationManageTool` (MCP-Tool)

- Keine funktionale Änderung; `compareProjection`-Schema-Beschreibung ggf. artneutral ergänzen.

### resx-Ressourcen (`Resources/Components/Pages/ReportDashboard.de.resx` + `.en.resx`)

- `ProjectionDetails_Security` entfernen, `ProjectionDetails_Entity` hinzufügen (de „Entität" / en „Entity"); übrige `ProjectionDetails_*`-Texte sind bereits neutral.

### DTO-XML-Dokumentation (Shared)

- `CompareProjection`-Param-Dokus „dividend projection" → artneutral: `ReportAggregationQuery`, `ReportPostingsQuery`, `ReportPostingsQueryRequest`, `ReportAggregatesQueryRequest`, `ReportFavoriteCreateRequest`, `ReportFavoriteUpdateRequest`, `ReportFavoriteDtos`.

### Dokumentation (`Docs/help/berichtswesen/`)

- `business-rules.md` — Regel „Dividendenhochrechnung nur für Wertpapierberichte" (Z. 30–47) zu einer artübergreifenden Regel umschreiben: Eventbegriff je Art, Dividenden-Netting/Bestandsprüfung als Security-Sonderfall, `AllHistory`-Ausschluss, Multi-Kind-Verhalten.
- `beschreibung.md` — Z. 13–15, 25, 34 (Security-Beschränkung, Beispiel, Verfügbarkeit) aktualisieren.
- `api.md` — Z. 13 (`CompareProjection`-Beschreibung) aktualisieren; `expectedItems`-Feld dokumentieren.
- `datenmodell.md` — Z. 18, 29–33 (Begriff „Dividendenhochrechnung" → „Hochrechnung"), neues DTO-Feld `ProjectionExpectedItems`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es kommen keine neuen Eingabefelder hinzu; `CompareProjection` bleibt ein boolsches Query-/Favoritenflag ohne neue Validierung (die AllHistory-Ausblendung ist eine Server-Gate-, keine Validierungsregel).

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **API-/Serialisierungsbruch (intern):** `ReportAggregatePointDto.ProjectionExpectedDividends` → `ProjectionExpectedItems` und `ReportProjectionExpectedDividendDto` → `ReportProjectionExpectedItemDto` ändern das JSON der Aggregat-API sowie die MCP-Ausgabe (`expectedDividends` → `expectedItems`, `securityId`/`securityName` → `entityId`/`entityName`/`kind`). Alle Konsumenten im Repo (razor, ViewModel, `ApiClient`, MCP, Tests) werden mit umgestellt; externe MCP-Nutzer sehen geänderte Feldnamen.
- **Mehr DB-Reads im Standardpfad:** Bei aktivierter Hochrechnung werden zusätzlich zu den `PostingAggregates` Rohpostings je Art im 2-Jahres-Fenster geladen (plus Security-Holdings). Gleiche Größenordnung wie der heutige Textfilter-Pfad; bei deaktivierter Option entsteht kein Mehraufwand.
- **Multi-Kind-Aktivierung:** Bisher ignorierte der Server `CompareProjection` bei Multi-Auswahl stillschweigend (`ComparedProjection=false`); künftig projiziert er je Art — gewollte Verhaltensänderung, der Test `QueryAsync_ProjectionIsIgnored_ForMultiKindAndInvalidSecuritySubtype` (Multi-Teil) kodiert das Altverhalten und wird fachlich angepasst.
- **`ApplyFavorite`/`ReloadAsync`-Verhalten:** Gespeicherte Bank-/Contact-/Sparplan-Favoriten mit `CompareProjection=true` aktivieren die Option beim Laden künftig wirksam statt sie zurückzusetzen — gewollte Wirkungsänderung.
- **Multi-Kind `Type:`-Rollup-Lücke:** Die verallgemeinerte Type-Aggregation muss die Design-Regel (Leafs mit `ParentGroupKey == "Type:{K}"` einbeziehen, Kategorie-Kinder nicht doppelt zählen) exakt umsetzen, sonst entstehen bei `IncludeCategory` falsche oder fehlende Summen.
- **Subtyp-gesplittete Security-Leafs im Standardpfad:** Ohne den Dividenden-Suffix-Filter würden Erwartungen auf Buy/Sell/Fee/Tax-/„ohne Subtyp"-Zeilen landen.
- **`ReportPostingsQueryService`:** Kein Umbau nötig — `netPath` bleibt `onlySecurity`-gebunden; `CompareProjection` beeinflusst dort nur das Lade-Fenster des Netto-Dividenden-Drill-Downs, die Hochrechnungsspalte hat weiterhin keinen Drill-Down (Scope-Ausschluss 3).
- **`HomeKpiGrid`:** unverändert — reicht `CompareProjection` durch, rendert nur `Amount` (KPI zeigt weiterhin keine Hochrechnung).

## Umsetzungsreihenfolge

1. **DTO-Verallgemeinerung (`FinanceManager.Shared`)**
   - Voraussetzungen: Keine.
   - `ReportProjectionExpectedItemDto` anlegen; `ReportAggregatePointDto.ProjectionExpectedItems` umstellen; `ReportProjectionExpectedDividendDto` entfernen; XML-Dokus artneutral. Build-Fehler zeigen alle Konsumenten (razor, MCP, Tests).

2. **`ReportProjectionCalculator` + interne Typen extrahieren**
   - Voraussetzungen: Schritt 1 (DTO); `ReportQuerySemantics` (TryParseGroupKey/ParseKindFromKey) — im Repo vorhanden.
   - Neue Datei `FinanceManager.Infrastructure/Reports/ReportProjectionCalculator.cs`: `ProjectionEvent`, `ProjectionCandidate`, `ProjectionPattern`, `ReportProjectionContext` und die verallgemeinerte `ApplyProjectionAmounts` inkl. aller Hilfsfunktionen — übernommen aus `QuerySecurityDividendsNetAsync` Z. 1304–1622, parametrisiert (`Kind`/`EntityId`, `nameResolver`, `hasActiveEntity`, Security-Leaf-Subtypfilter, generisches Type-Rollup).

3. **`ReportProjectionEventLoader` anlegen**
   - Voraussetzungen: Schritt 2 (liefert `ProjectionEvent`); vorhandene Infrastruktur `ApplyDateWindow`, `BudgetRulePatternMatcher`, `TextFilterList`, `PostingAggregateService.GetPeriodStart`.
   - Neue Datei `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`: je-Art-Eventladung inkl. `LinkedAccountPostings`-Ableitung und extrahiertem Security-Netting (`LoadSecurityDividendEventsAsync`) sowie `LoadSecurityHoldingTransactionsAsync`.

4. **`QuerySecurityDividendsNetAsync` auf die neuen Komponenten umstellen**
   - Voraussetzungen: Schritte 2–3.
   - `dividendEvents` aus dem Loader; `ApplyProjectionAmounts`-Aufrufe → Calculator; lokale Funktion und private Typen entfernen. Reine Refaktorierung — alle 25 `ReportAggregationProjectionTests` müssen unverändert grün bleiben.

5. **`QueryAsync`-Standardpfad erweitern**
   - Voraussetzungen: Schritte 2–4.
   - Gate-Umbau, Projektionsfenster, Loader-/Holding-Aufrufe, Calculator-Aufruf nach Latest-Period-Injektion, `hasProjectionData` in der Bereinigung, `ComparedProjection`-Rückgabe.

6. **UI umbauen (ViewModel + Page + resx)**
   - Voraussetzungen: Schritt 1 (DTO) — funktional unabhängig von 2–5.
   - `CanCompareProjection` beidseitig, `IsSecurityOnlySelection` entfernen, Overlay auf `ProjectionExpectedItems`/`EntityName`, resx `ProjectionDetails_Entity` de+en.

7. **MCP-Tool + Doku + XML-Dokus**
   - Voraussetzungen: Schritt 1.
   - `FmReportingAggregateTool` Output/Description/Schema; `FmReportsConfigurationManageTool` Beschreibung; `Docs/help/berichtswesen/*` (business-rules, beschreibung, api, datenmodell); Shared-XML-Dokus.

8. **Tests anpassen und neue Tests schreiben (Unit/Integration)**
   - Voraussetzungen: Schritte 1–6.
   - Betroffene Tests umbauen (siehe Tests-Abschnitt), neue artübergreifende Fälle, `EnumCoverageTests`-Eintrag für `ProjectionPattern`.

9. **E2E-Tests**
   - Voraussetzungen: Schritte 1–6; `PlaywrightWebAppFixture`, `TestUserSeeder`, `AuthGateway`, `ReportDashboardGateway`, `BrowserApiHelper`, `PostingAggregateService`-Seed-Pattern aus `ReportPostingsOverlayPlaywrightTests`.
   - Neue Datei `FinanceManager.Tests.E2E/Tests/Reports/ReportProjectionPlaywrightTests.cs` (Szenarien siehe unten).

10. **Alle Suiten laufen lassen + reale Abnahme**
    - Voraussetzungen: Schritte 1–9.
    - `dotnet build`, `dotnet test` aller drei Suiten (CI-Filter `--filter-not-trait "Category=OsInterface"` für Unit); manuelle UI-Abnahme: Bank-Bericht mit Hochrechnung öffnen, Spalte + Detail-Overlay sichten.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `QueryAsync_BankProjection_AddsUnconfirmedPriorYearPosting` | `ReportAggregationProjectionTests` (oder neue `ReportAggregationCrossKindProjectionTests.cs`) | Happy Path Bank: jährliche Vorjahresbuchung ohne aktuelle Buchung → `ComparedProjection == true`, `ProjectionAmount == Amount + erwarteter Betrag`, Detail mit `Kind = Bank` und `EntityName` = Kontoname. |
| `QueryAsync_BankProjection_TreatsCurrentPostingAsConfirmed` | dto. | Bestätigte Erwartung: aktuelle Buchung vorhanden → `ProjectionAmount == Amount`, keine Details. |
| `QueryAsync_BankProjection_NoPriorYearEvents_ProducesNoExpectation` | dto. | **Negativtest Verwerfen-Regel (Scope-Klärung):** Konto nur mit aktueller Buchung, Referenzjahr ohne Events → kein Muster, keine Erwartung: `ProjectionExpectedItems == null` und `ProjectionAmount == Amount` (die Leaf-Zuordnung setzt `Amount + 0` — kein erwarteter Anteil). |
| `QueryAsync_BankProjection_ComparePrevious_KeepsProjectionOnlyLatestRow` | dto. | **`hasProjectionData`-Abdeckung:** `ComparePrevious = true` + `CompareProjection`: Vorjahresbuchung (z. B. Mai 2025) ohne aktuelle Buchung → die injizierte Latest-Zeile (Mai 2026, `Amount = 0`, kein `PreviousAmount`, da April 2026 leer) darf von der Leer-Latest-Bereinigung (Z. 754–772) **nicht** verworfen werden: Zeile bleibt mit `ProjectionAmount` = erwartetem Betrag erhalten. Ohne den `hasProjectionData`-Term würde die gesamte Gruppe entfernt → fail-before/pass-after. |
| `QueryAsync_BankProjection_MonthlyPatternExpectsOpenMonth` | dto. | Monatsmuster-Übernahme: ≥6 regelmäßige Vorjahresmonate → Erwartung für den laufenden Monat ohne Buchung; vergangene verpasste Monate ohne Erwartung. |
| `QueryAsync_ContactProjection_QuarterlyPatternExpectsOpenQuarter` | dto. | Quartalsmuster für Contact inkl. Quarter-Intervall-Zuordnung. |
| `QueryAsync_SavingsPlanProjection_AnnualPatternExpectsPosting` | dto. | Jahresmuster für Sparplan; unverknüpfter Plan nutzt eigene Buchungen. |
| `QueryAsync_SavingsPlanProjection_LinkedAccountSource_UsesLinkedBankPostings` | dto. | `LinkedAccountPostings`: Erwartungen des verknüpften Plans stammen aus den Bank-Buchungen der verknüpften Konten (EntityId = PlanId). |
| `QueryAsync_MultiKindProjection_ProjectsEachKind` | dto. | Bank+Contact-Auswahl: beide Leafs projiziert, `Type:Bank`-/`Type:Contact`-Rollup korrekt, `ComparedProjection == true`. |
| `QueryAsync_MultiKindProjection_TypeRollupCountsCategoryChildrenOnce` | dto. | **Doppelzählungs-Test (Designentscheidung `Type:`-Rollup):** Multi-Kind (Bank + Contact) mit `IncludeCategory = true`, kategorisierter Kontakt mit Vorjahres-Erwartung → `Type:Contact`-`ProjectionAmount` enthält die Erwartung genau einmal (nur über die `Category:Contact:{id}`-Zeile, die `ParentGroupKey = Type:Contact` trägt; das Contact-Leaf mit `ParentGroupKey = Category:…` wird nicht zusätzlich gezählt) und `Type:Bank` enthält die Bank-Erwartung aus dem Leaf mit `ParentGroupKey = Type:Bank`. Ohne die `ParentGroupKey ∈ {null, "Type:{K}"}`-Regel wäre die Summe doppelt bzw. leer → fail-before/pass-after. |
| `QueryAsync_MultiKindProjection_SecurityExpectationsOnlyOnDividendRows` | dto. | Security in Multi-Auswahl: Erwartungen nur auf `Security:{id}:Dividend`-Zeilen; Buy-/Sell-Zeilen ohne `ProjectionAmount`-Erwartungsanteil. |
| `QueryAsync_MultiKindProjection_FullySoldSecurity_ProducesNoExpectation` | dto. | **`HasPositiveHolding`-Verdrahtung im Standardpfad:** Multi-Kind (Security + Bank), Wertpapier A vollständig verkauft (Bestand am Erwartungsdatum 0), Wertpapier B weiter gehalten, beide mit Vorjahres-Dividende → auf `Security:{A}:Dividend` keine Erwartung (`ProjectionExpectedItems == null`, `ProjectionAmount` ohne Erwartungsanteil), auf `Security:{B}:Dividend` Erwartung vorhanden. Belegt, dass das Holding-Delegat im Standardpfad geladen und nur für `Kind == Security` übergeben wird — ohne Verdrahtung würde A fälschlich projiziert → fail-before/pass-after. |
| `QueryAsync_Projection_ContradictingSecuritySubTypes_SuppressesOnlySecurity` | dto. | `SecuritySubTypes` ohne Dividend: Security-Erwartungen entfallen, Bank/Contact im selben Request werden weiterhin projiziert; reine Security-Auswahl → `ComparedProjection == false` (Altverhalten). |
| `QueryAsync_Projection_IsIgnored_ForAllHistory` (ggf. bestehend erweitern) | dto. | `AllHistory` bleibt wirkungslos für alle Arten. |
| `QueryAsync_Projection_AggregatesCategoryRows_ForContactAndSavingsPlan` | dto. | `IncludeCategory`: `Category:{kind}:{id}`-Zeilen summieren Kind-`ProjectionAmount` inkl. Details. |
| `QueryAsync_BankProjection_RespectsBankTextFilters` | dto. | `BankTextFilters` beschränken die erwarteten Events auf matchnde Buchungen (gefilterter Bericht = gefilterte Erwartungen). |
| `QueryAsync_BankProjection_RespectsAccountIdsFilter` | dto. | Nur Entitäten aus `Filters.AccountIds` erzeugen Erwartungen. |
| `QueryAsync_BankProjection_IgnoresOtherUsersPostings` | dto. | Besitzerisolation: Buchungen fremder User erzeugen keine Events/Erwartungen (Service-Level-Negativtest). |
| `QueryAsync_BankProjection_UsesValutaDateAndBookingFallback` | dto. | `UseValutaDate` steuert Eventdatum inkl. `MinValue`-Fallback auch für Nicht-Wertpapier-Arten. |
| `QueryAsync_Projection_YtdAndYearIntervals_ForBank` | dto. | Ytd-Abgrenzung (`MapProjectionPeriod` MinValue-Verwerfen) und Year-Intervall-Fenster für Bank — Übernahme der Intervallsemantik. |
| `ProjectionPattern_Values` | `EnumCoverageTests` | Vollständige Memberliste des neuen `internal enum ProjectionPattern` (Reflection-Muster wie `ReportGroupKeyTargetKind_Values`). |
| `CanCompareProjection_True_ForBankContactSavingsPlanAndMulti` / `False_ForAllHistory` | `ReportDashboardViewModelTests` | UI-Gate: aktiviert für jede Einzelart und Multi-Auswahl, deaktiviert bei `AllHistory`. |
| `AddAccount`/`AddContact`/`AddSavingsPlan`/`AddBankPosting`/`AddSavingsPlanAccountLink`-Helfer | `ReportAggregationProjectionTests` bzw. neue Testdatei | Seed-Helfer analog `AddDividendGroup`/`AddTrade` für die neuen Arten (Muster aus `SeedAllKindsAsync` in `ReportAggregationServiceTests` Z. 601). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `QueryAsync_ProjectionIsIgnored_ForNonSecurityKind` (`ReportAggregationProjectionTests`) | Kodiert die Security-Beschränkung: Bank wird jetzt projiziert — Test umschreiben in einen Bank-Projektionsfall (siehe neue Tests). |
| `QueryAsync_ProjectionIsIgnored_ForMultiKindAndInvalidSecuritySubtype` | Multi-Kind-Teil kodiert Altverhalten (Multi wird projiziert); Subtyp-Teil bleibt gültig nur für die Security-Erwartungen — Test in zwei Fälle aufspalten/anpassen. |
| `ReloadAsync_DisablesProjection_ForNonSecuritySelection` (`ReportDashboardViewModelTests` Z. 215) | Bank-Auswahl ist jetzt projektionsfähig → Test invertieren/umbenennen (Flag bleibt erhalten). |
| `ReloadAsync_DisablesProjection_ForMultiKindSelection` (Z. 243) | Multi-Auswahl projektionsfähig → analog anpassen. |
| `ReloadAsync_DisablesProjection_ForAllHistory` (Z. 270) | Bleibt fachlich gültig; ggf. Assertions an neues Gate anpassen. |
| Alle `ReportAggregationProjectionTests` mit `ProjectionExpectedDividends`/`ReportProjectionExpectedDividendDto`/`SecurityName`-Assertions | DTO-Umbenennung → `ProjectionExpectedItems`/`EntityName`; Fachlogik unverändert, Tests müssen nach Schritt 1 kompilieren. |
| `ProjectionColumn_IsVisibleAndTotalsProjection_WhenServerComparedProjection` (`ReportDashboardViewModelTests`) | DTO-Umbenennung in den Testdaten. |
| `FmReportingAggregateTool`-Tests (Output-Shape `expectedDividends`/`securityId`) | Neues Output-Feld `expectedItems` — Assertions anpassen. |

### E2E-Tests (primärer Funktionsnachweis)

Neue Datei `FinanceManager.Tests.E2E/Tests/Reports/ReportProjectionPlaywrightTests.cs` im `[Collection(PlaywrightCollection.CollectionName)]`-Muster (Fixture, `TestUserSeeder`, `AuthGateway`, direktes DB-Seeding von `Posting`s + `PostingAggregateService.RebuildForUserAsync`, Favoriten via `BrowserApiHelper` — Vorlage: `ReportPostingsOverlayPlaywrightTests`).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Bank-Bericht mit Hochrechnung (Happy Path): Vorjahres-Jahresbuchung ohne aktuelle Buchung seeden → Favorit `CompareProjection=true` → Dashboard öffnen → `th-projection`-Spalte sichtbar, Entity-Zeile zeigt hochgerechneten Wert, Info-Button öffnet `projection-detail-overlay` mit Kontoname + Erwartungsdatum | `ReportProjectionPlaywrightTests.ProjectionColumn_BankFavorite_ShowsExpectedAmountAndOverlay` | Vollständiger Nutzerfluss vom Favoriten/der Checkbox bis zur sichtbaren Spalte + Detail-Overlay | Nachweis der echten Server→API→Blazor-Kette inkl. `ComparedProjection`-Durchreichung und Overlay-Rendering — durch Unit-/Integrationstests nicht abgedeckt. |
| Pflicht | Filter-Dialog: `Hochrechnung`-Checkbox ist für Bank-Auswahl aktivierbar (vorher `disabled`), nach Aktivieren+Anwenden erscheint die Spalte | `ReportProjectionPlaywrightTests.ProjectionCheckbox_EnabledForBankKind_TogglesColumn` | Auslöser-Interaktion (Checkbox-Gate) im echten UI-Fluss | Das UI-Gate `CanCompareProjection` ist das zentrale freizugebende Element — nur UI-Test prüft `disabled`-Binding und Spalten-Einblendung. |
| Pflicht | `AllHistory`: Checkbox bleibt deaktiviert, Bericht ohne Hochrechnungsspalte | `ReportProjectionPlaywrightTests.ProjectionCheckbox_DisabledForAllHistory` | Negative Regel „AllHistory wirkungslos" am sichtbaren Bedienelement | Sichtbare Negativ-Bedingung direkt am Nutzerfluss. |
| Empfohlen | Multi-Kind-Favorit (Bank+Contact) mit `CompareProjection` → Spalte sichtbar, `Type:`-Zeilen tragen Werte | `ReportProjectionPlaywrightTests.ProjectionColumn_MultiKind_ShowsPerKindValues` | Mehrfachauswahl-Verhalten inkl. Typ-Rollup | Nachweis des gemischten Flusses (stärkster neuer Pfad). |
| Empfohlen | Security-Regression: Wertpapier-Favorit mit Hochrechnung zeigt weiterhin Spalte + Overlay (Entitätsspalte zeigt „Entität"-Label) | `ReportProjectionPlaywrightTests.ProjectionColumn_SecurityFavorite_StillWorks` | Keine Regression des Bestandsflusses inkl. verallgemeinertem Overlay | Der Umbau teilt den Codepfad — sichtbarer Bestandsfall muss weiterlaufen. |

Bestehende E2E-Tests, die angepasst werden müssen: keine — die vorhandenen Report-E2E-Tests aktivieren `CompareProjection` nicht.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Bank | Nutzer legt Bank-Favorit mit `CompareProjection` an bzw. aktiviert die Checkbox | Konto existiert; jährliche Vorjahresbuchung vorhanden, keine Buchung im laufenden Jahr; Aggregate per `PostingAggregateService` aktuell | Dashboard öffnen (`?favoriteId=…`) oder Checkbox aktivieren → `LoadAsync` → `QueryAsync` → Event-Load → Calculator | `Th_Projection`-Spalte erscheint; Konto-Zeile zeigt `ProjectionAmount` > `Amount`; Info-Button öffnet Overlay mit Kontoname, erwartetem Datum (Vorjahr+1J) und Betrag; Summenzeile aggregiert | E2E `ProjectionColumn_BankFavorite_ShowsExpectedAmountAndOverlay` + Unit `QueryAsync_BankProjection_AddsUnconfirmedPriorYearPosting` + manuelle Abnahme |
| 2 | Bestätigte Erwartung | wie 1 | wie 1, aber aktuelle Buchung im laufenden Jahr vorhanden | wie 1 | `ProjectionAmount == Amount`, kein Detail-Eintrag (Erwartung bestätigt) | Unit `QueryAsync_BankProjection_TreatsCurrentPostingAsConfirmed` |
| 3 | Multi-Kind | Favorit Bank+Contact mit `CompareProjection` | Konto und Kontakt je mit Vorjahres-Events | Dashboard öffnen | Beide Entitätszeilen und `Type:`-Zeilen tragen `ProjectionAmount`; Summenzeile aggregiert artübergreifend | E2E `ProjectionColumn_MultiKind_ShowsPerKindValues` + Unit `QueryAsync_MultiKindProjection_ProjectsEachKind` |
| 4 | AllHistory-Ausschluss | Intervall `AllHistory` wählen | beliebige Daten | Intervall wechseln | Checkbox deaktiviert; API-Aufruf liefert `ComparedProjection=false`, keine Spalte | E2E `ProjectionCheckbox_DisabledForAllHistory` + Unit-Test |
| 5 | Linked-Sparplan-Quelle | Favorit SavingsPlan + `LinkedAccountPostings` + `CompareProjection` | Verknüpfter Sparplan; Bank-Buchungen des Verknüpfungskontos im Vorjahr, keine aktuellen | Dashboard öffnen | Plan-Zeile zeigt Erwartung aus den Konto-Buchungen des Vorjahres | Unit `…LinkedAccountSource_UsesLinkedBankPostings` |
| 6 | Fremdbesitz (Sichtbarkeit) | API-Aufruf `report-aggregates` mit `CompareProjection` | Buchung eines anderen Users auf fremdem Konto im Vorjahr | Abfrage ausführen | Keine `ProjectionAmount`-Erwartung für fremde Daten; Erwartungen nur für eigene Entitäten | Unit `QueryAsync_BankProjection_IgnoresOtherUsersPostings` |
| 7 | Gespeicherter Favorit auf Bank | `ApplyFavorite`/`LoadAsync` eines Favoriten mit `CompareProjection=true`, `PostingKind=Bank` | Favorit wurde vor dem Feature gespeichert | Favorit laden | Option wird aktiv angewendet (früher still zurückgesetzt): Spalte + `ComparedProjection=true` | Unit/ViewModel-Test + reale Abnahme |
| 8 | Security-Regression | wie 1 mit Security-Dividenden-Favorit | Dividendengruppe Vorjahr, positiver Bestand | Dashboard öffnen | Unverändertes Verhalten: Netto-Dividenden-Hochrechnung mit Bestandsprüfung | Alle 25 bestehenden `ReportAggregationProjectionTests` unverändert grün + E2E `…SecurityFavorite_StillWorks` |

## Offene Punkte

Keine — alle in der `requirement.md` verzeichneten Fragen konnten per Codebase-Recherche oder Konsistenzschluss entschieden werden (siehe Scope-Klärung und Designentscheidungen). Die vier Scope-Ausschlüsse wurden am 2026-11-15 als `bestätigt` markiert: Sie schränken die naheliegende Nutzererwartung nicht ein, sondern setzen exakt um, was die Anforderung fordert — die bestehende Logik unverändert auf alle Postenarten zu übertragen —, und der Auftraggeber kündigt explizit eine fachliche Evaluation des Ergebnisses an.
