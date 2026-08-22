# Detail: Tests und Verifikation

## Vorhandene Abdeckung

`FinanceManager.Tests/Infrastructure/Budget/ReportCacheServiceTests.cs` prüft:

- Markierung eines Eintrags bei überlappendem Zeitraum.
- Ignorieren eines Eintrags mit fremdem Cache-Präfix.
- Markierung mehrerer überlappender Monatsbereiche.

Die Tests verwenden eine echte `ReportCacheService`-Instanz mit SQLite in-memory. Ihre Testdaten erzeugen Cache-Schlüssel mit einer konstanten Formatzeichenfolge und bilden damit das erwartete externe Format ab.

## Empfohlene fokussierte Tests

- Beispielausgabe für `2026-01-01`, `2026-01-31`, `BookingDate` entspricht `budgetreportraw-20260101-20260131-BookingDate`.
- Wiederholte Erstellung mit denselben Eingaben liefert denselben Schlüssel beziehungsweise denselben Cacheeintrag.
- Änderung von `from`, `to` oder `dateBasis` führt zu einem anderen Schlüssel.
- Kulturtest mit einer Kultur, die Datums- oder Enum-Darstellung anders formatiert, bestätigt die kulturinvariante Ausgabe.

Da `BuildKey` privat ist, sollte die Teststrategie bevorzugt über `SetBudgetReportRawDataAsync` und die gespeicherte `ReportCacheEntry` oder über einen öffentlichen Lese-/Schreib-Roundtrip erfolgen. Eine Reflexionstestung wäre enger, ist aber weniger stabil.

## Verifikation nach Umsetzung

1. Fokussierte Tests des Testprojekts ausführen.
2. Gesamtes relevantes .NET-Testprojekt ausführen, sofern die lokale Umgebung die Abhängigkeiten bereitstellt.
3. Quelltextprüfung bestätigen, dass `string.Format` nicht mehr mit einem interpolierten Parameter-Ausdruck als Formatstring aufgerufen wird.
4. Optional CodeQL erneut ausführen oder den Security-Scan im CI abwarten.
