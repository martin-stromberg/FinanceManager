# Tasks: Anzeige der Versionsnummer im Programmmenü

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI | `LoginStatus.razor`: `IInstalledReleaseMetadataProvider` per `@inject` einbinden | Offen | — |
| 2 | UI | `LoginStatus.razor`: Feld `_versionInfo` (`InstalledReleaseMetadataDto?`) und `OnInitializedAsync()` zum Laden über `GetAsync()` hinzufügen | Offen | — |
| 3 | UI | `LoginStatus.razor`: Anzeigetext-Logik inkl. Fallback `"Version unbekannt"` bei leerer/`null`-Version | Offen | — |
| 4 | UI | `LoginStatus.razor`: `@CurrentUser.UserId` durch Versionstext ersetzen (Logout-Button und Login-Zweig unverändert) | Offen | — |
| 5 | Tests | Test-Double `FakeCurrentUserService` für bUnit-Test bereitstellen | Offen | — |
| 6 | Tests | Test-Double `FakeInstalledReleaseMetadataProvider` für bUnit-Test bereitstellen | Offen | — |
| 7 | Tests | `LoginStatusTests`: `RendersVersion_WhenAuthenticated_AndVersionAvailable` | Offen | — |
| 8 | Tests | `LoginStatusTests`: `RendersFallback_WhenVersionIsNull` | Offen | — |
| 9 | Tests | `LoginStatusTests`: `DoesNotRenderUserId_WhenAuthenticated` | Offen | — |
| 10 | Tests | `LoginStatusTests`: `RendersLoginLink_WhenNotAuthenticated` | Offen | — |
| 11 | E2E-Tests | `VersionDisplayPlaywrightTests`: Nach Login zeigt der `.login-status`-Bereich den Versionstext statt der Benutzer-ID | Offen | — |
