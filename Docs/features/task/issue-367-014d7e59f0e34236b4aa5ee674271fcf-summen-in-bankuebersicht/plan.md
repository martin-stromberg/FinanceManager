# Umsetzungsplan: Summen in der Bankübersicht

## Ziel

Die generische Kontenliste unter `/list/accounts` erhält eine ausschließlich für Bankkonten gerenderte Statistik-Infokachel. Sie zeigt den aktuellen Gesamtsaldo, die Veränderung im laufenden Kalenderjahr und -monat sowie je ein Tortendiagramm für Kontoart und Bankkontakt. Tabelle und Statistik verwenden denselben serverseitigen Owner- und Suchfilter; Paging begrenzt nur die sichtbaren Tabellenzeilen.

## Verbindliche fachliche und technische Entscheidungen

### Kontenmenge und Salden

- Berücksichtigt werden alle `Account`-Datensätze des authentifizierten Benutzers, die den gemeinsamen `q`-Suchfilter erfüllen. `skip` und `take` beeinflussen nur `GET /api/accounts`, niemals die Statistik.
- Der aktuelle Gesamtsaldo ist `Sum(Account.CurrentBalance)` über genau diese ungepagte Kontenmenge.
- Jahres- und Monatsveränderung sind Period-to-date-Buchungssummen über `Posting.Amount` für `PostingKind.Bank` und die gefilterten Konto-IDs. Als fachliche Zeitachse gilt ausschließlich `Posting.BookingDate`; `ValutaDate` wird nicht verwendet.
- Das Intervall beginnt einschließlich um 00:00 Uhr am 1. Januar beziehungsweise am ersten Tag des aktuellen Monats und endet ausschließlich um 00:00 Uhr des auf den aktuellen lokalen Kalendertag folgenden Tages. Buchungen exakt auf dem Periodenbeginn zählen, Buchungen davor und zukünftige Buchungen ab morgen nicht.
- Vorläufige Buchungen, Original- und Stornobuchungen werden nicht gesondert herausgefiltert. Damit entspricht die Periodenberechnung der bestehenden `CurrentBalance`-Neuberechnung: Alle persistierten Bank-Postings zählen mit ihrem aktuellen `Amount`; Stornopaare wirken mit ihren Vorzeichen.
- Falls bereits heute future-datierte Buchungen im `CurrentBalance` enthalten sind, bleiben sie im aktuellen Gesamtsaldo enthalten, aber bewusst außerhalb der Period-to-date-Veränderungen. Diese Abgrenzung wird in Tests festgeschrieben.

### Injizierbarer Zeitgeber und Zeitzone

- Die bestehende DI-Registrierung `TimeProvider.System` bleibt die Produktionszeitquelle. `AccountService` verwendet keinen direkten Zugriff auf `DateTime.Now`, `DateTime.Today` oder `DateTime.UtcNow`.
- Ein neuer, kleiner `IAccountStatisticsPeriodProvider` in `FinanceManager.Application/Accounts` liefert `Today`, `YearStart`, `MonthStart` und `TomorrowExclusive` als lokale, `DateTimeKind.Unspecified`-Grenzen. Die Implementierung erhält `TimeProvider` und einen injizierbaren `ITimeZoneResolver`.
- Die Benutzerzeitzone wird serverseitig anhand `ownerUserId` aus `User.TimeZoneId` gelesen; ein vom Client übermittelter Zeitzonenparameter ist nicht Teil des API-Vertrags. Damit bleibt die Benutzerkonfiguration die einzige Quelle der Anwendungszeitzone.
- `ITimeZoneResolver` löst die gespeicherte IANA-ID über `TimeZoneInfo` auf. Bei fehlender oder ungültiger ID wird deterministisch `TimeZoneInfo.Utc` verwendet und eine Warnung ohne personenbezogene Daten protokolliert.
- Die lokale Tagesgrenze entsteht aus `TimeProvider.GetUtcNow()` und der aufgelösten Benutzerzeitzone. Da `BookingDate` im Bestand ein fachliches Datum ohne belastbare UTC-Semantik ist, werden die daraus erzeugten lokalen Grenzen nicht zurück nach UTC konvertiert, sondern direkt mit `BookingDate` verglichen.
- Unit-Tests ersetzen sowohl `TimeProvider` als auch `ITimeZoneResolver`; Integrationstests verwenden die bereits vorhandene Eigenschaft `TestWebApplicationFactory.FixedUtcNow` und eine fest gespeicherte Benutzerzeitzone.

### Vollständiger `q`-Suchvertrag

- `GET /api/accounts` erhält den optionalen Query-Parameter `q`; `GET /api/accounts/statistics` verwendet denselben Parameter. Der API-Client erzeugt beide URLs mit einem strukturierten Query-Builder und URL-Encoding.
- `null`, leerer oder ausschließlich aus Whitespace bestehender Text bedeutet "kein Suchfilter". Andernfalls wird `q` genau einmal mit `Trim()` normalisiert. Eingaben über 200 Zeichen liefern `400 Bad Request` bei beiden Endpunkten.
- Ein Konto trifft, wenn sein `Name` den getrimmten Suchtext als case-insensitiven Teilstring enthält oder seine IBAN den normalisierten IBAN-Suchtext als Teilstring enthält. Beide Felder sind mit OR verknüpft; `OwnerUserId` und ein vorhandener `bankContactId`-Filter sind mit AND verknüpft.
- Die Namenssuche ist kulturunabhängig case-insensitiv und behält innere Leerzeichen bei. `%`, `_`, `*`, `?`, `[` und `]` sind normale Zeichen, keine Wildcards.
- Für die IBAN-Suche werden Groß-/Kleinschreibung sowie ASCII-Leerzeichen, geschützte Leerzeichen und Bindestriche ignoriert, sowohl im Suchtext als auch im gespeicherten Wert. Ist der so normalisierte IBAN-Teil leer, wird nur die Namensbedingung ausgewertet. Verknüpfte Unter-IBANs bleiben außerhalb des Vertrags, weil auch die bestehende Suche nur `Account.Iban` berücksichtigt.
- `AccountService` kapselt Owner-, `q`- und optionalen Bankkontaktfilter in genau einer Query-Komposition, die Listen- und Statistikabfrage verwenden. Es bleibt keine zusätzliche clientseitige Filterung in `BankAccountListViewModel` bestehen.
- `ClearSearch` setzt `Search` auf leer, setzt Paging zurück und lädt erste Seite und Statistik ohne `q` neu. Nachgeladene Seiten verwenden stets denselben normalisierten Suchwert wie die aktuell sichtbare Statistik.

### Verlustfreie Diagrammsemantik für positive, negative und Nullsalden

- Ein Kreisdiagramm kann keine negativen Winkel darstellen. Deshalb ist die geometrische Slice-Größe einer Gruppe ihr **absolutes Saldenvolumen**: `PositiveBalance + NegativeBalanceMagnitude`, wobei `PositiveBalance = Sum(max(CurrentBalance, 0))` und `NegativeBalanceMagnitude = Sum(abs(min(CurrentBalance, 0)))` über die Konten der Gruppe gilt.
- Der Prozentnenner ist die Summe dieser absoluten Saldenvolumina über alle Gruppen. Dadurch sind alle positiven und negativen Einzelkontosalden in der Geometrie enthalten; gemischte Vorzeichen werden nicht auf null gekappt und heben sich nicht unsichtbar auf.
- Jede Gruppe liefert zusätzlich `NetBalance = PositiveBalance - NegativeBalanceMagnitude`, `AccountCount` und `ZeroBalanceCount`. Die Summe aller `NetBalance`-Werte muss exakt dem KPI `TotalBalance` entsprechen. `TotalGrossMagnitude` muss der Summe aller Gruppenvolumina entsprechen.
- Die Legende zeigt pro Gruppe den lokalisierten Namen, den vorzeichenbehafteten Nettosaldo, den Anteil am absoluten Saldenvolumen sowie die positiven Beträge, den Betrag der negativen Salden und die Anzahl der Nullsalden. Damit gehen weder Vorzeichen noch Nullkonten verloren.
- Direkt am Diagrammtitel steht die lokalisierte Metrikbezeichnung "Anteil am absoluten Saldenvolumen"; das Zentrum zeigt den signierten Gesamtsaldo mit der Beschriftung "Nettosaldo". ARIA-Texte enthalten dieselbe Nenner- und Vorzeicheninformation.
- Eine Gruppe mit ausschließlich Nullsalden bleibt in der Legende mit `0,0 %`, Nettosaldo `0`, Volumen `0` und ihrer Kontenanzahl sichtbar, erzeugt aber keinen SVG-Kreisabschnitt. Sind alle Gruppen null, wird kein Ring gezeichnet und zusätzlich der lokalisierte Text "Kein Saldenvolumen" ausgegeben.
- Die API liefert technische Kontoartschlüssel und Bankkontakt-ID/-name. Kontoarten werden erst in der UI lokalisiert. Nicht auflösbare Kontakte werden serverseitig unter einem stabilen Null-Schlüssel gruppiert und in der UI als "Unbekannter Bankkontakt" lokalisiert.

### Lade-, Leer- und Fehlerzustände

- `BankAccountListViewModel` erhält für die Statistik einen eigenen Zustandsautomaten `NotStarted`, `Loading`, `Loaded`, `Empty`, `Error`, eigene Daten und eine eigene Fehlermeldung. Statistikfehler werden nicht über den allgemeinen Listenfehlerzustand signalisiert.
- Beim ersten Öffnen und bei jeder Suche beziehungsweise jedem `ClearSearch` werden erste Listenseite und Statistik mit demselben normalisierten `q` neu geladen. Eine Request-Generation beziehungsweise CancellationToken-Verwaltung verhindert, dass eine langsamere alte Suchantwort neuere Ergebnisse überschreibt. Infinite Scroll lädt nur weitere Tabellenzeilen und löst keinen Statistikaufruf aus.
- **Laden:** Die Kachel bleibt in stabiler Höhe sichtbar, trägt `aria-busy="true"` und zeigt beschriftete Skeleton-/Platzhalterbereiche für drei KPIs und zwei Diagramme. Alte Statistikwerte werden nach einer Suchänderung entfernt, damit sie nicht neben bereits neu gefilterten Tabellenzeilen stehen.
- **Leer:** Bei `AccountCount == 0` zeigt die Kachel Gesamtsaldo, Jahres- und Monatsveränderung jeweils als formatierten Nullbetrag. Beide Diagrammbereiche zeigen den lokalisierten Leertext, keine Segmente und keine erfundenen Prozentwerte. Die generische Liste zeigt weiterhin ihren bestehenden Leerzustand.
- **Erfolg mit ausschließlich Nullsalden:** Dies ist `Loaded`, nicht `Empty`, weil Konten existieren. Die Nullkonten erscheinen wie oben definiert in den Legenden; die Ringe zeigen "Kein Saldenvolumen".
- **Fehler:** Nur der Statistikbereich zeigt einen lokalisierten `role="alert"`-Text und eine Icon-Schaltfläche "Erneut laden". Die Tabelle bleibt sichtbar und Suche, Paging/Infinite Scroll, Zeilenklick sowie die Ribbon-Aktionen `Back`, `New` und `ClearSearch` bleiben bedienbar. Die Wiederholung lädt ausschließlich die Statistik mit dem aktuell aktiven `q`.
- Ein Fehler der Liste folgt weiterhin dem bestehenden Listenverhalten und wird nicht als Statistikfehler maskiert. Ein erfolgreicher Statistikaufruf darf einen Listenfehler ebenfalls nicht überschreiben.

## API- und Datenverträge

1. Unter `FinanceManager.Shared/Dtos/Accounts/` werden `AccountStatisticsDto` und `AccountBalanceGroupDto` ergänzt.
   - `AccountStatisticsDto`: `AccountCount`, `TotalBalance`, `YearToDateChange`, `MonthToDateChange`, `TotalGrossMagnitude`, `ByAccountType`, `ByBankContact`.
   - `AccountBalanceGroupDto`: technischer `Key`, optionaler `DisplayName`, `NetBalance`, `PositiveBalance`, `NegativeBalanceMagnitude`, `GrossMagnitude`, `AccountCount`, `ZeroBalanceCount`.
   - Geldwerte sind `decimal`; alle Sammlungen sind nicht null und im Leerfall leer.
2. `IAccountService.ListAsync` erhält `string? q` vor dem `CancellationToken`; `GetStatisticsAsync(Guid ownerUserId, string? q, CancellationToken ct)` wird ergänzt.
3. `IApiClient`/`ApiClient.Accounts.cs` erweitern `GetAccountsAsync` um `q` und ergänzen `GetAccountStatisticsAsync(string? q, CancellationToken ct)`.
4. `AccountsController` bietet den authentifizierten Endpunkt `GET /api/accounts/statistics?q=...`. Erfolg liefert `200`; ungültige Suchlänge `400`; fehlende Authentifizierung aufgrund des vorhandenen `[Authorize]` `401`. Eine leere Kontenmenge ist `200` mit Null-KPIs und leeren Gruppenlisten, nicht `204` oder `404`.
5. `GET /api/accounts?skip=...&take=...&bankContactId=...&q=...` behält Paging und Bankkontaktfilter bei und wendet den gemeinsamen Suchvertrag vor Sortierung und Paging an.

## Umsetzungsschritte

1. **Perioden- und Suchinfrastruktur**
   - `IAccountStatisticsPeriodProvider`, `AccountStatisticsPeriodProvider` und `ITimeZoneResolver` anlegen und in `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` registrieren.
   - Die gemeinsame Account-Query-Komposition mit Owner-, `q`- und Bankkontaktfilter in `AccountService` einführen; Suchnormalisierung in einem reinen, separat testbaren Wertobjekt/Helper zentralisieren.
   - Keine neue Datenbankmigration ist erforderlich.

2. **Serverseitige Statistikaggregation**
   - Die gefilterten Konten ungepaginiert und nur mit den benötigten Feldern einschließlich Kontaktname projizieren.
   - Gesamtsaldo und beide Gruppierungen aus dieser Projektion berechnen. Für jede Gruppe Netto-, Positiv-, Negativ-, Brutto-, Konto- und Nullkonto-Werte erzeugen und die Invarianten `Sum(NetBalance) == TotalBalance` sowie `Sum(GrossMagnitude) == TotalGrossMagnitude` vor Rückgabe sicherstellen.
   - Periodengrenzen über Benutzerzeitzone und `TimeProvider` bestimmen und die Bank-Postings der gefilterten Konto-IDs getrennt für Jahr und Monat aggregieren. Bei leerer Kontenmenge ohne Posting-Abfrage das definierte leere DTO zurückgeben.
   - Gruppen stabil nach `GrossMagnitude` absteigend und anschließend technischem Schlüssel sortieren, damit Farben und Tests deterministisch bleiben.

3. **Controller und API-Client**
   - Listen- und Statistikendpunkt um den vollständigen `q`-Vertrag, CancellationToken, XML-Dokumentation und einheitliche Validierungsantworten ergänzen.
   - URL-Erzeugung ausschließlich über strukturierte Query-Parameter umsetzen; `q` nicht manuell an Strings anhängen.
   - Vorhandene Aufrufer und `StubAccountService` an die neue Servicesignatur anpassen.

4. **ViewModel-Integration**
   - Statistikdaten und separaten Statistikstatus in `BankAccountListViewModel` ergänzen.
   - Initialladen, Suchwechsel, `ClearSearch`, Retry und Infinite Scroll entsprechend dem oben definierten Zustands- und Request-Vertrag implementieren.
   - Die bisherige `.Where(...Name/IBAN...)`-Filterung nach dem paginierten API-Aufruf entfernen. `CanLoadMore` basiert weiterhin auf der ungefiltert vom Server zurückgegebenen Seitengröße.

5. **UI-Komposition und Diagramme**
   - `AccountsStatisticsTile.razor` als kontenspezifische Komponente anlegen und in `ListPage.razor` nur einbinden, wenn der Provider ein `BankAccountListViewModel` ist. Andere generische Listen führen keinen Statistikaufruf aus und ändern ihr Markup nicht.
   - `ReportKpiTile.razor` für Gesamtsaldo, Jahres- und Monatsveränderung wiederverwenden, sofern negative Werte und Kulturformatierung unverändert korrekt sind; andernfalls nur dessen Wertdarstellung rückwärtskompatibel erweitern.
   - `DonutChart.razor` rückwärtskompatibel um optionale formatierte Legendenwerte und Detailtexte erweitern. `Value` bleibt nichtnegativ und steuert nur die Geometrie; signierte Netto-, Positiv-/Negativ- und Nullinformationen kommen aus separaten Feldern und ARIA-Texten.
   - Stabile Selektoren wie `.accounts-statistics`, `[data-statistics-state]`, `[data-statistics-kpi]` und `[data-statistics-group-key]` für bUnit und Playwright vorsehen.
   - Desktop: Statistik als unverschachtelter, responsiver Bereich oberhalb der Tabelle beziehungsweise in einer breiten Zweispaltenanordnung; Mobile: einspaltig vor der Liste. Feste Mindesthöhen verhindern Layoutsprünge. Lange Kontaktbezeichnungen dürfen umbrechen und keine Werte, Legenden oder den Viewport überdecken.

6. **Lokalisierung**
   - Deutsche und englische Ressourcen für Kacheltitel, KPI-Titel, Diagrammtitel, absolute Saldenbasis, Nettosaldo, positive/negative Beträge, Nullsalden, unbekannten Kontakt, Laden, Leerzustand, Fehler, Retry und ARIA-Texte ergänzen.
   - Alle Geldwerte verwenden dieselbe aktuelle Kultur-/Currency-Formatierung wie `ListCellKind.Currency`; Prozente verwenden die aktuelle UI-Kultur und eine Nachkommastelle.

7. **E2E-Testinfrastruktur**
   - `AccountsApiSeedHelper` um Kontoart, Bankkontakt, Saldo und datierte Bank-Postings erweitern; direkte Datenbank-Seeds müssen anschließend dieselbe Salden-Neuberechnung wie die Produktion ausführen.
   - `ListPageGateway` um Suche, `ClearSearch`, Statistikstatus, KPI- und Gruppen-Locators, Retry sowie Desktop-/Mobile-Listenlocators erweitern.
   - `PlaywrightWebAppFixture` erhält eine ausschließlich im E2E-Prozess aktivierte, nach jedem Test zurückgesetzte Fault-Injection für den internen Aufruf `GET /api/accounts/statistics`. Sie darf den Listenendpunkt und keine Produktionskonfiguration beeinflussen. Damit ist der sichtbare Statistikfehler reproduzierbar testbar, obwohl der Blazor-Server den API-Aufruf serverseitig ausführt.

## Verbindliche Tests

### Unit- und Service-Tests

- `AccountStatisticsPeriodProvider_UsesFixedClockAndEuropeBerlinTimeZone`: fester UTC-Zeitpunkt, der in `Europe/Berlin` bereits auf den Folgetag/Monat fällt; prüft lokale Tages-, Monats- und Jahresgrenzen.
- `AccountStatisticsPeriodProvider_MissingOrInvalidTimeZone_FallsBackToUtc`: fehlende und ungültige ID ergeben reproduzierbare UTC-Grenzen.
- `GetStatistics_BookingDateBoundaries_AreInclusiveStartExclusiveTomorrow`: Buchungen exakt vor, auf und nach Jahres- und Monatsbeginn sowie exakt vor und auf `TomorrowExclusive`; fester Zeitgeber und feste Zeitzone.
- `GetStatistics_UsesBookingDateAndIncludesCurrentPostingSemantics`: abweichendes `ValutaDate`, vorläufige Buchung und Stornopaar belegen die festgelegte Semantik.
- `AccountQuery_SearchesNameAndNormalizedIbanCaseInsensitively`: Name und IBAN in unterschiedlicher Groß-/Kleinschreibung sowie IBAN mit Leerzeichen, geschütztem Leerzeichen und Bindestrich.
- `AccountQuery_NullWhitespaceAndClearedSearchDisableFilter`: `null`, leer, Whitespace und zurückgesetzter Suchwert ergeben dieselbe vollständige Kontenmenge.
- `AccountQuery_TreatsWildcardCharactersLiterallyAndRejectsOverlength`: `%`, `_`, `*`, `?`, `[` und `]` sind literal; mehr als 200 Zeichen werden abgelehnt.
- `GetStatistics_MixedPositiveNegativeAndZeroBalances_IsLossless`: prüft je Gruppe Netto, Positiv, Negativbetrag, Bruttovolumen, Konto-/Nullanzahl, Prozentnenner und beide Summeninvarianten.
- `GetStatistics_MixedSignsWithinSameGroup_PreservesGrossMagnitude`: `+100` und `-100` in derselben Gruppe ergeben Netto `0`, Brutto `200` und einen sichtbaren Slice.
- `GetStatistics_UnknownBankContact_UsesStableFallbackGroup`: verwaister/nicht auflösbarer Kontakt landet nur in der Fallback-Gruppe.
- `BankAccountListViewModel_SearchAndClearReloadListAndStatisticsWithSameQ`: initiale Suche und `ClearSearch` laden beide Datenquellen mit identischem Suchwert.
- `BankAccountListViewModel_StatisticsFailureDoesNotSetListError`: Statistikfehler setzt nur `StatisticsState.Error`; Liste, Records und Paging bleiben erhalten.
- `BankAccountListViewModel_RetryUsesCurrentSearchAndStaleResponseIsIgnored`: Retry und überlappende Suchanfragen respektieren den aktuellen Request.

### Controller-/Integrationstests

- `AccountsStatistics_Unauthenticated_Returns401` prüft den neuen Endpunkt ohne Anmeldung.
- `AccountsStatistics_UsesFixedClockAndUserTimeZoneAtYearAndMonthBoundary` verwendet `FixedUtcNow`, `Europe/Berlin` und Buchungen direkt vor/auf/nach beiden Grenzen.
- `AccountsListAndStatistics_ApplySameOwnerAndSearchScopeBeyondFirstPage` seedet mindestens 51 eigene Konten und fremde Konten; Liste und Statistik teilen Owner- und `q`-Filter, während die Statistik ungepaginiert bleibt.
- `AccountsListAndStatistics_SearchNameAndIbanContract` prüft Name, IBAN, Case, Separatornormalisierung, Literal-Wildcards, leeren/Whitespace-Suchtext und 200-/201-Zeichen-Grenze gegen beide Endpunkte.
- `AccountsStatistics_MixedBalances_GroupNetsEqualTotal` prüft positive, negative und Nullsalden in beiden Gruppierungen und die Netto-/Bruttoinvarianten.
- `AccountsStatistics_UnknownContactAndEmptyAccountSet_ReturnDefinedPayloads` prüft Fallbackgruppe und den `200`-Leerfall.
- `ApiClient_AccountsQueries_EncodeQAndDeserializeStatistics` prüft URL-Encoding und DTO-Deserialisierung für Liste und Statistik.

### bUnit-Komponententests

- `ListPage_Accounts_RendersStatisticsAndGenericListOnlyForAccounts` prüft kontenspezifische Einbindung und unveränderte andere Listentypen.
- `AccountsStatisticsTile_Loading_RendersStableBusySkeleton` prüft `aria-busy`, feste Bereiche und fehlende veraltete Werte.
- `AccountsStatisticsTile_Empty_RendersZeroKpisAndNoSlices` prüft Nullbeträge, beide Leertexte und keine Prozentwerte.
- `AccountsStatisticsTile_AllZero_RendersGroupsWithoutRingSegments` unterscheidet vorhandene Nullkonten vom leeren Kontenbestand.
- `AccountsStatisticsTile_Error_RendersAlertAndRetryWithoutHidingList` prüft Fehlertext, Retry-Ereignis und weiterhin vorhandenes Listenmarkup.
- `AccountsStatisticsTile_MixedBalances_ShowsSignedAmountsAndGrossPercentBasis` prüft Nettosaldo, Positiv-/Negativbeträge, Nullanzahl, Prozentbasis und ARIA-Texte für beide Diagramme.
- `AccountsStatisticsTile_UnknownAndLongBankContactLabel_IsCompleteAndWrappable` prüft Fallback-Lokalisierung, ungekürzten langen Text und passende CSS-Klasse.
- Bestehende `DonutChartTests_PercentCalculation` bleiben grün und werden um optionale Legendenwerte ergänzt, damit bestehende Aufrufer unverändert funktionieren.

### Playwright-E2E-Tests

- `AccountsOverview_ShowsStatisticsAlongsideTable` zeigt Statistik und mindestens eine Tabellenzeile gleichzeitig.
- `AccountsOverview_Mobile_RendersStatisticsWithoutHidingList` prüft bei `390x844`, dass Statistik, lange Legenden und mobile Kontokarte ohne horizontales Überlaufen oder Überdeckung nutzbar sind.
- `AccountsOverview_ShowsTotalYearChangeAndMonthChange` prüft bekannte Beträge, Beschriftungen und Currency-Formatierung.
- `AccountsOverview_ShowsAccountTypeAndBankContactDonuts` prüft beide Titel, Gruppen und Center-Nettosaldo.
- `AccountsOverview_MixedBalances_ShowsLosslessSignedDistribution` seedet positive, negative und Nullsalden, auch gemischte Vorzeichen in derselben Gruppe, und prüft sichtbare Netto-/Positiv-/Negativwerte, Nullanzahl, Brutto-Prozentbasis sowie `Sum(Gruppennetto) == Gesamtsaldo`.
- `AccountsOverview_UnknownAndLongBankContact_RemainsReadable` prüft den lokalisierten Fallback und einen sehr langen Kontaktnamen auf Desktop und Mobile.
- `AccountsOverview_Search_UpdatesTableAndStatisticsTogether` findet einen Treffer außerhalb der ersten 50 Konten über Name und in einem zweiten Durchlauf über formatierte, anders geschriebene IBAN.
- `AccountsOverview_ClearSearch_RestoresTableAndStatistics` wendet eine Suche an, löst die Ribbon-Aktion `ClearSearch` aus und prüft die wiederhergestellten Zeilen, KPIs und Diagrammgruppen.
- `AccountsOverview_InfiniteScroll_DoesNotChangeStatisticsTotal` prüft denselben Statistikwert vor und nach dem Nachladen.
- `AccountsOverview_OtherUsersAccountsRemainInvisibleEverywhere` seedet zwei Benutzer und prüft, dass fremder Name, IBAN, Saldo, Kontoart und Kontakt weder in Tabelle noch KPIs oder Legenden erscheinen.
- `AccountsOverview_Empty_ShowsZeroKpisEmptyChartsAndUsableRibbon` prüft den definierten Leerzustand und die weiterhin bedienbare Ribbon-Navigation.
- `AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable` aktiviert den eingegrenzten Statistik-Fault, prüft Alert und sichtbare Tabelle, führt Suche und Zeilennavigation aus, deaktiviert den Fault und prüft erfolgreichen Retry.
- `AccountsOverview_ClickingAccountRow_StillNavigatesToCard` bleibt als Navigationsregression bestehen.

## Verifikation

1. `dotnet build FinanceManager.sln --no-restore`
2. `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-restore`
3. `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-restore`
4. `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-restore`
5. Playwright-Screenshots für Desktop und `390x844` visuell auf Überdeckung, abgeschnittene lange Labels, stabile Ladehöhe und gleichzeitig nutzbare Tabelle prüfen.

Fehlende, übersprungene oder fehlgeschlagene E2E-Szenarien gelten für dieses UI-Feature als nicht erfüllt.

## Betroffene Dateien und Bereiche

- `FinanceManager.Shared/Dtos/Accounts/` sowie `IApiClient.cs` und `ApiClient.Accounts.cs`
- `FinanceManager.Application/Accounts/IAccountService.cs` und neue Perioden-/Zeitzonenabstraktionen
- `FinanceManager.Infrastructure/Accounts/AccountService.cs` und `ServiceCollectionExtensions.cs`
- `FinanceManager.Web/Controllers/AccountsController.cs`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs`
- `FinanceManager.Web/Components/Pages/ListPage.razor`
- neue `FinanceManager.Web/Components/Shared/AccountsStatisticsTile.razor` sowie rückwärtskompatible Anpassungen an `DonutChart.razor`/gegebenenfalls `ReportKpiTile.razor`
- bestehende Theme-/Layout-CSS-Dateien und deutsche/englische Ressourcen
- `FinanceManager.Tests/`, `FinanceManager.Tests.Integration/ApiClient/` und `FinanceManager.Tests.E2E/Tests/Accounts/`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`, `ListPageGateway.cs` und die eingegrenzte E2E-Fault-Injection in `PlaywrightWebAppFixture`

## Nicht im Umfang

- Keine Änderung an Kontenanlage, Kontenbearbeitung, Account-Card oder anderen generischen Listentypen.
- Keine Benutzerkonfiguration für Sichtbarkeit oder Diagrammmetrik und keine Navigation aus Diagrammsegmenten.
- Keine Umstellung bestehender Kontosalden auf eine neue Datenquelle und keine Datenbankmigration.
- Keine Suche nach verknüpften Unter-IBANs oder Bankkontaktnamen; der Vertrag bleibt bei Account-Name und primärer Account-IBAN.

## Offene Punkte

Keine. Zeitquelle, Zeitzone, Periodengrenzen, Suchvertrag, Diagrammsemantik, UI-Zustände und alle erforderlichen Testebenen sind verbindlich festgelegt.
