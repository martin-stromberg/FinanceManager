# Enums – Bestand

Für die Anforderung sind nur bestehende Enum-Werte relevant; die Anforderung schließt neue Scopes (und damit neue `McpDataType`-/`McpRightKind`-Werte) ausdrücklich aus.

## `McpDataType`
Datei: `FinanceManager.Domain/Mcp/McpDataType.cs`

Relevante Werte (von 19 definierten, `Accounts = 0` … `Administration = 18`):

| Wert | Bedeutung |
|------|-----------|
| `Securities = 9` | „Securities incl. price history" (Zeilen 41–42) — Datentyp, den `McpRecordScopeFilter.ApplyAsync` im Handler filtert und den das Required-Scope-Tupel verlangt; Kurse fallen unter denselben Scope |
| `SecurityCategories = 10` | „Security categories" (Zeilen 44–45) — zweiter Required Scope; zugleich die Filterdimension über `Security.CategoryId` (`SecurityCategoryIdsCsv` in `McpAccessScope`) |

Vollständige Memberliste wird durch `EnumCoverageTests.McpDataType_Values` (`FinanceManager.Tests/Common/EnumCoverageTests.cs` Zeilen 400 ff.) assertiert — neue Member müssten dort ergänzt werden (laut Anforderung nicht nötig).

## `McpRightKind`
Datei: `FinanceManager.Domain/Mcp/McpRightKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Read = 0` | Lesen/Auflisten/Exportieren — die vom Tool genutzte Rechteart (Zeilen 13–14) |
| `Suggest = 1` | Vorschlagsrecht (nicht betroffen) |

## `McpToolSecurityClass`
Datei: `FinanceManager.Domain/Mcp/McpToolSecurityClass.cs`

| Wert | Bedeutung |
|------|-----------|
| `Lesend = 0` | Sicherheitsklasse des Tools (Zeile 13) — bleibt unverändert (`FmSecuritiesAnalysisTool.cs` Zeile 32) |

## `McpAccessVerdict`
Datei: `FinanceManager.Application/Mcp/IMcpScopeEvaluator.cs` (Zeilen 8–21)

| Wert | Bedeutung |
|------|-----------|
| `Allowed = 0` | Datensatz sichtbar |
| `Denied = 1` | Durch Scope/Filter/Lid abgelehnt |
| `NotFound = 2` | Besitzerprüfung fehlgeschlagen — verhält sich wie „nicht vorhanden" (404-Semantik); entspricht dem bereits beobachteten Verhalten des Einzelpfads bei gefiltertem `securityId` |

## `McpAccessLevel` / `McpApprovalMode` / `McpToolExecutionMode`
- `McpAccessLevel` (`FinanceManager.Domain/Mcp/`): `Analyse` (Level der Test-Helfer), `Individuell` (einzige Stufe mit persistierten Scope-Zeilen → erst hier greifen Record-Filter-CSVs).
- `McpApprovalMode.PerAction`: Standard der Test-Helfer in `McpReadOnlyToolsTests`/`McpBudgetOverviewToolTests`.
- `McpToolExecutionMode` (`IMcpToolHandler.cs` Zeilen 15–20): `Interactive` (Default) vs. `ApprovedExecution` — für den rein lesenden Handler ohne Bedeutung.
