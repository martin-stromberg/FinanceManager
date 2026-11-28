# Detailinventar: Die acht In-Scope-ViewModels

Faktenstand aus dem Quellcode (Stand dieser Bestandsaufnahme). Je ViewModel: Ribbon-Manage-Gruppe,
Baseline-Modell für den Reset, `CardRecord`-Neuaufbau, Save-Pfad-Besonderheiten, Neuanlage-Modus.

Keines der acht ViewModels besitzt heute eine `Reset`-Ribbon-Aktion oder eine `ResetPendingAsync`-Methode
(verifiziert per Volltextsuche nach `"Reset"` bzw. `UiRibbonAction("Reset"` in `FinanceManager.Web/ViewModels`).

---

## 1. ContactCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` (360 Zeilen)

- **Baseline:** `public ContactDto? Contact { get; private set; }` (Z. 34)
- **Neuanlage:** `LoadAsync(Guid.Empty)` setzt `Contact = new ContactDto(Guid.Empty, name, ContactType.Organization, null, null, false, null)` mit `name` aus `InitPrefill` (Z. 87–93). Der initiale Zustand liegt damit vollständig im DTO vor.
- **CardRecord-Neuaufbau:** `private async Task<CardRecord> BuildCardRecordAsync(ContactDto c)` (Z. 140–166).
  - **Nebeneffekt:** ruft `ApiClient.ContactCategories_ListAsync()` auf, wenn `c.CategoryId` gesetzt ist (Z. 143–152, Best-Effort mit try/catch). Ein Reset löst hier also einen Lookup-Abruf aus — keine Datensatz-Reload, aber ein API-Call. (Offene Frage 3 der requirement.md.)
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 253–307):
  - `nav` Tab `Ribbon_Group_Navigation`, Sort 0 (Z. 255–258) mit `Back` (MobileShortcut).
  - `manage` Tab `Ribbon_Group_Manage`, Sort 10 (Z. 260–295):
    - `Save` Large, `Disabled = !HasPendingChanges`, `MobileShortcut = true` (Z. 262–289); der Callback führt Create/Update inline aus (`Contacts_CreateAsync`/`Contacts_UpdateAsync`), danach `BuildCardRecordAsync` + `ClearPendingChanges()`.
    - `Delete` Small, `Disabled = Contact==null` (Z. 290).
    - `OpenMerge` Small, `Disabled = Contact==null` (Z. 291–294).
  - `linked` Tab `Ribbon_Group_Linked`, Sort 55 (Z. 297–304): `OpenPostings`, `Attachments`.
- **Insert-Position für Reset:** zwischen `Save` (ende Z. 289) und `Delete` (Z. 290) in der `manage`-Liste.
- **Reset-Baseline:** `ClearPendingChanges()` + `CardRecord = await BuildCardRecordAsync(Contact)` mit `if (Contact != null)`-Guard (Referenzmuster).
- **Besonderheit:** `ValidateLookupField`-Override für `Enum:ContactType` schreibt den Lookup-Namen als pending Text (Z. 202–211) — wird durch `ClearPendingChanges()` korrekt verworfen.
- **Sonstiges:** `ReloadAsync` (Z. 216–219) ruft `InitializeAsync(Id)` — Reload wäre ein Server-Neuladen und ist für Reset **nicht** zu verwenden.

## 2. SavingsPlanCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` (967 Zeilen)

- **Baselines (zwei Ebenen):**
  - `private SavingsPlanDto? _loadedDto` (Z. 110) — zuletzt geladenes/gespeichertes DTO; wird in `LoadAsync` (Z. 165) und in `SaveAsync` (Z. 410/421) gesetzt.
  - `public SavingsPlanCreateRequest Model { get; private set; }` (Z. 91–92) — wird **bereits während der Eingabe mutiert**:
    - `ValidateFieldValue` Name (Z. 727–733), TargetDate (Z. 735–743).
    - `ValidateLookupField` Category (Z. 781–784).
    - `SaveAsync` setzt `Model = req` nach erfolgreichem Speichern (Z. 409/420); `req` stammt aus `BuildCreateRequestFromPending()` (Z. 436–538).
  - Konsequenz: `Model` ist **kein** verlässlicher Baseline-Träger. Edit-Modus: `Model` muss beim Reset aus `_loadedDto` neu abgeleitet werden (Muster: `LoadAsync` Z. 160). Neuanlage: `Model` auf den initialen `SavingsPlanCreateRequest` zurücksetzen — Initialname aus `InitPrefill` (Z. 144–145); `InitPrefill` ist im ViewModel über die Basisklasse noch verfügbar.
- **CardRecord-Neuaufbau:** `private async Task<CardRecord> BuildCardRecordAsync(SavingsPlanDto? dto)` (Z. 567–683).
  - Nutzt `dto`-Werte mit `Model`-Fallback; berücksichtigt pending Type/Category für Feldform (Interval-Feld nur bei `Recurring`, Z. 601/643–646; TargetDate-Feld nur wenn nicht `Open`, Z. 602/650–653); Kategoriename aus `Categories`-Liste (Z. 621–625, kein API-Abruf — `Categories` ist gecacht, `LoadCategoriesAsync` Z. 206–210).
  - Vorhandener Helfer `private async Task RebuildCardRecordAsync()` (Z. 756–766): try/catch + `CardRecord = await BuildCardRecordAsync(_loadedDto)` + `RaiseStateChanged()` — nahe am Reset-Muster, aber ohne `ClearPendingChanges()` und ohne `Model`-Neuableitung.
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 801–900):
  - `canSave` (Z. 803–809): `!string.IsNullOrWhiteSpace(effectiveName) && effectiveName.Trim().Length >= 2 && HasPendingChanges`.
  - `nav` Sort 0 (Z. 812–823).
  - `manage` Tab `Ribbon_Group_Manage`, Sort 10 (Z. 826–855): `Save` Large mit `Disabled = !canSave`, `MobileShortcut = true` (Z. 828–836); `Delete` Small `Disabled = !IsEdit` (Z. 837–845); `Archive` Small `Disabled = !IsEdit` (Z. 846–854). Reihenfolge in der Liste: Save, Delete, Archive.
  - `linked` Sort 55 (Z. 858–863): `Category`, `OpenPostings`, `OpenAttachments`.
  - `analysis` Sort 40 (Z. 866–877): `Recalculate`.
  - `bookings` Sort 45 (Z. 882–893): `ActualReport`.
  - Tab-Liste `{ nav, manage, analysis, bookings, linked }` (Z. 895); zusätzlich `baseRegs` des Basis-VMs werden angehängt (Z. 897–899).
- **Insert-Position für Reset:** in der `manage`-Liste direkt nach dem `Save`-Eintrag (nach Z. 836), vor `Delete`.
- **Disabled-Bedingung:** Referenzmuster `!HasPendingChanges`; beachten, dass hier `Save` zusätzlich die Namens-Validierung verlangt — für `Reset` ist `!HasPendingChanges` ausreichend und deckt den Zweck ab.
- **Besonderheit:** `_ = RebuildCardRecordAsync()` wird bei Type-/Interval-Änderung aufgerufen (Z. 745–749, 788–792) — der Neuaufbau ist asynchron und feuert `_ =`-Fire-and-forget; der Reset-Pfad sollte `await`en.

## 3. SavingsPlanCategoryCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs` (231 Zeilen)

- **Baseline:** `public string Name` (Z. 32) und `public Guid? SymbolId` (Z. 37) — kein DTO.
- **CardRecord-Neuaufbau:** `private CardRecord BuildCardRecord(string name, Guid? symbolId)` (Z. 92–100) — synchron, keine API-Aufrufe.
- **Save-Pfad-Besonderheit (persistierter Stand vs. Modell):** `SaveAsync` (Z. 107–142) kopiert **vor** dem API-Aufruf `CardRecord`-Werte in `Name`/`SymbolId` (Z. 110–116). Schlägt `SavingsPlanCategories_CreateAsync`/`_UpdateAsync` fehl (Z. 122–131: `return false`, ohne Reload), enthalten `Name`/`SymbolId` ungespeicherte Werte. Erst bei Erfolg lädt `await LoadAsync(Id)` (Z. 132) den persistierten Stand neu.
  - Konsequenz für Reset: `Name`/`SymbolId` allein sind auf dem Fehlerpfad **kein** Baseline des zuletzt persistierten Stands. Um das Akzeptanzkriterium „zuletzt geladener/gespeicherter Zustand" auf dem Fehlerpfad zu erfüllen, ist ein separater Baseline-Speicher (z. B. private Felder `_loadedName`/`_loadedSymbolId`, gesetzt in `LoadAsync` und nach erfolgreichem Save) erforderlich (offene Frage 2 der requirement.md — durch Code belegt).
- **Neuanlage:** `LoadAsync(Guid.Empty)` setzt `Name = string.Empty; SymbolId = null` (Z. 57–60); `InitPrefill` wird hier **nicht** ausgewertet (anders als bei den anderen Karten).
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 184–199):
  - `nav` Sort 0 (Z. 186–189).
  - `canSave = CardRecord != null && HasPendingChanges` (Z. 191).
  - `manage` Sort 10 (Z. 192–196): `Save` Large `Disabled = !canSave`, `MobileShortcut = true` (Z. 194); `Delete` Small `Disabled = Id==Guid.Empty` (Z. 195).
- **Insert-Position für Reset:** zwischen `Save` (Z. 194) und `Delete` (Z. 195).
- **Sonstiges:** `LoadAsync` ruft `ClearPendingChanges()` (Z. 76) und `SaveAsync` nach Erfolg `await LoadAsync(Id)` + `ClearPendingChanges()` (Z. 132–134).

## 4. SecurityCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs` (560 Zeilen)

- **Baseline:** `public SecurityDto? Security { get; private set; }` (Z. 32). `Security` wird nur in `LoadAsync` (Z. 152/167) und `SaveAsync` (Z. 226/243) gesetzt → trägt den zuletzt geladenen/gespeicherten Stand.
- **Zweitmodell:** `public EditModel Model { get; }` (Z. 43–63, 82) — Felder Name/Identifier/Description/AlphaVantageCode/CurrencyCode/CategoryId/SymbolAttachmentId/Region/Sector.
  - `LoadAsync` füllt `Model` **nicht** aus dem geladenen DTO (nur `Model.Name` im Neuanlage-Pfad, Z. 151).
  - `SaveAsync` ruft `ApplyPendingValuesToModel()` (Z. 211; Implementierung Z. 525–559) auf, das pending Werte in `Model` schreibt — **vor** dem API-Aufruf und ohne Rollback bei Fehlschlag.
  - `SaveAsync` ruft nach erfolgreichem Speichern **kein** `ClearPendingChanges()` auf (Z. 204–261 verifiziert) — `HasPendingChanges` bleibt nach dem Speichern ggf. `true`.
- **Neuanlage:** `LoadAsync(Guid.Empty)` setzt `Model.Name = InitPrefill`-Wert und `Security = new SecurityDto { Id = Guid.Empty, Name = Model.Name }` (Z. 148–156) plus `LoadCategoriesAsync()`.
- **CardRecord-Neuaufbau:** `private async Task<CardRecord> BuildCardRecordAsync(SecurityDto? dto)` (Z. 330–347) — nutzt `dto?.Feld ?? Model.Feld`-Fallbacks; Kategoriename aus `Categories`-Liste (Z. 338, gecacht, `LoadCategoriesAsync` Z. 194–197). Kein API-Abruf im Build selbst.
  - Nebenbemerkung: da `Model` durch `ApplyPendingValuesToModel` mutiert werden kann, können in Feldern mit `dto == null` (Neuanlage) bzw. `dto.Feld == null` verworfene `Model`-Werte in den Neuaufbau einfließen — die Baseline über `Security` allein deckt den Neuanlage-Fehlerpfad nicht vollständig ab; `Model` müsste beim Reset ggf. ebenfalls zurückgesetzt werden.
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 355–392):
  - `hasRequiredValues`/`canSave` (Z. 359–362): `HasPendingChanges` + Name & Identifier im `CardRecord` nicht leer.
  - `Navigation` Sort 0 (Z. 366–369).
  - `Ribbon_Group_Manage` Sort 10 (Z. 371–376): `Save` Large `Disabled = !canSave`, `MobileShortcut = true` (Z. 373); `Delete` Small `Disabled = !(Id != Guid.Empty && Security != null && !Security.IsActive)` (Z. 374); `Archive` Small `Disabled = !(Id != Guid.Empty && Security != null && Security.IsActive)` (Z. 375). Reihenfolge: Save, Delete, Archive.
  - `Ribbon_Group_Linked` Sort 55 (Z. 378–383): `Postings`, `Attachments`, `Prices`.
  - `Ribbon_Group_Reports` Sort 70 (Z. 385–388): `Performance`.
- **Insert-Position für Reset:** in der Manage-Liste zwischen `Save` (Z. 373) und `Delete` (Z. 374).
- **Reset-Baseline:** `ClearPendingChanges()` + `CardRecord = await BuildCardRecordAsync(Security)` mit `Security != null`-Guard; für Neuanlage stellt `Security` den Prefill-Stand bereit (`dto?.Name` greift vor `Model.Name`).

## 5. SecurityCategoryCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs` (263 Zeilen)

- **Baseline:** `public EditModel Model { get; }` (Z. 33; `EditModel` Z. 256–263 mit `Name`, `SymbolAttachmentId`) — `get`-only Property, Felder werden in-place mutiert.
- **Save-Pfad-Besonderheit:** analog `SavingsPlanCategoryCardViewModel`: `SaveAsync` (Z. 109–152) kopiert `CardRecord`-Werte **vor** dem API-Aufruf in `Model.Name`/`Model.SymbolAttachmentId` (Z. 111–117); bei Fehlschlag (`created == null` Z. 124–127 / `dto == null` Z. 134–139) enthält `Model` ungespeicherte Werte. Erfolgspfad: `await LoadAsync(Id)` (Z. 142) + `ClearPendingChanges()` (Z. 144).
  - Konsequenz: gleicher separater Baseline-Bedarf wie bei Sparplankategorie (offene Frage 2 der requirement.md — durch Code belegt).
- **Neuanlage:** `LoadAsync(Guid.Empty)` setzt `Model.Name = InitPrefill ?? string.Empty; Model.SymbolAttachmentId = null` (Z. 59–63).
- **CardRecord-Neuaufbau:** `private CardRecord BuildCardRecord(string name, Guid? symbolId)` (Z. 95–103) — synchron.
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 198–214):
  - `nav` Sort 0 (Z. 200–203).
  - `canSave = CardRecord != null && HasPendingChanges` (Z. 205).
  - `manage` Sort 10 (Z. 207–211): `Save` Large `Disabled = !canSave`, `MobileShortcut = true` (Z. 209); `Delete` Small `Disabled = Id == Guid.Empty` (Z. 210).
- **Insert-Position für Reset:** zwischen `Save` (Z. 209) und `Delete` (Z. 210).

## 6. BudgetPurposeCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs` (660 Zeilen)

- **Baseline:** `public BudgetPurposeDto? Purpose { get; private set; }` (Z. 34); gesetzt in `LoadAsync` (Z. 55/67) und `SaveAsync` (Z. 569/591).
- **Neuanlage:** `LoadAsync(Guid.Empty)` setzt `Purpose = new BudgetPurposeDto(Guid.Empty, Guid.Empty, name, null, BudgetSourceType.ContactGroup, Guid.Empty, null)` mit `name` aus `InitPrefill` (Z. 52–64); zusätzlich `EmbeddedList`-Initialisierung (`BudgetRuleListViewModel`, Z. 50/59–62) — die Liste braucht beim Reset nicht neu initialisiert zu werden.
- **CardRecord-Neuaufbau:** `private CardRecord BuildCardRecord(BudgetPurposeDto dto)` (Z. 235–272):
  - ruft `ApplyPendingValues(ApplyEnumTranslations(record))` (Z. 263),
  - setzt `UpdateSourceFieldLookup(record)` (Z. 265) — LookupType des `SourceId`-Felds aus dem aktuellen `SourceType`,
  - startet zwei Fire-and-forget-Lookup-Abrufe zur Namensauflösung (`_ = TryResolveAndSetSourceNameAsync(record, dto)` Z. 268 → `Contacts_GetAsync`/`ContactCategories_ListAsync`/`SavingsPlans_ListAsync` je SourceType, Z. 308–362; `_ = TryResolveAndSetCategoryNameAsync(record, dto)` Z. 269 → `Budgets_ListCategoriesAsync`, Z. 274–306). Beide best-effort mit try/catch und überschreiben keine Pending-Werte (Z. 289–292/323–327).
  - Konsequenz: ein Reset löst (wie beim Kontakt) Lookup-Nebenabrufe aus, kein Datensatz-Reload.
- **Feldabhängigkeit:** `ValidateFieldValue`-Override (Z. 493–522) und `ValidateLookupField`-Override (Z. 382–416) räumen bei `SourceType`-Änderung das `SourceId`-Feld (`ClearSourceField`, `_pendingFieldValues.Remove`) und schalten dessen `LookupType` um — der Neuaufbau via `BuildCardRecord(Purpose)` stellt diesen konsistenten Ausgangszustand wieder her.
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 451–490):
  - `nav` Sort 0 (Z. 453–456).
  - `manage` Sort 10 (Z. 458–462): `Save` Large `Disabled = !HasPendingChanges`, `MobileShortcut = true` (Z. 460); `Delete` Small `Disabled = Id == Guid.Empty` (Z. 461).
  - `linked` Sort 55 (Z. 464–484): `Category`.
- **Insert-Position für Reset:** zwischen `Save` (Z. 460) und `Delete` (Z. 461).
- **Reset-Baseline:** `ClearPendingChanges()` + `CardRecord = BuildCardRecord(Purpose)` mit `Purpose != null`-Guard (synchron, kein await nötig — trotzdem in `ResetPendingAsync` als Task-Methode analog Referenz möglich).

## 7. BudgetRuleCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs` (553 Zeilen)

- **Baseline:** `public BudgetRuleDto? Rule { get; private set; }` (Z. 40); gesetzt in `LoadAsync` (Z. 78/83) und `SaveAsync` (Z. 236/250/271).
- **Neuanlage:** `LoadAsync(Guid.Empty)` parst `InitPrefill` (`"purpose:{guid}"`, `"category:{guid}"` oder Legacy-Guid, Z. 55–75) und setzt `BudgetPurposeId`/`BudgetCategoryId` sowie `Rule` als initialen DTO (Z. 77–79).
- **CardRecord-Neuaufbau:** `private CardRecord BuildCardRecord(BudgetRuleDto dto)` (Z. 128–148):
  - `ApplyPendingValues(ApplyEnumTranslations(record))` (Z. 142),
  - `ApplyIntervalDependentState(record, …)` (Z. 145) — schaltet `CustomIntervalMonths` abhängig vom Interval ab/zu und entfernt ggf. dessen Pending-Wert (Z. 513–529). Kein API-Abruf.
- **Feldabhängigkeit:** `ValidateLookupField`/`ValidateFieldValue` für `Card_Caption_BudgetRule_Interval` (Z. 465–511) aktualisieren `ApplyIntervalDependentState` — Neuaufbau stellt konsistenten Zustand wieder her.
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 362–414):
  - `nav` Sort 0 (Z. 364–390) mit `Back`-Callback, der `InitBack`/`BudgetPurposeId`-Fallback nutzt.
  - `manage` Sort 10 (Z. 392–396): `Save` Large `Disabled = !HasPendingChanges`, `MobileShortcut = true` (Z. 394); `Delete` Small `Disabled = Id == Guid.Empty` (Z. 395).
  - `pattern` Tab `Ribbon_Group_Fields`, Sort 47 (Z. 398–411): `ClearPurposePattern` (setzt Pending-Werte für Pattern/UseRegex, Z. 534–552 — ein Reset verwirft auch diese Aktion sauber).
- **Insert-Position für Reset:** zwischen `Save` (Z. 394) und `Delete` (Z. 395).
- **Reset-Baseline:** `ClearPendingChanges()` + `CardRecord = BuildCardRecord(Rule)` mit `Rule != null`-Guard.

## 8. McpAccessCardViewModel

**Datei:** `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs` (787 Zeilen)

- **Baseline:** `public McpAccessDetailDto? Access { get; private set; }` (Z. 39); gesetzt in `LoadAsync` (Z. 93/106/119).
- **Konstruktor:** `McpAccessCardViewModel(IServiceProvider sp, IApiClient apiClient)` (Z. 30–33) — `IApiClient` wird direkt injiziert (`_api`), nicht über `ServiceProvider` aufgelöst; relevant für das Test-Setup (siehe `McpAccessCardViewModelTests.CreateVm`).
- **Kein Neuanlage-Modus:** `LoadAsync(Guid.Empty)` setzt `Access = null` + Fehler `Err_NotFound` (Z. 91–96); Erstellung ausschließlich über `McpAccessCreateWizard` (Kommentar Z. 80–82). `Guid.Empty`-Reset-Pfad entfällt damit; `Access`-Guard genügt.
- **CardRecord-Neuaufbau:** `private CardRecord BuildCardRecord(McpAccessDetailDto? a)` (Z. 672–715) — synchron; nutzt `ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>()` (Z. 674); `ApplyPendingValues(ApplyEnumTranslations(record))` (Z. 714). Kein API-Abruf. Editable-Flag: `a is not null && Status != Revoked` (Z. 675).
- **Sonderverhalten Sofort-Persistierung (Scope-Matrix/Grants):**
  - `SaveScopesAsync` (Z. 276–309) → `Mcp_SetScopesAsync` sofort, danach `LoadAsync` (Z. 301).
  - `SaveScopeFiltersAsync` (Z. 320–339) → `Mcp_SetFiltersAsync` sofort.
  - `SaveApprovalGrantsAsync` (Z. 351–394) → `Mcp_SetApprovalGrantsAsync` sofort.
  - `StartIndividualEditAsync` (Z. 223–264) → `Mcp_UpdateAccessAsync` + `Mcp_SetScopesAsync`, danach `ClearPendingChanges()` (Z. 251) + `LoadAsync`.
  - Konsequenz (per Anforderung akzeptiert): Reset verwirft nur die Karten-Pending-Felder (`Mcp_Card_Name`, `Mcp_Card_Description`, `Mcp_Card_Level`, `Mcp_Card_ApprovalMode`, `Mcp_Card_ExpiresUtc`, `Mcp_Card_AllowedIpRanges` — Keys in `BuildCardRecord` Z. 690–701 und `GetPendingText`/`Resolve*` Z. 140–212); bereits persistierte Scope-/Grant-Änderungen bleiben bestehen.
- **Save-Pfad:** `SaveAsync` (Z. 403–487) liest Pending-Werte via `GetPendingText`/`ResolvePendingApprovalMode`/`ResolvePendingExpiresUtc`; bei Erfolg `ClearPendingChanges()` (Z. 478) + `await LoadAsync(Access.Id)` (Z. 479). Enthält Bestätigungslogik für Level-Downgrade (Z. 419–427), PreApproved-Booking/Delete (Z. 434–443) und Individual-Seeding (Z. 448–456).
- **Ribbon** (`GetRibbonRegisterDefinition`, Z. 734–786):
  - `navigation`-Liste (Z. 736–742): `Back` mit Payload `"card/setup"`, `MobileShortcut = true`.
  - `manage`-Liste (Z. 744–749): `Save` Large, **`Disabled = Access is null` — nicht `!HasPendingChanges`**, `MobileShortcut = true`. Danach konditional `RotateToken`/`Deactivate`|`Activate`/`Revoke` (Z. 751–777), alle Small.
  - Tabs: `Ribbon_Group_Navigation` Sort 0, `Ribbon_Group_Manage` Sort 10 (Z. 779–783). Keine weiteren Gruppen.
- **Insert-Position für Reset:** in der `manage`-Liste direkt nach dem `Save`-Eintrag (nach Z. 748) — noch innerhalb der Initialisierungsliste bzw. vor den konditionalen `manage.Add(...)`-Aufrufen.
- **Disabled-Bedingung:** `!HasPendingChanges` (Referenzmuster). Optional mit `Access is null` kombinieren (`Access is null || !HasPendingChanges`), um den Fall `Access == null` + versehentliche Pending-Werte abzudecken — Entscheidung in der Planung.
- **Sonstiges:** `IsMcpDisabled` (Z. 63) erzeugt `CardRecord = BuildCardRecord(null)` (Z. 107) — Reset-Guard `Access != null` lässt diesen Zustand unverändert.
