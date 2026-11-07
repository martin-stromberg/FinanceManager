# Abnahmeprüfung – Entwicklungsschritt 7

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 7 ist in Commit `f79fda3b` enthalten) gegen Schritt 7 in `project-plan.md` (Zeilen 139–151). Code-Review plus Testlauf.

- **AK 1 – Verknüpfungen anzeigen/hinzufügen/entfernen (nur eigene Konten):** Neues Panel `FinanceManager.Web/Components/Shared/SavingsPlanLinkedAccountsPanel.razor`, eingebunden in `CardPage.razor` (Zeilen 51–54) für `SavingsPlanCardViewModel` im Edit-Modus — analog zur `LinkedIbans`-Verwaltung auf der Konto-Karte. Liste zeigt verknüpfte Konten (Name + IBAN) mit Entfernen-Button; Add-Select bietet nur `LinkableAccounts` = eigene Konten abzüglich bereits verknüpfter. `SavingsPlanCardViewModel.LoadLinkedAccountsAsync` (Zeilen ~216–241) lädt Link-IDs via `SavingsPlans_GetLinkedAccountsAsync` und Konten via owner-scoped `GetAccountsAsync` — Fremdkonten sind API-seitig nicht wählbar. `AddAccountLinkAsync`/`RemoveAccountLinkAsync` (Zeilen ~243–311) rufen die Schritt-3-Endpunkte und refreshen Link-State **und** Analyse.

- **AK 2 – Kennzeichnung informeller Postings und Istmeldungen:** Neue „Merkmale"-Spalte in `BasePostingsListViewModel.cs` (Spalte `marks`, Zeilen ~34–37; Zelltext `BuildMarksText` Zeilen ~131–152): `informell` bei `Informal`, lokalisiertes SubType-Label (`Posting_Flag_ActualReport` → „Istmeldung") — Vorbild der bestehenden Flag-Spalten (Vorläufig/Storno) unmittelbar daneben. Detailkarte `PostingsCardViewModel.cs` (Zeilen ~101–113): Boolean-Feld „Informell" plus Textfeld „Plan-Buchungsart" mit `Enum:SavingsPlanPostingSubType`-Lookup, übersetzt via `ApplyEnumTranslations` (Mechanismus in `BaseViewModel.cs:557` vorhanden, Enum-Resolution über geladene Assemblies). Lokalizer-Keys in `Pages.resx`/`.de`/`.en` vorhanden.

- **AK 3 – Anzeige folgt Kontosalden / Fallback:** Karte, Liste und Analyse lesen über die bestehenden Endpunkte (`GetAsync`, `analysis`) → liefern ab Schritt 2 automatisch konto-basierte Werte; nach `RemoveAccountLinkAsync` wird `LoadAnalysisAsync` aufgerufen und der letzte-Link-Fallback greift serverseitig (Ableitung). Getestet: `RemoveAccountLinkAsync_CallsApi_AndRestoresReportEligibility` sowie die Analyse-Fallback-Tests aus Schritt 2.

- **Testnachweis:** `dotnet test FinanceManager.Tests` (gefiltert) → `SavingsPlanCardLinkTests` 5/5, `PostingsListMarksColumnTests` 3/3 bestanden (Teil des 33/33-Laufs; ViewModel-Tests mit gemocktem `IApiClient`).

- **Robustheit:** Fehler beim Link-Laden lassen die Karte benutzbar (Catch → leere Listen, `RaiseStateChanged`); `LinkableAccounts` leer → Select deaktiviert mit „Kein Konto verfügbar". Busy-Guards im Panel verhindern Doppel-Submits.

- **Randnotiz:** `GetAccountsAsync(0, 500)` deckelt die Auswahl implizit auf 500 Konten — für realistische Installationen ausreichend, kein Requirement-Bruch.
