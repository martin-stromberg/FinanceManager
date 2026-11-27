# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Bewertung der Befunde aus `review-usability.1.md`

Die beiden Altbefunde („Neu laden"/„Suche zurücksetzen" fehlen auf `SavingsPlansListViewModel` und `SecuritiesListViewModel`) werden gemäß Scope-Festlegung des Auftraggebers **nicht als Befund** gewertet: Fehlende Aktionen werden bewusst nicht implementiert, sondern als GitHub-Issues erfasst. Für genau diese beiden ViewModels existiert **Issue #598** (OPEN: „feat: 'Neu laden' und 'Suche zuruecksetzen' auf Sparplan- und Wertpapieruebersicht"). Eine Gruppe darf damit weniger Aktionen enthalten als die Zielkonfiguration nennt — dokumentierter Scope, kein Befund.

Verifizierte Auslagerungs-Issues (Stichprobe via `gh issue list`): #593 („Zurücksetzen" auf Detailansichten), #594 (Sparplankategorieübersicht), #595 (Benutzerübersicht), #596 (KI-Zugänge-Übersicht), #597 („Kategorie" Wertpapierdetail), #598 (Sparplan-/Wertpapierübersicht), #599–#609 (die zehn gemeldeten Fehlverhalten inkl. defekter Zurück-Routen und wirkungsloser Suchen). Anmerkung: Einige in der Anforderung als „neu → Issue" markierte Aktionen existieren bereits und bleiben bestehen — „Passwort zurücksetzen" (`SetPassword` in `UserCardViewModel.cs`, Label per resx umbenannt), „Deaktivieren"/„Zugang widerrufen" (`McpAccessCardViewModel.cs` Zeilen 762/774) sowie „Regeln → Neu" auf der Budgetzweckdetailansicht (kommt über das Kind-ViewModel `BudgetRuleListViewModel.cs` Zeile 139–157 als aggregierte Gruppe „Regeln", groß, Sort 60 — exakte Zielposition).

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Menüband-Gruppen, -Reihenfolge und Symbolgrößen je Ansicht → unauffällig. Alle geänderten `GetRibbonRegisterDefinition`-Implementierungen vergeben explizite `UiRibbonTab.Sort`-Indizes; `Ribbon.razor` sortiert stabil via `OrderBy(g => g.Sort)` (Zeile 253). Verifizierte Zielkonfigurationen:
  - Kontenübersicht: Navigieren(0, konditional), Verwalten/„Neu" groß(10), Filter/„Suche zurücksetzen" groß(30) — `BankAccountListViewModels.cs` Zeilen 134–152
  - Kontaktübersicht: Verwalten „Neu"+"Gruppen" groß(10), Ansicht „Neu laden" groß + „Suche zurücksetzen" klein(30) — `ContactListViewModel.cs` Zeilen 110–123
  - Kontaktdetails: Zurück groß(0), Speichern groß/Löschen klein/„Zusammenführen" klein(10, aus „Verknüpfte Informationen" umgruppiert), Buchungen+Anhänge klein(55) — `ContactCardViewModel.cs`
  - Sparplanübersicht: Verwalten „Neu"+"Kategorien" groß(10), Ansicht Toggle klein(30); „Neu laden"/„Suche zurücksetzen" → Issue #598 — `SavingsPlansListViewModel.cs` Zeilen 295–315
  - Sparplandetails: Zurück(0), Speichern groß/Löschen+Archivieren klein(10), Analyse „Neu berechnen" groß(40), Buchungen „Istmeldung" groß(45), Verknüpfte Info(55) — `SavingsPlanCardViewModel.cs`
  - Sparplankategorie-Übersicht/Details: Zurück(0)/Neu groß(10) bzw. Zurück/Speichern/Löschen — fehlende Ansicht-Aktionen → Issue #594
  - Wertpapierübersicht: Verwalten „Neu"+"Kategorien" groß(10), Ansicht Toggle klein(30), Berichte „Depot-Bericht" groß(70); „Neu laden"/„Suche zurücksetzen" → Issue #598 — `SecuritiesListViewModel.cs` Zeilen 170–215
  - Wertpapierdetails: Zurück(0), Speichern/Löschen/Archivieren(10), Verknüpfte Info Buchungen/Anhänge/Kurse klein(55), Berichte „Detaillierte Analyse" groß(70, umgruppiert); „Kategorie" → Issue #597 — `SecurityCardViewModel.cs` Zeilen 364–390
  - Wertpapierkategorie-Übersicht/Details: Zurück groß(0)/Neu groß(10) bzw. Zurück/Speichern/Löschen — `SecurityCategoriesListViewModel.cs`, `SecurityCategoryCardViewModel.cs`
  - Budgetübersicht: Verwalten „Neu"+"Kategorien" groß(10), Ansicht „Neu laden" groß/„Suche zurücksetzen" klein(30), Analyse Vormonat/Aktueller Monat/Vorjahr/Aktuelles Jahr klein(40), Berichte „Bericht" groß(70) — `BudgetPurposeListViewModel.cs` Zeilen 135–203
  - Budgetzweckdetails: Zurück(0), Speichern/Löschen(10), Verknüpfte Info „Kategorie" klein(55), Regeln „Neu" groß(60 via Kind-VM) — `BudgetPurposeCardViewModel.cs`, `BudgetRuleListViewModel.cs`
  - Budgetzweckregeldetails: Zurück groß(0), Speichern groß/Löschen klein(10), Felder(47, vorhandene Gruppe unverändert) — `BudgetRuleCardViewModel.cs` Zeilen 362–413
  - Budgetbericht: Navigieren konditional(0), Verwalten „Einstellungen" groß(10), Analyse 5× klein(40), Export „Excel exportieren" klein(80) — `BudgetReportViewModel.cs` Zeilen 327–434
  - Benutzerübersicht: Verwalten „Neu" groß(10); Ansicht-Aktionen → Issue #595 — `UserListViewModel.cs`
  - Benutzerdetails: Zurück(0), Verwalten Speichern groß/Löschen/Passwort zurücksetzen/Aktivieren/Unblock klein(10) — `UserCardViewModel.cs` Zeilen 372–415
  - KI-Zugänge-Übersicht: Verwalten „Neu" groß(10), Ansicht „Neu laden" groß(30), Verknüpfte Info Genehmigungen/Protokoll/KI-Sicherheit klein(55); „Suche zurücksetzen" → Issue #596 — `McpAccessListViewModel.cs` Zeilen 167–254
  - KI-Zugang-Details: Zurück(0), Verwalten Speichern groß/Rotate/Deaktivieren/Widerrufen klein(10) — `McpAccessCardViewModel.cs` Zeilen 779–785
  - Einstellungen: Verwalten Speichern groß/Zurücksetzen klein(10), Daten „Aggregatposten neu berechnen"+"Cache zurücksetzen" klein(15), Profil „Zeitzone neu ermitteln" klein(20), KI 3× klein(35), Programmversion(50), Sicherungen(int.MaxValue, letzte Position) — `SetupCardViewModel.cs` Zeilen 463–530, `SetupProfileViewModel.cs`, `SetupMcpViewModel.cs`, `SetupUpdateViewModel.cs`, `SetupBackupsViewModel.cs`; Aggregation über `BaseViewModel.GetRibbonRegisters` (Kind-VMs, Zeilen 688–704)
- Konditionales „Zurück" auf der Kontenübersicht → unauffällig. Navigationsgruppe wird nur bei gesetztem `InitBack` erzeugt (`BankAccountListViewModels.cs` Zeile 136); `ListPage.razor` Zeile 156 setzt `InitBack` nur bei same-origin `?back=`-Parameter (`Nav.IsSameOriginUri`, `NavigationManagerExtensions.cs` Zeilen 168–196 — verhindert Open Redirects). Regulär (Nav-Link `list/accounts` ohne Parameter) erscheint kein Zurück-Button — exakt die Anforderung.
- Konditionales „Zurück" auf dem Budgetbericht → unauffällig. `ListPage.razor` Zeile 212 hängt `?back=<Herkunfts-URL>` an `reports/budget`; `BudgetReport.razor` (Zeilen 22–35, 65–77) setzt `SetBackNavigation` nur bei validem same-origin Ziel und navigiert beim Klick dorthin; `BudgetReportViewModel.cs` Zeile 329 erzeugt die Navigationsgruppe nur bei `InitBack`. Direktaufruf ohne Kontext → keine Navigationsgruppe.
- „Suche zurücksetzen" nur aktiviert bei eingegebenem Suchbegriff → unauffällig (`Disabled: string.IsNullOrWhiteSpace(Search)` auf Konten-, Kontakt- und Budgetübersicht; Handler `ClearSearch`/`ClearFilter` in `ListPage.razor` Zeilen 250–258).
- „Neu laden"/„Suche zurücksetzen" auf Sparplan- und Wertpapierübersicht → kein Befund (Issue #598, dokumentierter Scope, s. o.).
- Inhaltspanel auf Übersichtsseiten (kein Layout-Sprung Übersicht ↔ Detail) → unauffällig. `ListPage.razor` Zeile 27 umschließt Menüband und Listeninhalt mit `card-page-content` — derselbe Glas-Panel-Container wie `CardPage.razor` Zeile 35 (`.card-page-content` in `app.css` Zeilen 335–366). `ConfirmationDialogHost` und Overlays wurden korrekt außerhalb des Panels belassen (Kommentar Zeilen 52–55: `backdrop-filter` würde `position:fixed`-Overlays sonst auf die Panel-Bounds begrenzen). `GenericListPage.razor` ist reine eingebettete Listenkomponente (auch in `GenericCardPage`/`SetupMcpTab` verwendet) — ein Panel im Inneren würde dort doppeln; die eigenständige Übersichtsseite ist `ListPage`.
- Leere Menüband-Gruppen bei ausgeblendeten Aktionen → unauffällig (`Ribbon.razor` Zeile 220–221 entfernt Gruppen ohne sichtbare Items — kein leerer Gruppenrahmen, z. B. wenn die konditionale Navigationsgruppe nicht erzeugt wird).
- Löschen-Symbol in der Sicherungs-Tabelle lesbar (Ruhe + Hover) → unauffällig. Kaskade verifiziert: `button.danger` liefert roten Hintergrund (`theme.Dark.css` Zeilen 140–143), `button.icon-btn.danger` (Spezifität 0,2,1) setzt Ruhe-Glyphenfarbe `#000` (Zeile 192–194), Hover `button.icon-btn.danger:hover:not(:disabled)` (0,3,1) behält roten Hintergrund und setzt Glyphe auf `#2a0606` — identisch zum `button.danger`-Kontrast (Zeilen 204–207). Schwarz auf Rot lesbar; kein Rot-auf-Rot mehr.
- Favicon → unauffällig. Neues, nicht generisches Symbol (`finance_manager.svg`: „F"-Monogramm als Chart-Achse mit Trendlinie im Teal→Violet-Akzentgradient des Redesigns, dunkler Hintergrund); in `App.razor` Zeile 8 als SVG-Favicon eingebunden, `.ico` bleibt als Fallback; `.ico`/`.png` neu generiert; `manifest.json` Theme-/Hintergrundfarben an das dunkle Redesign angepasst (#080a0f).
- Gruppen-/Register-Umbenennungen → unauffällig. `Setup_Section_Update`: „Aktualisierungen"→„Programmversion" (de)/„Program version" (en) — steuert Ribbon-Gruppe (`SetupUpdateViewModel.cs` Zeile 391) und Einstellungs-Register (`SetupCardViewModel.cs` Zeile 73) gleichermaßen. `SetupBackup_Titel`: „Datensicherungen"→„Sicherungen" — Ribbon-Gruppe der Sicherungs-Aktionen (`SetupBackupsViewModel.cs` Zeile 241). Neue Keys `Ribbon_Group_View` („Ansicht"/„View") und `Ribbon_Group_Data` („Daten"/„Data") in neutral + de + en resx; Tooltip `Hint_RebuildAggregates` in de + en übersetzt und verdrahtet (`SetupCardViewModel.cs` Zeile 500).
- Aktionswirksamkeit → unauffällig. Alle geänderten Aktionen haben verdrahtete Handler (direkte Callbacks oder `RaiseUiActionRequested` mit `switch`-Handler in `ListPage.razor`/`CardPage.razor`/`BudgetReport.razor`); „Zurück" mit fehlendem/ungültigem Ziel fällt auf bekannte Fallbacks zurück (Postings-Karte bzw. Listen-Root, `ListPage.razor` Zeilen 214–242). Deaktivierte Zustände (z. B. „Suche zurücksetzen" ohne Suchbegriff, „Speichern" ohne Änderungen) sind per `Disabled`-Flag visuell ausgewiesen.
- Interne Kennungen / Auswahlaufgaben → unauffällig. Keine Eingabe technischer IDs erforderlich; reine Umorganisation benannter Menüband-Aktionen.

Build-Check: `dotnet build FinanceManager.Web` erfolgreich (0 Fehler, 6 bereits vorhandene NU1510-Warnungen).

## Geprüfte Dateien

- `FinanceManager.Web/Components/App.razor`
- `FinanceManager.Web/Components/Pages/BudgetReport.razor`
- `FinanceManager.Web/Components/Pages/ListPage.razor`
- `FinanceManager.Web/Components/Pages/GenericListPage.razor` (Eingebettetes Listenelement — unverändert, Kontext geprüft)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (Vergleichs-Layout — unverändert, Kontext geprüft)
- `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor` (unverändert, Kontext zum Farbfix geprüft)
- `FinanceManager.Web/Components/Shared/Ribbon.razor`
- `FinanceManager.Web/Extensions/NavigationManagerExtensions.cs`
- `FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx`
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`, `BaseCardViewModel.cs`, `RibbonModels.cs`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs`
- `FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs`, `ContactCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs`, `SavingsPlanCardViewModel.cs`, `Categories/SavingsPlanCategoryListViewModel.cs`, `Categories/SavingsPlanCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs`, `SecurityCardViewModel.cs`, `Categories/SecurityCategoriesListViewModel.cs`, `Categories/SecurityCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs`, `BudgetPurposeCardViewModel.cs`, `BudgetRuleCardViewModel.cs`, `BudgetRuleListViewModel.cs`, `BudgetReportViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`, `SetupBackupsViewModel.cs`, `SetupProfileViewModel.cs`, `SetupMcpViewModel.cs`, `SetupUpdateViewModel.cs` (Kontext), `UserListViewModel.cs`, `UserCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Mcp/McpAccessListViewModel.cs`, `McpAccessCardViewModel.cs`
- `FinanceManager.Web/wwwroot/css/theme.Dark.css`, `app.css`
- `FinanceManager.Web/wwwroot/icons/finance_manager.svg`, `finance_manager-64x64.ico`, `finance_manager.png`, `manifest.json`
