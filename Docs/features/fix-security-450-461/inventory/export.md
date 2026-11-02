# CSV-/XLSX-Export (#457)

## `ExportFileHelper`
Datei: `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` (`internal static`)

| Methode | Zeile | Kurzbeschreibung |
|---------|-------|------------------|
| `CsvEscape(string?)` | 20–25 | **Finding-Stelle**: entfernt `\r`, ersetzt `\n` durch Leerzeichen, verdoppelt `"`, setzt den Wert in Anführungszeichen. **Keine Behandlung von Formel-Präfixen** (`=`, `+`, `-`, `@`) und führenden Steuerzeichen (Tab, `\v` etc.). Da alle Werte gequotet sind, reicht das Quoting allein nicht gegen Formel-Interpretation durch Excel/LibreOffice. |
| `WriteCsvAsync` | 35–45 | UTF-8 **mit BOM**, Header + `string.Join(';', cols)` — die Spalten werden bereits gequotet übergeben. |
| `WriteXlsx` / `CreateRow` | 55–121 | OpenXML-Erzeugung; Strings werden mit `DataType = CellValues.String` geschrieben → **nicht formel-auswertbar** (literal). |
| `ResolveName` / `Resolve*NamesAsync` | 129–190 | Owner-beschränkte Namensauflösung (Accounts/Contacts/SavingsPlans/Securities). |

## CSV-Verbraucher von `CsvEscape`
| Datei | Zeilen | Userkontrollierte Spalten |
|-------|--------|---------------------------|
| `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` | 332–341 | `CurrencyCode`, `Subject`, `RecipientName`, `BookingDescription`, `contactName`, `savingsName`, `securityName` — Header (Z. 15): `EntryNumber;BookingDate;ValutaDate;Amount;Currency;Subject;RecipientName;Description;Status;IsAnnounced;IsCostNeutral;Contact;SavingsPlan;Security;SecurityTransactionType;SecurityQuantity;SecurityFeeAmount;SecurityTaxAmount` |
| `FinanceManager.Infrastructure/Reports/PostingExportService.cs` | 421–427 | `Subject`, `RecipientName`, `Description`, `accountName`, `contactName`, `savingsName`, `securityName` — Header (Z. 15): `BookingDate;ValutaDate;Amount;Kind;Subject;RecipientName;Description;Account;Contact;SavingsPlan;Security;SecuritySubType;Quantity` |

**Achtung Lücke:** Beide Services schreiben numerische Felder (`Amount`, `Quantity`, `EntryNumber` etc.) und Datumsfelder offenbar ungequotet in das Spalten-Array — `CsvEscape` deckt nur die String-Spalten ab; Zeilenbau jeweils in privaten Row-Mappern (Z. ~415–430 / ~325–345). Zu prüfen, ob auch Zahlen-/Datumsfelder aus Nutzerdaten (z. B. Strings via ToString) ein Formel-Risiko tragen.

## Export-Endpunkte (Ausgabe `text/csv`)
| Datei | Zeile | Endpunkt |
|-------|-------|----------|
| `FinanceManager.Web/Controllers/PostingsController.cs` | 503–511 | Export als `StreamCallbackResult` `text/csv; charset=utf-8`, Dateiname `{kind}_{name}_{yyyyMMddHHmm}.csv` via `ExportRequestHelper.SanitizeFileName`; `CountAsync` > `maxRows` → 400. |
| `FinanceManager.Web/Controllers/StatementDraftsController.cs` | 755–760 | `text/csv`-Export pro Draft, Dateiname `StatementDraft_{name}_{yyyyMMddHHmm}.csv`. |
| `PostingExportService` Z. 229 / `StatementDraftExportService` Z. 177 | — | `contentType = "text/csv"` bei `PostingExportFormat.Csv`; XLSX-Pfade via `WriteXlsx` (Z. 235/183). |

## `BudgetReportExportService`
Datei: `FinanceManager.Web/Services/BudgetReportExportService.cs` — erzeugt **ausschließlich XLSX** (`GenerateXlsxAsync`, Z. 56–283); der Klassenkommentar „Returns a small CSV stream" (Z. 24–26) ist veraltet. Textzellen via `SharedStringItem`/`CellValues.SharedString` bzw. `CellValues.String` → nicht formel-auswertbar. Registriert als `IBudgetReportExportService` (ProgramExtensions.cs:146).

## Bestehende Schutzmechanismen
- CSV: Quoting aller String-Spalten + Zeilenumbruch-Neutralisierung; Dateinamen-Sanitizing in den Controllern.
- XLSX: Zelltyp `String`/SharedString verhindert Formel-Interpretation.
- Export-Row-Limit (`maxRows` in `PostingsController`).

## Relevante Tests
- `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs`: `StreamCsvAsync_*` (4 Tests: Order, Namensauflösung, FromTo-Filter, SearchQuery), `GenerateAsync_Xlsx_ShouldReturnSpreadsheetWithRows`, `CountAsync_ShouldReturnEntryCount`.
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`: `ExportAsync_ShouldReturnCsvFile_WithEntries` (Z. 423), `..._ShouldReturnXlsxFile` (456), `..._ShouldReturnBadRequest_ForInvalidFormat` (511).
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs`.
