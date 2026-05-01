# F017 – Wertpapier-Renditeanalyse

## Einleitung

Die Renditeanalyse zeigt Ihnen, wie sich Ihre Wertpapier-Investitionen wirklich entwickelt haben. Sie sehen auf einen Blick, wie viel Gewinn oder Verlust ein Wertpapier eingebracht hat – inklusive Dividenden und nach Steuern. So können Sie fundierte Entscheidungen treffen, ohne selbst rechnen zu müssen.

Die Funktion gliedert sich in zwei Bereiche: eine **kompakte Rendite-Box** direkt auf der Wertpapierseite und eine **ausführliche Detailseite** mit fünf Analysebereichen.

## Wer nutzt es?

**Privatanleger**, die ihre Wertpapier-Bestände im FinanceManager verwalten und verstehen möchten, wie gut ihre Investitionen wirklich laufen. Die Funktion ist vollständig auf Laien ohne Finanzkenntnisse ausgerichtet – alle Kennzahlen enthalten erklärende Hinweistexte.

---

## Die kompakte Rendite-Box

Sobald Sie eine Wertpapierseite öffnen, erscheint automatisch die Rendite-Box. Hier sehen Sie die wichtigsten Zahlen auf einen Blick:

| Anzeige | Was bedeutet das? |
|---------|-------------------|
| **Gesamtrendite (absolut)** | Wie viel Euro Gewinn oder Verlust haben Sie insgesamt erzielt? Positiv = grüner Pfeil ▲, negativ = roter Pfeil ▼ |
| **Gesamtrendite (in %)** | Der Gewinn oder Verlust als Prozentzahl, bezogen auf das eingesetzte Kapital |
| **CAGR (Ø Jahresrendite)** | Wie viel Rendite haben Sie durchschnittlich pro Jahr erzielt? |
| **TWR (zeitgewichtete Rendite)** | Rendite des Wertpapiers unabhängig davon, wann Sie wie viel investiert haben |
| **Einstandskurs** | Der durchschnittliche Preis, den Sie pro Anteil bezahlt haben |
| **Aktueller Kurs** | Der heutige Marktpreis pro Anteil |
| **Investiertes Kapital** | Wie viel Geld Sie insgesamt investiert haben |
| **Aktueller Marktwert** | Was Ihre Anteile heute wert sind |

Unterhalb der Zahlen zeigt ein **Mini-Chart** die Wertentwicklung Ihres Portfolios seit dem ersten Kauf. Die obere Linie zeigt den Marktwert, die untere Linie das investierte Kapital – je größer der Abstand, desto besser.

Über den Button **„Detaillierte Renditeanalyse →"** gelangen Sie zur vollständigen Analyseseite.

> **Hinweis:** Wenn kein aktueller Kurs verfügbar ist, erscheint ein Hinweistext mit dem Datum des letzten bekannten Kurses. Renditekennzahlen, die den aktuellen Kurs benötigen, werden dann mit „–" angezeigt.

---

## Die Detailseite `/securities/{id}/performance`

Die Detailseite gliedert sich in fünf Tabs (Registerkarten). Jeder Tab beleuchtet einen anderen Aspekt Ihrer Investition.

---

### Tab 1: Übersicht

Hier sehen Sie das **Performance-Chart**: ein Liniendiagramm, das zeigt, wie sich der Wert Ihrer Investition im Zeitverlauf entwickelt hat.

**Zeitraumauswahl:** Sie können zwischen sechs Zeiträumen wählen:

- **1 Monat** – Entwicklung der letzten 4 Wochen
- **3 Monate** – letztes Quartal
- **6 Monate** – letztes halbes Jahr
- **1 Jahr** – die letzten 12 Monate
- **3 Jahre** – die letzten drei Jahre
- **Gesamt** – seit Ihrem ersten Kauf

Das Diagramm zeigt zwei Linien: Ihre tatsächliche Portfoliowertentwicklung (blau, durchgezogen) und Ihr ursprünglich investiertes Kapital (gestrichelt). So sehen Sie sofort, ob Ihr Wertpapier das eingesetzte Kapital übertroffen hat.

---

### Tab 2: Zeitliche Entwicklung

Dieser Tab zeigt, wie sich Ihre Rendite über die einzelnen Jahre und Monate verteilt hat.

**Jahresrenditen-Balkendiagramm:**
Jedes abgeschlossene Kalenderjahr erhält einen eigenen Balken. Grüne Balken zeigen Gewinnerjahre, rote Verlustjahre. Das laufende Jahr wird als „YTD" (Year-to-Date) angezeigt.

**Monatsrenditen-Heatmap:**
Eine farbliche Tabelle zeigt alle Monate der letzten Jahre. Dunkelgrüne Felder bedeuten starke Monate, dunkelrote schwache Monate. So erkennen Sie auf einen Blick saisonale Muster.

**Dividendenverlauf:**
Balkendiagramm der erhaltenen Dividenden pro Jahr – aufgeteilt in Brutto (vor Steuer) und Netto (nach Steuer), sowie die aufgelaufene Gesamtdividende.

---

### Tab 3: Cashflows

Dieser Tab zeigt alle Geldbewegungen, die mit diesem Wertpapier verbunden sind, in zeitlicher Reihenfolge.

**Transaktions-Timeline:**
Jede Transaktion erscheint als Eintrag mit Datum, Typ und Betrag:

| Icon/Farbe | Transaktionstyp | Bedeutung |
|-----------|----------------|-----------|
| 🟥 (rot, negativ) | **Kauf** | Sie haben Geld investiert |
| 🟩 (grün, positiv) | **Verkauf** | Sie haben Anteile verkauft |
| 🟩 (grün, positiv) | **Dividende** | Sie haben eine Ausschüttung erhalten |
| 🟥 (rot, negativ) | **Steuer** | Abgezogene Quellensteuer oder Abgeltungssteuer |

Ein Klick auf einen Eintrag öffnet die Detailansicht dieser Buchung.

**Jahres-Kostenübersicht:**
Ein Balkendiagramm zeigt, wie viele Gebühren und Steuern in jedem Jahr angefallen sind – nützlich für Ihre Steuererklärung.

---

### Tab 4: Kennzahlen

Dieser Tab enthält alle wichtigen Finanzkennzahlen. Jede Zahl trägt ein **[?]-Symbol**, das auf Wunsch eine kurze Erläuterung und die Berechnungsformel zeigt.

**Rendite-Kennzahlen:**

| Kennzahl | Was Sie damit erfahren |
|----------|------------------------|
| **Bruttorendite** | Ihr Gesamtgewinn vor Steuern, in Prozent |
| **Nettorendite** | Ihr Gesamtgewinn nach Steuern – was wirklich bei Ihnen ankommt |
| **TWR (zeitgewichtete Rendite)** | Rendite des Wertpapiers selbst, unabhängig von Ihrem Investitionszeitpunkt. Ermöglicht fairen Vergleich mit Benchmarks. |
| **IRR (persönliche Rendite)** | Wie hoch Ihre tatsächliche jährliche Rendite ist – unter Berücksichtigung *wann* Sie wie viel investiert haben. Entspricht dem Zinssatz, bei dem Ihr Investment „geradeso aufgegangen" wäre. |
| **CAGR (Ø Jahresrendite)** | Die durchschnittliche Wachstumsrate pro Jahr, als wäre Ihr Investment gleichmäßig gewachsen |

**Risiko-Kennzahlen:**

| Kennzahl | Was Sie damit erfahren |
|----------|------------------------|
| **Volatilität** | Wie stark schwankt der Kurs? Hohe Volatilität = große Ausschläge nach oben *und* unten. Angabe als Prozent pro Jahr. |
| **Maximaler Drawdown** | Der schlimmste Kursrückgang vom letzten Höchststand bis zum nachfolgenden Tiefststand. Beispiel: „–18,5 %" bedeutet, der Kurs fiel vom Hoch um fast ein Fünftel. |

**Gewinn-/Verlust-Aufteilung:**

| Kennzahl | Was Sie damit erfahren |
|----------|------------------------|
| **Realized Gains** | Gewinne, die Sie durch bereits *abgeschlossene* Verkäufe tatsächlich eingenommen haben |
| **Unrealized Gains** | Gewinne, die noch „im Papier" stecken – noch nicht realisiert, weil Sie noch nicht verkauft haben |
| **Dividendenrendite (aktuelles Jahr)** | Wie viel Dividende Sie in diesem Kalenderjahr bezogen auf den Kurswert erhalten haben |
| **Steuerquote** | Welcher Anteil Ihrer Bruttorendite an den Fiskus geflossen ist |

**Optionale Kennzahl:**

| Kennzahl | Was Sie damit erfahren |
|----------|------------------------|
| **Sharpe Ratio** | Risikobereinigte Rendite: Wie viel Mehrrendite haben Sie für das eingegangene Risiko erhalten? Muss in den Einstellungen aktiviert werden (→ Abschnitt Benutzereinstellungen). |

> **Hinweis:** Wenn die IRR-Berechnung kein Ergebnis liefert (mathematisch nicht lösbar), erscheint „–" mit einem Hinweis-Tooltip.

---

### Tab 5: Benchmark

Ein **Benchmark** ist ein Vergleichswertpapier, das Ihnen zeigt, wie Ihre Investition im Vergleich zum „Markt" abgeschnitten hat. Sie wählen selbst, welches Wertpapier als Maßstab dienen soll – zum Beispiel ein ETF auf den DAX oder S&P 500.

**Was zeigt der Chart?**
Beide Wertpapiere werden auf einen gemeinsamen Startpunkt (Basis 100) normiert. So sehen Sie sofort, ob Ihr Wertpapier besser oder schlechter als der Benchmark gelaufen ist.

**Benchmark nicht konfiguriert?**
Wenn Sie noch keinen Benchmark eingestellt haben, erscheint in diesem Tab ein Hinweistext mit einem direkten Link zu den **Einstellungen**.

---

## Benutzereinstellungen

In den **Benutzereinstellungen** können Sie drei Optionen für die Renditeanalyse festlegen:

| Einstellung | Beschreibung |
|-------------|-------------|
| **Benchmark-Wertpapier** | Wählen Sie aus Ihren vorhandenen Wertpapieren eines als Vergleichsmaßstab aus. Sie können das Benchmark-Wertpapier jederzeit ändern oder entfernen. |
| **Sharpe Ratio anzeigen** | Diese Kennzahl ist standardmäßig ausgeblendet. Aktivieren Sie sie, wenn Sie die risikobereinigte Rendite auswerten möchten. |
| **Risikofreier Zinssatz** | Wird nur benötigt, wenn die Sharpe Ratio aktiviert ist. Tragen Sie hier den aktuellen risikofreien Zinssatz ein (z. B. „0,04" für 4 %). Ein negativer Wert wird nicht akzeptiert. |

---

## Schritt-für-Schritt-Anleitung

### Rendite-Box auf der Wertpapierseite lesen

1. Sie navigieren zu **Wertpapiere** und öffnen das gewünschte Wertpapier.
2. Die Rendite-Box lädt automatisch und zeigt Ihnen Gesamtrendite, CAGR, TWR und weitere Kennzahlen.
3. Sie fahren mit der Maus über das **[?]-Symbol** neben einer Zahl, um eine Erklärung der Berechnungsformel zu sehen.
4. Sie klicken auf **„Detaillierte Renditeanalyse →"**, um zur Vollansicht zu wechseln.

### Detailseite aufrufen und Tabs erkunden

1. Sie klicken auf **„Detaillierte Renditeanalyse →"** oder navigieren direkt zu `/securities/{id}/performance`.
2. Die Seite öffnet sich auf Tab **„Übersicht"** mit dem Performance-Chart.
3. Sie klicken auf einen Zeitraum-Button (z. B. **„1 Jahr"**), um den Zeitraum anzupassen.
4. Sie wechseln durch Klick auf die Tab-Bezeichnungen zu weiteren Analysen.

### Benchmark einrichten

1. Sie öffnen **Einstellungen** (oder **Setup**) und suchen den Abschnitt **„Wertpapier-Einstellungen"**.
2. Sie wählen im Dropdown **„Benchmark-Wertpapier"** ein Wertpapier aus Ihrer Liste aus.
3. Sie klicken **Speichern**.
4. Beim nächsten Öffnen der Renditedetailseite ist Tab **„Benchmark"** sichtbar und der Vergleichschart wird geladen.

---

## Beispiel

Sie besitzen 200 Anteile eines MSCI-World-ETFs, die Sie über drei Jahre in mehreren Tranchen gekauft haben:

- **Kauf 1 (Jan 2022):** 100 Anteile zu je 50 € = 5.000 €
- **Kauf 2 (Jun 2023):** 100 Anteile zu je 55 € = 5.500 €
- **Dividende (Dez 2023):** 180 € Ausschüttung netto
- **Aktueller Kurs (heute):** 62 €

Die Rendite-Box zeigt Ihnen:
- Gesamtrendite: ▲ +1.880 € (+18,1 %)
- CAGR: ▲ +7,4 % p.a.
- Investiertes Kapital: 10.500 €
- Aktueller Marktwert: 12.400 €

Sie klicken auf **„Detaillierte Renditeanalyse →"** und wechseln zum Tab **„Cashflows"**. Dort sehen Sie alle Käufe und die Dividende in chronologischer Reihenfolge. Im Tab **„Kennzahlen"** sehen Sie zusätzlich die IRR (Ihre persönliche Rendite) und den maximalen Drawdown aus dem Jahr 2022.

---

## Was passiert im Hintergrund?

Wenn Sie die Wertpapierseite öffnen, berechnet das System automatisch alle Kennzahlen. Damit das schnell geht, werden die Ergebnisse für eine Stunde gespeichert. Sobald Sie eine neue Transaktion erfassen oder neue Kursdaten vorliegen, werden die gespeicherten Ergebnisse automatisch verworfen und beim nächsten Aufruf neu berechnet.

Fehlen Kursdaten für einzelne Tage (z. B. Wochenenden oder Feiertage), wird der zuletzt bekannte Kurs weiterverwendet. Das System zeigt einen Hinweis, wenn größere Kurslücken vorliegen.

---

## Bekannte Einschränkungen

| Einschränkung | Details |
|---------------|---------|
| **TWR-Abweichung bei mehreren Transaktionen am selben Tag** (BUG-1) | Wenn an einem einzelnen Tag mehrere Käufe oder Verkäufe erfasst sind, kann die zeitgewichtete Rendite (TWR) leicht vom exakten theoretischen Wert abweichen. Die Abweichung ist in der Regel unter 0,1 % und hat keinen Einfluss auf Ihre Entscheidungen. |
| **Benchmark-Vergleich ohne Testabdeckung** | Für die Benchmark-Berechnungen sind aktuell noch nicht alle automatischen Tests implementiert. Bitte prüfen Sie die Benchmark-Werte bei auffälligen Abweichungen manuell. |
| **Nur interne Wertpapiere als Benchmark** | Als Benchmark können nur Wertpapiere gewählt werden, die bereits im FinanceManager erfasst sind. Externe Indizes (z. B. direkt von einem Datenanbieter) sind nicht möglich. |
| **Keine Währungsumrechnung** | Alle Berechnungen erfolgen in der Originalwährung des Wertpapiers. Wertpapiere in Fremdwährung werden nicht in Euro umgerechnet. |

---

## Häufige Fragen (FAQ)

**Warum zeigt die Rendite-Box manchmal „–" statt einer Zahl?**
Das passiert, wenn ein aktueller Kurs fehlt oder wenn nicht genug Daten für eine sinnvolle Berechnung vorliegen (z. B. keine Käufe erfasst). Stellen Sie sicher, dass Kursdaten aktuell sind (F007).

**Was ist der Unterschied zwischen TWR und IRR?**
Der TWR zeigt die Rendite des Wertpapiers selbst – unabhängig davon, wann Sie wie viel investiert haben. Der IRR zeigt Ihre *persönliche* Rendite und berücksichtigt, dass große Investments zu einem ungünstigen Zeitpunkt schlechter wirken. Für einen ehrlichen Marktvergleich nutzen Sie den TWR; für eine persönliche Bilanz den IRR.

**Warum stimmen Realized Gains und Gesamtrendite nicht überein?**
Realized Gains zeigen nur die abgeschlossenen Verkäufe. Die Gesamtrendite enthält zusätzlich den Unrealized Gain (den noch nicht realisierten Gewinn auf noch gehaltene Anteile) sowie Dividenden.

**Ich sehe den Tab „Benchmark" nicht. Was mache ich?**
Der Tab erscheint erst, wenn Sie in den Einstellungen ein Benchmark-Wertpapier ausgewählt haben. Folgen Sie den Schritten im Abschnitt „Benchmark einrichten".

**Kann ich Renditen exportieren (z. B. als Excel-Datei)?**
Ein Export ist in der aktuellen Version nicht vorgesehen. Einzelne Kennzahlen können manuell aus der Ansicht abgelesen werden.

---

## Verwandte Funktionen

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md) – Wie Sie Wertpapiere anlegen und verwalten
- [F007 – Wertpapierpreise](./F007-wertpapierpreise.md) – Automatischer Kursabruf via AlphaVantage
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md) – Benchmark und Sharpe Ratio konfigurieren
- [F017 – Renditeanalyse (Domänen-Dokumentation)](./F017-renditeanalyse-domain.md) – Technische Details für Sachbearbeiter
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md) – Übergreifende Finanzberichte
