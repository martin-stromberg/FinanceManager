# Help-Dateien und Build-Einbindung

## Statische Help-Ablage
Verzeichnis: `FinanceManager.Web/wwwroot/help/`

- `css/help-page.css` enthält die Styles für die Help-Oberfläche.
- `js/help-search.js` enthält den clientseitigen Such- und Navigationscode.
- `de/.gitkeep` und `en/.gitkeep` markieren die sprachspezifischen Verzeichnisse.
- Im aktuellen Arbeitsbaum sind unter `wwwroot/help/de` und `wwwroot/help/en` keine Markdown- oder JSON-Suchindexdateien vorhanden; die API erwartet diese Dateien dort beziehungsweise Markdown unter `docs/business/features`.

## `FinanceManager.Web.csproj`
Datei: `FinanceManager.Web/FinanceManager.Web.csproj`

| Eintrag | Vorhandenes Verhalten |
|---|---|
| `None Include="wwwroot\\help\\**" CopyToOutputDirectory="PreserveNewest"` | Übernimmt den gesamten Help-Unterbaum bei Änderungen in die Build-Ausgabe. |
| `Content Remove="wwwroot\\help\\de\\f004b.html"` | Entfernt eine deutsche Legacy-HTML-Datei aus der Content-Menge. |
| `Content Remove="wwwroot\\help\\en\\f004b.html"` | Entfernt eine englische Legacy-HTML-Datei aus der Content-Menge. |
| `Content Update="wwwroot\\help\\de\\f001.html"` mit `CopyToOutputDirectory=Never` | Verhindert die Übernahme dieser deutschen Legacy-HTML-Datei in die Ausgabe. |

Die Projektdatei enthält keine Integritätsprüfung, Signaturprüfung oder sonstige technische Absicherung gegen nachträgliche Manipulation der Help-Dateien. Die Kopierregel beschreibt nur das Build-/Ausgabeverhalten.

