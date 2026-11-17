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

## Goldenes Kreuz: Anzeige nur bei erreichter oder naher Schwelle

**Beschreibung:** Das Statistikfeld „Goldenes Kreuz" auf der Wertpapierkarte
wird nur angezeigt, wenn eine Goldenes-Kreuz-Situation vorliegt.

**Bedingungen:**
- `Crossed`: Kurzfristiger Durchschnitt (SMA50) ≥ langfristiger Durchschnitt
  (SMA200).
- `Approaching`: SMA50 liegt unter SMA200, aber um höchstens
  `ApproachThresholdPercent` (Standard 3 %).

**Verhalten:**
- `Crossed`/`Approaching`: Box mit Kennzahlen und Info-Panel wird angezeigt.
- `Far` oder `InsufficientData` (weniger als `LongWindow` Kurse): Box wird
  vollständig ausgeblendet – auch bei ausreichenden Kursdaten.

**Umsetzung:** `GoldenCrossAnalyzer.Analyze`, `GoldenCrossWidget.razor`.

## Goldenes Kreuz: Startseiten-Hinweis einmal pro Zyklus

**Beschreibung:** Nach jedem Kursabruf wird die Phase neu bewertet und bei
Bedarf ein ereignisgesteuerter Hinweis auf der Startseite erzeugt.

**Bedingungen:**
- Die neue Phase ist höher als `Security.GoldenCrossNotifiedPhase`
  (`Far` → `Approaching` → `Crossed`).
- `User.GoldenCrossNotificationsEnabled` ist gesetzt (Standard `true`).

**Verhalten:**
- Hinweis wird erzeugt und die gemeldete Phase am Wertpapier gespeichert;
  dieselbe Phase wird nicht erneut gemeldet.
- Fällt die Phase auf `Far` zurück, wird die gespeicherte Phase zurückgesetzt,
  so dass ein neuer Zyklus wieder Hinweise auslöst.
- Ist die Benutzereinstellung deaktiviert, wird die Phase gespeichert, aber
  kein Hinweis erzeugt.

**Umsetzung:** `GoldenCrossService.EvaluateAndNotifyAsync`, aufgerufen im
Kurs-Pflegepass `ISecurityPriceUpdateService` (ausgelöst u. a. durch
`SecurityPriceWorker`, Kurs-Backfill und Kursimport).

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
- `CacheValidUntilUtc` des Eintrags liegt in der Zukunft,
  `NeedsRefresh == false` und `Parameter` entspricht der aktuellen
  `CacheSchemaVersion` (aktuell `"6"`): der gecachte Bericht wird
  unverändert zurückgegeben.
- `CacheValidUntilUtc` ist überschritten (Monatswechsel), der Eintrag fehlt,
  `NeedsRefresh == true` oder `Parameter` stammt aus einer älteren
  Schema-Version (z. B. nach einer DTO-Erweiterung): der Bericht wird neu
  berechnet und mit neuem `CacheValidUntilUtc` (Ende des aktuellen
  Monats) gespeichert.

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
- Formel bei belastbarer Datenbasis:
  `LiquidityRatio = depotCashBalance / (TotalMarketValue + depotCashBalance)`.
- Ist der abgeleitete Cash-Bestand negativ, der aktuelle Depot-Marktwert
  kleiner oder gleich `0` oder der Nenner kleiner oder gleich `0`, wird keine
  Quote berechnet (`LiquidityRatio = null`) und die UI zeigt `n/a`.

**Umsetzung:** `PortfolioAnalysisReportService.LoadDepotCashBalanceAsync` und
`PortfolioAnalysisReportService.BuildCashflow`.

## Depot-Analysebericht: Jahresserie der Depotentwicklung

**Beschreibung:** Die Kachel „Depotentwicklung Jahreswechsel" liefert pro
Kalenderjahr der Depot-Historie eine Zeile aus Jahresend-Depotwert,
Netto-Dividenden und kumuliert eingezahltem Kapital.

**Bedingungen:**
- Es liegen Wertpapierbuchungen des Benutzers vor; andernfalls bleibt die
  Serie leer und die Kachel zeigt eine Leeranzeige.

**Verhalten:**
- Die Serie läuft vom Jahr der ältesten Transaktion bis zum laufenden
  Jahr; werden heute keine Bestände mehr gehalten (Komplettverkauf),
  endet sie im Jahr der jüngsten Transaktion.
- Stichtag je Jahr ist der 31.12.; für das laufende Jahr mit gehaltenen
  Beständen das Tagesdatum — diese Zeile trägt `IsYtd = true` und wird
  in der UI mit "*" markiert.
- `YearEndValue` ist der Marktwert aller Positionen zum Stichtag; je
  Position zählt der letzte bekannte Kurs am oder vor dem Stichtag,
  Positionen ganz ohne Kurs tragen 0 bei.
- `NetDividends` = Summe der `Dividend`-Buchungen des Jahres minus der
  Absolutbeträge aller `Tax`-Buchungen des Jahres.
- `CumulativeInvestedCapital` = FIFO-Anschaffungskostenbasis
  (`TotalCostBasis - StandaloneFeeTotal`), je Position auf die
  Transaktionen bis zum Stichtag berechnet.
- `NetFlow` = Summe der `Buy`-Buchungsbeträge (absolut) minus Summe der
  `Sell`-Buchungsbeträge des Jahres; positiv bedeutet Netto-Einzahlung.
- `YearStartValue` ist der `YearEndValue` des Vorjahres; im ersten Jahr
  der Serie dient das eigene `CumulativeInvestedCapital` als Startwert.
- Der Depotwert umfasst nur den Wertpapier-Marktwert, nicht den Saldo
  von Verrechnungskonten.

**Umsetzung:** `PortfolioAnalysisReportService.BuildDepotDevelopment`
unter Verwendung von `GetSeriesYearRange`, `GetYearReferenceDate`,
`GetPortfolioValueOnDate`, `SecurityValuationHelper` und
`IFifoCostBasisCalculator.Calculate`.

## Depot-Analysebericht: Rendite-Basis der Depotentwicklung

**Beschreibung:** Die Spalte „Rendite" der Jahrestabelle bezieht die
Netto-Dividende auf eine vom Anwender wählbare Basis.

**Bedingungen:**
- Auswahl „Jahresanfangswert" (Standard) oder „Eingezahltes Kapital
  (Yield on Cost)" im Auswahlfeld „Rendite-Basis".

**Verhalten:**
- Jahresanfangswert: `Rendite = NetDividends / YearStartValue`; die
  Jahrestabelle zeigt zusätzlich Depotwert, Veränderung zum Vorjahr
  (absolut und prozentual) und die Netto-Dividende.
- Eingezahltes Kapital: `Rendite = NetDividends / Σ NetFlow` (kumulierte
  Ein-/Auszahlungen bis zum Jahresstichtag über die gesamte Historie,
  unabhängig vom Zeitraum-Filter); die Tabelle zeigt statt Depotwert
  und Veränderung die Spalten Jahr, Ein-/Auszahlungen (Summe),
  Netto-Dividende und Rendite, das Diagramm wechselt auf zwei Balken
  je Jahr (`NetFlow` und `NetDividends`).
- Ist die Basis 0, steht in der Spalte „Rendite" `—` statt eines
  Prozentwerts.
- Reicht die Breite der Kachel nicht aus (unter ~560 px — etwa auf
  schmalen Bildschirmen oder in mehrspaltigen Kachel-Rastern), reduziert
  sich die Tabelle im Modus „Jahresanfangswert" per CSS-Container-Query
  auf vier Spalten („Jahr", „Dividende/Depotwert", „Veränderung €/%",
  „Rendite"): die Zellen für Depotwert und Veränderung zeigen je zwei
  Werte, die Spalten „Veränderung %" und „Netto-Dividende" entfallen.
  Die Tabelle verwendet `table-layout: fixed` bei `overflow-x: hidden`,
  sodass horizontales Scrollen niemals erforderlich oder möglich ist.
- Umschalten der Basis ebenso wie der Zeitraum-Filter („Gesamte
  Historie", „Letzte 5 Jahre", „Letzte 10 Jahre" — verankert am letzten
  Jahr der Serie, nicht am Kalenderjahr) erfolgen rein clientseitig;
  beide Auswahlen werden im `localStorage` des Browsers persistiert und
  beim nächsten Aufruf der Seite wiederhergestellt.

**Umsetzung:** `PortfolioDepotDevelopmentCard.razor` (komponenteninterne
Enums `DepotDevelopmentYieldBasis` und `DepotDevelopmentPeriodFilter`,
Persistenz über `IJSRuntime`/`localStorage`).

## Depot-Analysebericht: Nachträglich eingeführte Kacheln werden in gespeicherte Konfigurationen gemergt

**Beschreibung:** Kacheln, die erst nach dem Speichern einer eigenen
Kachel-Konfiguration hinzukamen, fehlen in deren `TileOrder`. Beim Laden
der Konfiguration werden sie automatisch nachgezogen, damit sie sichtbar
und im Bearbeitungsmodus verwaltbar sind.

**Bedingungen:**
- Eine gespeicherte `PortfolioKpiConfiguration` enthält nicht alle
  `PortfolioTileId`-Werte.

**Verhalten:**
- Fehlende Kachel-IDs werden ans Ende von `TileOrder` angehängt.
- Gehört eine fehlende Kachel zur Default-Aktiv-Menge (`Structure`,
  `Performance`, `Cashflow`, `DepotDevelopment`), wird sie zusätzlich in
  `ActiveTileIds` ergänzt und sofort angezeigt; andere neue Kacheln
  (z. B. `Risk`) bleiben inaktiv, sind aber im Bearbeitungsmodus
  aktivierbar.
- Eine vom Benutzer bewusst deaktivierte Kachel wird nicht erneut
  aktiviert, da nur in `TileOrder` fehlende IDs ergänzt werden.
- Das gemergte Ergebnis wird nur zurückgegeben; persistiert wird es erst
  beim nächsten expliziten Speichern der Konfiguration.

**Umsetzung:** `PortfolioAnalysisReportController.MergeMissingTiles`,
angewendet in `GetKpiConfigurationAsync` auf gespeicherte Konfigurationen
und auf die Default-Konfiguration.
