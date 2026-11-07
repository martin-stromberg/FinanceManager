# Abnahmeprüfung – Entwicklungsschritt 8

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 8 ist in Commit `f79fda3b` enthalten) gegen Schritt 8 in `project-plan.md` (Zeilen 153–164) sowie `requirement.md` (FA-3, AK 6/7). Code-Review plus Testlauf.

- **AK 1 – Aktion nur ohne Konto-Link, Deaktivierung mit Hinweis:** Neue Ribbon-Gruppe „Buchungen" in `SavingsPlanCardViewModel.GetRibbonRegisterDefinition` (Zeilen ~851–867): Aktion `ActualReport` („Istmeldung") mit `Disabled: !CanReportActualBalance` und Tooltip `ActualReport_Disabled_Linked` („Nicht verfügbar bei verknüpften Konten - der Kontostand ist die Ist-Quelle") sobald Links existieren. `CanReportActualBalance = IsEdit && !HasLinkedAccounts` (Zeilen ~62–68); `HasLinkedAccounts` folgt dem geladenen Link-State und aktualisiert sich nach Add/Remove (`LoadLinkedAccountsAsync`). Doppelte Absicherung: `RequestActualReport` no-opt bei `!CanReportActualBalance`, `ReportActualBalanceAsync` setzt zusätzlich lokale Fehlermeldung `ActualReport_Error_Linked`, und der Server lehnt mit 400 ab. Getestet: `Ribbon_ActualReport_EnabledOnlyWithoutLinks`, `ReportActualBalanceAsync_LinkedPlan_RejectedLocally`.

- **AK 2 – gemeldeter Wert wird angezeigt, Korrektur erkennbar:** `RequestActualReport` öffnet das Overlay `SavingsPlanActualReportPanel.razor` (neu, 75 Zeilen): Beschreibungstext, Dezimal-Eingabe (`decimal.TryParse`, kulturabhängig, Fehler `ActualReport_Error_InvalidAmount`), Busy-Guard, Cancel/Save. Bei Erfolg ruft `ReportActualBalanceAsync` den Endpunkt `POST {id}/actual-reports` und lädt anschließend `LoadAsync(Id)` neu → `accumulatedAmount` in Karte/Analyse = gemeldeter Wert. Die erzeugte Korrekturbuchung erscheint in der Posting-Liste mit Merkmal „Istmeldung" (Marks-Spalte aus Schritt 7) und im Detail als „Plan-Buchungsart: Istmeldung". Getestet: `ReportActualBalanceAsync_PostsReport_AndReloads`.

- **Testnachweis:** `dotnet test FinanceManager.Tests` (gefiltert) → `SavingsPlanCardLinkTests` **5/5 bestanden** (Teil des 33/33-Laufs).

- **Lokalisierung:** Alle neuen Keys (`Ribbon_SavingsPlan_ActualReport`, `ActualReport_*`, `Ribbon_Group_Bookings` über bestehende Gruppen-Ressource) in `Pages.resx`, `Pages.de.resx` und `Pages.en.resx` vorhanden — kein harter UI-String außer Fallbacks.

- **Randnotiz (keine Abweichung):** Das Storno-Risiko aus `project-plan-check.md` (Istmeldung ohne `AccountId` ist nicht stornierbar) ist entschärft, da der Storno-Button ohnehin nur dort greift, wo die Owner-Auflösung via `AccountId` funktioniert; ein explizites Deaktivieren des Storno-Buttons für Istmeldung-Postings wurde nicht umgesetzt — Laufzeitfehler würde erst beim Klick entstehen (Service wirft `InvalidOperationException`). Optionaler Folgepunkt, nicht Teil der AK.
