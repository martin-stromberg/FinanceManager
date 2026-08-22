# Anforderung: Unkontrollierte Formatzeichenfolge im Budget-Report-Cache beheben

## Metadaten

- Aufgaben-ID: `fa6d1aba-88fd-4f64-8a23-fe86848369dd`
- Branch: `task/issue-318-fa6d1aba88fd4f648a23fe86848369dd-csuncontrolled-format-string`
- Alert-Typ: CodeScanning
- Severity: high
- Status: open
- Tool: CodeQL
- Regel: `cs/uncontrolled-format-string`
- Betroffene Stelle: `FinanceManager.Infrastructure/Budget/ReportCacheService.cs:183`
- Alert-URL: https://github.com/martin-stromberg/FinanceManager/security/code-scanning/37

## Kontext

Der CodeQL-Alert meldet, dass die in `BuildKey` verwendete Formatzeichenfolge von Parametern einer ASP.NET-Core-MVC-Action abhängen kann. Dadurch wird eine potenziell unkontrollierte Formatzeichenfolge an `string.Format` übergeben.

Aktuell wird der Cache-Schlüssel für rohe Budgetberichtsdaten sinngemäß wie folgt erzeugt:

```csharp
private const string KeyPrefix_BudgetReportRawData = "budgetreportraw";
private static string BuildKey(DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis)
    => string.Format(CultureInfo.InvariantCulture, $"{KeyPrefix_BudgetReportRawData}-{from:yyyyMMdd}-{to:yyyyMMdd}-{dateBasis}");
```

## Ziel

Die Erstellung des Cache-Schlüssels muss so angepasst werden, dass keine von externen beziehungsweise MVC-Action-Parametern abhängige Formatzeichenfolge an `string.Format` übergeben wird. Das Verhalten und die Eindeutigkeit der Cache-Schlüssel müssen erhalten bleiben.

## Anforderungen

1. Entferne die Verwendung einer dynamisch interpolierten Zeichenfolge als Formatzeichenfolge für `string.Format`.
2. Erzeuge den Schlüssel weiterhin mit dem Präfix `budgetreportraw`, den Datumswerten im Format `yyyyMMdd` und dem Wert von `BudgetReportDateBasis`.
3. Verwende weiterhin eine kulturinvariante Darstellung, sofern für die Schlüsselbestandteile eine Formatierung erforderlich ist.
4. Stelle sicher, dass gleiche Eingaben denselben Schlüssel und unterschiedliche relevante Eingaben weiterhin unterschiedliche Schlüssel erzeugen.
5. Die Änderung muss auf die betroffene Schlüsselgenerierung begrenzt bleiben und darf das fachliche Verhalten des Budgetbericht-Cache nicht verändern.

## Akzeptanzkriterien

- [ ] CodeQL meldet für `cs/uncontrolled-format-string` an der betroffenen Stelle keinen Befund mehr.
- [ ] `BuildKey` verwendet keine von Methodenparametern abhängige Formatzeichenfolge als erstes Argument von `string.Format`.
- [ ] Ein Beispielschlüssel behält die Struktur `budgetreportraw-{from:yyyyMMdd}-{to:yyyyMMdd}-{dateBasis}` bei.
- [ ] Die Schlüsselgenerierung ist kulturinvariant und deterministisch.
- [ ] Bestehende Tests bleiben erfolgreich; falls für `BuildKey` noch keine geeigneten Tests existieren, werden fokussierte Tests für Format und Determinismus ergänzt.

## Nicht im Umfang

- Keine Änderung an der Cache-Strategie oder Cache-Lebensdauer.
- Keine Änderung am MVC-Action-Vertrag.
- Keine allgemeine Überarbeitung weiterer Formatierungen außerhalb der betroffenen Methode.
