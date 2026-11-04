# Testergebnisse – EP-08 Stammdaten-Pflege (Issue #486)

## Befehle

```
dotnet build FinanceManager.Web/FinanceManager.Web.csproj   → 0 Fehler (nur vorhandene Warnungen)
dotnet build FinanceManager.Tests/FinanceManager.Tests.csproj → 0 Fehler
dotnet test  FinanceManager.Tests/FinanceManager.Tests.csproj
```

## Ergebnis

| Suite | Ergebnis |
|---|---|
| `FinanceManager.Tests` gesamt | **1788/1788 bestanden**, 0 fehlgeschlagen, 0 übersprungen (~39 s) |
| davon neu `McpMasterDataWriteToolsTests` | **39/39 bestanden** |

## Abgedeckte Fälle (neu)

**Katalog**: 8 Deskriptoren — Namen, Sicherheitsklasse `Schreibvorbereitend`,
Scope-Tupel exakt nach Katalog §3.4 (Kontakte/Konten/Sparpläne/Wertpapiere/
Budgets/Kategorien/Anhänge/Reports-Konfiguration), F3-Matrix-Validität,
JSON-Schema parsebar.

**T22 contacts**: create (Owner-Übergabe, untrusted_data, `ContactCreated`-Audit),
fremde CategoryId + `Self`-Typ → `Err_Mcp_InvalidArguments` ohne Service-Wirkung,
update-Merge mit Feld-Deltas (`ContactUpdated`), fremder Kontakt → `contact:null`,
Alias add/remove, Record-Filter-Denial (Kategorie-CSV) → `Err_Mcp_ScopeDenied`,
merge+delete → `Err_Mcp_ApprovalRequired` mit `MergeAsync`/`DeleteAsync`
`Times.Never`, delete ohne Löschung-Scope → `Err_Mcp_ScopeDenied`.

**T23 accounts**: create + add/removeLinkedIban wirken; update-Merge mit Deltas;
delete → approval-required ohne Wirkung.

**T24 savingsPlans**: create mit Intervall/Kategorie/ContractNumber +
`SavingsPlanCreated`-Audit; archivierter Plan → `Err_Mcp_InvalidState`;
archive → approval-required, `ArchiveAsync`/`DeleteAsync` `Times.Never`.

**T25 securities**: create inkl. Region/Sektor/AlphaVantageCode; ungültige
Currency → Denial vor Wirkung; archive → approval-required, `Times.Never`.

**T26 budgets**: purpose-create mit Source-Auflösung (fremd → Denial vor Wirkung);
rule-create purpose-gebunden inkl. Pattern/UseRegex + CustomMonths-Pflicht;
override-create (BudgetPeriodKey); category-create; purpose/category-delete →
approval-required ohne Wirkung.

**T27 categories**: create + rename mit Name-Delta (`CategoryCreated`/
`CategoryUpdated`); delete → approval-required ohne Wirkung.

**T28 attachments**: upload dekodiert base64 → Service-Stream; Audit-Payload mit
sha256 + Metadaten, niemals Inhalt; fremdes Ziel → Denial vor Wirkung; reassign
verifiziert beide Enden (fremdes Ziel → Denial, `ReassignAsync` `Times.Never`);
update/setCategory wirken; delete → approval-required.

**T29 reports_configuration**: favorite-create inkl. postingKinds + Filter-DTO;
homeKpi-create (ReportFavorite-Referenz owner-aufgelöst; fremd → Denial;
Predefined ohne predefinedType → Denial); kpi-update-Merge mit Deltas;
favorite/homeKpi-delete → approval-required ohne Wirkung.
