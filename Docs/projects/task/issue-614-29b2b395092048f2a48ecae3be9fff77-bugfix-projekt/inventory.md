# Bestandsaufnahme (Inventory) — Bugfix-Projekt Issue #614

Repository: `D:\Repositories\softwareschmiede\29b2b395-0920-48f2-a48e-cae3be9fff77`
Branch: `task/issue-614-…-bugfix-projekt` | Blazor Server, .NET 10 (`net10.0`)

## 1. Generische Listeninfrastruktur

### Seiten & Provider

- **`FinanceManager.Web\Components\Pages\ListPage.razor`** — Route `@page "/list/{Kind}/{SubKind?}/{Id?}"`, `?back=` via `[SupplyParameterFromQuery(Name="back")]` (Z. 100). Erzeugt Provider über `ListViewModelFactory` (Z. 143–144), setzt `SetBackNavigation(Nav.IsSameOriginUri(Back) ? Back : null)` (Z. 156), rendert `<Ribbon>` + `<GenericListPage>` (Z. 29, 38–47).
- **`FinanceManager.Web\Components\Pages\GenericListPage.razor`** (460 Z.):
  - Suche: `<input … @oninput="OnSearchInput" />` nur wenn `ShowSearch && Provider.AllowSearchFiltering` (Z. 15–18).
  - Datumsbereich: zwei `InputDate` (`_from`/`_to`) nur wenn `ShowRange && Provider.AllowRangeFiltering` (Z. 19–27); `ShowRange="false"` kann den Filter komplett ausblenden (verwendet in `SetupMcpTab.razor` Z. 66).
  - `OnSearchInput` (Z. 382–389): `Provider.SetSearch(value)` → `Provider.ResetAndSearch()` → `await Provider.LoadAsync()`.
  - `OnRangeChanged` (Z. 391–398): `Provider.SetRange(_from,_to)` → `ResetAndSearch()` → `LoadAsync()`. `_from`/`_to` werden aus `Provider.RangeFrom/RangeTo` bei Provider-Wechsel und bei `StateChanged` synchronisiert (Z. 336–354).
  - Infinite Scroll über JS `fmInfinite.observe` → `[JSInvokable] LoadMoreFromJs` → `Provider.LoadMoreAsync()` (Z. 356–380).
- **`FinanceManager.Web\ViewModels\Common\IListProvider.cs`**: `SetSearch`/`SetRange` (Z. 98/105), `Search`, `RangeFrom`, `RangeTo`, `AllowRangeFiltering` (Z. 54), `AllowSearchFiltering` (Z. 59), `ResetAndSearch` (Z. 110), `ClearSearch`/`ClearRange`, `LoadAsync`/`LoadMoreAsync`, `GetRibbonRegisters`.
- **`FinanceManager.Web\ViewModels\Common\BaseListViewModel.cs`** (`BaseListViewModel<TItem>`):
  - `AllowRangeFiltering` (Z. 84) und `AllowSearchFiltering` (Z. 89): `virtual`, `protected set`, **Default `true`**.
  - `SetSearch` (Z. 181–184) setzt nur State, kein Reload; `SetRange` (Z. 197–200); `ClearSearch`/`ClearRange` (Z. 189–206); `ResetAndSearch` (Z. 211–215) cleart `Items` + `CanLoadMore = true`.
  - `InitializeAsync` (Z. 102–110): `CheckAuthentication()` → `LoadAsync()`. `LoadAsync` (Z. 127–138): `Loading`/`Loaded`-Flags, `Items.Clear()`, `LoadPageAsync(resetPaging: true)`, `BuildRecords()`. `LoadMoreAsync` (Z. 144–150) → `LoadPageAsync(false)`.
  - `abstract LoadPageAsync(bool resetPaging)` (Z. 158) — hier gehört die Filterauswertung hin.
- **`ListViewModelFactory`** (`FinanceManager.Web\ViewModels\ListViewModelFactory.cs`): `Create(kind, subKind, id)` (Z. 72–149) mappt `accounts/contacts/postings/savings-plans/securities/statement-drafts/users/mcp-accesses/budget(+s)` auf die VMs.

### Überschreibungen `AllowRangeFiltering`/`AllowSearchFiltering` (Ist-Bestand)

| VM | Datei:Zeile | Wert |
|---|---|---|
| `ContactListViewModel` | `ViewModels\Contacts\ContactListViewModel.cs:29` | `AllowRangeFiltering => false` |
| `BankAccountListViewModel` | `ViewModels\Accounts\BankAccountListViewModels.cs:39` | `AllowRangeFiltering => false` |
| `ContactGroupListViewModel` | `ViewModels\Contacts\Groups\ContactGroupListViewModel.cs:28` | `=> false` |
| `SavingsPlanCategoryListViewModel` | `ViewModels\SavingsPlans\Categories\SavingsPlanCategoryListViewModel.cs:30` | `=> false` |
| `SecurityCategoriesListViewModel` | `ViewModels\Securities\Categories\SecurityCategoriesListViewModel.cs:46,51` | beide `false` (mit `protected set`-Shim) |
| `SecurityPricesListViewModel` | `ViewModels\Securities\Prices\SecurityPricesListViewModel.cs:32` | `AllowSearchFiltering = false` |
| `BudgetRuleListViewModel` | `ViewModels\Budget\BudgetRuleListViewModel.cs:33-34` | beide `false` (Konstruktor) |
| `StatementDraftsListViewModel` | `ViewModels\StatementDrafts\StatementDraftsListViewModel.cs:35-36` | beide `false` (Konstruktor) |
| `BudgetCategoryListViewModel` | `ViewModels\Budget\BudgetCategoryListViewModel.cs:25` | `=> true` (explizit, Range wird an API weitergereicht) |
| Postings-VMs (`Account/Contact/SavingsPlan/SecurityPostingsListViewModel`) | je Z. ~23–28 | `AllowRangeFiltering = true` (Konstruktor) |

**Nicht überschrieben** (Filter werden angezeigt, aber ignoriert → Bugs #599/#601/#604): `SavingsPlansListViewModel`, `SecuritiesListViewModel`, `UserListViewModel`, `McpAccessListViewModel`.

### Suche in funktionierenden Listen

- **Serverseitig**: `ContactListViewModel.LoadPageAsync` (Z. 61): `Contacts_ListAsync(skip, take, type: null, all: false, nameFilter: Search)`; `BankAccountListViewModel` (Z. 69, 78): `GetAccountsAsync(_skip, PageSize, null, q)` mit `NormalizeSearchForRequest` (Z. 225–233); `McpAccessListViewModel` (Z. 83–87): `Mcp_ListAccessesAsync(Search, skip, 100)`; `BudgetPurposeListViewModel` (Z. 53–59): `Budgets_ListPurposesAsync(_skip, PageSize, null, Search, range.From, range.To)`.
- **Clientseitig**: bisher **keine** Liste filtert `Items` selbst — für #599/#601/#604 wäre das neu (Listen sind ohnehin vollständig geladen).

## 2. Sparpläne

- **`FinanceManager.Web\ViewModels\SavingsPlans\SavingsPlansListViewModel.cs`** (328 Z.):
  - `LoadPageAsync` (Z. 52–112): `SavingsPlans_ListAsync(OnlyActive)` (Z. 57) — **`Search`/`RangeFrom`/`RangeTo` werden nicht ausgewertet**; Kategorien via `SavingsPlanCategories_ListAsync` (Z. 60); füllt Caches `_intervalByPlan`, `_typeByPlan` (Z. 63–69), `Items` = `SavingsPlanListItem(Id, Name, Type.ToString(), CategoryName?, SymbolId)`; `CanLoadMore = false` (Z. 83, „API returns full list"); danach `_displaySymbolByPlan` (Z. 91–95) und parallel `_analysisByPlan` via `SavingsPlans_AnalyzeAsync(i.Id)` je Item (Z. 98–108, best-effort `catch {}`); ruft selbst nochmal `BuildRecords()` (Z. 111).
  - `IntervalMonths` (Z. 119–130): Monthly→1, BiMonthly→2, Quarterly→3, SemiAnnually→6, Annually→12.
  - `BuildRecords` (Z. 136–276): 7 Spalten (symbol/name/target/balance/remaining/date/status); Recurring → Kalender-SVG mit Monatszahl (Z. 153–183, 215–228); Status-SVG completed/overdue/unreachable/reachable (Z. 230–261); `AllowHtml: true` für date/status (Z. 270–271).
  - `ToggleActive` (Z. 281–286); Ribbon: `New` (MobileShortcut), `Categories` → `OpenCategories`; `ToggleActive` in View-Gruppe (Z. 293–316).
- **`SavingsPlanListItem`** (`ViewModels\SavingsPlans\SavingsPlanListItem.cs:14`): `(Guid Id, string Name, string Type, string? CategoryName, Guid? SymbolId)`, `GetNavigateUrl() => $"card/savings-plans/{Id}"`. **Kein `ContractNumber`/`TargetDate`/`Interval`/`Type`-Enum im Item** — für #599-Filter muss die Projektion erweitert oder vor `Items.AddRange` auf DTO-Ebene gefiltert werden.
- **Entität `FinanceManager.Domain\Savings\SavingsPlan.cs`**: Felder `OwnerUserId, Name, Type, TargetAmount, TargetDate, Interval, IsActive, ArchivedUtc, CategoryId, ContractNumber, SymbolAttachmentId`. **`AdvanceTargetDateIfDue(DateTime asOfUtc)`** (Z. 176–194): nur für `Type == Recurring` mit `Interval`+`TargetDate`; rückt `TargetDate` in `while (TargetDate <= asOfUtc)`-Schleife via `AddIntervalWithMonthEndRule` vor. **`AddIntervalWithMonthEndRule` (private static, Z. 205–228)**: Monatsend-Semantik — war der Tag Monatsletzter, landet das Ergebnis auch auf Monatsletztem; sonst Tag gecappt auf `DaysInMonth`. Für #599 (Fälligkeiten in beide Zeitrichtungen) ist die Methode `private` — ggf. `internal`/`public` machen oder Logik im VM nachbauen (Sichtbarkeit beachten).
- **DTO `FinanceManager.Shared\Dtos\SavingsPlans\SavingsPlanDto.cs`**: `Id, Name, Type, TargetAmount, TargetDate, Interval, IsActive, CreatedUtc, ArchivedUtc, CategoryId, ContractNumber, SymbolAttachmentId, RemainingAmount, CurrentAmount, OwnAmount`. Enums: `SavingsPlanType { OneTime, Recurring, Open }` (`SavingsPlanType.cs`), `SavingsPlanInterval { Monthly, BiMonthly, Quarterly, SemiAnnually, Annually }` (`SavingsPlanInterval.cs`).
- **API**: `IApiClient.cs:930` `SavingsPlans_ListAsync(bool onlyActive = true, CancellationToken ct)` (volle Liste, **kein** Such-/Range-Parameter — Filterung muss clientseitig erfolgen); `:971` `SavingsPlans_AnalyzeAsync(Guid id, CancellationToken ct)` → `SavingsPlanAnalysisDto` (`TargetReachable, TargetAmount, TargetDate, AccumulatedAmount, RequiredMonthly, MonthsRemaining, …`).
- **Tests**: `FinanceManager.Tests\ViewModels\SavingsPlansViewModelTests.cs` (Moq-Setup s. §11); `FinanceManager.Tests\Savings\` (Service-/Analyse-Tests, u. a. `SavingsPlanAnalysisTests`, `SavingsPlanScheduleDeficitTests`, `SavingsPlanServiceTests`).

## 3. Wertpapiere

- **`ViewModels\Securities\SecuritiesListViewModel.cs`** (229 Z.): `LoadAsync(CancellationToken)` (Z. 46–97): `Securities_ListAsync(OnlyActive)` → `Items` aus `SecurityListItem(Id, Name, Identifier, AlphaVantageCode, CategoryId, CategoryName, IsActive, SymbolId)` (Z. 53) — **`Search` ignoriert, kein Override von `AllowRangeFiltering`** (Range-Filter wird angezeigt, wirkt aber nichts). `LoadPageAsync` delegiert an `LoadAsync`, `CanLoadMore=false` (Z. 114–119). `BuildRecords` (Z. 124–147): Spalten symbol/name/identifier/alphavantage/category/status.
- **`SecurityListItem`** (`ViewModels\Securities\SecurityListItem.cs:15`): `(Id, Name, Identifier, AlphaVantageCode, CategoryId, CategoryName, IsActive, SymbolId)` — alle für #601 nötigen Suchfelder bereits im Item vorhanden.
- **`SecurityDto`** (`Shared\Dtos\Securities\SecurityDto.cs`): `Name, Identifier (ISIN/WKN/Ticker), AlphaVantageCode, CategoryId, CategoryName, IsActive, SymbolAttachmentId, CurrencyCode, HasPriceError, OpenQuantity, Region, Sector`.
- **API**: `IApiClient.cs:1047` `Securities_ListAsync(bool onlyActive = true, CancellationToken ct)` — kein Suchparameter.
- **Test**: `FinanceManager.Tests\ViewModels\SecuritiesListViewModelTests.cs` vorhanden.

## 4. Benutzer

- **`ViewModels\Setup\UserListViewModel.cs`** (128 Z.): `LoadAsync` (Z. 48–66): `ApiClient.Admin_ListUsersAsync(ct)` → `UserListItem(u.Id, u.Username, u.IsAdmin, u.Active, u.LockoutEnd, u.LastLoginUtc)` — **`Search` ignoriert, `AllowRangeFiltering` nicht überschrieben** (Bug #604). `LoadPageAsync` (Z. 72–77) → volle Liste, `CanLoadMore=false`. `BuildRecords` (Z. 82–102): username/admin/active/lockedUntil/lastLogin. Ribbon nur `New` (Z. 109–127).
- **`UserListItem`** (Z. 19): `GetNavigateUrl() => $"card/users/{Id}"`.
- **API**: `IApiClient.cs:249` `Admin_ListUsersAsync(CancellationToken ct)` → `IReadOnlyList<UserAdminDto>`; `UserAdminDto` (`Shared\Dtos\Users\UserAdminDto.cs:14`): `(Id, Username, IsAdmin, Active, LockoutEnd, LastLoginUtc, PreferredLanguage)`.
- **Testklasse**: `FinanceManager.Tests\Web\UsersViewModelTests.cs` (nicht unter `ViewModels\`!) — `CreateListVm()` mit `ServiceCollection` + `Mock<IApiClient>` + `FakeStringLocalizer<>` (Z. 28–38, 239–252).

## 5. Karten-/Back-Navigation

### `CardPage.razor` `OnUiActionRequested` — `case "Back"` (Z. 133–198)

Reihenfolge der Payload-Auswertung:

1. **Direkt-URL** (Z. 136–142): `payload` beginnt mit `"/"` **oder** `"http"` **und** `IsSameOriginUri(payload)` → `NavigateTo(payload)`. ⚠️ **App-relative Payloads ohne `/` (z. B. `"card/setup"`, `"card/budget/purposes/{id}"`) fallen hier durch** — Kern der Bugs #602/#606–#608.
2. **`?back=`** (Z. 146–150): `IsSameOriginUri(Back)` → `NavigateTo(Back!)`.
3. **GUID-Payload für Statement-Draft-Entries** (Z. 154–163): `Guid.TryParse(payload)` + `kind=="statement-drafts" || sub=="entries" || VM ist StatementDraftEntryCardViewModel` → `card/statement-drafts/{guid}`.
4. **Leerer Payload** (Z. 166–169): `BuildListPath()` (Z. 93–98: `list/{kind}` bzw. `list/{kind}/{subKind}`).
5. **Komma-Payload** (Z. 173–194): `parts = payload.Split(',')`; `parts.Length == 1` → **Legacy-Subtyp** `list/{Kind}/{parts[0]}`; `mode == "card"` → `card/{Kind}/{arg}`; `mode == "list"` → `list/{Kind}/{arg}`; sonst `BuildListPath()`.

`?back=` wird ausschließlich über `[SupplyParameterFromQuery(Name="back")]` (Z. 87) übernommen und mit `NavigationManager.IsSameOriginUri` validiert (Open-Redirect-Schutz, `Extensions\NavigationManagerExtensions.cs:186–247`; WHATWG-härtende `IsSafeNavigationCandidate` Z. 224–247). URL-Bau mit `Uri.EscapeDataString` in `BuildCardPath` (Z. 100–106) und bei `OpenBankContact`/`OpenAccountDetails` (Z. 240, 253).

### Weiterleitungs-Mechanik

- **`ICardInitializable`** (`ViewModels\Common\ICardInitializable.cs`): `InitializeAsync(Guid)`, `SetInitValue(string?)`, `SetBackNavigation(string?)`. `CardPage` ruft bei Resolve via `CardViewModelResolver`/`CardRouteAttribute` `SetBackNavigation(IsSameOriginUri(Back) ? Back : null)` + `SetInitValue(Prefill)` (Z. 397–407).
- **`BaseViewModel`**: `SetBackNavigation`/`InitBack` (`BaseViewModel.cs:109–120`); `RaiseUiActionRequested` (Z. 284–301); **`RequestOpenLinkedCard(route, linkedId)`** (Z. 356–379): baut `card/{route}/{id}?back={EscapeDataString(nav.Uri)}` und sendet als `"OpenPostings"`-Aktion — Referenzmuster für `?back=`-Weitergabe (Ketten).
- **Beispiel-Payloads in `CardPage`**: `OpenBankContact` (Z. 233–243): Payload `"kind,{guid}"` → `card/{kind}/{cid}?back={BuildCardPath(Kind,SubKind,Id,Back)}`; `OpenAccountDetails` (Z. 244–256) analog; `NewRule` (Z. 348–372): baut `card/budget/rules/new?prefill=…&back=…`.

### Alle `RaiseUiActionRequested("Back", …)` mit app-relativem Pfad ohne führenden Slash (komplette Liste)

| Fundstelle | Payload | Kontext |
|---|---|---|
| `ViewModels\Mcp\McpAccessCardViewModel.cs:740` | `"card/setup"` | Ribbon Back (#608) |
| `ViewModels\Mcp\McpSecurityOverviewViewModel.cs:150` | `"card/setup"` | (#607) |
| `ViewModels\Mcp\McpAuditViewModel.cs:331` | `"card/setup"` | (#606) |
| `ViewModels\Mcp\McpApprovalCenterViewModel.cs:403` | `"card/setup"` | (#607) |
| `ViewModels\Budget\BudgetRuleCardViewModel.cs:340` | `InitBack` (kann `"card/budget/purposes/{id}"` aus `SetBackNavigation` Z. 100/104 sein) | Back-Button |
| `BudgetRuleCardViewModel.cs:345` | `$"card/budget/purposes/{BudgetPurposeId}"` | `NavigateBackExtended` nach Delete |
| `BudgetRuleCardViewModel.cs:377/381` | `InitBack` / `$"card/budget/purposes/{BudgetPurposeId}"` | Ribbon Back (#602) |
| `ViewModels\Budget\BudgetPurposeCardViewModel.cs:481` | `$"card/budget/categories/{id.Value}"` | Ribbon „Category" |
| `ViewModels\Budget\BudgetCategoryListViewModel.cs:94` | `"list/budget/purposes"` | ListPage-Back ⚠️ **`ListPage.razor` ignoriert Payload komplett** (Z. 214–242): Back geht über `?back=` → postings-Sonderfall → `list/{Kind}` = `list/budget` (funktioniert zufällig, da Factory `budget` ohne SubKind → Purposes) |

**Sonderfälle (funktionieren bereits, nicht als Bug zählen):**
- `StatementDraftEntryCardViewModel` Z. 964, 1176, 1201, 1237, 1362: `"card,{DraftId}"` → Komma-Branch `card/{Kind}/{arg}` produziert `card/statement-drafts/{draftGuid}` (Kind=`statement-drafts`). OK.
- `PostingsCardViewModel.cs:134`: Payload `kindLower` z. B. `"account/{guid}"` → einteiliger Legacy-Branch → `list/postings/account/{guid}` — beabsichtigtes Format.
- Alle übrigen VMs senden `"Back"` ohne Payload (Fallback `BuildListPath()` / `?back=`).

## 6. Verknüpfte Karten

- **`SavingsPlanCardViewModel.cs:860`**: Ribbon-Aktion `"Category"` (Disabled wenn `Model.CategoryId is null`) sendet `RaiseUiActionRequested("OpenCategory", $"card/savings-plans/categories/{Model.CategoryId}")` — **ohne `?back=`** (Bug #600). `CardPage` `case "OpenCategory"` (Z. 257–259): `NavigateTo(payload)` unverändert — Fix muss `?back=` anhängen (Muster `OpenBankContact`, Z. 233–243, mit `BuildCardPath(Kind,SubKind,Id,Back)` + `Uri.EscapeDataString`).
- **`StatementDraftEntryCardViewModel`**: sendet `"Back", $"card,{DraftId}"` (Z. 1176 u. a.) — zurück zur Draft-Karte, aber **ohne `?back=`** → die Draft-Karte verliert ihre eigene Herkunft. Einstieg in die Entry-Karte erfolgt aus der Embedded-Liste `StatementDraftEntriesListViewModel` (`internal`, `BaseListViewModel<StatementDraftEntryItem>`, `draftId` im Ctor Z. 53–59).
- `?back=`-Auswertung nur in `CardPage` (s. §5); Escaping überall `Uri.EscapeDataString`. **`ListPage.razor` wertet `?back=` ebenfalls** (Z. 100, 156, 217–220) — dort hat `Back` Vorrang vor Provider-Payloads nur in `case "Back"` ohne Payload-Auswertung.

## 7. Analyse-Buttons

- **`ViewModels\Budget\BudgetPurposeListViewModel.cs`**, Analyse-Gruppe `Ribbon_Group_Analysis`, `Sort: 40` (Z. 150–188):
  - `PrevMonth` (`Ribbon_PrevMonth`, Z. 152–160): **absolut** — `start = Erster des Vormonats von DateTime.Today`, `end = Monatsende`; `SetRange(start,end)` + `RaiseUiActionRequested("Reload")`.
  - `ThisMonth` (`Ribbon_ThisMonth`, Z. 161–169): aktueller Monat (absolut).
  - `PrevYear` (`Ribbon_PrevYear`, Z. 170–178): Kalendervorjahr 1.1.–31.12.
  - `ThisYear` (`Ribbon_ThisYear`, Z. 179–187): laufendes Kalenderjahr.
  - **Kein `NextMonth`/`NextYear`**, keine relative Verschiebung (Bug #603).
  - Zeitraumwirkung: `InitializeAsync` setzt Default aktueller Monat (Z. 32–40); `LoadPageAsync` nutzt `GetEffectiveMonthlyRange()` (Z. 94–106): `RangeFrom`/`RangeTo`, normiert `from<=to` (tauscht bei Umkehrung), → `DateOnly`; an API als `range.From/range.To`.
- **`ViewModels\Budget\BudgetReportViewModel.cs`**: Analyse-Gruppe `Sort: 40` (Z. 364–410): `PrevMonth`→`ShiftAsOfMonthAsync(-1)`, `ThisMonth`→`SetAsOfToCurrentMonthAsync`, `NextMonth`→`ShiftAsOfMonthAsync(+1)`, `PrevYear`→`ShiftAsOfYearAsync(-1)`, `NextYear`→`ShiftAsOfYearAsync(+1)` — **relativ** zu `Settings.AsOfDate`, monatsend-normiert (`DateOnly(shifted.Year, shifted.Month, DaysInMonth)`, Z. 513–538). `LoadAsync` (Z. 137–252): `to = Settings.AsOfDate`, `from = Erster des Monats - (Settings.Months-1)` (Z. 145–146). Back-Gruppe nur wenn `InitBack` gesetzt (Z. 329–346).
- **resx** (`FinanceManager.Web\Resources\Pages.{de,en}.resx` + neutrales `Pages.resx`, je ~Z. 2031–2042, 2172–2178): vorhanden `Ribbon_PrevMonth`, `Ribbon_ThisMonth`, `Ribbon_PrevYear`, `Ribbon_ThisYear`, `Ribbon_NextMonth`, `Ribbon_NextYear`. **`Ribbon_CurrentMonth`/`Ribbon_CurrentYear` existieren NICHT** — für #603 `ThisMonth`/`ThisYear` weiterverwenden.
- Host-Seite: `Components\Pages\BudgetReport.razor` (`@page "/reports/budget"`, VM direkt instanziiert Z. 19–26, `SetBackNavigation` fehlt hier — Back-Tab hängt von `?back=` ab, gesetzt in `ListPage` `OpenBudgetReport` Z. 212).

## 8. MCP/KI

- **`ViewModels\Mcp\McpAccessListViewModel.cs`** (380 Z.):
  - `IsMcpDisabled` (Z. 58) — gesetzt via `ApiClient.Mcp_GetStatusAsync` → `!status.Enabled` (Z. 72–73, best-effort `catch`) oder bei `LastErrorCode == "Err_Mcp_Disabled"` (Z. 95–98).
  - `LoadPageAsync` (Z. 65–101): bei disabled `CanLoadMore=false`, early return; sonst `Mcp_ListAccessesAsync(Search, skip, 100)`.
  - `BuildRecords` (Z. 106–148): disabled → einzelne gemutete Hinweiszeile `Mcp_Disabled_Hint` (Z. 118–133).
  - `RequestCreateWizard` (Z. 155–162): `UiOverlaySpec(typeof(McpAccessCreateWizard))` via `RaiseUiActionRequested("New", spec)`.
  - Ribbon (Z. 169–255): `New` (**`Disabled` fest `false`, `MobileShortcut = true`** — Bug #605), `Reload`, Linked-Gruppe `Approvals`/`AuditLog`/`Security` (Security disabled für Nicht-Admins Z. 236).
- **`Components\Shared\McpAccessCreateWizard.razor`** (251 Z.): `OnInitializedAsync` (Z. 151–175) lädt `Mcp_GetLevelsAsync` + `Mcp_GetOptionsAsync` (TLS-Warnung); **kein Status-/Disabled-Guard** — für #605 Hinweis-Mechanismus hier ergänzen (`Mcp_GetStatusAsync` + `Mcp_Disabled_Hint` statt Schritte). `CreateAsync` (Z. 198–235), `FinishAsync` → `OverlayOnFinished("card/mcp-accesses/{id}")` (Z. 237–250). Cascading Parameters `OverlayClose`/`OverlayOnFinished` (Z. 129–132).
- **`UiRibbonAction`** (`ViewModels\Common\RibbonModels.cs:42–76`): `(Id, Label, IconSvg, Size, Disabled, Tooltip, Callback)` + init-only `FileCallback`, `MobileShortcut`, `Hidden` (Z. 75: komplett weggelassen). `UiRibbonTab(Title, Items, Sort)`, `UiRibbonRegister(Kind, Tabs)`, Enums `UiRibbonItemSize` (Z. 6), `UiRibbonRegisterKind` (Z. 17).
- **`Components\Shared\Ribbon.razor`**: Disabled-Buttons: `disabled="@item.Disabled"` + `aria-disabled` (Z. 70–71, 131–132); `OnItemClick` no-op wenn Disabled (Z. 319–323). `Hidden` wird in `BuildTabsToRender` herausgefiltert (Z. 196–199); Gruppen ohne Items werden entfernt (Z. 221); Einzelaktion-Gruppen bekommen auto `MobileShortcut=true` (Z. 224–227); Mobile-Shortcuts nur für `MobileShortcut && !Disabled` (Z. 95). Gruppen-Merge über gleichen Titel, Sortierung nach `Sort` (Z. 188–190, 253).
- **`ViewModels\Setup\SetupMcpViewModel.cs`**: `RequestNewAccess()` → `Accesses.RequestCreateWizard()` (Z. 199); `Accesses.UiActionRequested` wird via `ForwardUiAction` an eigenes Event weitergereicht (Z. 36–38, 263–278). `InitializeFeatureState(mcpEnabled, pendingApprovals)` (Z. 89–94).
- **`Components\Pages\Setup\SetupMcpTab.razor`**: bei `!_vm.McpEnabled` Hinweis `McpSetup_DisabledHint` (Z. 46–50); sonst „Neu"-Button → `_vm.RequestNewAccess()` (Z. 55) + `<GenericListPage … ShowRange="false">` (Z. 57–67) — zweiter, ungeschützter Einstiegspunkt für #605.
- **API**: `IApiClient.cs:1722` `Mcp_GetStatusAsync(CancellationToken)` → `McpStatusDto(bool Enabled)` (`Shared\Dtos\Mcp\McpSettingsDtos.cs:25`); `:1730` `Mcp_ListAccessesAsync(string? search, int skip, int take, CancellationToken)`.

## 9. Budgetregeln

- **`ViewModels\Budget\BudgetRuleListViewModel.cs`** (164 Z.): `BudgetPurposeId`/`BudgetCategoryId` (Z. 20, 25); `InitializeForPurposeAsync`/`InitializeForCategoryAsync` (Z. 41–57); `LoadPageAsync` Guard Z. 68–72 (beide leer → `Items.Clear`, kein API-Call); `Budgets_ListRulesByPurposeAsync`/`…ByCategoryAsync` (Z. 80, 84). **Ribbon `NewRule`** (Z. 141–156): Payload `(BudgetPurposeId.HasValue && != Empty) ? $"purpose,{id}" : $"category,{BudgetCategoryId!.Value}"` — **`BudgetCategoryId!` NRE wenn beide leer** (Bug #609); `Disabled` ist fest `false`.
- **`BudgetPurposeCardViewModel`**: `LoadAsync` (Z. 40–92): `EmbeddedList ??= CreateSubViewModel<BudgetRuleListViewModel>()` (Z. 50); bei `id == Guid.Empty` → `rulesNew.InitializeForPurposeAsync(Guid.Empty)` → beide IDs ungültig → Guard greift, **aber die „Neu"-Aktion bleibt klickbar** → NRE (Bug #609). Nach erfolgreichem `SaveAsync` wird `InitializeForPurposeAsync(Id)` nachgeladen (Z. 574–578, 596–599). `CardRoute("budget","purposes")` (Z. 15).
- **`BudgetCategoryCardViewModel`**: `LoadAsync` (Z. 37–81): bei `id == Guid.Empty` → **`EmbeddedList = null`** (Z. 51) — hier keine Regelliste sichtbar, anderes Bild als bei Purpose; bei geladenem Datensatz `CreateSubViewModel<BudgetRuleListViewModel>(singletonPerType: true)` + `InitializeForCategoryAsync(id)` (Z. 66–68).
- **Embedded-Liste-Rendern**: `GenericCardPage.razor:55–64` rendert `Provider.EmbeddedList` via `<GenericListPage … ItemClick="OnEmbeddedItemClick">`.
- **`BudgetRuleCardViewModel`** (`CardRoute("budget","rules")`, Z. 11): `LoadAsync` parst `InitPrefill` `purpose:{guid}`/`category:{guid}`/Legacy-Guid (Z. 55–80); setzt bei fehlendem `InitBack` `SetBackNavigation($"card/budget/purposes/{…}")` bzw. `…/categories/{…}` (Z. 96–106) — **relativ ohne `/`** (#602). `NavigateBackExtended` (Z. 336–349) nach Delete; Back-Callback Z. 373–389.
- **`CardPage` `case "NewRule"`** (Z. 348–372): baut `card/budget/rules/new?prefill={scope}:{id}&back={aktuelle Karten-URL}`.

## 10. Charts

- **`Components\Shared\BudgetLineChart.razor`** (193 Z.) — Bug #610:
  - `<svg viewBox="0 0 1000 325" preserveAspectRatio="none">` (Z. 5); Konstanten `left=60, right=980, top=20, bottom=300` (Z. 124–127); X-Achsenlabels `text-anchor='middle'` bei `y=320` (Z. 47–53); `_xAxisY` bei 0-Linie oder `min` (Z. 129–131).
  - X-Position: `x = left + (count==1 ? (right-left)/2 : (right-left)*i/(count-1))` (Z. 138) → **erster/letzter Punkt bei x=60/980, mittig verankerte Labels laufen über den ViewBox-Rand** (clipping).
  - Label-Schritt: `maxLabels = 12`, `labelStep = count<=12 ? 1 : Ceil(count/12)`; Label wenn `i % labelStep == 0 || i == count-1` (Z. 133–150) → letztes Label immer am rechten Rand.
  - CSS: `wwwroot\css\app.BudgetLineChart.css`, `wwwroot\css\theme.Dark.BudgetLineChart.css` (in `App.razor` Z. 19/47 eingebunden).
  - Nutzung: `Components\Pages\BudgetReport.razor:147–151`.
- **Identisches Muster (Schwester-Charts, Rand-Prüfung für #610):**
  - `Components\Shared\PortfolioBarLineChart.razor`: gleiche Konstanten `ChartLeft=60, ChartRight=980, ChartTop=20, ChartBottom=300`, `AxisLabelY = ChartBottom+20` (Z. 49–54), Labels `text-anchor='middle'` (Z. 36).
  - `Components\Shared\PortfolioGroupedBarChart.razor`: identisch (Z. 50–54, 37).
  - `Components\Shared\DonutChart.razor`, `ReturnSummaryWidget.razor` (Sparkline), `Pages\Securities\*Tab.razor` (dynamische viewBox) — andere Muster.
- **bUnit-Test-Konventionen** (`FinanceManager.Tests\Components\Shared\`): `public sealed class XTests : BunitContext`, `Render<T>(builder => builder.Add(c => c.Prop, val))`, `cut.Find/FindAll(css)`, `WithInvariantCulture(() => …)` aus `FinanceManager.Tests\TestHelpers\CultureTestHelper.cs`. Beispiel-Chart-Test: `PortfolioBarLineChartTests.cs` (assertiert `cy`/`height`-Attribute). Weitere: `DonutChartTests_PercentCalculation`, `MiniBarChartTests_BarScaling`, `PortfolioGroupedBarChartTests`. Für `BudgetLineChart` **kein Test vorhanden**.

## 11. Test-Infrastruktur

- **Framework**: `xunit.v3` 4.0.3 + `Microsoft.Testing.Platform` (`Microsoft.NET.Test.Sdk` 18.12), `Moq` 4.21, `FluentAssertions` 8.11, `bunit` 2.15.2 (`FinanceManager.Tests\FinanceManager.Tests.csproj`). `WarningsAsErrors += NU1605;CS1591` → **XML-Doku auf öffentlichen API ist Pflicht** (auch in Tests).
- **ViewModel-Testmuster** (z. B. `ViewModels\SavingsPlansViewModelTests.cs:24–31`): `ServiceCollection` mit `TestCurrentUserService` (eigene `ICurrentUserService`-Implementierung, `IsAuthenticated=true`), `apiMock.Object` als Singleton, `IStringLocalizer<>` → `TestLocalizer<>`/`FakeStringLocalizer<>` (Echo-Localizer), dann `new SavingsPlansListViewModel(sp)` oder `ActivatorUtilities.CreateInstance<T>(services)`.
- **Ordnerstruktur**: `FinanceManager.Tests\ViewModels\` (VM-Tests), `FinanceManager.Tests\Web\` (UsersViewModelTests, RazorRootAbsolutePathTests), `FinanceManager.Tests\Components\` + `Components\Shared\` (bUnit), `FinanceManager.Tests\Savings\`, `\Budget\`, `\Controllers\`, `\Common\EnumCoverageTests.cs`, `\TestHelpers\CultureTestHelper.cs`.
- **`EnumCoverageTests`** (`Common\EnumCoverageTests.cs`): assertiert vollständige Memberliste je Enum — **bei neuen Enum-Mitgliedern ergänzen** (AGENT.md-Pflicht).
- **`RazorRootAbsolutePathTests`** (`Web\RazorRootAbsolutePathTests.cs`): statischer Scan — verbietet `href="/`/`src="/`/`NavigateTo("/`/`fetch("/`/`url(/`/`location`-Zuweisungen in `*.razor`, `*.cs`, `wwwroot`. Erlaubt: relative Pfade (`Nav.NavigateTo($"card/contacts/{id}")`), `@page`-Deklarationen, `Request.PathBase`-Konkatenationen.
- **Test-Filter (xunit.v3 / MTP)**: `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -- --filter-class "FinanceManager.Tests.ViewModels.SavingsPlansViewModelTests"` bzw. `--filter-method`, `--filter-query`, `--filter-not-trait "Category=OsInterface"`.

## 12. Ressourcen

- **Ablage**: `FinanceManager.Web\Resources\` — zentrale `Pages.resx` (neutral), `Pages.de.resx`, `Pages.en.resx` plus bereichsbezogene `Components\…\<Name>.{de,en}.resx` (Convention `Resources/<Bereich>/<Name>.<culture>.resx`). Neue UI-Texte **immer de+en** (AGENT.md; neutral parallel pflegen — alle drei Dateien enthalten dieselben Keys).
- **`Mcp_Disabled_Hint`**: vorhanden `Pages.de.resx:3080` („Die KI-Integration (MCP) ist derzeit nicht aktiviert…"), `Pages.en.resx:3080`, `Pages.resx:3080`. Auch `McpSetup_DisabledHint` (Setup-Tab), `Mcp_Token_TlsWarning`, `McpWizard_*` vorhanden.
- **Ribbon-Labels vorhanden** (Pages.*.resx ~Z. 282, 549, 1983–2042, 2172–2178): `Ribbon_Back`, `Ribbon_Group_Analysis`, `Ribbon_Group_Rules`, `Ribbon_Group_Manage`, `Ribbon_Group_Navigation`, `Ribbon_Group_Linked`, `Ribbon_PrevMonth`, `Ribbon_ThisMonth`, `Ribbon_PrevYear`, `Ribbon_ThisYear`, `Ribbon_NextMonth`, `Ribbon_NextYear`. **Fehlend**: `Ribbon_CurrentMonth`, `Ribbon_CurrentYear` (bei Bedarf anzulegen, de+en+neutral).
- Pre-Commit-Hooks `.githooks\` (`translation-check.py`, `razor-l10n-check.py`) prüfen resx-Konsistenz/hartkodierte Strings.

## 13. Ribbon-Systematik

- **`IRibbonProvider`** (`ViewModels\ViewModelBase.cs:14–36`): `GetRibbonRegisters(IStringLocalizer)`, `GetActiveTab<TTabEnum>()`, `SetActiveTab<TTabEnum>()`.
- **`BaseViewModel.GetRibbonRegisters`** (`BaseViewModel.cs:688–704`): aggregiert `GetRibbonRegisterDefinition` (virtuell, Z. 711) + Register aller **aktiven Child-ViewModels** (`_childViewModels` via `CreateSubViewModel`, Z. 694–701) — deshalb erscheint die `NewRule`-Aktion der `BudgetRuleListViewModel` auf der Purpose-/Category-Karte.
- **Gruppenreihenfolge** (AGENT.md): Navigation `Sort: 0` → Manage `Sort: 10` → View/Filter `Sort: 30` → Analysis `Sort: 40` → Fields `Sort: ~47` → Linked `Sort: 55` → Rules `Sort: 60` → Reports `Sort: 70` → Export `Sort: 80`. `Ribbon.razor` merged Gruppen nach Titel und sortiert via `UiRibbonTab.Sort` (Z. 253).
- **`Disabled`**: rendert `disabled`-Attribut + Click-Guard; `Hidden` filtert komplett (Ribbon.razor Z. 196–199); `MobileShortcut` für Primäraktion (auto bei Einzelitem-Gruppe).
- Eintragsaktionen (Bearbeiten/Löschen) gehören nur auf Karten; Listen nur Neuanlage/Massenaktionen (AGENT.md). Labels aus resx, Icons `icons/sprite.svg#…`.

## Build-/Test-Kommandos & Einschränkungen

- Build: `dotnet build` (Solution-Root; `Directory.Build.props`). Tests: `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj` (MTP: Filter hinter `--`: `--filter-class "…"`, `--filter-method "…"`, `--filter-query "…"`, `--filter-not-trait "Category=OsInterface"`). E2E/Integration analog eigene csproj (`FinanceManager.Tests.E2E`, `FinanceManager.Tests.Integration`).
- **`dotnet format` ist relevant**: CI führt `dotnet format --verify-no-changes` aus.
- **Einschränkungen**:
  - `@rendermode InteractiveServer` Pflicht auf jeder Blazor-Seite.
  - **Keine root-absoluten Links/URLs** (`href="/…"`, `NavigateTo("/…")`, `fetch("/…")`, `url(/…)`) — Guard-Test `RazorRootAbsolutePathTests`; interne Ziele relativ (`card/…`, `list/…`, `api/…`). **Wichtig für #602/#606–#608**: Die geforderte Fix-Richtung ist, `CardPage` app-relative Payloads zentral zu akzeptieren — relative Navigationen entsprechen der PathBase-Konvention, `/`-Präfixe wären ein Rückfall-Risiko.
  - AGENT.md: Ribbon-Gruppenreihenfolge (s. §13), resx-Pflicht (de+en), `EnumCoverageTests` bei neuen Enum-Membern, EF-Migrationen nur via `dotnet ef` (hier irrelevant — keine Schemaänderungen), Detail-Aktionen nur auf Karten.
  - `CS1591` als Fehler: neue/erweiterte öffentliche Members brauchen XML-Doku.
  - Suche/Range-Filter für #599/#601/#604: Issue-Empfehlung = **clientseitige** Filterung, da alle drei Listen ungepagt vollständig laden (APIs bieten keine passenden Parameter).
