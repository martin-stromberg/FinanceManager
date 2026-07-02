# Testplan: Wertpapierkurse (ING)

> **Basis:** `Docs/tests/wertpapierkurse-ing-testluecken.md`  
> **Stand:** 2026-07-02  
> **Ziel:** Alle WPK-Lücken mit direkt umsetzbaren Tests schließen (P1 zuerst).

---

## Umsetzungsstand (Ist)

Die identifizierten Kernlücken wurden implementiert und liegen in folgenden Testdateien:

- `FinanceManager.Tests.Integration/ApiClient/ApiClientSecuritiesTests.cs` (Import-Endpunkt: Erfolg, NotFound, BadRequest)
- `FinanceManager.Tests/Shared/ApiClientSecuritiesImportPricesTests.cs` (Multipart + DTO/Fehler-Mapping)
- `FinanceManager.Tests/ViewModels/SecurityPricesViewModelTests.cs` (Ribbon/Overlay-Flow auf Kursseite)
- `FinanceManager.Tests/Components/SecurityPriceImportPanelTests.cs` (UI-Importinteraktion)
- `FinanceManager.Tests/Infrastructure/Securities/IngSecurityPriceImportServiceTests.cs` (Parser/CanHandle)
- `FinanceManager.Tests/Infrastructure/Securities/SecurityPriceServiceUpsertTests.cs` (Upsert-Guard- und Randfälle)

Damit ist der Plan weiterhin als Referenz nutzbar, der aktuelle Umsetzungsstand ist jedoch bereits im Testcode abgebildet.

---

## Umsetzungsreihenfolge (priorisiert)

1. **WPK-GAP-001** (P1) – API-Endpoint Erfolgsfall  
2. **WPK-GAP-002** (P1) – API-Endpoint Owner-Scoping/NotFound  
3. **WPK-GAP-003** (P1) – API-Endpoint Bad-Request-Pfade  
4. **WPK-GAP-004** (P1) – Shared ApiClient Import-Methode  
5. **WPK-GAP-005** (P2) – SecurityCardViewModel Import-Action/Overlay  
6. **WPK-GAP-006** (P2) – SecurityPriceImportPanel Interaktion/Fehler  
7. **WPK-GAP-007** (P2) – `IngSecurityPriceImportService.CanHandle`  
8. **WPK-GAP-008** (P2) – Parser-Fehlerfälle im ING-Import  
9. **WPK-GAP-009** (P2) – Guard-/Randfälle `SecurityPriceService.UpsertDailyPricesAsync`

---

## WPK-GAP-001 [P1] – API-Endpoint Erfolgsfall (E2E)

- **Ziel-Testdatei:** `FinanceManager.Tests.Integration/ApiClient/SecuritiesImportEndpointTests.cs` *(neu)*
- **Testklasse:** `SecuritiesImportEndpointTests`

### Konkrete Testfälle
1. **`ImportPrices_ShouldReturn200AndExpectedCounters_WhenValidIngCsvUploaded`**  
   - **Given:** Authentifizierter User, bestehendes eigenes Security-Objekt, valide ING-CSV mit mix aus neu/gleich/aktualisiert.  
   - **When:** `POST /api/securities/{id}/prices/import` mit `multipart/form-data` (`file`, `provider=ing`).  
   - **Then:** `200 OK`, Response enthält erwartete Zähler (`Inserted/Updated/Unchanged/Skipped/Errors`), danach `GET /prices` zeigt persistierte Werte.

### Testdoubles / Fixtures / Datenaufbau
- `TestWebApplicationFactory` als Fixture.
- Seed über API (Register + Security Create), keine direkten DB-Manipulationen nötig.
- CSV als `MemoryStream` (Header `Zeit;Kurs`, Datum `dd.MM.yyyy HH:mm:ss`, Dezimal mit Komma).

### Definition of Done
- Test läuft stabil grün im Integration-Projekt.
- Assert deckt sowohl HTTP-Status als auch alle Import-Zähler ab.
- Persistenz wurde über nachgelagerten Read (`Securities_GetPricesAsync`) verifiziert.

---

## WPK-GAP-002 [P1] – API-Endpoint Owner-Scoping / NotFound

- **Ziel-Testdatei:** `FinanceManager.Tests.Integration/ApiClient/SecuritiesImportEndpointTests.cs` *(Erweiterung)*
- **Testklasse:** `SecuritiesImportEndpointTests`

### Konkrete Testfälle
1. **`ImportPrices_ShouldReturn404_WhenSecurityDoesNotExist`**  
   - **Given:** Authentifizierter User, zufällige nicht existente `securityId`.  
   - **When:** Import-Request gegen diese ID.  
   - **Then:** `404 NotFound`.
2. **`ImportPrices_ShouldReturn404_WhenSecurityBelongsToAnotherUser`**  
   - **Given:** User A besitzt Security, User B ist eingeloggt.  
   - **When:** User B importiert auf Security von User A.  
   - **Then:** `404 NotFound`.

### Testdoubles / Fixtures / Datenaufbau
- Zwei getrennte authentifizierte Clients (eigene Cookie-Container) auf derselben `TestWebApplicationFactory`.
- Security-Erstellung unter User A, Import-Aufruf unter User B.

### Definition of Done
- Beide NotFound-Pfade sind separat getestet und grün.
- Kein False-Positive durch fehlende Authentifizierung (nur Owner-Scoping-Fall).

---

## WPK-GAP-003 [P1] – API-Endpoint Bad-Request-Pfade

- **Ziel-Testdatei:** `FinanceManager.Tests.Integration/ApiClient/SecuritiesImportEndpointTests.cs` *(Erweiterung)*
- **Testklasse:** `SecuritiesImportEndpointTests`

### Konkrete Testfälle
1. **`ImportPrices_ShouldReturn400_WhenFileIsEmpty`**  
   - **Given:** Eigene Security, leere Datei (`Length=0` oder leerer Stream).  
   - **When:** Import-Aufruf.  
   - **Then:** `400 BadRequest`, Fehlercode/Payload für ungültige Datei.
2. **`ImportPrices_ShouldReturn400_WhenProviderIsUnsupported`**  
   - **Given:** Eigene Security, valide CSV, `provider=unknown-provider`.  
   - **When:** Import-Aufruf.  
   - **Then:** `400 BadRequest`, Fehlerpayload referenziert Provider-Fehler.
3. **`ImportPrices_ShouldReturn400_WhenNoValidRowsAreFound`**  
   - **Given:** Eigene Security, CSV mit nur invalider Datenzeile(n).  
   - **When:** Import-Aufruf.  
   - **Then:** `400 BadRequest`, Fehlercode `Err_Invalid_Import`.

### Testdoubles / Fixtures / Datenaufbau
- Gleiche Fixture wie GAP-001.
- Drei explizite CSV-Varianten: leer, valide+falscher Provider, nur ungültige Zeilen.

### Definition of Done
- Alle drei 400-Pfade grün.
- Statuscode + Fehlerpayload (mind. `code`, `message`) werden je Fall geprüft.

---

## WPK-GAP-004 [P1] – Shared ApiClient Import-Methode

- **Ziel-Testdatei:** `FinanceManager.Tests/Shared/ApiClientSecuritiesImportPricesTests.cs` *(neu)*
- **Testklasse:** `ApiClientSecuritiesImportPricesTests`

### Konkrete Testfälle
1. **`Securities_ImportPricesAsync_ShouldSendMultipartWithFileAndProvider_WhenCalled`**  
   - **Given:** `ApiClient` mit delegierendem `HttpMessageHandler`, Stream + Dateiname + Provider.  
   - **When:** `Securities_ImportPricesAsync` wird aufgerufen.  
   - **Then:** Request ist `POST /api/securities/{id}/prices/import`, `multipart/form-data` enthält Part `file` (inkl. Dateiname/ContentType) und `provider`.
2. **`Securities_ImportPricesAsync_ShouldDeserializeResultDto_WhenApiReturns200`**  
   - **Given:** Handler liefert `200` mit JSON-`SecurityPriceImportResultDto`.  
   - **When:** ApiClient-Methode läuft.  
   - **Then:** Rückgabe-DTO enthält korrekte Zähler und Fehlerliste.
3. **`Securities_ImportPricesAsync_ShouldSetLastError_WhenApiReturns400`**  
   - **Given:** Handler liefert `400` mit API-Error-Payload.  
   - **When:** ApiClient-Methode läuft.  
   - **Then:** Exception/Fehlerpfad tritt auf und `LastError`/`LastErrorCode` werden gesetzt.

### Testdoubles / Fixtures / Datenaufbau
- `DelegateHandler : HttpMessageHandler` wie in bestehenden ViewModel-Tests.
- JSON-Responses als StringContent.
- Kein WebApplicationFactory nötig (isolierter Client-Contract-Test).

### Definition of Done
- Multipart-Aufbau und DTO-Deserialisierung sind mit harten Asserts abgesichert.
- Error-Mapping (`LastError*`) ist explizit getestet.

---

## WPK-GAP-005 [P2] – UI ViewModel Ribbon-/Overlay-Flow

- **Ziel-Testdatei:** `FinanceManager.Tests/ViewModels/SecurityPricesViewModelTests.cs`
- **Testklasse:** `SecurityPricesViewModelTests`

### Konkrete Testfälle
1. **`RibbonImportPricesAction_ShouldOpenImportOverlay`**  
   - **Given:** Geladene Kursliste mit gültiger `SecurityId`, Subscriber auf `UiActionRequested`.  
   - **When:** Ribbon-Action `ImportPrices` wird ausgeführt.  
   - **Then:** Action `OpenOverlay` wird gesendet, Payload ist `UiOverlaySpec` mit `ComponentType == SecurityPriceImportPanel`.
2. **`RequestOpenImport_RaisesOverlayWithImportPanelAndSecurityId`**  
   - **Given:** `SecurityPricesListViewModel` mit gültiger `SecurityId`.  
   - **When:** `RequestOpenImport()` wird direkt ausgelöst.  
   - **Then:** Overlay-Parameter enthalten `SecurityId` und Import-Panel-Typ.

### Testdoubles / Fixtures / Datenaufbau
- `Mock<IApiClient>` + minimaler ServiceProvider analog bestehender ViewModel-Tests.
- Test-Localizer für stabile Labelkeys.
- Ribbon-Auswahl per `Action == "ImportPrices"`.

### Definition of Done
- Overlay-Flow ist auf der Kursseite (`SecurityPricesListViewModel`) abgesichert.
- Test validiert konkret `SecurityId` + `SecurityPriceImportPanel`.

---

## WPK-GAP-006 [P2] – UI Panel Import-Interaktion / Fehleranzeige

- **Ziel-Testdatei:** `FinanceManager.Tests/Components/SecurityPriceImportPanelTests.cs` *(neu)*
- **Testklasse:** `SecurityPriceImportPanelTests`

### Konkrete Testfälle
1. **`Panel_ShouldCallApiAndRenderCounters_WhenImportSucceeds`**  
   - **Given:** Renderte Komponente mit gültiger `SecurityId`, API-Mock liefert Importresultat.  
   - **When:** Datei wird ausgewählt und Import-Button geklickt.  
   - **Then:** `Securities_ImportPricesAsync` wurde aufgerufen, Ergebniszähler (`Inserted/Updated/Unchanged/Skipped`) werden gerendert.
2. **`Panel_ShouldRenderLineErrors_WhenResultContainsErrors`**  
   - **Given:** API-Mock liefert Result mit `Errors`.  
   - **When:** Import erfolgreich abgeschlossen.  
   - **Then:** Fehlerliste (`<li>`) mit Zeilennummer/Message sichtbar.
3. **`Panel_ShouldShowErrorAndHideResult_WhenApiThrows`**  
   - **Given:** API-Mock wirft Exception und setzt `LastError`.  
   - **When:** Import wird gestartet.  
   - **Then:** `_error`-Block sichtbar, Result-Block nicht gerendert.

### Testdoubles / Fixtures / Datenaufbau
- bUnit `BunitContext`.
- `Mock<IApiClient>` + `PagesStringLocalizer`.
- Test-`IBrowserFile` (Fake/Stub) mit CSV-Inhalt für `InputFile`.

### Definition of Done
- Erfolgs- und Fehlerpfad im Panel sind jeweils automatisiert abgedeckt.
- API-Aufrufparameter (`SecurityId`, `provider=ing`, Dateiname) sind verifiziert.

---

## WPK-GAP-007 [P2] – `CanHandle`-Entscheidungslogik

- **Ziel-Testdatei:** `FinanceManager.Tests/Infrastructure/Securities/IngSecurityPriceImportServiceTests.cs` *(Erweiterung)*
- **Testklasse:** `IngSecurityPriceImportServiceTests`

### Konkrete Testfälle
1. **`CanHandle_ShouldReturnTrue_WhenProviderIsIng_IgnoringCaseAndWhitespace`** *(Theory)*  
   - **Given:** Context mit Provider-Varianten (`"ing"`, `" ING "`, `"InG"`).  
   - **When:** `CanHandle(context)`.  
   - **Then:** `true`.
2. **`CanHandle_ShouldReturnTrue_WhenProviderMissingButFileExtensionIsCsv`**  
   - **Given:** Provider leer/null, Dateiname `prices.csv`.  
   - **When:** `CanHandle(context)`.  
   - **Then:** `true`.
3. **`CanHandle_ShouldReturnFalse_WhenProviderNotIngAndFileIsNotCsv`**  
   - **Given:** Provider `other`, Dateiname `prices.txt`.  
   - **When:** `CanHandle(context)`.  
   - **Then:** `false`.

### Testdoubles / Fixtures / Datenaufbau
- Kein DB-Setup nötig.
- `Mock<ISecurityPriceService>` nur für Konstruktorabhängigkeit.
- `SecurityPriceImportContext`-Instanzen pro Variante.

### Definition of Done
- Provider- und Dateiendungs-Fallback vollständig als Unit-Tests dokumentiert.
- Theory deckt Case/Whitespace robust ab.

---

## WPK-GAP-008 [P2] – Parser-Fehlerfälle ING-Import

- **Ziel-Testdatei:** `FinanceManager.Tests/Infrastructure/Securities/IngSecurityPriceImportServiceTests.cs` *(Erweiterung)*
- **Testklasse:** `IngSecurityPriceImportServiceTests`

### Konkrete Testfälle
1. **`ImportAsync_ShouldSkipRowAndAddError_WhenColumnsAreMissing`**  
   - **Given:** CSV-Zeile ohne erforderliche `Kurs`-Spalte bzw. unvollständige Trennung.  
   - **When:** `ImportAsync`.  
   - **Then:** `Skipped` erhöht, Fehlerliste enthält Zeilenfehler.
2. **`ImportAsync_ShouldSkipRowAndAddError_WhenCloseIsNegative`**  
   - **Given:** CSV mit negativem Kurswert.  
   - **When:** `ImportAsync`.  
   - **Then:** Zeile wird nicht importiert, Fehler wird im Result geführt.
3. **`ImportAsync_ShouldContinueWithValidRows_WhenMixedValidAndInvalidRowsExist`**  
   - **Given:** Mix aus validen + oben genannten invaliden Zeilen.  
   - **When:** `ImportAsync`.  
   - **Then:** valide Zeilen gehen an `UpsertDailyPricesAsync`, invalide werden als `Skipped`+`Errors` gezählt.

### Testdoubles / Fixtures / Datenaufbau
- CSV als UTF8-`MemoryStream`.
- `Mock<ISecurityPriceService>` zur Verifikation der tatsächlich weitergereichten validen Items.

### Definition of Done
- Fehlende Spalten und negative Kurse sind explizit getestet.
- Parser-Verhalten ist für Mischdateien stabil abgesichert.

---

## WPK-GAP-009 [P2] – Upsert-Guard-/Randfälle

- **Ziel-Testdatei:** `FinanceManager.Tests/Infrastructure/Securities/SecurityPriceServiceUpsertTests.cs` *(Erweiterung)*
- **Testklasse:** `SecurityPriceServiceUpsertTests`

### Konkrete Testfälle
1. **`UpsertDailyPricesAsync_ShouldThrow_WhenSecurityIsNotOwnedByUser`**  
   - **Given:** Security gehört User A, Aufruf mit User B.  
   - **When:** `UpsertDailyPricesAsync`.  
   - **Then:** erwartete Exception (Ownership-Guard).
2. **`UpsertDailyPricesAsync_ShouldReturnZeroCounters_WhenItemsAreEmpty`**  
   - **Given:** gültige eigene Security, leere Item-Liste.  
   - **When:** `UpsertDailyPricesAsync`.  
   - **Then:** alle Zähler `0`, keine Persistenzänderung.
3. **`UpsertDailyPricesAsync_ShouldThrow_WhenCloseIsNegative`**  
   - **Given:** Item-Liste enthält negativen `Close`.  
   - **When:** `UpsertDailyPricesAsync`.  
   - **Then:** erwartete Exception, keine Datensatzänderung.

### Testdoubles / Fixtures / Datenaufbau
- `AppDbContext` InMemory wie in bestehender Testklasse.
- Zwei User + eine Security für Ownership-Fall.
- Bestehende Preise als Baseline zur Persistenz-Assertion.

### Definition of Done
- Alle drei Guard-Fälle sind grün.
- Exception-Typ und Nebenwirkungen (keine ungewollten Writes) sind verifiziert.

---

## Empfohlene Implementierungsreihenfolge (Dateiebene)

1. `FinanceManager.Tests.Integration/ApiClient/SecuritiesImportEndpointTests.cs` (GAP-001..003)
2. `FinanceManager.Tests/Shared/ApiClientSecuritiesImportPricesTests.cs` (GAP-004)
3. `FinanceManager.Tests/Web/ViewModels/Securities/SecurityCardViewModelTests.cs` (GAP-005)
4. `FinanceManager.Tests/Components/SecurityPriceImportPanelTests.cs` (GAP-006)
5. Erweiterung `IngSecurityPriceImportServiceTests.cs` (GAP-007..008)
6. Erweiterung `SecurityPriceServiceUpsertTests.cs` (GAP-009)

---

## Ausführung (nach Implementierung)

```powershell
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter "FullyQualifiedName~SecuritiesImportEndpointTests"
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~ApiClientSecuritiesImportPricesTests|FullyQualifiedName~SecurityCardViewModelTests|FullyQualifiedName~SecurityPriceImportPanelTests|FullyQualifiedName~IngSecurityPriceImportServiceTests|FullyQualifiedName~SecurityPriceServiceUpsertTests"
dotnet test FinanceManager.sln
```
