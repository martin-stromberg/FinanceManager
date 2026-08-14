### Fachliche Zusammenfassung

Der Depot-Analysebericht soll in der Cashflow-Kachel wieder eine echte Liquiditätsquote ausweisen. Die Kennzahl setzt das nicht investierte Guthaben, das fachlich zum Wertpapierdepot gehört, ins Verhältnis zum Gesamtwert des Depots. Dafür muss die bestehende Depot-Aggregation in `PortfolioAnalysisReportService` um eine nachvollziehbare Ermittlung depotbezogener Kontosalden erweitert werden; `PortfolioCashflowDto` und `PortfolioCashflowCard.razor` erhalten das Feld bzw. die KPI-Zeile wieder zurück. Zusätzlich muss der Cache des Depot-Analyseberichts auch bei Änderungen der für die Liquidität relevanten Kontostände invalidiert werden.

### Betroffene Klassen und Komponenten

- Datenmodellklassen:
  - `Account`: bestehende Kontostände über `CurrentBalance`; mögliche Erweiterung oder Nutzung bestehender Merkmale zur Abgrenzung depotbezogener Liquiditätskonten.
  - `Posting`: bestehende Zuordnung von Wertpapierbuchungen über `SecurityId`, `SecuritySubType`, `AccountId` und `GroupId` als mögliche Grundlage für verknüpfte Verrechnungskonten.
  - `Security`: bestehende Wertpapierpositionen als Depotbestand.
  - Annahme: Falls die bestehende Zuordnung über Wertpapierbuchungen nicht eindeutig genug ist, wird ein neues persistentes Zuordnungsmodell für Depot-/Verrechnungskonten benötigt.
- DTOs:
  - `PortfolioCashflowDto`: neues bzw. wiederhergestelltes Feld `LiquidityRatio`.
  - `PortfolioAnalysisReportDto`: indirekt betroffen, da es `PortfolioCashflowDto` enthält und gecacht serialisiert wird.
- Logikklassen / Services:
  - `PortfolioAnalysisReportService`: Erweiterung von `LoadPositionsAsync` und/oder `BuildCashflow` um Cash-Salden und Berechnung der Liquiditätsquote.
  - `PortfolioAnalysisReportCacheService`: Prüfung der Cache-Schema-Version, da sich die serialisierte DTO-Form ändert.
  - Services, die Kontostände oder kontobezogene Buchungen verändern, insbesondere `StatementDraftService`, `AccountService` und `PostingReversalService`, sofern sie relevante Kontosalden beeinflussen.
  - Bestehende Invalidierungspfade über `IPortfolioAnalysisReportCacheService.InvalidateCacheAsync`.
- Interfaces:
  - `IPortfolioAnalysisReportService`: voraussichtlich keine Signaturänderung.
  - `IPortfolioAnalysisReportCacheService`: voraussichtlich keine Signaturänderung.
  - Annahme: Falls die Liquiditätskonten über einen eigenen Service ermittelt werden, kann ein neues internes Interface für diese Ermittlung sinnvoll sein.
- Enums:
  - `SecurityPostingSubType`: bestehende Untertypen für Wertpapierbuchungen als fachlicher Anknüpfungspunkt.
  - `PostingKind`: bestehende Unterscheidung für Wertpapier- und Kontobuchungen.
  - Annahme: Neue Enum-Werte sind nur erforderlich, falls eine explizite Kontorolle modelliert wird.
- UI-Komponenten / Controller:
  - `PortfolioCashflowCard.razor`: KPI-Zeile "Liquiditätsquote" mit `KpiInfoButton` wieder ergänzen.
  - Lokalisierungsressourcen für Label und Info-Button-Erklärung der Liquiditätsquote.
  - `PortfolioAnalysisReportController`: indirekt betroffen über DTO-Serialisierung.
  - `PortfolioAnalysisReportPage.razor` und `PortfolioAnalysisReportPageViewModel`: voraussichtlich keine fachliche Änderung, solange die Cashflow-Kachel das erweiterte DTO direkt nutzt.
- Tests:
  - `PortfolioAnalysisReportServiceTests`: Berechnung der Liquiditätsquote mit depotbezogenen Kontosalden, leeres Depot, Depot ohne Cash-Konto und Division-durch-null-Fälle.
  - `PortfolioAnalysisReportCacheServiceTests`: Cache-Verhalten bei geändertem DTO-Schema und Invalidierung.
  - Tests für die Services, die Kontostände ändern und künftig `InvalidateCacheAsync` auslösen müssen.
  - UI-/ViewModel-Tests für Darstellung und Lokalisierung der KPI-Zeile in `PortfolioCashflowCard.razor`, soweit im Projekt vorhanden.
  - Dokumentationsaktualisierung unter `Docs/help/wertpapiermanagement/`.

### Implementierungsansatz

Die Ermittlung der Liquidität sollte an die bestehenden Wertpapierbuchungen angebunden werden, weil aktuell kein separates Depotobjekt existiert und der Bericht benutzerweit über alle `Security`-Datensätze aggregiert. Naheliegend ist, die `AccountId`-Werte aus `Posting`-Datensätzen mit `SecurityId != null` und `SecuritySubType != null` als Verrechnungskonten des Depotbestands zu interpretieren und deren aktuelle `Account.CurrentBalance`-Summe als Liquidität zu verwenden. Diese Annahme muss in der Planung fachlich geprüft werden, weil ein einzelnes Konto auch nicht-depotbezogene Guthaben enthalten kann.

`PortfolioAnalysisReportService.GetPortfolioAnalysisReportAsync` sollte neben den bestehenden Positionsdaten auch die relevanten Cash-Salden laden oder ein erweitertes internes Snapshot-Modell an `BuildCashflow` übergeben. Die Berechnung lautet fachlich: `LiquidityRatio = cashBalance / (structure.TotalMarketValue + cashBalance)`, sofern der Nenner größer als `0` ist; andernfalls `0` oder `null` entsprechend der bestehenden DTO-Konvention für diese Kachel. Da `PortfolioCashflowDto` aktuell nur nicht-nullable `decimal`-Werte enthält, ist ein nicht-nullable `decimal LiquidityRatio` konsistent, sofern undefinierte Fälle als `0m` dargestellt werden sollen.

Die UI ergänzt in `PortfolioCashflowCard.razor` eine weitere `portfolio-kpi-row` mit Prozentformatierung und `KpiInfoButton`. Die Erklärung sollte die verwendete Formel und die fachliche Abgrenzung der einbezogenen Konten nennen. Die Chart-Daten der Cashflow-Kachel sollten nur erweitert werden, wenn eine Quote dort sinnvoll visualisiert werden kann; wegen abweichender Einheit ist eine reine KPI-Zeile naheliegender.

Bei DTO-Erweiterung muss `PortfolioAnalysisReportCacheService` die `CacheSchemaVersion` erhöhen, damit bestehende JSON-Cache-Einträge mit altem `PortfolioCashflowDto` nicht als gültig verwendet werden. Cache-Invalidierung ist zusätzlich bei Vorgängen erforderlich, die `Account.CurrentBalance` depotbezogener Konten ändern. Die bestehenden optional injizierten Aufrufe von `IPortfolioAnalysisReportCacheService.InvalidateCacheAsync` in `SecurityPriceService` und `PostingReversalService` dienen als Muster.

### Konfiguration

Es ist keine globale Anwendungskonfiguration ableitbar. Falls die Zuordnung über `Posting.AccountId` nicht zuverlässig genug ist, sollte die Konfiguration auf Datensatzebene erfolgen, z. B. durch eine explizite Markierung oder Zuordnung von `Account` als depotbezogenes Verrechnungskonto. Eine benutzerspezifische Einstellung wäre nur dann passend, wenn mehrere Depots oder unterschiedliche Liquiditätsabgrenzungen pro Benutzer unterstützt werden sollen.

### Offene Fragen

- Sollen alle Konten, die jemals in Wertpapierbuchungen (`Posting.SecurityId != null`, `SecuritySubType != null`) als `AccountId` vorkamen, vollständig als depotbezogene Liquidität zählen?
- Wie soll mit Konten umgegangen werden, die sowohl Wertpapierverrechnung als auch normale Zahlungsverkehrs- oder Sparkonto-Buchungen enthalten?
- Soll die Liquiditätsquote bei leerem Depot bzw. Gesamtwert `0` als `0 %` oder als nicht verfügbar (`null`) modelliert werden?
- Soll negatives Cash-Guthaben auf Verrechnungskonten die Quote negativ machen oder auf `0` begrenzt werden?
- Müssen mehrere Depots pro Benutzer fachlich unterschieden werden, oder bleibt der bestehende benutzerweite Depot-Analysebericht maßgeblich?
