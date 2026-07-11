# Blazor Resources

## Relevante Dateien

- `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.de.resx`
- `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.en.resx`

## Bestehende Keys

Die Report-Dashboard-Ressourcen enthalten Labels fuer:

- `FilterGroup_Comparisons`
- `Label_ComparePrev`
- `Label_CompareYear`
- `Th_Amount`
- `Th_Prev`
- `Th_DeltaPrev`
- `Th_YearAgo`
- `Th_DeltaYear`
- `Total_Label`

## Benoetigte Keys

Fuer die neue Option und Spalte werden mindestens benoetigt:

- Label fuer Checkbox: `Label_CompareProjection` oder `Label_Projection`
- Tabellenkopf: `Th_Projection`
- optional Tooltip/Hinweis fuer deaktivierte Option: `Hint_ProjectionSecurityOnly`

## Encoding-Hinweis

Die vorhandenen `.resx`-Dateien enthalten teils ASCII-Umschreibungen (`fuer`, `ausgewaehlte`) und teils Sonderzeichen. Bei neuen deutschen Texten sollte der bestehende Stil der jeweiligen Datei beachtet werden. Fuer die sichtbare UI ist "Hochrechnung" fachlich vorgegeben.
