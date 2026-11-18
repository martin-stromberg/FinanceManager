# Umsetzungsplan: Kontierungsregeln-Panel als Overlay mit Ribbon-Aufruf und Symbolbuttons

## Übersicht

Die Verwaltung der Kontierungsregeln (`PostingRulesPanel`) auf der Bankkonto-Karte wird von einer dauerhaft eingebetteten Anzeige auf ein modales Overlay umgestellt, das über einen neuen Aktionsbutton `OpenPostingRules` in der Ribbon-Gruppe „Buchungen" geöffnet wird. Gleichzeitig werden die vier Textbuttons des Panels (`btn btn-sm`) durch Symbolbuttons (`icon-btn` + SVG aus `/icons/sprite.svg` + lokalisiertem Tooltip) ersetzt. Fachlogik, APIs, Datenmodell und alle übrigen Bereiche der Karte bleiben unverändert — reine Darstellungs-/Bedienungsänderung.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | `PostingRulesPanel` auf der Bankkonto-Karte (`CardPage.razor`, `[CardRoute("accounts")]`) inkl. Ribbon-Definition des `BankAccountCardViewModel` |
| Auslösende Ereignisse | Klick auf neuen Ribbon-Button `OpenPostingRules` in Gruppe `Ribbon_Group_Bookings` → `UiActionRequested` mit `UiOverlaySpec`-Payload; Schließen über `icon-btn`-Schließen-Button oder Backdrop-Klick des `OverlayHost` |
| Erwartete Wirkung | Modaler Dialog (`.split-center`/`.split-dialog`) zeigt das Panel mit Titelzeile „Kontierungsregeln" und Schließen-Button über dem Karteninhalt; Regeln lassen sich darin wie bisher auflisten/anlegen/bearbeiten/löschen |
| Umgang mit fehlenden Objekten | **Ungespeichertes Konto** (`Account == null \|\| Account.Id == Guid.Empty`): Ribbon-Button `Disabled` + Tooltip-Begründung (kein Öffnen möglich). **Keine Regeln vorhanden**: Panel zeigt bestehende Leermeldung `PostingRules_Empty`. **`PostingRules` noch nicht geladen**: entfällt — `LoadAsync` lädt Regeln bei bestehenden Konten bereits via `LoadPostingRulesAsync` |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Änderungen an Fachlogik/APIs der Kontierungsregeln (`LoadPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync`, `AccountsController`-Endpunkte, `AccountService`) | Anforderung ist explizit rein visuell/bedienerisch | bestätigt (2026-11-18, aus Anforderung/Repo-Konventionen abgeleitet) |
| 2 | Keine Umstellung anderer eingebetteter Panels (`LinkedIbansPanel`, `SavingsPlanLinkedAccountsPanel`) auf Overlays | Nicht angefordert; `LinkedIbansPanel` bleibt unterhalb der Karte | bestätigt (2026-11-18, aus Anforderung/Repo-Konventionen abgeleitet) |
| 3 | Kein Zähler/Badge mit Regelanzahl auf Ribbon-Button oder Karte | Offene Frage 4 der Anforderung — vermutlich Out of Scope, da Regeln nicht mehr permanent sichtbar | bestätigt (2026-11-18, aus Anforderung/Repo-Konventionen abgeleitet) |
| 4 | Keine Änderungen am generischen Overlay-Mechanismus (`OverlayHost`/`UiOverlaySpec`) | Vorhandener Mechanismus deckt den Bedarf vollständig ab | bestätigt (2026-11-18, aus Anforderung/Repo-Konventionen abgeleitet) |
| 5 | Keine Anpassungen an Übersichts-/Listenseiten | Betroffen ist ausschließlich die Bankkonto-Karte | bestätigt (2026-11-18, aus Anforderung/Repo-Konventionen abgeleitet) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Overlay-Mechanismus | Bestehender `OverlayHost` + `UiOverlaySpec(typeof(PostingRulesPanel), parms)`; `OverlayHost` ist auf `CardPage.razor` (Z. 77) bereits verdrahtet | Referenzmuster vorhanden (`RequestOpenAttachments` in `BaseViewModel`, `SetPassword` in `UserCardViewModel`, `RequestActualReport` in `SavingsPlanCardViewModel`); kein neuer Mechanismus nötig |
| Overlay-Titel | `Parameters["OverlayTitle"] = localizer["PostingRules_Section_Title"]` — **nicht** `Parameters["Title"]` | `OverlayHost` filtert `OverlayTitle` aus den Komponenten-Parametern heraus und zeigt ihn via `GetOverlayTitle()` an; `Title` bliebe im Dictionary und würde ohne `[Parameter] string? Title` am Panel eine `DynamicComponent`-Ausnahme werfen (dokumentierte Falle, inventory/logic.md) |
| Parameterübergabe ans Panel | `["Vm"] = this`, `["Localizer"] = localizer` im Spec-`Parameters`-Dictionary | Deckt die beiden `[Parameter]` `Vm`/`Localizer` des Panels ab; identisch zum Muster `["Vm"] = this` in `SavingsPlanCardViewModel.RequestActualReport` |
| Verfügbarkeit bei Neuanlage | `Disabled` statt `Hidden`, zusätzlich lokalisierter Tooltip-Hinweis im Disabled-Zustand | Deckt sich mit dem bestehenden Muster aller kontoabhängigen Ribbon-Aktionen (`Delete`, `CreatePreliminaryDraft`, `ReevaluatePostingTypes`); ein deaktivierter Button ohne Erklärung ist keine zulässige Blockade-Kommunikation — daher `Tooltip = isNew ? localizer["Ribbon_PostingRules_DisabledHint"] : null` (fällt bei `null` auf `Label` zurück) |
| Symbolbutton-Stil | `icon-btn` (global definiert, `app.css` Z. 347) bzw. `icon-btn danger` für Entfernen; Tooltip via `title` aus den bestehenden `PostingRules_*_Button`-Keys | Referenzmuster `AttachmentsPanel.razor` Z. 117–123 und `SetupAttachmentCategoriesTab.razor`; **kein** `icon-btn small` (`.small` ist in `app.ReportDashboard.css` definiert und nicht global verfügbar) |
| Interne Titelzeile des Panels | Entfernen (`<tr>` mit `PostingRules_Section_Title`, Z. 9–13) sowie `margin-top:1.25rem` am Panel-Div | Im Overlay liefert die Dialog-Titelzeile (`<h2>` + Schließen-Button) bereits die Überschrift; doppelte Überschrift und oberer Abstand wären redundant |
| Aktions-Id | `OpenPostingRules`; kein Eintrag im `OnUiActionRequested`-Switch der `CardPage` | Der `UiOverlaySpec`-Payload wird allein vom `OverlayHost` verarbeitet (analog `OpenAttachments`, das im Switch bewusst No-op ist) |

## Programmabläufe

### Kontierungsregeln-Overlay öffnen und schließen

1. Nutzer öffnet `/card/accounts/{id}` eines **gespeicherten** Kontos → `BankAccountCardViewModel.LoadAsync` lädt das Konto und ruft `LoadPostingRulesAsync` auf → `PostingRules` ist befüllt.
2. `Ribbon.razor` rendert die Gruppe „Buchungen" (`Ribbon_Group_Bookings`) mit `CreatePreliminaryDraft`, `ReevaluatePostingTypes` und dem neuen `OpenPostingRules` (`UiRibbonItemSize.Small`, `Disabled == false`).
3. Klick auf `OpenPostingRules` → `Callback` erzeugt `new UiOverlaySpec(typeof(PostingRulesPanel), { ["Vm"] = this, ["Localizer"] = localizer, ["OverlayTitle"] = <lokalisierter Titel> })` und raist `RaiseUiActionRequested("OpenPostingRules", spec)`.
4. `OverlayHost.OnUiActionRequested` erkennt den `UiOverlaySpec`-Payload → setzt `_spec`/`_visible` → rendert `.split-center` > `.split-dialog` mit Titelzeile („Kontierungsregeln") + Schließen-`icon-btn` (`#clear`) sowie `DynamicComponent` → `PostingRulesPanel` mit übergebenen Parametern.
5. Schließen: Klick auf Schließen-Button oder auf den Backdrop → `OverlayHost.CloseOverlay()` → `_spec = null`, Dialog und Panel werden aus dem DOM entfernt; der Karteninhalt darunter bleibt unverändert erhalten. Beim nächsten Öffnen wird eine frische Panel-Instanz erzeugt (Edit-Zustand `_editRuleId`/`_panelError` damit zurückgesetzt; die Regelliste lebt im `BankAccountCardViewModel` weiter).

Beteiligte Klassen/Komponenten: `BankAccountCardViewModel`, `UiRibbonAction`, `UiOverlaySpec`, `OverlayHost`, `PostingRulesPanel`, `Ribbon.razor`, `CardPage.razor`.

### Regeln im Overlay verwalten (unveränderte Fachlogik, neue Button-Darstellung)

1. Liste: bestehende Regelzeilen zeigen je zwei `icon-btn` — Bearbeiten (`#edit`) und Entfernen (`#trash`, `icon-btn danger`), beide mit `title`-Tooltip und `disabled="@_busy"`.
2. Eingabezeile: Abbrechen-`icon-btn` (`#undo`) nur bei `_editRuleId.HasValue`; Hinzufügen/Speichern-`icon-btn` (`#plus` im Anlage-, `#check` im Bearbeitungsmodus).
3. `BeginEdit`, `CancelEdit`, `SaveRuleAsync` (inkl. Leer-Pattern-Vorprüfung → `_panelError`), `RemoveRuleAsync` und `OnKeyDownAsync` (Enter → Speichern) bleiben fachlich identisch.

Beteiligte Klassen/Komponenten: `PostingRulesPanel`, `BankAccountCardViewModel` (`UpsertPostingRuleAsync`, `RemovePostingRuleAsync`).

### Ribbon-Button bei neuer, ungespeicherter Karte

1. Nutzer öffnet `/card/accounts/new` → `LoadAsync(Guid.Empty)` erzeugt leeres `AccountDto` → `Account.Id == Guid.Empty`.
2. `GetRibbonRegisterDefinition` setzt `Disabled = true` und `Tooltip = localizer["Ribbon_PostingRules_DisabledHint"]` auf `OpenPostingRules`.
3. `Ribbon.razor` rendert den Button mit `disabled`-Attribut und dem Hinweis-`title`; `OnItemClick` führt den `Callback` bei `Disabled` nicht aus — das Overlay kann nicht geöffnet werden, und der Tooltip erklärt die Blockade.

Beteiligte Klassen/Komponenten: `BankAccountCardViewModel`, `Ribbon.razor`.

## Neue Klassen

Keine — alle Änderungen erfolgen an bestehenden Komponenten/Klassen.

## Änderungen an bestehenden Klassen

### `BankAccountCardViewModel` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`)

- **Geänderte Methoden:** `GetRibbonRegisterDefinition` — in `bookingActions` (Z. 318–345) wird eine dritte `UiRibbonAction` ergänzt:
  - `Id = "OpenPostingRules"`, `Label = localizer["Ribbon_PostingRules"]`, `IconSvg` mit `#filters`, `UiRibbonItemSize.Small`
  - `Disabled = Account == null || Account.Id == Guid.Empty`, `Tooltip = isNew ? localizer["Ribbon_PostingRules_DisabledHint"].Value : null`
  - `Callback`: baut `UiOverlaySpec(typeof(PostingRulesPanel), params)` und raist `RaiseUiActionRequested("OpenPostingRules", spec)`; `return Task.CompletedTask`
  - Position: nach `ReevaluatePostingTypes` am Ende der Buchungen-Gruppe (sekundäre Aktion, kein `MobileShortcut` — konsistent mit `ReevaluatePostingTypes`)
  - Die Komponentenreferenz `typeof(PostingRulesPanel)` erfordert ggf. ein `using FinanceManager.Web.Components.Shared;` im ViewModel (analog `SavingsPlanCardViewModel` → `SavingsPlanActualReportPanel`).

### `PostingRulesPanel` (`FinanceManager.Web/Components/Shared/PostingRulesPanel.razor`)

- **Geändertes Markup:**
  - Titel-`<tr>` (Z. 9–13) entfernen — Overlay-Titelzeile übernimmt die Überschrift; `margin-top:1.25rem` am Panel-Div (Z. 6) entfernen.
  - Zeilenbuttons (Z. 45–56): `class="btn btn-sm"`/`btn btn-sm btn-danger` + Text → `class="icon-btn"` mit `<svg><use href="/icons/sprite.svg#edit"/></svg>` bzw. `class="icon-btn danger"` mit `#trash`; `title`-Attribut aus `PostingRules_Edit_Button`/`PostingRules_Remove_Button`; `disabled="@_busy"` und `@onclick` unverändert.
  - Eingabezeilen-Buttons (Z. 106–120): Abbrechen → `icon-btn` mit `#undo`, `title` aus `PostingRules_Cancel_Button` (Sichtbarkeitsregel `_editRuleId.HasValue` bleibt); Hinzufügen/Speichern → `icon-btn` mit `#plus` bzw. `#check` (Umschaltung wie bisher über `_editRuleId.HasValue`), `title` aus `PostingRules_Add_Button`/`PostingRules_Save_Button`.
- **Geänderte Methoden:** Keine — sämtliche Handler bleiben unverändert.
- **Neue Parameter:** Keine — `Vm` und `Localizer` reichen; `OverlayTitle` wird vom `OverlayHost` konsumiert und nicht an die Komponente durchgereicht.

### `CardPage.razor` (`FinanceManager.Web/Components/Pages/CardPage.razor`)

- **Geändertes Markup:** Zeile 48 (`<PostingRulesPanel Vm="bankVm" Localizer="Localizer" />`) entfernen. Der umschließende `@if`-Block (Z. 46–53) **bleibt bestehen**, da er weiterhin `LinkedIbansPanel` (Z. 49–52, Bedingung `bankVm.EffectiveIsCollectionAccount`) rendert — die `if`-Bedingung `bankVm.Account?.Id != Guid.Empty` ist für `LinkedIbansPanel` weiterhin erforderlich. `SavingsPlanLinkedAccountsPanel`-Block (Z. 55–58) und `OverlayHost` (Z. 77) bleiben unverändert.
- Kein neuer Fall im `OnUiActionRequested`-Switch nötig.

### Ressourcen (`FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx`)

- **Neuer Key `Ribbon_PostingRules`:** de „Kontierungsregeln", en „Posting rules" (Label des Ribbon-Buttons).
- **Neuer Key `Ribbon_PostingRules_DisabledHint`:** de z. B. „Nur für gespeicherte Konten verfügbar", en „Available for saved accounts only" (Tooltip im Disabled-Zustand).
- Wiederverwendet: `PostingRules_Section_Title` (Overlay-Titel), `PostingRules_Edit_Button`/`_Remove_Button`/`_Cancel_Button`/`_Add_Button`/`_Save_Button` (Tooltips der Symbolbuttons — bereits lokalisiert vorhanden).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen — die bestehende Leer-Pattern-Vorprüfung (`SaveRuleAsync` → `_panelError` mit `PostingRules_Error_PatternRequired`) und serverseitige Validierung bleiben unverändert wirksam.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Playwright-Tests:** `ManagePostingRules_ViaUi` und `ManagePostingRules_ValidationError_ViaUi` (`AccountPostingRulesPlaywrightTests`) erwarten `.posting-rules-panel` sofort sichtbar und klicken `button.btn-primary`/`button.btn-danger` — beide schlagen nach der Umstellung fehl und müssen angepasst werden (Ribbon-Klick vorab + neue Selektoren).
- **`LinkedIbansPanel`:** Beim Entfernen des eingebetteten `<PostingRulesPanel>` darf der `if`-Block nicht komplett gelöscht werden — `LinkedIbansPanel` bleibt eingebettet und sichtbar (nur bei `EffectiveIsCollectionAccount`).
- **`DynamicComponent`-Parameterfalle:** `Parameters["Title"]` würde ungefiltert an `PostingRulesPanel` weitergereicht und ohne deklarierten `Title`-Parameter eine Ausnahme werfen — daher `OverlayTitle` verwenden (siehe Designentscheidungen).
- **Eingabezustand über Overlay-Öffnungen hinweg:** `_editRuleId`/`_panelError`/`_busy` leben in der Komponente und werden beim Schließen verworfen (frische Instanz je Öffnung); `Vm.NewPostingRule*` bleiben im ViewModel erhalten — unverändertes Verhalten des bisherigen Panels, akzeptabel.
- **Unit-Test `GetRibbonRegisters_GroupOrder_PlacesLinkedLast`:** assertiert nur die Gruppenreihenfolge `[Navigation, Manage, Bookings, Linked]` — bleibt grün, da die neue Aktion innerhalb der bestehenden Gruppe liegt.
- **Mobile Ribbon:** `OpenPostingRules` erscheint als `Small`-Item in der mobilen Gruppenansicht (ohne `MobileShortcut`) — konsistent mit `ReevaluatePostingTypes`, kein Risiko.

## Umsetzungsreihenfolge

1. **resx-Keys anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `Ribbon_PostingRules` und `Ribbon_PostingRules_DisabledHint` in `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` ergänzen (de/en/neutral gespiegelt).

2. **`PostingRulesPanel` auf Symbolbuttons umstellen**
   - Voraussetzungen: Schritt 1 (Tooltips nutzen vorhandene `PostingRules_*_Button`-Keys, keine neuen Keys nötig — Schritt 1 ist dennoch Voraussetzung für konsistente Reihenfolge, technisch nicht blockierend).
   - Beschreibung: Vier Textbuttons durch `icon-btn`-Symbolbuttons mit Sprite-Icons und `title`-Tooltips ersetzen; interne Titelzeile und `margin-top` entfernen.

3. **Ribbon-Aktion `OpenPostingRules` im `BankAccountCardViewModel` ergänzen**
   - Voraussetzungen: Schritt 1 (Label-/Tooltip-Keys); `UiOverlaySpec`/`RaiseUiActionRequested` in `BaseViewModel` vorhanden; `OverlayHost` auf `CardPage` verdrahtet.
   - Beschreibung: Dritte `UiRibbonAction` in `bookingActions` mit Disabled-Logik, Conditional-Tooltip und `UiOverlaySpec(typeof(PostingRulesPanel), { Vm, Localizer, OverlayTitle })`.

4. **Eingebettetes Panel aus `CardPage.razor` entfernen**
   - Voraussetzungen: Schritt 3 (Panel muss über Overlay erreichbar sein, bevor die Einbettung entfällt).
   - Beschreibung: Zeile 48 löschen; `if`-Block für `LinkedIbansPanel` erhalten.

5. **Unit-Tests ergänzen**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: In `AccountDetailViewModelTests` neue Tests für die Ribbon-Aktion (Vorhandensein, Disabled bei Neuanlage, `UiOverlaySpec`-Payload beim Callback) — Helfer `CreateVm()`/`DummyLocalizerGeneric<Pages>` vorhanden.

6. **E2E-Tests anpassen und erweitern**
   - Voraussetzungen: Schritte 2–4.
   - Beschreibung: `ManagePostingRules_ViaUi` und `ManagePostingRules_ValidationError_ViaUi` um Ribbon-Klick (`button#OpenPostingRules`) und neue Selektoren ergänzen; in `ManagePostingRules_ViaUi` wird die Regel-Anlage über `PressAsync("Enter")` im Muster-Feld ausgelöst (Nachweis Enter-Speicherung, FA-5); zwei neue E2E-Tests (Overlay öffnen/schließen; Button disabled bei `/card/accounts/new`) in `AccountPostingRulesPlaywrightTests`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetRibbon_BookingsGroup_ContainsOpenPostingRulesAction` | `AccountDetailViewModelTests` | Buchungen-Tab enthält `UiRibbonAction` mit `Id == "OpenPostingRules"`, `Size == Small` |
| `OpenPostingRules_Disabled_ForNewAccount` / `_Enabled_ForSavedAccount` | `AccountDetailViewModelTests` | `Disabled == true` bei `Account.Id == Guid.Empty` (Neuanlage), `false` nach Laden eines gespeicherten Kontos; Tooltip nur im Disabled-Fall gesetzt |
| `OpenPostingRules_Callback_RaisesUiOverlaySpec` | `AccountDetailViewModelTests` | `UiActionRequested`-Event mit `UiOverlaySpec`: `ComponentType == typeof(PostingRulesPanel)`, `Parameters` enthält `Vm` (= ViewModel), `Localizer`, `OverlayTitle`; `Modal == true` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ManagePostingRules_ViaUi` (`AccountPostingRulesPlaywrightTests`) | Panel nicht mehr eingebettet → vorher `button#OpenPostingRules` klicken und auf `.split-dialog .posting-rules-panel` warten; `button.btn-primary`/`button.btn-danger`-Selektoren durch `icon-btn`-Selektoren ersetzen; Anlage der Regel via `PressAsync("Enter")` im Muster-`input` statt Button-Klick (deckt `OnKeyDownAsync` → `SaveRuleAsync`, FA-5) |
| `ManagePostingRules_ValidationError_ViaUi` (`AccountPostingRulesPlaywrightTests`) | Gleiche Gründe; `button.btn-primary` → Add-`icon-btn` |

Keine weiteren bestehenden Tests betroffen: `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (nur Gruppenreihenfolge), Service-/Integrations-Tests und `EnumCoverageTests` berühren die UI-Darstellung nicht.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|---------------------|
| Pflicht | Overlay öffnen → Regel anlegen (via Enter-Taste im Muster-Feld) → bearbeiten → löschen → Overlay schließen | `AccountPostingRulesPlaywrightTests`, angepasster `ManagePostingRules_ViaUi` (plus Edit-Schritt und `PressAsync("Enter")`-Anlage) | AK 1, 3, 5 (inkl. FA-5 Enter-Speicherung) | Vollständiger Benutzerfluss Ribbon → Overlay → Symbolbuttons/Enter → Persistenz; nur über UI prüfbar |
| Pflicht | Validierungsfehler im Overlay (leeres Muster → Fehlerzeile, keine Regel) | angepasster `ManagePostingRules_ValidationError_ViaUi` | AK 5 | Panel-Fehleranzeige muss im Overlay-Kontext sichtbar sein |
| Pflicht | Ribbon-Button öffnet Overlay; Panel nicht dauerhaft sichtbar; Schließen via `icon-btn`; Karte darunter unverändert | Neuer Test `PostingRulesOverlay_OpensAndCloses_ViaRibbon` | AK 2, 3, 4, 8 | Kern der Anforderung: Overlay-Lebenszyklus statt Einbettung |
| Pflicht | `button#OpenPostingRules` disabled auf `/card/accounts/new` (inkl. Tooltip-Hinweis) | Neuer Test `PostingRulesRibbon_Disabled_OnNewAccount` | AK 6 | Sichtbarkeits-/Berechtigungsregel nur über UI nachweisbar |

Selektor-Strategie für die Icon-Buttons (locale-unabhängig): `button.icon-btn` gefiltert über das enthaltene `use[href='/icons/sprite.svg#<id>']` (z. B. `#plus` = Hinzufügen, `#check` = Speichern, `#trash` = Entfernen, `#undo` = Abbrechen, `#edit` = Bearbeiten) — alternativ `title`-Regex auf de/en-Label, konsistent zum bestehenden Teststil (`Suchtext|pattern`).

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ManagePostingRules_ViaUi` | Overlay-Öffnung via Ribbon + `icon-btn`-Selektoren (s. o.) + Anlege-Schritt per `PressAsync("Enter")` im Muster-Feld (Enter-Speicherung, FA-5) |
| `ManagePostingRules_ValidationError_ViaUi` | dto. |

Übrige Tests der Klasse (`ClassifiedEntry_*`, `ReevaluatePostingTypes_ViaRibbon` etc.) berühren das Panel nicht und bleiben unverändert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Regel über Overlay verwalten | Nutzer klickt `OpenPostingRules` in Ribbon-Gruppe „Buchungen" | Gespeichertes Bankkonto geöffnet (`/card/accounts/{id}`), Regeln via `LoadPostingRulesAsync` geladen, kein Panel auf der Seite sichtbar | Ribbon-Klick → `.split-dialog` mit Titel „Kontierungsregeln" und `.posting-rules-panel` erscheint → Muster „Zins" + Postenart „Zinsen" + Reihenfolge eingeben → Enter-Taste im Muster-Feld drücken (`OnKeyDownAsync` → Speichern) → Regelzeile erscheint → `#edit`-Icon-Button der Zeile klicken (Werte wandern in Eingabezeile, `#undo`/`#check` sichtbar) → `#check` klicken → `#trash` der Zeile klicken (Zeile verschwindet) → Schließen-`icon-btn` (`#clear`) klicken | Overlay öffnet/schließt modal über der Karte; Regel wird angelegt, aktualisiert und gelöscht (API-Check: Liste leer/nicht leer); Karteninhalt darunter bleibt erhalten; keine Textbuttons, nur `icon-btn` mit Tooltips | E2E `ManagePostingRules_ViaUi` (angepasst) + `PostingRulesOverlay_OpensAndCloses_ViaRibbon`; reale Abnahme |
| 2 | Ribbon-Button bei Neuanlage blockiert | Nutzer öffnet `/card/accounts/new` | Kein gespeichertes Konto (`Account.Id == Guid.Empty`) | Ribbon-Gruppe „Buchungen" ansehen → `button#OpenPostingRules` hat `disabled`; Hover zeigt lokalisierten Hinweis-Tooltip; Klick ohne Wirkung | Button ausgegraut, Tooltip erklärt Blockade, kein Overlay | E2E `PostingRulesRibbon_Disabled_OnNewAccount` |
| 3 | Validierungsfehler im Overlay | Nutzer klickt Hinzufügen-`icon-btn` (`#plus`) mit leerem Muster | Overlay geöffnet auf gespeichertem Konto, keine Regeln | Add-Button ohne Muster klicken → Fehlerzeile im Panel (`PostingRules_Error_PatternRequired`); API-Liste bleibt leer; `option[value='0']` im Postenart-Select weiterhin nicht vorhanden | Fehlermeldung sichtbar im Overlay, kein Datensatz | E2E `ManagePostingRules_ValidationError_ViaUi` (angepasst) |
| 4 | Unveränderte Nebenbereiche | Karte laden (Sammelkonto + Nicht-Sammelkonto) | Bankkonto mit/ohne `EffectiveIsCollectionAccount` | Karte öffnen → unterhalb der Karte kein `.posting-rules-panel`; `LinkedIbansPanel` weiterhin eingebettet sichtbar (nur Sammelkonto); übrige Ribbon-Gruppen/Aktionen unverändert | Nur `LinkedIbansPanel` eingebettet; Panel nur über Ribbon erreichbar | E2E `PostingRulesOverlay_OpensAndCloses_ViaRibbon` (assertiert `.posting-rules-panel` vor Klick nicht sichtbar); reale Abnahme Sammelkonto |
| 5 | Lokalisierung | UI-Sprache de/en wechseln | Overlay geöffnet | Ribbon-Label, Overlay-Titel und alle Button-Tooltips in beiden Sprachen prüfen | Keine hartkodierten Strings; Tooltips lokalisiert | Manuelle Abnahme / resx-Review (Keys in de + en vorhanden) |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Ribbon-Button bei ungespeichertem Konto: deaktiviert oder ausgeblendet? | **Deaktiviert** mit erklärendem Tooltip (`Ribbon_PostingRules_DisabledHint`) — konsistent mit `Delete`/`CreatePreliminaryDraft`/`ReevaluatePostingTypes`, die dasselbe `Account.Id == Guid.Empty`-Disabled-Muster nutzen |
| 2 | Konkrete Icon-Auswahl aus `/icons/sprite.svg` | Ribbon `OpenPostingRules`: `#filters` (Regel-/Matching-Semantik); Bearbeiten `#edit`; Entfernen `#trash` (konsistent zur `Delete`-Ribbon-Aktion der Karte); Abbrechen `#undo` (AttachmentsPanel-Konvention); Hinzufügen `#plus`; Speichern `#check` (visuell von `#plus` unterscheidbar). Kein neues Sprite-Symbol nötig |
| 3 | Overlay-Form: bestehender `OverlayHost` (modal, `.split-dialog`) oder andere Form (z. B. Seitenpanel)? | **Bestehender `OverlayHost`/`UiOverlaySpec`** — etablierter Mechanismus, auf `CardPage` bereits verdrahtet, Referenz `AttachmentsPanel` |
| 4 | Hinweis/Badge mit Regelanzahl auf Ribbon-Button oder Karte? | **Out of Scope** (Empfehlung): Anforderung verlangt keinen Zähler; `Small`-Ribbon-Items unterstützen kein Badge-Konzept |
