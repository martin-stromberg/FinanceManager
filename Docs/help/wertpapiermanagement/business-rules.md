← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Business Rules

## Pflichtfelder beim Wertpapier

**Beschreibung:** Für ein Wertpapier sind Name, Kennung und Währung zwingend.

**Bedingungen:**
- Eingaben dürfen nicht leer sein.

**Verhalten:**
- Gültige Eingaben: Wertpapier wird erstellt/aktualisiert.
- Ungültige Eingaben: Vorgang wird abgebrochen.

**Umsetzung:** `Security.Update`.

## Preisfehler wird explizit markiert

**Beschreibung:** Fehler bei Kursabrufen werden am Wertpapierzustand gespeichert.

**Bedingungen:**
- Externer Abruf/Import meldet Fehler.

**Verhalten:**
- Wertpapier setzt `HasPriceError`, `PriceErrorMessage`, `PriceErrorSinceUtc`.
- Nach erfolgreicher Aktualisierung kann der Fehlerzustand entfernt werden.

**Umsetzung:** `Security.SetPriceError` und `Security.ClearPriceError`.

## AlphaVantage-Key-Aufloesung

**Beschreibung:** Kursabrufe verwenden bevorzugt den persoenlichen
AlphaVantage API Key des anfragenden Benutzers und fallen nur bei fehlendem
persoenlichem Key auf einen freigegebenen Admin-Key zurueck.

**Bedingungen:**
- Der anfragende Benutzer hat einen gespeicherten AlphaVantage API Key.
- Oder ein Administrator hat einen Key gespeichert und
  `ShareAlphaVantageApiKey` aktiviert.

**Verhalten:**
- Persoenlicher Key vorhanden: Der persoenliche Key wird fuer den Abruf
  verwendet.
- Kein persoenlicher Key, aber freigegebener Admin-Key vorhanden: Der
  Admin-Key wird als Shared-Fallback verwendet.
- Kein Key verfuegbar: Der AlphaVantage-Abruf kann nicht ausgefuehrt werden.
- Gespeicherte Keys werden vor der Nutzung entschluesselt; der Klartext wird
  nicht in Logs, Profilantworten oder UI-Ausgaben offengelegt.

**Umsetzung:** `AlphaVantagePriceProvider`, `AlphaVantageKeyResolver`,
`DataProtectionAlphaVantageSecretProtector`.

## Depot-Analysebericht: Kachel-Konfiguration muss konsistent sein

**Beschreibung:** Beim Speichern der Kachel-Sichtbarkeit/-Reihenfolge für den
Depot-Analysebericht muss mindestens eine Kachel aktiv sein, und die
Reihenfolge muss exakt die aktiven Kachel-IDs ohne Duplikate enthalten.

**Bedingungen:**
- `PortfolioKpiConfigurationRequest.ActiveTileIds` ist leer.
- Oder `TileOrder` enthält Duplikate oder deckt `ActiveTileIds` nicht
  vollständig ab.

**Verhalten:**
- Gültige Eingabe: Konfiguration wird gespeichert, Berichts-Cache wird
  invalidiert.
- Ungültige Eingabe: `400 Bad Request` mit Validierungsfehler, nichts wird
  gespeichert.

**Umsetzung:** `PortfolioAnalysisReportController.SaveKpiConfigurationAsync`.

## Depot-Analysebericht: Monatliche Cache-Gültigkeit

**Beschreibung:** Der berechnete Depot-Analysebericht wird pro Benutzer
zwischengespeichert und gilt bis zum Ende des Kalendermonats, in dem er
berechnet wurde.

**Bedingungen:**
- Es existiert ein `ReportCacheEntry` mit Schlüssel
  `portfolio-analysis-report-{OwnerUserId:N}`.

**Verhalten:**
- `CacheValidUntilUtc` des Eintrags liegt in der Zukunft und
  `NeedsRefresh == false`: der gecachte Bericht wird unverändert
  zurückgegeben.
- `CacheValidUntilUtc` ist überschritten (Monatswechsel), der Eintrag fehlt,
  oder `NeedsRefresh == true`: der Bericht wird neu berechnet und mit neuem
  `CacheValidUntilUtc` (Ende des aktuellen Monats) gespeichert.

**Umsetzung:** `PortfolioAnalysisReportCacheService.GetPortfolioReportAsync`,
`PortfolioAnalysisReportService.EndOfMonthUtc`.

## Depot-Analysebericht: Automatische Cache-Invalidierung

**Beschreibung:** Bestimmte Datenänderungen verwerfen den Berichts-Cache
sofort, damit der nächste Aufruf frische Werte liefert, statt bis zum
regulären Monatswechsel zu warten.

**Bedingungen:**
- Eine neue Kursnotierung wird angelegt oder ein Kurs-Batch-Import fügt
  neue/geänderte Kurse ein (`SecurityPriceService`).
- Eine Buchung mit `Kind == PostingKind.Security` wird über
  `PostingReversalService.ReversePostingAsync` storniert.
- Ein Kontoauszugsentwurf wird gebucht und das betroffene Konto hat
  `SecurityProcessingEnabled == true` oder beim Buchen werden
  Wertpapier-Postings erzeugt.
- Die Kachel-Konfiguration wird gespeichert
  (`PortfolioAnalysisReportController.SaveKpiConfigurationAsync`).
- Der Benutzer löst manuell "Aktualisieren" aus
  (`POST /api/portfolio/cache/reset`).

**Verhalten:**
- Der `ReportCacheEntry` des betroffenen Benutzers wird gelöscht.

**Umsetzung:** `IPortfolioAnalysisReportCacheService.InvalidateCacheAsync`,
aufgerufen aus `SecurityPriceService.CreateAsync`,
`SecurityPriceService.UpsertDailyPricesAsync`,
`PostingReversalService.ReversePostingAsync` und
`StatementDraftService.BookAsync`.

## Depot-Analysebericht: Liquiditätsquote

**Beschreibung:** Die Cashflow-Kachel weist aus, welcher Anteil des aktuellen
Depotwerts als abgeleitete Liquidität auf Verrechnungskonten liegt.

**Bedingungen:**
- Wertpapier-Postings des Benutzers liefern nicht leere `GroupId`-Werte.
- Bank-Postings derselben Gruppen liefern `AccountId`-Werte.
- Die gefundenen Konten gehören demselben Benutzer.

**Verhalten:**
- Der aktuelle Saldo jedes gefundenen Kontos wird genau einmal summiert.
- Formel: `LiquidityRatio = depotCashBalance / (TotalMarketValue + depotCashBalance)`.
- Ist der aktuelle Depot-Marktwert kleiner oder gleich `0` oder ist der
  Nenner kleiner oder gleich `0`, wird `0 %` angezeigt.
- Negative Kontosalden werden nicht gekappt.

**Umsetzung:** `PortfolioAnalysisReportService.LoadDepotCashBalanceAsync` und
`PortfolioAnalysisReportService.BuildCashflow`.
