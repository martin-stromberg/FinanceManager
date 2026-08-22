# Umsetzungsplan: Unkontrollierte Formatzeichenfolge im Budget-Report-Cache

## Ziel

Den CodeQL-Befund `cs/uncontrolled-format-string` in `ReportCacheService.BuildKey` beheben, ohne das bestehende Cache-Key-Format oder das fachliche Verhalten des Budget-Report-Caches zu verändern.

## Geplante Änderungen

### 1. Schlüsselgenerierung absichern

Datei: `FinanceManager.Infrastructure/Budget/ReportCacheService.cs`

- Die Übergabe der bereits interpolierten Zeichenfolge als Formatstring an `string.Format` entfernen.
- `BuildKey(DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis)` auf eine Formatierung mit statischem Formatkontext umstellen, beispielsweise über `string.Create(CultureInfo.InvariantCulture, $"...")` oder eine gleichwertige API.
- Den bestehenden Präfix `budgetreportraw`, die Reihenfolge und die Trennzeichen unverändert beibehalten.
- Die Datumswerte weiterhin explizit mit `yyyyMMdd` und `CultureInfo.InvariantCulture` formatieren.
- Die bisherige Textdarstellung von `BudgetReportDateBasis` beibehalten, sodass etwa weiterhin `BookingDate` im Schlüssel erscheint.
- Keine Änderungen an den Aufrufern, der öffentlichen Cache-Schnittstelle, der EF-Entity, der Cache-Invalidierung, der Refresh-Logik oder der DI-Registrierung vornehmen.

Erwartetes Beispiel:

`budgetreportraw-20260101-20260131-BookingDate`

### 2. Schlüsselverhalten testen

Datei: `FinanceManager.Tests/Infrastructure/Budget/ReportCacheServiceTests.cs`

- Die bestehende Teststruktur mit SQLite in-memory und einer konkreten `ReportCacheService`-Instanz weiterverwenden.
- Einen fokussierten Test über die öffentliche Schreib-/Lese-Schnittstelle ergänzen, der die erwartete Schlüsselstruktur indirekt über den gespeicherten `ReportCacheEntry.CacheKey` bestätigt.
- Für dieselben Eingaben Determinismus beziehungsweise denselben Cache-Schlüssel prüfen.
- Prüfen, dass eine Änderung von `from`, `to` oder `dateBasis` zu einem anderen Schlüssel führt.
- Die Schlüsselprüfung unter einer abweichenden aktuellen Kultur ausführen oder die Formatierungslogik anderweitig so verifizieren, dass die Ausgabe nachweislich kulturinvariant bleibt; den ursprünglichen Thread-/Kulturzustand im Test wiederherstellen.
- Den bestehenden privaten Test-Helfer nur dann anpassen, wenn dies für die Tests erforderlich ist. Er darf weiterhin einen konstanten Formatstring verwenden, da der CodeQL-Befund die Produktionsmethode betrifft.

## Verifikation

1. Fokussierten Test ausführen:
   `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter FullyQualifiedName~ReportCacheServiceTests`
2. Das gesamte Testprojekt ausführen:
   `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
3. Den relevanten Quelltext prüfen und bestätigen, dass `BuildKey` keinen interpolierten Ausdruck mehr als erstes Argument von `string.Format` verwendet.
4. Sicherstellen, dass die bestehende Testabdeckung für Cache-Invalidierung weiterhin erfolgreich ist.
5. CodeQL beziehungsweise den CI-Code-Scanning-Lauf prüfen; erwartet wird, dass der Befund `cs/uncontrolled-format-string` an `ReportCacheService.cs` entfällt.

## Akzeptanzkriterien

- `BuildKey` verwendet keine von Methodenparametern abhängige Formatzeichenfolge für `string.Format`.
- Die Ausgabe bleibt für gleiche Eingaben identisch und für relevante unterschiedliche Eingaben unterscheidbar.
- Das Format `budgetreportraw-{from:yyyyMMdd}-{to:yyyyMMdd}-{dateBasis}` bleibt unverändert.
- Die Ausgabe ist kulturinvariant und deterministisch.
- Bestehende sowie ergänzte fokussierte Tests sind erfolgreich.
- Die Änderung bleibt auf die Schlüsselgenerierung und deren Tests begrenzt.

## Offene Punkte

Keine.
