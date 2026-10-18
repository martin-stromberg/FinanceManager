# Übersetzte Anforderung: Analyse von Todeskreuz und weiteren Umkehrsignalen

Quelle: Issue #406 — „Analyse von Todeskreuz und weiteren Umkehrsignalen"

## Fachliche Zusammenfassung

Die bestehende Golden-Cross-Analyse (bullishes Signal: `GoldenCrossAnalyzer`, `GoldenCrossService`, `GoldenCrossWidget`) soll um ein Pendant für bärische Trendwendesignale ergänzt werden. Für jedes Wertpapier sollen aus den gespeicherten Schlusskursen (`SecurityPrice`) mehrere Umkehrsignale erkannt, bewertet und zu einem Gesamt-Trendstatus zusammengeführt werden. Das Ergebnis wird – analog zum Golden Cross – über einen API-Endpunkt bereitgestellt und auf der Wertpapier-Karteikarte (`CardPage.razor`) als Widget mit Bedeutungserklärungen angezeigt. Liegt kein Umkehrsignal vor, wird dies explizit, aber knapp ausgegeben.

## Akzeptanzkriterien (aus der Anforderung)

1. **Todeskreuz (Death Cross)**
   - Erkennen, ob der 50-Tage-Durchschnitt den 200-Tage-Durchschnitt von oben nach unten kreuzt.
   - Exaktes Kreuzungsdatum bestimmen und als „Death-Cross-Date" ausgeben.
   - Signal als potenzielle Trendwende nach unten interpretieren.

2. **Bruch der 200-Tage-Linie**
   - Erkennen, ob der Kurs die 200-Tage-Linie nach unten durchbricht.
   - Signifikanz anhand der Dauer des Durchbruchs bewerten.
   - Klassifikation: „kurzfristige Schwäche" vs. „strukturelle Trendwende".

3. **Marktstruktur-Umkehr**
   - Erkennung niedrigerer Hochs und niedrigerer Tiefs in der Kursentwicklung.
   - Bewertung, ob eine bärische Marktstruktur entsteht.
   - Klassifikation der Struktur, z. B. „beginnender Abwärtstrend", „Trendverlust", „Seitwärtsphase mit Abwärtstendenz".

4. **Indikatorbasierte Umkehrsignale (kursbasiert)**
   - MACD berechnen und bärisches Crossover (MACD-Linie kreuzt Signallinie von oben nach unten) erkennen.
   - RSI berechnen und negative (bärische) Divergenzen zwischen Kurs und RSI erkennen.
   - Interpretation als Momentumverlust / Trendabschwächung.

5. **Kombination und Bewertung**
   - Alle erkannten Signale in einer strukturierten Übersicht zusammenführen.
   - Trendwende-Wahrscheinlichkeit anhand von Anzahl und Stärke der Signale bewerten.
   - Aktuellen Trendstatus kennzeichnen, z. B. „Trend intakt", „Trend geschwächt", „Trendwende wahrscheinlich".

6. **Ausgabe**
   - Alle erkannten Umkehrsignale strukturiert darstellen, jeweils mit kurzer Bedeutungserklärung.
   - Explizite, kurze Ausgabe, wenn kein Umkehrsignal vorliegt.
   - Aktueller Trendstatus klar benannt.

## Betroffene Klassen und Komponenten

Voraussichtlich betroffene/neue Artefakte (Namen lehnen sich an die Golden-Cross-Konvention an):

- **Datenmodell (`FinanceManager.Domain`)**: ggf. neue Enums für Signal-/Statusklassifikationen; `Security` muss nicht zwingend erweitert werden (die Analyse ist berechnend, nicht persistierend — Annahme, im Plan zu prüfen). Die Anforderung „Speicherung des Datums als Death-Cross-Date" kann als Feld im Analyseergebnis/DTO interpretiert werden; eine Persistenz auf der Entität ist eine mögliche Alternative.
- **Logik (`FinanceManager.Application`)**: neuer Analyzer (z. B. `TrendReversalAnalyzer` / `BearishSignalAnalyzer`) unter `Securities/`, der die Signale 1–4 berechnet und das Gesamtergebnis (Punkt 5) erzeugt; neue Optionsklasse analog `GoldenCrossOptions` (Fenster 50/200 wiederverwenden oder eigenständig konfigurierbar).
- **Interfaces**: neuer Service-Contract analog `IGoldenCrossService` (z. B. `ITrendReversalService` / `IReversalSignalService`).
- **Infrastructure**: Service-Implementierung analog `GoldenCrossService` (liest `SecurityPrices`, ruft Analyzer, mappt auf DTO), Registrierung in `ServiceCollectionExtensions`.
- **Shared**: neues DTO in `FinanceManager.Shared/Dtos/Securities/` (Signalliste mit Typ, Datum, Stärke, Erklärung; Gesamtstatus) + `IApiClient`-Methode + Implementierung in `ApiClient.Securities.cs`.
- **Web**: API-Endpunkt in `SecuritiesController` (z. B. `GET /api/securities/{id}/reversal-signals` o. ä.); neues Widget analog `GoldenCrossWidget.razor` auf `CardPage.razor`; Lokalisierungsressourcen (`*.resx` de/en); ggf. neuer Localizer-Service, falls Benachrichtigungstexte benötigt werden.
- **Tests**: `FinanceManager.Tests/Securities/` — Analyzer-Tests, Service-Tests, DTO-/Enum-Mapping-Tests analog `GoldenCrossAnalyzerTests`, `GoldenCrossServiceTests`, `GoldenCrossDtoEnumMappingTests`.
- **Dokumentation**: `Docs/help/wertpapiermanagement/` erweitern (neues Kapitel/Datei für Umkehrsignale neben `goldenes-kreuz.md`).

## Implementierungsansatz

- Die Berechnung erfolgt rein auf den vorhandenen `SecurityPrice`-Schlusskursen — dieselbe Datenbasis wie `GoldenCrossAnalyzer.Analyze` (Serie `(DateTime Date, decimal Close)`).
- Statische Analyzer-Klasse mit reinem Rechenkern (wie `GoldenCrossAnalyzer`), Service in Infrastructure lädt die Kursdaten (`LongWindow * 3` bzw. ausreichend Historie für MACD/RSI) und mappt auf DTOs.
- MACD: EMA(12) − EMA(26), Signallinie EMA(9) über MACD; bärisches Crossover = letzter Übergang von MACD > Signal nach MACD < Signal.
- RSI: klassischer 14-Perioden-RSI (Wilder); negative Divergenz = Kurs bildet höheres Hoch, RSI bildet niedrigeres Hoch über vergleichbaren Swing-Hochs.
- Marktstruktur: Swing-High/Swing-Low-Erkennung kann an die `GoldenCrossAnalyzer`-Logik (`SwingLookaround`) angelehnt werden; niedrigere Hochs/Tiefs invertieren die vorhandene Auswertung.
- Gesamtbewertung: Zähl-/Gewichtungslogik über die erkannten Signale → Trendstatus-Enum (z. B. `Intact`, `Weakened`, `ReversalLikely`).
- Ausgabe lokalisiert (de/en) über `IStringLocalizer`-Ressourcen, wie beim `GoldenCrossWidget`.
- Annahme: Keine Home-Page-Benachrichtigung gefordert — die Anforderung verlangt nur Anzeige/„Ausgabe". Ob `EvaluateAndNotifyAsync`-Artiges gewünscht ist, ist als offener Punkt zu klären.

## Konfiguration

- Neue Optionssektion analog `GoldenCrossOptions.SectionName` (z. B. `ReversalSignals`/`TrendReversal`) mit Fensterlängen für SMA (50/200), MACD (12/26/9), RSI (14), Durchbruch-Schwellen (Dauer in Tagen für „kurzfristige Schwäche" vs. „strukturelle Trendwende").
- Alternativ: Wiederverwendung von `GoldenCrossOptions` für die SMA-Fenster; eigenständige Sektion ist sauberer, weil die Anforderung mehr Indikatoren betrifft.

## Offene Fragen

1. Soll das Ergebnis persistiert werden (Feld auf `Security` + Migration) oder reicht eine berechnete Ausgabe wie beim Golden Cross? — Vorschlag: berechnet, konsistent zum Golden Cross; „Speicherung" = Feld im DTO.
2. Sind Home-Page-Benachrichtigungen bei erkannten Umkehrsignalen gewünscht (analog `EvaluateAndNotifyAsync` + `GoldenCrossNotificationsEnabled`)? — Vorschlag: nicht gefordert, weglassen.
3. Wo genau soll die Ausgabe erscheinen — eigenes Widget unter dem `GoldenCrossWidget` auf der `CardPage`? — Vorschlag: eigenes Widget im selben Stil.
4. Schwellenwerte (Durchbruchsdauer, Divergenz-Lookback, Gewichtung der Signale) sind nicht spezifiziert — Vorschlag: sinnvolle Defaults in Optionsklasse, dokumentiert.
