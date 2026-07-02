# Testlücken – Feature „Wertpapierkurse (ING)“

- **[WPK-GAP-001] [P1] API-Endpoint: Erfolgsfall des Imports ist ungetestet (Ende-zu-Ende)**
  - **Nicht abgedeckte Funktionalität:** `POST /api/securities/{id}/prices/import` liefert bei gültiger ING-Datei `200 OK` mit korrekten Zählern (`inserted/updated/unchanged/skipped/errors`).
  - **Betroffene produktive Dateien:** `FinanceManager.Web/Controllers/SecuritiesController.cs`, `FinanceManager.Infrastructure/Securities/IngSecurityPriceImportService.cs`, `FinanceManager.Infrastructure/Securities/SecurityPriceService.cs`
  - **Bestehende zugehörige Tests:** Kein API-/Integrationstest für den Endpoint vorhanden (nur Unit-Tests auf Service-Ebene).
  - **Testbares Akzeptanzkriterium:** Bei Upload einer gültigen ING-CSV auf ein bestehendes eigenes Wertpapier antwortet die API mit `200` und einem `SecurityPriceImportResultDto`, dessen Zähler den tatsächlich persistierten Änderungen entsprechen.

- **[WPK-GAP-002] [P1] API-Endpoint: Owner-Scoping/NotFound-Pfad ist ungetestet**
  - **Nicht abgedeckte Funktionalität:** Import darf nur für eigene Wertpapiere erfolgen; fremde/nicht existente IDs müssen `404` liefern.
  - **Betroffene produktive Dateien:** `FinanceManager.Web/Controllers/SecuritiesController.cs`
  - **Bestehende zugehörige Tests:** Kein Test für `ImportPricesAsync` mit fremder oder unbekannter `securityId`.
  - **Testbares Akzeptanzkriterium:** Für eine `securityId`, die nicht dem aktuellen User gehört (oder nicht existiert), liefert `POST /prices/import` immer `404`.

- **[WPK-GAP-003] [P1] API-Endpoint: Bad-Request-Pfade für Datei/Provider/leerem Import sind ungetestet**
  - **Nicht abgedeckte Funktionalität:** `400` bei leerer Datei, nicht unterstütztem Provider oder wenn keine validen Kurszeilen gefunden wurden.
  - **Betroffene produktive Dateien:** `FinanceManager.Web/Controllers/SecuritiesController.cs`, `FinanceManager.Infrastructure/Securities/SecurityPriceImportServiceFactory.cs`
  - **Bestehende zugehörige Tests:** Kein API-Test für Fehlerpfade des Import-Endpunkts.
  - **Testbares Akzeptanzkriterium:** Die API liefert für (a) leere Datei, (b) unbekannten Provider, (c) Datei ohne valide Zeilen jeweils `400` mit Fehlerpayload.

- **[WPK-GAP-004] [P1] Shared ApiClient: Import-Methode ist ungetestet**
  - **Nicht abgedeckte Funktionalität:** `Securities_ImportPricesAsync` baut `multipart/form-data` korrekt auf (Datei + Provider) und deserialisiert das Importresultat.
  - **Betroffene produktive Dateien:** `FinanceManager.Shared/ApiClient.Securities.cs`, `FinanceManager.Shared/IApiClient.cs`
  - **Bestehende zugehörige Tests:** `FinanceManager.Tests.Integration/ApiClient/ApiClientSecuritiesTests.cs` nutzt den Import-Call nicht.
  - **Testbares Akzeptanzkriterium:** Ein ApiClient-Integrationstest ruft `Securities_ImportPricesAsync` erfolgreich auf und verifiziert, dass die Response-Werte korrekt im DTO ankommen.

- **[WPK-GAP-005] [P2] UI ViewModel: Ribbon-/Overlay-Flow für Import ist ungetestet**
  - **Nicht abgedeckte Funktionalität:** „Import“-Aktion ist nur im Detailkontext nutzbar und löst `OpenImportPrices` mit `SecurityPriceImportPanel` aus.
  - **Betroffene produktive Dateien:** `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs`
  - **Bestehende zugehörige Tests:** Keine Tests für `SecurityCardViewModel` vorhanden.
  - **Testbares Akzeptanzkriterium:** Bei `Id == Guid.Empty` ist die Aktion deaktiviert/ohne Overlay; bei gültiger `Id` wird ein Overlay mit `SecurityId` und Komponente `SecurityPriceImportPanel` angefordert.

- **[WPK-GAP-006] [P2] UI Panel: Import-Interaktion und Fehleranzeige sind ungetestet**
  - **Nicht abgedeckte Funktionalität:** Panel ruft bei Dateiauswahl den ApiClient auf, zeigt Ergebniszähler und Fehlerliste bzw. API-Fehler an.
  - **Betroffene produktive Dateien:** `FinanceManager.Web/Components/Shared/SecurityPriceImportPanel.razor`
  - **Bestehende zugehörige Tests:** Keine Komponententests für `SecurityPriceImportPanel` vorhanden.
  - **Testbares Akzeptanzkriterium:** Nach erfolgreichem Import werden alle Zähler gerendert; bei API-Fehler wird `_error` angezeigt und kein Ergebnisblock gerendert.

- **[WPK-GAP-007] [P2] Import-Service: `CanHandle`-Entscheidungslogik ist nur teilweise abgedeckt**
  - **Nicht abgedeckte Funktionalität:** Fallback über Dateiendung `.csv` sowie Provider-Case/Whitespace-Verhalten von `CanHandle`.
  - **Betroffene produktive Dateien:** `FinanceManager.Infrastructure/Securities/IngSecurityPriceImportService.cs`
  - **Bestehende zugehörige Tests:** `FinanceManager.Tests/Infrastructure/Securities/IngSecurityPriceImportServiceTests.cs` testet nur `ImportAsync`, nicht `CanHandle`.
  - **Testbares Akzeptanzkriterium:** Unit-Tests zeigen: `provider=ing` (auch mit abweichender Groß-/Kleinschreibung/Whitespace) akzeptiert, `.csv` ohne Provider akzeptiert, nicht-CSV ohne passenden Provider wird abgelehnt.

- **[WPK-GAP-008] [P2] Import-Service: zentrale Parser-Fehlerfälle sind ungetestet**
  - **Nicht abgedeckte Funktionalität:** Validierung für fehlende Spalten und negative Kurse wird nicht explizit getestet.
  - **Betroffene produktive Dateien:** `FinanceManager.Infrastructure/Securities/IngSecurityPriceImportService.cs`
  - **Bestehende zugehörige Tests:** `IngSecurityPriceImportServiceTests` deckt nur ungültiges Datum/ungültiges Zahlenformat ab.
  - **Testbares Akzeptanzkriterium:** Für Zeilen mit fehlenden Spalten und für negative `Close`-Werte erhöht sich `Skipped`; passende Zeilenfehler sind im Result enthalten.

- **[WPK-GAP-009] [P2] Upsert-Logik: Guard-/Randfälle sind ungetestet**
  - **Nicht abgedeckte Funktionalität:** Verhalten bei fremdem Wertpapier (Exception), leerer Item-Liste (0-Zähler), negativem `Close` (Exception).
  - **Betroffene produktive Dateien:** `FinanceManager.Infrastructure/Securities/SecurityPriceService.cs`
  - **Bestehende zugehörige Tests:** `FinanceManager.Tests/Infrastructure/Securities/SecurityPriceServiceUpsertTests.cs` deckt nur Mischfall + Duplicate-Date ab.
  - **Testbares Akzeptanzkriterium:** Unit-Tests verifizieren die drei Guard-Fälle inklusive erwarteter Exceptions bzw. Nullzählung ohne Persistenzänderung.
