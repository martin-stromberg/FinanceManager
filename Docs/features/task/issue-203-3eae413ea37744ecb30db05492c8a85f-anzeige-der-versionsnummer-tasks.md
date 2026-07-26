# Aufgaben: Anzeige der Versionsnummer im Programmmenü

| # | Aufgabe | Status | Testnachweis |
|---|---------|--------|--------------|
| 1 | `LoginStatus.razor` um Versionsanzeige erweitern (Injektion `IInstalledReleaseMetadataProvider`, `OnInitializedAsync`, `_versionInfo`, `DisplayVersion`, Markup-Ersatz Benutzer-ID → Versionstext) | Erledigt | `LoginStatusTests.RendersVersion_WhenAuthenticated_AndVersionAvailable` |
| 2 | Fallback `"Version unbekannt"` bei leerer/fehlender Version | Erledigt | `LoginStatusTests.RendersFallback_WhenVersionIsNull` |
| 3 | Benutzer-ID vollständig entfernen (kein `title`/Tooltip) | Erledigt | `LoginStatusTests.DoesNotRenderUserId_WhenAuthenticated` |
| 4 | Nicht-authentifizierter Zweig unverändert (Login-Link, kein Versionstext) | Erledigt | `LoginStatusTests.RendersLoginLink_WhenNotAuthenticated` |
| 5 | bUnit-Testklasse `LoginStatusTests` inkl. Test-Doubles `FakeCurrentUserService` / `FakeInstalledReleaseMetadataProvider` anlegen | Erledigt | `LoginStatusTests` (alle vier Fact-Methoden) |
| 6 | E2E-Test `VersionDisplayPlaywrightTests`: Versionstext/Fallback statt Benutzer-ID nach Login | Erledigt | `VersionDisplayPlaywrightTests.Login_ShowsVersionText_InsteadOfUserId` |
