# Übersetzte Anforderung – Issue #593: „Zurücksetzen"-Aktion (Änderungen verwerfen) auf Detailansichten ergänzen

## Fachliche Zusammenfassung

Auf acht Karten-Detailansichten fehlt die Ribbon-Aktion „Zurücksetzen", die ungespeicherte Feldänderungen verwirft und die Karte auf den zuletzt geladenen bzw. zuletzt erfolgreich gespeicherten Zustand zurückstellt – ohne Server-Neuladen. Referenzimplementierung ist die bestehende `Reset`-Aktion in `BankAccountCardViewModel` (`UiRibbonAction("Reset", …)` in `GetRibbonRegisterDefinition`, Zeile 291, und `ResetPendingAsync`, Zeilen 373–389: `ClearPendingChanges()` + Neuaufbau des `CardRecord` aus dem im ViewModel gehaltenen `Account`-DTO).

Der verbindliche Scope ist durch die Anwender-Antworten im Issue-Kommentar final geklärt: Betroffen sind die Detailansichten **Kontakt**, **Sparplan**, **Sparplankategorie**, **Wertpapier**, **Wertpapierkategorie**, **Budgetzweck**, **Budgetzweckregel** und **KI-Zugang (MCP-Zugang)**. Die im Issue offen geführten Ansichten **Kontaktgruppe**, **Budgetkategorie**, **Kontoauszugs-Entwürfe** und **Benutzer** wurden ausdrücklich aus dem Scope genommen. Die Sofort-Persistierung der MCP-Scope-Matrix wird akzeptiert; als verbindliche Nebenpflicht ist im Rahmen dieses Issues ein **Folge-Issue zum Aufräumen der KI-Zugangsansicht** anzulegen. Das Redesign aus #590 ist bereits umgesetzt und im Staging – es gibt keine Umsetzungsreihenfolge-/Merge-Konflikt-Abhängigkeit; die Umsetzung erfolgt auf dem neuen Ribbon-Design.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine neue Ribbon-Aktion `"Reset"` in der `Ribbon_Group_Manage`-Gruppe des `GetRibbonRegisterDefinition(...)` jeder der acht betroffenen Card-ViewModels samt zugehöriger Reset-Methode (Muster: private `ResetPendingAsync()`).
- **Auslösende Ereignisse:** Nutzerklick auf den Ribbon-Button „Zurücksetzen" (`Ribbon_Reset`, de: „Zurücksetzen", en: „Reset" – Ressourcen bereits vorhanden in `FinanceManager.Web/Resources/Pages(.de|.en).resx`, jeweils Zeile ~306).
- **Platzierung und Darstellung (identisch zur Referenz):**
  - Gruppe `Ribbon_Group_Manage`, direkt hinter der `Save`-Aktion.
  - `UiRibbonItemSize.Small`; Icon `<svg><use href='icons/sprite.svg#undo'/></svg>`; Aktions-ID `"Reset"`; Label `localizer["Ribbon_Reset"].Value`.
  - Disabled-Zustand: `!HasPendingChanges` (die Aktion ist nur aktiv, wenn ungespeicherte Änderungen vorliegen).
  - `MobileShortcut`: Die Referenz setzt `true`; die Projektregel sieht `MobileShortcut` für die primäre Aktion vor (hier `Save`). Gegen die Ribbon-Regel zu prüfen und konsistent zu entscheiden (Präferenz: kein `MobileShortcut` auf `Reset`, da `Save` die primäre Aktion bleibt).
- **Erwartete Wirkung:**
  - `ClearPendingChanges()` und anschließend `CardRecord` aus dem im ViewModel gehaltenen Modell/DTO neu aufbauen – **kein** erneuter Server-Abruf.
  - Die Aktion verwirft ausschließlich lokale, noch nicht gespeicherte Änderungen; zuletzt persistierte Werte (Stand des letzten Ladens oder des letzten erfolgreichen Speicherns) bleiben erhalten und werden angezeigt.
  - Anschließend `RaiseStateChanged()`, damit Ribbon (Disabled-Zustand) und Karte aktualisiert werden.
- **Neuanlage-Modus (`Id == Guid.Empty`):** Zurücksetzen stellt den initialen – leeren bzw. per `InitPrefill` vorbefüllten – Zustand der Neuanlage wieder her (kein API-Aufruf, kein Löschen des Datensatzes).
- **KI-Zugang (MCP) – Sonderverhalten:** Die Scope-Matrix wird sofort persistiert (`SaveScopesAsync` → `Mcp_SetScopesAsync`, `McpAccessCardViewModel` Zeilen 276–309). „Zurücksetzen" verwirft nur die Kartenfelder (Name, Beschreibung, Level, ApprovalMode, ExpiresUtc, AllowedIpRanges), **nicht** bereits gespeicherte Scope-Änderungen. Dieses Verhalten ist durch die Anwenderantwort akzeptiert; das Aufräumen der KI-Zugangsansicht erfolgt über das verpflichtend anzulegende Folge-Issue.
- **Umgang mit fehlenden Objekten:**
  - Kein Datensatz geladen (z. B. `Contact == null`, `Security == null`, `Access == null` nach Ladefehler): `HasPendingChanges` ist in der Regel `false` und die Aktion damit deaktiviert; die Reset-Methode ist defensiv gegen `null`-Modelle abzusichern (Referenz: `if (Account != null)`-Guard in `ResetPendingAsync`, Zeile 379).
  - Ausnahmen beim Neuaufbau des `CardRecord` werden wie in der Referenz tolerant behandelt (try/catch, `RaiseStateChanged()` am Ende).
- **Verbindliche Nebenpflicht:** Im Rahmen dieses Issues ist ein **Folge-Issue** in `martin-stromberg/FinanceManager` anzulegen, das die Aufräumung der KI-Zugangsansicht beschreibt (inkonsistente Persistenz: Kartenfelder lokal pending vs. Scope-Matrix sofort persistiert).
- **Scope-Ausschlüsse (verbindlich per Anwenderantwort, keine Änderung an diesen Ansichten):**
  - **Kontaktgruppe** (`ContactGroupCardViewModel`): bei der Analyse bereits in Ordnung bewertet – keine Ergänzung.
  - **Budgetkategorie** (`BudgetCategoryCardViewModel`): bei der Analyse bereits in Ordnung bewertet – keine Ergänzung.
  - **Kontoauszugs-Entwürfe** (`StatementDraftCardViewModel`, `StatementDraftEntryCardViewModel`): bei der Analyse bereits in Ordnung bewertet – eigener Entwurfsworkflow (QuickEdit-Abbrechen, `Ribbon_ResetDuplicate`) bleibt unverändert.
  - **Benutzer** (`UserCardViewModel`): „Zurücksetzen" wird nicht benötigt und kann ausgelassen werden (Felder ohnehin nur im Neuanlage-Modus editierbar, `Save` für bestehende Benutzer deaktiviert – `UserCardViewModel` Zeile 371).
  - Keine Änderung an `Ribbon_Reset`-Ressourcen (vorhanden), kein neues Icon ( `icons/sprite.svg#undo` vorhanden), keine Datenmodell-/Persistenzänderung (kein Backup-/Migrationsbedarf), keine Änderung an der Ribbon-Infrastruktur (`UiRibbonAction`, `UiRibbonTab`, `Ribbon`).
  - Kein Server-Reload als Teil des Reset-Verhaltens.

## Betroffene Klassen und Komponenten

Referenzimplementierung (unverändert): `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` – Reset-Aktion Zeile 291 (`Small`, `undo`-Icon, `disabled = !HasPendingChanges`, `MobileShortcut = true`), `ResetPendingAsync` Zeilen 373–389.

In-Scope-ViewModels (je neue `"Reset"`-Aktion hinter `"Save"` plus Reset-Methode):

- `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` – Manage-Tab Zeilen 260–295 (`Save` Z. 262, danach `Delete` Z. 290, `OpenMerge` Z. 291). Baseline: `Contact` (`ContactDto`, Z. 34); Neuaufbau via `BuildCardRecordAsync(Contact)` (Z. 140–166). Neuanlage: `Contact` enthält initialen DTO inkl. `InitPrefill`-Name (Z. 89–91).
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` – Manage-Tab Zeilen 826–855 (`Save` Z. 828, danach `Delete` Z. 837, `Archive` Z. 846). Baseline: `_loadedDto` (`SavingsPlanDto`, Z. 110) bzw. im Neuanlage-Modus das initiale `Model` (`SavingsPlanCreateRequest`, Z. 145); Neuaufbau via `BuildCardRecordAsync(_loadedDto)` (Z. 567–683), vorhandener `RebuildCardRecordAsync` (Z. 756–766) als Ausgangspunkt. **Besonderheit:** `Model` wird bereits während der Eingabe mutiert (Name Z. 727–733, TargetDate Z. 735 ff., Type/Interval/Category Z. 781–784) – der Reset muss `Model` zusätzlich aus `_loadedDto` neu ableiten (Muster `LoadAsync` Z. 160) bzw. im Neuanlage-Modus auf den initialen `SavingsPlanCreateRequest` (InitPrefill) zurücksetzen.
- `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs` – Manage-Tab Zeilen 192–196 (`Save` Z. 194, `Delete` Z. 195). Baseline: `Name`/`SymbolId` (Z. 32/37); Neuaufbau via `BuildCardRecord(Name, SymbolId)` (Z. 92–100). **Besonderheit:** `SaveAsync` überträgt `CardRecord`-Werte vor dem API-Aufruf in `Name`/`SymbolId` (Z. 110–116) – nach einem fehlgeschlagenen Speichern enthält das Modell ungespeicherte Werte; der zuletzt persistierte Stand ist ggf. separat zu halten, damit das Akzeptanzkriterium „zuletzt geladener/gespeicherter Zustand" auch auf dem Fehlerpfad gilt.
- `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs` – Manage-Tab Zeilen 371–376 (`Save` Z. 373, `Delete` Z. 374, `Archive` Z. 375). Baseline: `Security` (`SecurityDto`, Z. 32); Neuaufbau via `BuildCardRecordAsync(Security)` (Z. 330–347). Neuanlage: `Security` enthält bereits den initialen DTO (`Id = Guid.Empty`, Prefill-Name, Z. 151–155).
- `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs` – Manage-Tab Zeilen 207–211 (`Save` Z. 209, `Delete` Z. 210). Baseline: `Model` (`EditModel` mit `Name`/`SymbolAttachmentId`); Neuaufbau via `BuildCardRecord(Model.Name, Model.SymbolAttachmentId)` (Z. 95–103). **Besonderheit:** gleiche Save-Vorab-Mutation wie Sparplankategorie (Z. 111–117) – zuletzt persistierter Stand ggf. separat halten.
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs` – Manage-Tab Zeilen 458–462 (`Save` Z. 460, `Delete` Z. 461). Baseline: `Purpose` (`BudgetPurposeDto`, Z. 34); Neuaufbau via `BuildCardRecord(Purpose)` (Z. 235). `ValidateFieldValue`-Override (Z. 493–522) mutiert Felder des `CardRecord` (SourceType-/SourceId-Abhängigkeit) – der Neuaufbau stellt den konsistenten Ausgangszustand wieder her.
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs` – Manage-Tab Zeilen 392–396 (`Save` Z. 394, `Delete` Z. 395). Baseline: `Rule` (`BudgetRuleDto`, Z. 40); Neuaufbau via `BuildCardRecord(Rule)` (Z. 128). Neuanlage: `Rule` enthält initialen DTO inkl. Prefill-Parent (Z. 77–79).
- `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs` – Manage-Liste Zeilen 744–749 (`Save` Z. 746, disabled `Access is null`; danach konditional `RotateToken`/`Activate`/`Deactivate`/`Revoke` Z. 751–777). Baseline: `Access` (`McpAccessDetailDto`, Z. 39); Neuaufbau via `BuildCardRecord(Access)` (Z. 672–715). Kein Neuanlage-Modus (Erstellung ausschließlich per Wizard, Z. 80–82). Scope-Matrix bleibt vom Reset unberührt (Sofort-Persistierung, s. o.).

Ausgeschlossene ViewModels (keine Änderung): `UserCardViewModel.cs`, `ContactGroupCardViewModel.cs`, `BudgetCategoryCardViewModel.cs`, `StatementDraftCardViewModel.cs`, `StatementDraftEntryCardViewModel.cs`.

- `FinanceManager.Web/ViewModels/Common/BaseCardViewModel.cs` – `ClearPendingChanges()` (Z. 134), `HasPendingChanges` (Z. 93), `ApplyPendingValues` (Z. 143–188): wiederverwendet, keine Änderung.
- Tests: Vorbild `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs` (DI-Setup mit `Mock<IApiClient>`, `DummyLocalizer`, `ServiceCollection`; Ribbon-Assertions via `GetRibbonRegisters`/`FindRibbonAction`). Neue bzw. erweiterte ViewModel-Tests pro betroffenem ViewModel: Reset-Aktion in der `Ribbon_Group_Manage` hinter `Save` vorhanden (klein, `undo`-Icon, `Ribbon_Reset`-Label), Disabled-Verhalten mit/ohne Pending-Changes, Verwerfen der ungespeicherten Werte (`CardRecord` zeigt Baseline, `HasPendingChanges == false`), Neuanlage-Reset auf Ausgangszustand, kein API-Aufruf beim Reset. Bestehende Ribbon-Shape-Tests (`FinanceManager.Tests/ViewModels/RibbonConfigTests.cs`, `ContactsViewModelTests.cs`, `UsersViewModelTests.cs` u. a.) sind auf neue Assertions/Anpassungen zu prüfen.

## Implementierungsansatz

Pro betroffenem ViewModel nach dem Referenzmuster:

1. In `GetRibbonRegisterDefinition` der `Ribbon_Group_Manage` direkt hinter der `Save`-Aktion eine `UiRibbonAction("Reset", localizer["Ribbon_Reset"].Value, "<svg><use href='icons/sprite.svg#undo'/></svg>", UiRibbonItemSize.Small, !HasPendingChanges, null, async () => { await ResetPendingAsync(); })` einfügen.
2. Private Methode `ResetPendingAsync()` ergänzen: `ClearPendingChanges()`, danach `CardRecord` aus dem im ViewModel gehaltenen Modell/DTO neu aufbauen (pro ViewModel die oben genannte Baseline), `RaiseStateChanged()` – analog `BankAccountCardViewModel.ResetPendingAsync` (Z. 373–389), ohne API-Aufruf.
3. Besonderheiten:
   - `SavingsPlanCardViewModel`: `Model` aus `_loadedDto` neu ableiten bzw. im Neuanlage-Modus auf den initialen Zustand (InitPrefill) zurücksetzen; vorhandener `RebuildCardRecordAsync` kann wiederverwendet werden.
   - `SavingsPlanCategoryCardViewModel`/`SecurityCategoryCardViewModel`: Baseline des zuletzt persistierten Stands sicherstellen (Vorab-Mutation des Modells in `SaveAsync` berücksichtigen).
   - `McpAccessCardViewModel`: Reset nur für Kartenfelder; Scope-Matrix unverändert lassen.
4. Folge-Issue zum Aufräumen der KI-Zugangsansicht anlegen (z. B. via `gh issue create`, Verweis auf #593).

Keine neuen Ressourcen-Strings, Icons, API-Endpunkte, Entitäten oder Migrationen erforderlich; keine Backup-Auswirkungen.

## Konfiguration

Keine – das Verhalten ist fest verdrahtet und nicht konfigurierbar.

## Offene Fragen

1. Soll `MobileShortcut` auf der `Reset`-Aktion gesetzt werden (Referenz `BankAccountCardViewModel` setzt `true`) oder bleibt es wegen der Regel „primäre Aktion erhält `MobileShortcut`" ungesetzt (`Save` ist primär)? — Annahme: ungesetzt, Entscheidung konsistent in der Planung treffen.
2. Müssen `SavingsPlanCategoryCardViewModel` und `SecurityCategoryCardViewModel` den zuletzt persistierten Stand separat halten, damit „Zurücksetzen" auch nach einem fehlgeschlagenen Speichern den persistierten Stand wiederherstellt? — Annahme: ja, weil das Akzeptanzkriterium den zuletzt geladenen/gespeicherten Zustand verlangt.
3. Gilt das Akzeptanzkriterium „kein Server-Neuladen" auch für `ContactCardViewModel.BuildCardRecordAsync`, das beim Neuaufbau die Kategorieliste lädt (`ContactCategories_ListAsync`, Z. 145–151)? — Annahme: ja, das Kriterium betrifft das Neuladen des Datensatzes selbst; Lookup-Nebenabrufe des Bestands-Build-Pfads sind zulässig bzw. nach Möglichkeit zu vermeiden (z. B. durch Caching der Kategorien), Entscheidung in der Planung.
4. Titel und Beschreibung des Folge-Issues für die Aufräumung der KI-Zugangsansicht. — Annahme: wird bei Umsetzung formuliert (Inhalt: Kartenfelder pending vs. Scope-Matrix sofort persistiert, Reset-Umfang, Konsistenz der Ansicht).
