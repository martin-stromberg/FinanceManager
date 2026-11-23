# Bestandsaufnahme: MCP `fm_securities_analysis` — `latestPrice` (letzter gespeicherter Kurs inkl. Datum) im Wertpapier-Ausgabeobjekt (Issue #559)

Analysiert wurden der MCP-Lesehandler `FmSecuritiesAnalysisHandler` (`FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`), die Kursabfrage-Kette `ISecurityPriceService`/`SecurityPriceService`, die beteiligten DTOs/Entities (`SecurityDto`, `SecurityPriceDto`, `Security`, `SecurityPrice`), der Record-Scope-Filter `McpRecordScopeFilter` sowie der Test- und Dokumentationsbestand rund um das Tool T07 — bezogen auf die Anforderung, pro Wertpapier ein always-on `latestPrice { close, date }` auszugeben.

## Zusammenfassung

- **Ausgabeobjekt ohne `latestPrice`:** `FmSecuritiesAnalysisHandler.InvokeAsync` (`FmSecuritiesAnalysisTool.cs` Zeilen 73–162) erzeugt pro sichtbarem Wertpapier ein anonymes Objekt mit `id`, `categoryId`, `currencyCode`, `isActive`, `createdUtc`, `archivedUtc`, `symbolAttachmentId`, `hasPriceError`, `openQuantity`, `prices` und `untrusted_data` — kein Feld für den letzten gespeicherten Kurs. `prices` wird nur bei `includePrices` gefüllt (Einzelpfad Default `true`, Listenpfad Default `false`).
- **Filter-Reihenfolge bereits korrekt:** `McpRecordScopeFilter.ApplyAsync` (Zeilen 111–113, Dimension `Security.CategoryId`) läuft **vor** jeder Kursabfrage; ein nicht gefundenes/fremdes oder scope-gefiltertes `securityId` erzeugt bereits heute eine leere `securities`-Liste — die offene Frage 5 der Anforderung ist damit faktisch beantwortet (geführt wie „nicht vorhanden").
- **N+1-Falle besteht:** Die Kursabfrage erfolgt derzeit pro Wertpapier über `_prices.ListAsync(owner, s.Id, 0, priceTake, ct)` (Zeile 121) — mit impliziter Besitzerprüfung je Abfrage (`SecurityPriceService.ListAsync` Zeilen 88–89). Ein always-on `latestPrice` im Listenpfad würde dieses Muster auf jede Listenausgabe ausdehnen.
- **Keine passende Service-Methode:** `ISecurityPriceService` bietet `ListAsync` (absteigende Historie; `take: 1` funktional korrekt, aber N+1) und `GetLatestDateAsync` (nur das Datum, kein Kurs) — **keine** Batch-Abfrage `SecurityId`-Menge → letzter Kurs. `SecurityPriceService` ist die einzige produktive Implementierung (`ServiceCollectionExtensions.cs` Zeile 132).
- **„Letzter Kurs" ist deterministisch:** Unique-Index `{ SecurityId, Date }` auf `SecurityPrice` (`AppDbContext.cs` Zeile 721) — pro Wertpapier und Datum höchstens ein Kurs; Mehrfach-Kurse am selben Datum (offene Frage 4) sind modellseitig ausgeschlossen.
- **Wiederverwendbare Shape:** `SecurityPriceDto(DateTime Date, decimal Close)` deckt exakt `{ close, date }` ab; `prices[].date` wird als `DateTime` via `System.Text.Json` serialisiert (offene Frage 3 zum Datumsformat bleibt eine Design-Entscheidung — Bestand ist ISO-8601 mit Zeitanteil).
- **`hasPriceError` bereits im Output:** Das Flag stammt aus `SecurityDto.HasPriceError` (`Security.HasPriceError`, Zeile 105) und ist unabhängig von gespeicherten Kursen; die Anforderungs-Entscheidung (letzter Kurs trotz Fehlerflag vs. `null`) betrifft nur die neue Feldlogik — Bestand enthält keine Verknüpfung.
- **Compile-Breaker beachten:** `SecurityPriceErrorRecoveryTests.SpySecurityPriceService` (`FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs` Zeilen 37–85) implementiert `ISecurityPriceService` manuell — ein neues Interface-Member erfordert dessen Erweiterung.
- **Variante-(b)-Auswirkungen erfasst:** `SecurityDto` trägt kein Kursfeld; es speist `GET /api/securities` (`SecuritiesController.ListAsync`, Zeilen 109–112) und den MCP-Handler gleichermaßen — eine DTO-Erweiterung würde API und Blazor-UI treffen.
- **Dokumentation:** `Docs/help/mcp-server/api.md` (Tabelle „Werkzeugverträge (Detailergänzungen)") enthält keinen Eintrag für `fm_securities_analysis`; `Docs/planung/mcp-integration/scope-und-toolkatalog.md` Zeile 102 führt T07 mit `ISecurityPriceService`-Referenz.
- **Test-Ausgangszustand:** Unit-Suite 2428/2428 bestanden, Integration-Suite 176/176 bestanden, E2E-Suite 167/176 bestanden mit **9 Fehlschlägen** (8× Timeout/Umgebung — davon 1 flaky und im Re-Lauf grün, 1× deterministischer Assert auf Statement-Draft-Link) — alle ohne fachlichen Bezug zu `fm_securities_analysis`. Details, Re-Lauf und TRX-Nachweise: [Tests](inventory/tests.md).
- **Testlücke:** Kein einziger Test ruft `FmSecuritiesAnalysisHandler.InvokeAsync` auf — T07 ist nur auf Descriptor-/Scope-Ebene abgedeckt (`McpReadOnlyToolsTests` Zeilen 75, 86, 114); Handler-/Shape-Tests sowie Tests einer etwaigen Batch-Methode fehlen vollständig.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
