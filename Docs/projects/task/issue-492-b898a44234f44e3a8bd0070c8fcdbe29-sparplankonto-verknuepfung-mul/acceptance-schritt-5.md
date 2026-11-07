# Abnahmeprüfung – Entwicklungsschritt 5

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 5 ist in Commit `64afbfef` enthalten) gegen Schritt 5 in `project-plan.md` (Zeilen 109–123) sowie `requirement.md` (FA-3, AK 6/7). Code-Review plus Testlauf.

- **AK 1 – Differenzbuchung, keine Kontobewegung:** `SavingsPlanService.ReportActualBalanceAsync` (`FinanceManager.Infrastructure/Savings/SavingsPlanService.cs:615–662`) ermittelt `accumulated` aus den Plan-Postings bis heute und bucht `reportedBalance - accumulated` als `Posting` mit `Kind == PostingKind.SavingsPlan`, `AccountId == null` (strukturell keine Kontobewegung), `SavingsPlanId == plan.Id`, Subject „Istmeldung", Marker `SavingsPlanPostingSubType.ActualReport`. `GetCurrentAmountAsync`/Analyse summieren das Posting anschließend mit → `accumulated` = gemeldeter Wert (`AccumulatedAmount` in der Response = `accumulated + difference`). Getestet: `..._CreatesMarkedDifferencePostingWithoutAccount`, `..._AccumulatedMatchesReportedBalance`, `..._BelowAccumulated_CreatesNegativePosting`.

- **AK 2 – erkennbar als Korrektur, nicht informell:** Marker ist Enum-Feld `SavingsPlanSubType` auf `Posting` (`FinanceManager.Domain/Postings/Posting.cs`, Property + `SetSavingsPlanSubType`), in der Ausgabe als `savingsPlanSubType` im `PostingServiceDto` und in `fm_postings_query` (`FmPostingsQueryTool.cs:161`) ausgewiesen; Subject „Istmeldung" zusätzlich. Die Buchung hat `GroupId == Guid.Empty` → `IsInformalPosting` liefert `false` (getestet: `GetSavingsPlanPostings_ActualReport_NotInformalAndMarked`). Kein neuer `PostingKind` → läuft ohne Umbau durch alle Plan-Filter/Aggregate; `_aggregateService.UpsertForPostingAsync` wird mitgeführt (Zeilen 655–659).

- **AK 3 – Abweisung bei Konto-Link:** Service wirft `InvalidOperationException`, sobald ein `SavingsPlanAccountLink` existiert (Zeilen 620–625), noch bevor gebucht wird. Getestet: `ReportActualBalanceAsync_LinkedPlan_ThrowsInvalidOperationException`.

- **AK 4 – Budget-Actuals unverändert:** Reine `SavingsPlanId`-Queries würden die Korrektur ungefiltert einbeziehen; daher expliziter Ausschluss `p.SavingsPlanSubType != SavingsPlanPostingSubType.ActualReport` in `BudgetPurposeService.cs:351–357` und `BudgetImpactEvaluationService.cs:243`. `BudgetReportService.BuildRealizationsAsync` ist strukturell nicht betroffen (kontakt-basierte Realizations + `GroupId`-Fallback; die Istmeldung hat weder Kontakt-Posting noch `GroupId`). Getestet: `ListOverview_ActualReport_ExcludedFromActuals`.

- **AK 5 – Migration, ApiClient, owner-scoped Endpunkt:** Migration `20261107214418_AddPostingSavingsPlanSubType` (`FinanceManager.Infrastructure/Migrations/`) im `dotnet ef`-Scaffold-Format inkl. aktualisiertem `AppDbContextModelSnapshot` (nullable INT-Spalte `SavingsPlanSubType` auf `Postings`). Endpunkt `POST /api/savings-plans/{id}/actual-reports` (`SavingsPlansController.cs:421–441`): owner-scoped über Service (`ArgumentException` → 404), Linked-Plan → 400 `Err_ActualReport_NotAllowed`, `ModelState`-Validierung des `[Required]`-Request-Felds. ApiClient `SavingsPlans_ReportActualBalanceAsync` + `IApiClient`-Spiegel; Request/Response-DTOs in `SavingsPlanRequests.cs`. `PostingBackupDto` führt das Marker-Feld mit (Default `null` → abwärtskompatibel; Verdrahtung Export/Import = Schritt 9, bereits vorbereitet).

- **Testnachweis:** `dotnet test FinanceManager.Tests` (gefiltert) → `SavingsPlanServiceActualReportTests` **5/5**, zugehörige Informal-/Budget-Tests grün (Teil des 33/33-Laufs).

- **Kein Storno:** Reversal-Owner-Auflösung läuft nur über `AccountId` → Istmeldung ist nicht stornierbar (Vorgehensentscheidung 7, vom Requirement nicht gefordert; Korrektur per neuer Istmeldung).

- **Randnotiz:** Buchungsdatum = `DateTime.UtcNow.Date` (Zeile 635); bei Server-Zeitzonen hinter UTC könnte das Datum einen Tag vor `DateTime.Today` liegen — fachlich ohne Belang (beide Grenzen „≤ heute" eingeschlossen).
