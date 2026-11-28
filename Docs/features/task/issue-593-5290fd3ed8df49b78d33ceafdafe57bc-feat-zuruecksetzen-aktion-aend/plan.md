# Umsetzungsplan: „Zurücksetzen"-Aktion (Änderungen verwerfen) auf acht Karten-Detailansichten (Issue #593)

## Übersicht

Acht Card-ViewModels erhalten die Ribbon-Aktion `Reset` in der Gruppe `Ribbon_Group_Manage` direkt hinter `Save`. Die Aktion verwirft ungespeicherte Feldänderungen (`ClearPendingChanges()`) und baut das `CardRecord` aus dem im ViewModel gehaltenen Baseline-Modell (DTO bzw. persistierte Modellwerte) neu auf — ohne Server-Neuladen des Datensatzes. Referenzimplementierung ist `BankAccountCardViewModel` (Aktion Z. 291, `ResetPendingAsync` Z. 373–389). Drei ViewModels (`SavingsPlanCardViewModel`, `SavingsPlanCategoryCardViewModel`, `SecurityCategoryCardViewModel` sowie `SecurityCardViewModel` als Nebenfall) benötigen eine Baseline-Absicherung, weil ihr Edit-`Model` vor/während `SaveAsync` ohne Rollback mutiert wird. Als verbindliche Nebenpflicht ist ein Folge-Issue zum Aufräumen der KI-Zugangsansicht anzulegen. Abgesichert wird der Change durch neue ViewModel-Unit-Tests je betroffenem ViewModel, Playwright-E2E-Tests inkl. Screenshot-Artefakten für den UI-Fluss und die projektregelkonforme reale End-to-End-Abnahme.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen aus `requirement.md` und verifiziert am Code (`inventory/`):

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine neue `UiRibbonAction("Reset", …)` je betroffenem Card-ViewModel in der `Ribbon_Group_Manage`-Liste direkt hinter `Save` sowie eine private Methode `ResetPendingAsync()` (`ClearPendingChanges()` + `CardRecord`-Neuaufbau aus der ViewModel-Baseline + `RaiseStateChanged()`, try/catch-tolerant) |
| Auslösende Ereignisse | Nutzerklick auf den Ribbon-Button „Zurücksetzen" (Aktions-ID `Reset`, Label `Ribbon_Reset` — resx de/en bereits vorhanden) |
| Darstellung | `UiRibbonItemSize.Small`, Icon `<svg><use href='icons/sprite.svg#undo'/></svg>` (relativ, regelkonform), `Disabled = !HasPendingChanges` (MCP: `Access is null \|\| !HasPendingChanges`), **kein** `MobileShortcut` (siehe Designentscheidung 1) |
| Erwartete Wirkung | Ungespeicherte Änderungen werden verworfen; die Karte zeigt den zuletzt geladenen bzw. zuletzt erfolgreich gespeicherten Zustand; Ribbon-Disabled-Zustände aktualisieren sich per `RaiseStateChanged()`; kein Datensatz-Reload vom Server (Lookup-Nebenabrufe des Bestands-Build-Pfads bleiben zulässig, siehe Designentscheidung 4) |
| Neuanlage-Modus | `Id == Guid.Empty`: Reset stellt den initialen Zustand (leer bzw. `InitPrefill`) wieder her — kein API-Aufruf, kein Löschen |
| Fehlende Objekte / Fehlermodelle | Baseline `null` (Ladefehler): `HasPendingChanges == false` → Aktion deaktiviert; `ResetPendingAsync` ist zusätzlich per `if (Baseline != null)`-Guard + try/catch abgesichert (Referenzmuster) |
| MCP-Sonderfall | Scope-Matrix/Grants/Filter werden sofort persistiert; `Reset` verwirft nur die Karten-Pending-Felder (Name, Beschreibung, Level, ApprovalMode, ExpiresUtc, AllowedIpRanges) |

### Scope-Ausschlüsse

Alle Ausschlüsse sind im Issue bzw. in `requirement.md` verbindlich festgelegt — es verbleibt kein `offener` Punkt:

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Änderung an `UserCardViewModel` | Save für bestehende Benutzer deaktiviert (Z. 371), Felder nur im Neuanlage-Modus editierbar (Z. 342–344) — „Zurücksetzen" ausdrücklich nicht benötigt | bestätigt (Issue-Anwenderantwort) |
| 2 | Keine Änderung an `ContactGroupCardViewModel` | Bei Issue-Analyse „in Ordnung" bewertet | bestätigt (Issue-Anwenderantwort) |
| 3 | Keine Änderung an `BudgetCategoryCardViewModel` | Bei Issue-Analyse „in Ordnung" bewertet | bestätigt (Issue-Anwenderantwort) |
| 4 | Keine Änderung an `StatementDraftCardViewModel` / `StatementDraftEntryCardViewModel` | Eigener Entwurfsworkflow (QuickEdit-Abbrechen, `Ribbon_ResetDuplicate`) bleibt unverändert | bestätigt (Issue-Anwenderantwort) |
| 5 | Keine neuen resx-Keys, Icons, API-Endpunkte, Entitäten, EF-Migrationen; keine Backup-Auswirkungen | `Ribbon_Reset` und `icons/sprite.svg#undo` vorhanden; keine Persistenzänderung | bestätigt (requirement.md) |
| 6 | Keine Änderung an Ribbon-Infrastruktur (`UiRibbonAction`, `UiRibbonTab`, `Ribbon.razor`, `BaseCardViewModel`, `BaseViewModel`) | Wiederverwendung der bestehenden Mechanik | bestätigt (requirement.md) |
| 7 | Kein Server-Neuladen als Teil des Reset-Verhaltens (kein `ReloadAsync`/`InitializeAsync`-Aufruf) | Fachliche Anforderung; `ReloadAsync` ruft `InitializeAsync(Id)` mit Datensatz-GET — verboten | bestätigt (requirement.md) |
| 8 | `SetupCardViewModel.ResetAll` bleibt unverändert (inkl. `MobileShortcut = true`) | Aggregat-Reset anderer Semantik, nicht kartengebunden; `SetupCardViewModelTests` assertiert genau eine Reset-Aktion | bestätigt (scope-fremd, inventory §3.6) |

## Designentscheidungen

| # | Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|----------------------|------------------|------------|
| 1 | `MobileShortcut` auf `Reset` | **Nicht setzen** — weder auf den acht neuen Aktionen noch (Konsistenz-Angleichung) auf `BankAccountCardViewModel.Reset` (dort wird `{ MobileShortcut = true }` entfernt, Z. 291). `SetupCardViewModel.ResetAll` bleibt unverändert (Scope-Ausschluss 8) | AGENT.md: „die primäre Aktion erhält `MobileShortcut = true`" — primär ist `Save`. `Ribbon.razor` rendert **alle** `MobileShortcut && !Disabled`-Items als Header-Shortcuts (Z. 95–113): mit dem Flag auf `Reset` stünden mobil zwei Shortcuts in der Manage-Gruppe, was die Sekundäraktion visuell gleichstellt. Die requirement-Präferenz deckt sich mit der Regel; die Referenz weicht ab und wird regelkonform nachgezogen, damit alle Karten-Reset-Aktionen konsistent sind. Kein Test assertiert `MobileShortcut` auf der Bankkonto-Reset-Aktion |
| 2 | Baseline bei `SavingsPlanCategoryCardViewModel` und `SecurityCategoryCardViewModel` | **Snapshot/Rollback im Save-Pfad**: vor dem Kopieren der `CardRecord`-Werte in `Name`/`SymbolId` bzw. `Model.*` (SavingsPlanCategory Z. 110–116, SecurityCategory Z. 111–117) einen Snapshot der bisherigen Werte ziehen und auf allen Fehlschlag-Pfaden (`created == null`, `updated == null`, `catch`) restaurieren. `ResetPendingAsync` nutzt danach direkt `Name`/`SymbolId` bzw. `Model` als Baseline | Hält die öffentlichen Modellwerte garantiert auf dem zuletzt geladenen/gespeicherten Stand — erfüllt das Akzeptanzkriterium auch nach fehlgeschlagenem Speichern ohne zusätzlichen `_loaded*`-Zustand und behebt nebenbei die bestehende Inkonsistenz, dass z. B. `Title => Name` nach einem Fehlschlag ungespeicherte Werte zeigt (SavingsPlanCategory Z. 42). Alternative „separate `_loadedName`/`_loadedSymbolId`-Felder" wäre gleichwertig, aber mehr Code ohne Mehrwert |
| 3 | Baseline bei `SecurityCardViewModel` | Gleicher Snapshot/Rollback-Ansatz: vor `ApplyPendingValuesToModel()` (Z. 211/525–559) einen Snapshot der `Model`-Felder ziehen und auf Fehlschlag-Pfaden restaurieren. `ResetPendingAsync` nutzt `Security` (`SecurityDto`, Z. 32) als Baseline und baut `CardRecord = await BuildCardRecordAsync(Security)` mit `Security != null`-Guard | `BuildCardRecordAsync` nutzt `dto?.Feld ?? Model.Feld`-Fallbacks (Z. 330–347); ohne Rollback könnten nach einem fehlgeschlagenen Save verworfene `Model`-Werte in Felder mit `dto.Feld == null` einfließen. `Security` wird nur in `LoadAsync`/`SaveAsync` gesetzt und trägt damit den persistierten Stand |
| 4 | Lookup-Nebenabrufe beim Reset | **Zulässig, keine Codeänderung**: `ContactCardViewModel.BuildCardRecordAsync` (`ContactCategories_ListAsync`, Z. 147) und `BudgetPurposeCardViewModel.BuildCardRecord` (Fire-and-forget `TryResolveAndSet*`, Z. 268–269) lösen Lookup-Calls aus — kein Caching eingeführt | Das Kriterium „kein Server-Neuladen" betrifft den Datensatz selbst; die Referenz macht dasselbe (`Contacts_GetAsync` im Build, `BankAccountCardViewModel` Z. 184). Beide Builds sind best-effort mit try/catch und überschreiben keine Pending-Werte |
| 5 | `SavingsPlanCardViewModel` — `Model`-Neuableitung | `ResetPendingAsync` leitet `Model` zusätzlich neu ab: Edit-Modus `Model = new SavingsPlanCreateRequest(dto.Name, dto.Type, dto.TargetAmount, dto.TargetDate, dto.Interval, dto.CategoryId, dto.ContractNumber)` aus `_loadedDto` (Muster `LoadAsync` Z. 160); Neuanlage `Model = new SavingsPlanCreateRequest(InitPrefill-Name, SavingsPlanType.OneTime, null, null, null, null, null)` (Muster Z. 144–145). Danach `CardRecord = await BuildCardRecordAsync(_loadedDto)` | `Model` wird bereits während der Eingabe mutiert (Z. 727–784) und ist kein Baseline-Träger; `_loadedDto` (`SavingsPlanDto`, Z. 110) trägt den zuletzt geladenen/gespeicherten Stand. `InitPrefill` bleibt über die Basisklasse verfügbar |
| 6 | `McpAccessCardViewModel` — Disabled-Bedingung und Umfang | `Disabled = Access is null \|\| !HasPendingChanges`; `ResetPendingAsync`: `ClearPendingChanges()` + `if (Access != null) CardRecord = BuildCardRecord(Access)` + `RaiseStateChanged()` (synchroner Build, Z. 672–715 — dennoch `async Task`-Methode analog Referenz). Scope-Matrix/Grants/Filter bleiben unberührt | Referenzmuster `!HasPendingChanges`, ergänzt um den `Access is null`-Guard analog zur `Save`-Aktion (Z. 746). Sofort-Persistierung der Scope-Matrix ist per Anwenderantwort akzeptiert |
| 7 | Einfügestil der `UiRibbonAction` | Jeweils im Codestil des vorhandenen Manage-Blocks (positionale Parameter bei `ContactCardViewModel`/`BankAccountCardViewModel`-Stil, benannte Parameter bei `SavingsPlanCardViewModel`-Stil Z. 828–855) | Konsistenz je Datei, minimaler Diff |
| 8 | Folge-Issue KI-Zugangsansicht | Verpflichtender Umsetzungsschritt: `gh issue create` in `martin-stromberg/FinanceManager` mit Verweis auf #593 | Verbindliche Nebenpflicht aus dem Issue; Inhalt siehe Umsetzungsreihenfolge Schritt 9 |

## Programmabläufe

### Reset-Fluss auf einer Karten-Detailseite (alle acht ViewModels identisch im Muster)

1. `CardPage.razor` rendert `Ribbon` mit dem ViewModel als `IRibbonProvider`; `Ribbon.BuildTabsToRender()` ruft `Provider.GetRibbonRegisters(Localizer)` auf → das überschriebene `GetRibbonRegisterDefinition` liefert die `Ribbon_Group_Manage`-Gruppe mit `Save`, **`Reset`**, `Delete`/`Archive`/…
2. Nutzer ändert ein editierbares Feld → `ValidateFieldValue`/`ValidateLookupField` schreibt `_pendingFieldValues` und ruft `RaiseStateChanged()` → `HasPendingChanges == true` → beim nächsten Ribbon-Aufbau ist `Reset` aktiviert (`Disabled` wird pro Render neu ausgewertet, `Ribbon.razor` Z. 164–170).
3. Nutzer klickt `Reset` → `Callback` ruft `ResetPendingAsync()`:
   - `ClearPendingChanges()` leert `_pendingFieldValues`.
   - `CardRecord` wird aus der Baseline neu aufgebaut (`BuildCardRecord(Async)` des jeweiligen ViewModels; `ApplyPendingValues` findet keine Pending-Werte mehr → persistierte Werte sichtbar; VM-spezifische Build-Seiteneffekte wie `ApplyIntervalDependentState`, `UpdateSourceFieldLookup`, Fire-and-forget-Lookups laufen wie beim initialen Build).
   - Baseline `null` → Guard überspringt den Neuaufbau; Ausnahmen → `catch` tolerant.
   - `RaiseStateChanged()` → Ribbon baut sich neu auf, `Reset` und `Save` sind wieder `Disabled`.
4. Kein Datensatz-GET; `ReloadAsync`/`InitializeAsync` werden nicht aufgerufen.

Beteiligte Klassen/Komponenten: die acht Card-ViewModels, `BaseCardViewModel` (`ClearPendingChanges`, `HasPendingChanges` — unverändert), `BaseViewModel` (`RaiseStateChanged` — unverändert), `Ribbon.razor`/`GenericCardPage.razor` (unverändert).

## Neue Klassen

Keine Produktiv-Klassen. Neue Testklassen:

- `FinanceManager.Tests/ViewModels/BudgetPurposeCardViewModelTests.cs` (bisher keine Testabdeckung des ViewModels)
- `FinanceManager.Tests/ViewModels/BudgetRuleCardViewModelTests.cs` (bisher keine Testabdeckung des ViewModels)
- `FinanceManager.Tests.E2E/Tests/Cards/CardResetPlaywrightTests.cs` (E2E-Nachweis des UI-Flusses)

## Änderungen an bestehenden Klassen

Gemeinsames Muster je ViewModel — zwei Ergänzungen:

- **`GetRibbonRegisterDefinition`**: in der `Ribbon_Group_Manage`-Aktionsliste **direkt hinter `Save`** einfügen:
  `new UiRibbonAction("Reset", localizer["Ribbon_Reset"].Value, "<svg><use href='icons/sprite.svg#undo'/></svg>", UiRibbonItemSize.Small, !HasPendingChanges, null, async () => { await ResetPendingAsync(); })` — **ohne** `MobileShortcut` (Designentscheidung 1).
- **Neue private Methode `ResetPendingAsync()`** nach dem Referenzmuster (`BankAccountCardViewModel` Z. 373–389): try/catch, `ClearPendingChanges()`, Baseline-Guard, `CardRecord`-Neuaufbau, `RaiseStateChanged()`.

Pro ViewModel im Einzelnen:

### 1. `ContactCardViewModel` (`FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs`)

- Ribbon-Insert: in der `manage`-Liste zwischen `Save` (Ende Z. 289) und `Delete` (Z. 290).
- `ResetPendingAsync`: `if (Contact != null) CardRecord = await BuildCardRecordAsync(Contact)` — Baseline `Contact` (`ContactDto`, Z. 34; Neuanlage-DTO inkl. `InitPrefill`-Name Z. 89–91). Lookup-Nebenabruf im Build akzeptiert (Designentscheidung 4).

### 2. `SavingsPlanCardViewModel` (`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`)

- Ribbon-Insert: in der `manage`-Liste nach `Save` (Ende Z. 836), vor `Delete` (Z. 837), im benannten-Parameter-Stil.
- `ResetPendingAsync`: `ClearPendingChanges()`; `Model` neu ableiten (Edit: aus `_loadedDto` nach Muster Z. 160; Neuanlage: initialer `SavingsPlanCreateRequest` mit `InitPrefill`-Name nach Muster Z. 144–145); `CardRecord = await BuildCardRecordAsync(_loadedDto)`; `RaiseStateChanged()`. Kein Guard nötig außer try/catch (`_loadedDto` darf `null` sein — `BuildCardRecordAsync(SavingsPlanDto? dto)` akzeptiert `null`).

### 3. `SavingsPlanCategoryCardViewModel` (`FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs`)

- Ribbon-Insert: zwischen `Save` (Z. 194) und `Delete` (Z. 195).
- **Save-Rollback** (Designentscheidung 2): am Anfang von `SaveAsync` (vor Z. 110) `var prevName = Name; var prevSymbolId = SymbolId;` sichern; auf allen Fehlschlag-Pfaden (Z. 123 `created == null`, Z. 129 `updated == null`, `catch` Z. 137–141) `Name = prevName; SymbolId = prevSymbolId;` restaurieren.
- `ResetPendingAsync`: `CardRecord = BuildCardRecord(Name, SymbolId)` (synchron, Z. 92–100). Neuanlage stellt automatisch `string.Empty`/`null` her (Z. 57–60).

### 4. `SecurityCardViewModel` (`FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs`)

- Ribbon-Insert: zwischen `Save` (Z. 373) und `Delete` (Z. 374).
- **Save-Rollback** (Designentscheidung 3): vor `ApplyPendingValuesToModel()` (Z. 211) Snapshot aller `Model`-Felder; auf Fehlschlag-Pfaden restaurieren.
- `ResetPendingAsync`: `if (Security != null) CardRecord = await BuildCardRecordAsync(Security)` — Baseline `Security` (`SecurityDto`, Z. 32; Neuanlage-DTO inkl. Prefill-Name Z. 151–155).

### 5. `SecurityCategoryCardViewModel` (`FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs`)

- Ribbon-Insert: zwischen `Save` (Z. 209) und `Delete` (Z. 210).
- **Save-Rollback**: Snapshot von `Model.Name`/`Model.SymbolAttachmentId` vor der Vorab-Mutation (Z. 111–117); Restauration auf Fehlschlag-Pfaden (Z. 124–127/134–139/catch).
- `ResetPendingAsync`: `CardRecord = BuildCardRecord(Model.Name, Model.SymbolAttachmentId)` (synchron, Z. 95–103). Neuanlage stellt `InitPrefill`-Name wieder her (Z. 59–63 — `Model` ist nach Rollback-Garantie stets Baseline).

### 6. `BudgetPurposeCardViewModel` (`FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs`)

- Ribbon-Insert: zwischen `Save` (Z. 460) und `Delete` (Z. 461).
- `ResetPendingAsync`: `if (Purpose != null) CardRecord = BuildCardRecord(Purpose)` — Baseline `Purpose` (`BudgetPurposeDto`, Z. 34; Neuanlage-DTO inkl. `InitPrefill`-Name Z. 52–64). `UpdateSourceFieldLookup` und die Fire-and-forget-Namensauflösung laufen im Build wie beim Initial-Build (Designentscheidung 4); `EmbeddedList` (`BudgetRuleListViewModel`) wird nicht neu initialisiert.

### 7. `BudgetRuleCardViewModel` (`FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs`)

- Ribbon-Insert: zwischen `Save` (Z. 394) und `Delete` (Z. 395).
- `ResetPendingAsync`: `if (Rule != null) CardRecord = BuildCardRecord(Rule)` — Baseline `Rule` (`BudgetRuleDto`, Z. 40; Neuanlage-DTO inkl. Prefill-Parent Z. 55–79). `ApplyIntervalDependentState` läuft im Build und stellt den konsistenten Interval-Zustand wieder her; die `ClearPurposePattern`-Aktion gesetzte Pending-Werte werden mitverworfen.

### 8. `McpAccessCardViewModel` (`FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs`)

- Ribbon-Insert: in der `manage`-Initialisierungsliste direkt nach `Save` (nach Z. 748) — vor den konditionalen `manage.Add(...)`-Blöcken (Z. 751 ff.).
- `Disabled = Access is null || !HasPendingChanges` (Designentscheidung 6).
- `ResetPendingAsync`: `ClearPendingChanges()`; `if (Access != null) CardRecord = BuildCardRecord(Access)` (synchron, Z. 672–715); `RaiseStateChanged()`. Scope-Matrix/Grants/Filter unberührt.

### 9. `BankAccountCardViewModel` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`) — Konsistenz-Angleichung

- Z. 291: `{ MobileShortcut = true }` an der `Reset`-Aktion entfernt (Designentscheidung 1). Aktion und `ResetPendingAsync` bleiben sonst unverändert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen — vorhandene `canSave`-Bedingungen (Namens-Validierung etc.) bleiben unverändert; `Reset` verwendet ausschließlich `!HasPendingChanges` (MCP: `Access is null || !HasPendingChanges`).

## Konfigurationsänderungen

Keine — Verhalten fest verdrahtet, nicht konfigurierbar.

## Seiteneffekte und Risiken

- **Save-Rollback verändert Fehlschlag-Verhalten sichtbar:** Nach einem fehlgeschlagenen Speichern zeigen `Name`/`Title`/`Model` wieder die persistierten Werte statt der verworfenen — die Pending-Werte im `CardRecord` bleiben unverändert sichtbar (`_pendingFieldValues` wird nicht angerührt). Das ist die fachlich gewünschte Baseline-Semantik; als Seiteneffekt zu nennen, weil `Title` (`SavingsPlanCategoryCardViewModel` Z. 42) sich bei Fehlschlag künftig korrekt verhält.
- **`Disabled` ist zeitpunktbezogen:** Wird beim Ribbon-Aufbau ausgewertet; `RaiseStateChanged()` am Ende von `ResetPendingAsync` sorgt für den sofortigen Wechsel zurück auf `Disabled` — analog zum Referenzverhalten.
- **Fire-and-forget-Lookups beim Neuaufbau** (`BudgetPurposeCardViewModel` Z. 268–269): laufen asynchron weiter und setzen Anzeigenamen best-effort; sie überschreiben keine Pending-Werte und gefährden den Reset nicht.
- **`McpAccessCardViewModel.BuildCardRecord` benötigt `IStringLocalizer<Pages>` im ServiceProvider** (Z. 674): Test-Setup muss das offene `IStringLocalizer<>` registrieren (Muster `McpAccessCardViewModelTests.CreateVm`, Z. 30–45).
- **Kein Bruch bestehender Ribbon-Tests:** Kein existierender Test enumeriert die Manage-Item-Liste exakt (`RibbonConfigTests` asserted nur `Linked`-Items, `SecurityCategoryDetailViewModelTests` sucht `Save` per `First`, `SavingsPlanCardLinkTests` asserted Tab-Titel-Sequenz, nicht Aktionen); `SetupCardViewModelTests` zählt Reset-Aktionen nur im Setup-Aggregat — unberührt.
- **`MobileShortcut`-Entfernung an `BankAccountCardViewModel.Reset`:** Mobiler Header der Manage-Gruppe zeigt künftig nur noch `Save` als Shortcut — gewünschte Regelkonformität; kein Test betroffen.
- **Neuanlage-Reset bei `SavingsPlanCategoryCardViewModel`:** `InitPrefill` wird dort nicht ausgewertet (Z. 57–60) — initialer Zustand ist leer; korrekt und dokumentiert, keine Abweichung zum Akzeptanzkriterium.

## Umsetzungsreihenfolge

1. **Baseline-Absicherung in den drei mutierenden ViewModels**
   - Voraussetzungen: Keine.
   - Beschreibung: Snapshot/Rollback in `SavingsPlanCategoryCardViewModel.SaveAsync` (vor Z. 110), `SecurityCategoryCardViewModel.SaveAsync` (vor Z. 111) und `SecurityCardViewModel.SaveAsync` (vor `ApplyPendingValuesToModel`, Z. 211) einbauen.

2. **`Reset`-Aktion + `ResetPendingAsync` in den acht ViewModels**
   - Voraussetzungen: Schritt 1 (für die drei betroffenen VMs).
   - Beschreibung: Pro ViewModel die `UiRibbonAction("Reset", …)` hinter `Save` einfügen und `ResetPendingAsync` nach den in „Änderungen an bestehenden Klassen" benannten Baselines implementieren; `MobileShortcut` an `BankAccountCardViewModel.Reset` entfernen.

3. **Unit-Tests schreiben/erweitern**
   - Voraussetzungen: Schritt 2; vorhandene Testinfrastruktur (`CreateVm`-Fabriken, `DummyLocalizer`, `FindRibbonAction`-Muster aus `AccountDetailViewModelTests` Z. 324–331, Callback-Invokation Z. 310–311).
   - Beschreibung: Tests gemäß Abschnitt „Tests" — je ViewModel Ribbon-Shape-, Disabled-, Verwerfungs-, Neuanlage- und Kein-API-Aufruf-Assertions; Fehlschlag-Save-Baseline-Tests für die drei Rollback-VMs; MCP-Sonderfall-Assertions.

4. **E2E-Tests schreiben**
   - Voraussetzungen: Schritt 2; `PlaywrightWebAppFixture`, `TestUserSeeder`, `AuthGateway`, `BrowserApiHelper`, `AccountsApiSeedHelper` vorhanden.
   - Beschreibung: `CardResetPlaywrightTests` gemäß E2E-Tabelle (Kontakt Happy Path inkl. Disabled-Umschaltung, Sparplan mit `Model`-Besonderheit, KI-Zugang-Sonderfall mit Admin-Enablement, Kontakt-Neuanlage). Screenshot-/HTML-Artefakte entstehen automatisch beim Session-Dispose (`PlaywrightBrowserSession` Z. 61–75).

5. **Unit- und Komponententests ausführen**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -- --filter-not-trait "Category=OsInterface"` — alle bestehenden plus neuen Tests müssen grün sein.

6. **E2E-Tests ausführen**
   - Voraussetzungen: Schritt 4; lokal installierte Playwright-Browser.
   - Beschreibung: `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -- --filter "FullyQualifiedName~CardReset"` — die neuen Tests müssen grün sein; die erzeugten Screenshot-Artefakte auf die sichtbare Rücksetzung sichtprüfen.

7. **Folge-Issue anlegen (verbindliche Nebenpflicht)**
   - Voraussetzungen: Keine — kann parallel zu Schritt 2 erfolgen, spätestens vor dem PR.
   - Beschreibung: `gh issue create --repo martin-stromberg/FinanceManager --title "KI-Zugangsansicht: Persistenzverhalten vereinheitlichen (Kartenfelder pending vs. Scope-Matrix sofort)" --body …` — Inhalt: Bezug zu #593; Befund, dass Scope-Matrix (`Mcp_SetScopesAsync`), Filter (`Mcp_SetFiltersAsync`) und Grants (`Mcp_SetApprovalGrantsAsync`) sofort persistieren, während Kartenfelder pending sind und `Reset` nur diese verwirft; Zielbild: konsistentes Persistenz-/Reset-Verhalten der Ansicht klären und vereinheitlichen. Issue-Nummer im PR-/Commit-Kontext referenzieren.

8. **Reale End-to-End-Abnahme (manuell, Pflicht gemäß Projektregeln)**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Anwendung starten, als Benutzer anmelden, mindestens drei Karten (z. B. Kontakt, Sparplan, KI-Zugang) öffnen, Felder ändern, `Zurücksetzen` klicken und die sichtbare Rücksetzung sowie den Disabled-Umschaltzyklus prüfen. Ergebnis in `test-results.md` dokumentieren; bei Nichtdurchführbarkeit an den Anwender eskalieren.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft |
|--------------------|------------|-------------------|
| `Ribbon_ContainsReset_AfterSave_InManageGroup` + `Reset_DiscardsPendingChanges` + `Reset_NewMode_RestoresPrefill` + `Reset_NoReload` | `FinanceManager.Tests/Web/ViewModels/Contacts/ContactCardViewModelTests.cs` (erweitern) | `Reset` in `Ribbon_Group_Manage` direkt nach `Save`, `Small`, `undo`-Icon im IconSvg, Label `Ribbon_Reset` (Echo-Localizer), `MobileShortcut` nicht gesetzt; nach `ValidateFieldValue` ist `Reset` enabled, Callback-Verwerfen stellt `CardRecord`-Baseline her und `HasPendingChanges == false`; Neuanlage mit `InitPrefill` stellt Prefill-Stand her; `Contacts_GetAsync` wird nicht erneut aufgerufen (`Times.Once` nach Load) |
| `Reset_DiscardsPending_RestoresPersistedState`, `Reset_NewMode_RestoresInitPrefill`, `Reset_AfterFailedSave_RestoresPersistedBaseline`, `Reset_ReDerivesModel` | `FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs` (erweitern) oder neue `SavingsPlanCardViewModelTests` | Neben Ribbon-Shape/Disabled: `Model.Name`/`Model.Type` nach Eingabe-Mutation durch Reset wieder auf `_loadedDto`-Werten; fehlgespeicherter Zustand führt nicht zu falschen Baselines; Neuanlage-`SavingsPlanCreateRequest` mit `InitPrefill`-Name |
| `Reset_*` (Shape, Disabled, Discard, FailedSave-Baseline, NewMode-Empty) | `FinanceManager.Tests/ViewModels/SavingsPlanCategoryDetailViewModelTests.cs` (erweitern, bisher nur Smoke Z. 75–80) | Insb.: nach simuliertem Fehlschlag (`SavingsPlanCategories_UpdateAsync` → `null`) stellt Reset den persistierten Namen her (`Name`/`SymbolId` wurden zurückgerollt) |
| `Reset_*` (Shape, Disabled, Discard, FailedSave-ModelRollback, NewMode-Prefill) | `FinanceManager.Tests/Web/ViewModels/Securities/SecurityCardViewModelTests.cs` (erweitern) | Baseline `Security`-DTO; `Model`-Fallback-Felder zeigen nach fehlgeschlagenem Save + Reset die persistierten Werte |
| `Reset_*` (Shape, Disabled, Discard, FailedSave-Baseline, NewMode-Prefill) | `FinanceManager.Tests/ViewModels/SecurityCategoryDetailViewModelTests.cs` (erweitern) | Analog Sparplankategorie inkl. `InitPrefill`-Neuanlage (Z. 59–63) |
| `Reset_*` (Shape, Disabled, Discard, NewMode-Prefill, NoReload) | `FinanceManager.Tests/ViewModels/BudgetPurposeCardViewModelTests.cs` (**neu**) | Baseline `Purpose`; SourceType/SourceId-Konsistenz nach Neuaufbau; Neuanlage-Prefill |
| `Reset_*` (Shape, Disabled, Discard, NewMode-Prefill-Parent, ClearPurposePattern-Verwerfung) | `FinanceManager.Tests/ViewModels/BudgetRuleCardViewModelTests.cs` (**neu**) | Baseline `Rule`; Interval-abhängiger Feldzustand konsistent; Pattern-Pending-Werte werden mitverworfen |
| `Reset_*` (Shape, Disabled `Access is null \|\| !HasPendingChanges`, Discard-Nur-Kartenfelder, Kein-Scope-Call, Null-Access-Guard) | `FinanceManager.Tests/ViewModels/McpAccessCardViewModelTests.cs` (erweitern) | Nach Reset: `HasPendingChanges == false`, `CardRecord` zeigt `Access`-Werte; `Mcp_SetScopesAsync`/`Mcp_SetFiltersAsync`/`Mcp_SetApprovalGrantsAsync` werden **nicht** aufgerufen (Moq `Times.Never`); mit `Access == null` keine Exception |
| `ResetRibbonAction_*`-Konsistenztest (optionaler parametrierter Gemeinschaftstest über alle acht VMs: Aktion hinter `Save`, Small, Icon, Label, kein `MobileShortcut`) | Neue Hilfsklasse `FinanceManager.Tests/ViewModels/CardResetRibbonConsistencyTests.cs` | Verhindert zukünftiges Abweichen vom vereinbarten Shape |

### Betroffene bestehende Tests

Keine brechenden Anpassungen erwartet — kein bestehender Test enumeriert die Manage-Item-Liste exakt (Details inventory/tests-resources-excluded.md §1). Bei unerwarteten Treffern: Assertions auf die neue Aktion erweitern, nicht entfernen. `SetupCardViewModelTests` (genau eine `Reset`-Aktion im Setup-Aggregat, Z. 208–218) bleibt unberührt, da `SetupCardViewModel` nicht geändert wird.

### E2E-Tests (primärer Funktionsnachweis, inkl. Screenshot-Nachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Kontakt-Karte: Feld ändern → `Reset` aktiv → Klick → ursprünglicher Wert sichtbar, `Save`/`Reset` wieder deaktiviert | `FinanceManager.Tests.E2E/Tests/Cards/CardResetPlaywrightTests.cs` — `ContactCard_Reset_RestoresPersistedValues` | Happy Path Ribbon-Klick → sichtbare Rücksetzung + Disabled-Umschaltung | Nur der echte Browser-Rendervorgang beweist sichtbare Feld-Rücksetzung, Ribbon-Aktualisierung (`RaiseStateChanged`) und `Disabled`-Umschaltung; Seed: `POST /api/contacts`; Route `card/contacts/{id}`; Feld: `.card-input` in der Name-Zeile (`tr:has(th)` mit Name-Caption); Reset-Button `button#Reset`; Screenshot-Artefakt via `PlaywrightBrowserSession`-Dispose |
| Pflicht | Sparplan-Karte: Name/Type-Änderung → `Reset` → persistierte Werte sichtbar (`Model`-Neuableitung) | `CardResetPlaywrightTests` — `SavingsPlanCard_Reset_RestoresPersistedValues` | Reset bei input-mutiertem `Model`; Route `card/savings-plans/{id}`; Seed `POST /api/savings-plans` (`SavingsPlanCreateRequest`-Muster aus `RibbonGroupOrderPlaywrightTests`) | Deckt den komplexesten Baseline-Fall (Type-abhängige Feldform, `Model`-Neuableitung) am lebenden System ab |
| Pflicht | Kontakt-Neuanlage: Prefill-/Edit-Werte → `Reset` → initialer (leerer/Prefill-)Zustand, kein Server-Call, kein Datensatz angelegt | `CardResetPlaywrightTests` — `ContactCardNew_Reset_RestoresInitialState` | Neuanlage-Akzeptanzszenario; Route `card/contacts/new` | Beweist, dass Reset im New-Modus nicht persistiert/löscht und den Initialzustand wiederherstellt |
| Pflicht | KI-Zugang (MCP): Kartenfeld ändern → `Reset` → Kartenfeld zurückgesetzt; Scope-Matrix-Stand bleibt bestehen | `CardResetPlaywrightTests` — `McpAccessCard_Reset_KeepsScopes` | MCP-Sonderfall; Admin-Login, `PUT /api/mcp-audit/settings` (Enable), `POST /api/mcp-accesses` (`McpAccessCreateRequest`), Route `card/mcp-accesses/{id}`; Scope-Zustand nach Sofort-Persistierung per UI-Checkbox-Matrix beobachten bzw. via `GET /api/mcp-accesses/{id}` verifizieren | Sonderverhalten „nur Kartenfelder" ist nur im Gesamtsystem glaubwürdig nachweisbar (Sofort-Persistierung + partieller Reset) |
| Pflicht | Reale Abnahme: drei Karten manuell durchgehen | Manuell (Schritt 8), dokumentiert in `test-results.md` | Alle Akzeptanzszenarien am lebenden System | Projektregel: reale End-to-End-Abnahme zusätzlich zu automatisierten Tests |

Welche bestehenden E2E-Tests müssen angepasst werden? — Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path — Edit-Modus | Nutzer klickt `Zurücksetzen` im Ribbon | Gespeicherter Datensatz geladen; mind. ein Feld geändert (`HasPendingChanges == true`, `Save`/`Reset` aktiviert) | Feld ändern → `Reset` klicken | Karte zeigt den zuletzt geladenen/gespeicherten Wert; `Save` und `Reset` sind wieder deaktiviert; kein Datensatz-Reload (kein Netzwerk-GET auf die Entität) | E2E `ContactCard_Reset_RestoresPersistedValues` + Unit-Tests je VM + Screenshot-Artefakt + reale Abnahme |
| 2 | Neuanlage | Nutzer klickt `Zurücksetzen` auf `card/<kind>/new` | Neuanlage-Karte mit `InitPrefill`- bzw. Leerzustand, Felder geändert | Felder ändern → `Reset` klicken | Karte stellt den initialen Neuanlage-Zustand wieder her (Prefill-Name bzw. leer); kein API-Aufruf, kein Datensatz angelegt/gelöscht | E2E `ContactCardNew_Reset_RestoresInitialState` + Unit-`Reset_NewMode_*` je VM |
| 3 | Disabled-Zustand | Karte ohne ungespeicherte Änderungen | Datensatz geladen oder Ladefehler (`HasPendingChanges == false`) | Ribbon betrachten; Feld ändern; erneut betrachten | `Reset` ist ohne Pending-Changes deaktiviert, wird nach erster Feldänderung aktiviert und nach Reset/Save wieder deaktiviert | E2E `ContactCard_Reset_*` (Disabled-Assertions vor/nach Edit und nach Klick) + Unit-Disabled-Tests |
| 4 | Fehler-/Null-Modell | Ladefehler (Datensatz nicht gefunden / API-Fehler) | Baseline `null` (`Contact`/`Security`/`Purpose`/`Rule`/`Access == null`), ggf. Fehlerhinweis auf der Karte | Ribbon betrachten; `Reset` ggf. per Test-Invokation aufrufen | `Reset` deaktiviert (`!HasPendingChanges`, MCP: `Access is null`); direkte Invokation wirft keine Exception (Guard + try/catch), Zustand bleibt stabil | Unit-Tests `Reset_NullBaseline_NoThrow` je VM (Reflection/Callback-Invokation) |
| 5 | Fehlgespeicherter Stand (Baseline-Rollback) | Speichern schlägt fehl (API liefert `null`/Exception) | Editierter Datensatz, `Save` fehlgeschlagen, Pending-Werte weiterhin sichtbar | `Save` scheitern lassen → `Reset` klicken | Karte stellt den **zuletzt persistierten** Stand her — nicht die vor dem API-Aufruf ins Modell kopierten Werte (betrifft `SavingsPlanCategory`, `SecurityCategory`, `Security`; `SavingsPlan` via `Model`-Neuableitung aus `_loadedDto`) | Unit-`Reset_AfterFailedSave_*` in den drei Rollback-VMs + Sparplan |
| 6 | MCP-Sonderfall | `Reset` auf der KI-Zugangskarte | MCP-Zugang geladen; Kartenfelder geändert; Scope-Matrix wurde vorher geändert (sofort persistiert) | Kartenfeld ändern → `Reset` klicken | Kartenfelder (Name, Beschreibung, Level, ApprovalMode, ExpiresUtc, AllowedIpRanges) zeigen persistierte Werte; Scope-Matrix-Stand bleibt erhalten (kein `Mcp_Set*`-Aufruf durch Reset) | E2E `McpAccessCard_Reset_KeepsScopes` + Unit (`Times.Never` auf `Mcp_Set*Async`) + Folge-Issue für das Aufräumen |
| 7 | Alle acht Ansichten konsistent | Ribbon-Aufbau auf jeder der acht Detailseiten | Jeweils Datensatz geladen | Ribbon der acht Routen prüfen (`card/contacts/{id}`, `card/savings-plans/{id}`, `card/savings-plans/categories/{id}`, `card/securities/{id}`, `card/securities/categories/{id}`, `card/budget/purposes/{id}`, `card/budget/rules/{id}`, `card/mcp-accesses/{id}`) | `Reset` sitzt in `Ribbon_Group_Manage` direkt hinter `Save`, Small, `undo`-Icon, lokalisiertes Label, kein mobiler Shortcut | Konsistenztest `CardResetRibbonConsistencyTests` + reale Abnahme |

## Offene Punkte

Keine — alle offenen Fragen aus `requirement.md` wurden anhand der Bestandsaufnahme als Designentscheidungen getroffen:

1. `MobileShortcut` auf `Reset` → **ungesetzt** (Regel: primäre Aktion = `Save`); Referenz `BankAccountCardViewModel` wird regelkonform nachgezogen (Designentscheidung 1).
2. Separater persistierter Stand für die Kategorie-ViewModels → **erforderlich**, umgesetzt als Snapshot/Rollback im Save-Pfad statt zusätzlicher Baseline-Felder (Designentscheidungen 2/3).
3. „Kein Server-Neuladen" bei `ContactCardViewModel.BuildCardRecordAsync` → **ja**, betrifft den Datensatz; Lookup-Nebenabrufe bleiben referenzkonform zulässig, kein Caching (Designentscheidung 4).
4. Folge-Issue-Titel/-Beschreibung → **festgelegt** (Umsetzungsreihenfolge Schritt 7); Anlage ist verbindlicher Umsetzungsschritt.
