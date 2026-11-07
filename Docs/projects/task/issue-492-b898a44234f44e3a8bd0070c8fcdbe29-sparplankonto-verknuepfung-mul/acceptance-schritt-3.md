# Abnahmeprüfung – Entwicklungsschritt 3

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 3 ist in Commit `64afbfef` enthalten) gegen Schritt 3 in `project-plan.md` (Zeilen 80–92) sowie `requirement.md` (FA-1, AK 3). Code-Review plus Integrationstestlauf.

- **AK 1 – Links per API setzen/auflisten/entfernen:** Subressourcen-Muster analog `linked-ibans` umgesetzt in `FinanceManager.Web/Controllers/SavingsPlansController.cs`: `GET {id}/linked-accounts` (Zeilen 336–343), `POST {id}/linked-accounts/{accountId}` (Zeilen 359–382), `DELETE {id}/linked-accounts/{accountId}` (Zeilen 396–403). Alle drei delegieren an `ISavingsPlanAccountLinkService` mit `_current.UserId` → owner-scoped.

- **AK 2 – Fehlercodes:** Fremder/unbekannter Plan → `ArgumentException` mit `ParamName == "savingsPlanId"` → **404** (AddLink, Zeilen 374–377; GetLinkedAccounts liefert `null` → `NotFound`, Zeile 342; RemoveLink `false` → `NotFound`, Zeile 402). Fremdes/unbekanntes Konto → `ArgumentException` → **400** (Zeilen 378–381). Duplikat → `InvalidOperationException` → **400** mit `Err_Link_Duplicate` (Zeilen 370–373). Entspricht den „üblichen Fehlercodes" der Projekt-Konvention (`ApiErrorFactory`/`ApiErrorDto`).

- **AK 3 – ApiClient + Integrationstests:** `FinanceManager.Shared/ApiClient.SavingsPlans.cs` (Zeilen 40–83): `SavingsPlans_GetLinkedAccountsAsync` (null bei 404), `SavingsPlans_AddAccountLinkAsync` (true bei 204, false + `LastError` bei 400/404), `SavingsPlans_RemoveAccountLinkAsync`; Interface-Spiegelung in `IApiClient.cs` (Zeilen ~943–956). Integrationstests `FinanceManager.Tests.Integration/ApiClient/ApiClientSavingsPlanLinksTests.cs` belegen Ende-zu-Ende über `TestWebApplicationFactory`: Setzen → Auflisten → Analyse-Umschaltung auf Kontosaldo → Entfernen → Fallback (`SavingsPlanAccountLinks_Set_List_Remove_AndAnalysisSwitch`) sowie Duplikat- und NotFound-Abweisung (`SavingsPlanAccountLinks_Duplicate_AndNotFound`).

- **Testnachweis:** `dotnet test FinanceManager.Tests.Integration --filter "FullyQualifiedName~ApiClientSavingsPlanLinksTests"` → **2/2 Tests bestanden**.

- **Vorgriff auf Schritt 5:** Der Diff enthält bereits den Istmeldung-Endpunkt `POST {id}/actual-reports` (Zeilen 421–441) samt `SavingsPlanActualReportRequest`/`SavingsPlanActualReportResponse`-DTOs und ApiClient-Methode — Commit `64afbfef` bündelt die Schritte 2–5; fachlich sauber getrennt und hier nicht als Manko gewertet.

- **`GetLinkedAccounts`-Semantik:** Der Service liefert `null` bei unbekanntem/fremdem Plan (dokumentierter Vertrag aus Schritt 1); der Controller mappt korrekt auf 404 — die in `acceptance-schritt-1.md` angemerkte Pflicht zur sauberen `null`-Behandlung ist erfüllt.
