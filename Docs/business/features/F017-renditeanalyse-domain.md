# F017 – Wertpapier-Renditeanalyse (Domänen-Dokumentation)

> Diese Dokumentation richtet sich an **Sachbearbeiter und Entwickler**, die die Geschäftsregeln und Berechnungslogik der Renditeanalyse verstehen oder prüfen müssen. Für die Anwender-Perspektive siehe [F017 – Wertpapier-Renditeanalyse](./F017-renditeanalyse.md).

---

## Überblick

Die Renditeanalyse wertet die vorhandenen Wertpapiertransaktionen (`Posting`) und Kursdaten (`SecurityPrice`) eines Nutzers aus. Alle Kennzahlen werden serverseitig berechnet und für jeweils eine Stunde zwischengespeichert. Es werden keine neuen Datensätze dauerhaft erzeugt – die Ergebnisse sind jederzeit aus den Rohdaten reproduzierbar.

---

## Kennzahl-Definitionen

### 1. TWR – Zeitgewichtete Rendite (Modified Dietz)

**Zweck:** Bewertet die Rendite des Wertpapiers unabhängig davon, wann Kapital investiert oder abgezogen wurde. Ermöglicht fairen Vergleich mit einem Benchmark.

**Methode:** Modified Dietz (GIPS-konform, Approximation der exakten Sub-Perioden-Verkettung)

**Formel je Subperiode:**

```
Perioden-Return = (Endwert – Anfangswert – Externer Cashflow)
                  ÷ (Anfangswert + 0,5 × Externer Cashflow)
```

Alle Subperioden-Returns werden verkettet:

```
TWR = (1 + R₁) × (1 + R₂) × … × (1 + Rₙ) – 1
```

**Subperioden:** Eine neue Subperiode beginnt bei jedem externen Cashflow-Ereignis (Kauf, Verkauf, Dividende).

**Grenzfall – Division by Zero (S-2):**
Wenn `Anfangswert + 0,5 × Cashflow = 0` (typisch beim allerersten Kauf, da der Anfangswert = 0), entsteht eine Division durch null.
**Regelung:** Diese Subperiode wird übersprungen und erhält eine Rendite von 0. Die Verkettung fährt mit der nächsten Subperiode fort. In diesem Fall kann der TWR geringfügig vom theoretischen Wert abweichen (→ bekannte Einschränkung BUG-1).

**Grenzfall – Gleicher Buchungstag:**
Mehrere Transaktionen am selben Kalendertag werden aggregiert, bevor die Subperioden gebildet werden. So entstehen keine Perioden der Länge null.

---

### 2. IRR – Interner Zinsfuß (XIRR)

**Zweck:** Persönliche Rendite des Anlegers auf Basis der tatsächlichen Cashflows und deren genauen Zeitpunkte.

**Definition:** Der Zinssatz `r`, bei dem der Kapitalwert (NPV) aller Cashflows (Käufe negativ, Verkäufe positiv, Dividenden positiv, Steuern negativ, aktueller Marktwert als Terminal-Cashflow positiv) gleich null ist.

**Lösungsverfahren:** Newton-Raphson mit Bisection-Fallback

- Abbruchbedingung: `|f(r)| < 1e-10` oder maximale Iterationsanzahl von **100**
- Bei Nicht-Konvergenz: Rückgabe von `null`; UI zeigt „–" mit erklärendem Tooltip
- Bisection-Fallback: Intervall `[–0,99; 10,0]`, wird aktiviert, wenn die Newton-Raphson-Ableitung NaN ergibt oder negativ ist

**Tageszählkonvention:** **Actual/365** (entspricht dem Excel-XIRR-Standard)

**Grenzfall – kein positiver Cashflow:**
Bei einem reinen Kauf-Portfolio ohne Verkäufe oder Dividenden ist der IRR mathematisch nicht definierbar → Rückgabe `null`.

---

### 3. CAGR – Durchschnittliche jährliche Wachstumsrate

**Formel:**

```
CAGR = (Endwert ÷ Anfangswert)^(1 ÷ Haltedauer_in_Jahren) – 1
```

**Gültigkeitsbereich:**
- Anfangswert > 0 (sonst: `null`)
- Haltedauer > 0 Tage (sonst: `null`)
- Negative Endwerte ergeben mathematisch gültige, aber schwer interpretierbare CAGR-Werte; werden berechnet und zurückgegeben

---

### 4. Volatilität (annualisiert)

**Methode:** Annualisierte Standardabweichung der täglichen logarithmischen Renditen

**Formel:**

```
Tagesrendite_t = ln(Kurs_t ÷ Kurs_{t-1})

Volatilität = Standardabweichung(Tagesrenditen) × √252
```

**Bessel-Korrektur:** Ja (Division durch `n – 1`, nicht `n`) – Standard für Stichproben-Standardabweichung

**Annualisierungsfaktor:** √252 (Anzahl der Handelstage pro Jahr)

**Mindestdaten:** Mindestens 2 Kursdatenpunkte erforderlich; sonst `null`

---

### 5. Maximaler Drawdown

**Definition:** Der größte prozentuale Rückgang vom lokalen Höchststand bis zum nachfolgenden Tiefststand innerhalb der gesamten Kurszeitreihe.

**Formel:**

```
Max. Drawdown = (Tiefster Wert nach dem Peak – Peak) ÷ Peak
```

Ergebnis ist immer ≤ 0 (negativ oder null). Beispiel: `–0,185` = 18,5 % Wertverlust vom Höchststand.

**Berechnung:** Zeitliche Vorwärtssuche über alle Kurswerte; bei jedem neuen Höchststand wird der maximale Rückgang bis zum nächsten Punkt neu bewertet.

---

### 6. Sharpe Ratio (opt-in)

**Aktivierung:** Nur wenn die Benutzereinstellung `ShowSharpeRatio = true`. Ohne Aktivierung wird die Kennzahl weder berechnet noch angezeigt.

**Formel:**

```
Sharpe Ratio = (Portfoliorendite p.a. – Risikofreier Zinssatz) ÷ Volatilität
```

**Konfiguration:**
- `RiskFreeRate` ist vom Nutzer als Dezimalzahl einzugeben (z. B. `0,04` für 4 %)
- Muss ≥ 0 sein; negative Werte werden mit Validierungsfehler abgewiesen
- Wenn `Volatilität = 0`: Rückgabe `null` (Division durch null vermieden)

---

## FIFO-Kostenbasis

### Was ist FIFO?

**FIFO** (First In, First Out) ist die gesetzlich verbreitete Methode zur Ermittlung des Einstandspreises bei Teilverkäufen. Die zuerst gekauften Anteile gelten als zuerst verkauft.

### Lot-Bildung

Jeder Kauf (`SecurityPostingSubType.Buy`) erzeugt ein **Lot** in einer geordneten Warteschlange:

```
Lot = (Kaufdatum, Menge, Kosten pro Anteil)
```

**Sortierung:** BookingDate aufsteigend; bei gleichem Datum nach der Datenbank-Einfügungsreihenfolge (`Posting.Id` aufsteigend). Diese Reihenfolge ist deterministisch und reproduzierbar.

**Kostenbasis je Lot:**
Kaufpreis (Amount) plus etwaige Gebühren (Fee-Postings mit derselben `GroupId` wie der Kauf).

### Verkaufs-Auflösung (FIFO)

Bei einem Verkauf (`SecurityPostingSubType.Sell`) wird die älteste Lot-Menge zuerst aufgelöst:

1. Ältestes Lot aus der Warteschlange nehmen
2. Realisierter Gewinn += (Verkaufspreis – Einstandspreis) × min(Lot.Menge, Restmenge)
3. Restmenge des Lots zurück in die Warteschlange, wenn noch Anteile verbleiben
4. Wiederholen, bis die Verkaufsmenge vollständig aufgelöst ist

### Grenzfall – Oversell

Wenn die Verkaufsmenge den vorhandenen FIFO-Bestand übersteigt (z. B. durch fehlende Import-Buchungen):

- Der verfügbare Bestand wird vollständig aufgelöst
- Die Restmenge wird protokolliert und als **Warnmeldung** im Ergebnis zurückgegeben
- Die Berechnung bricht **nicht ab** (kein Fehler); das Ergebnis enthält einen `OversellWarning`-Hinweis
- Die UI zeigt einen entsprechenden Hinweistext

---

## Sicherheitsregeln

### S-1 – Ownership-Scoping bei Buchungsabfragen

Das `Posting`-Datenbankobjekt hat **kein direktes `OwnerUserId`-Feld**. Die Nutzerzuordnung erfolgt über die verknüpfte `Security`-Entität:

```sql
WHERE p.SecurityId = @securityId
  AND p.Security.OwnerUserId = @userId
```

Dies entspricht einem Inner Join auf die `Security`-Tabelle. Kein Buchungsdatensatz eines anderen Nutzers kann durch diese Abfrage ausgelesen werden, auch wenn die `SecurityId` bekannt wäre.

**Pflicht:** Alle Abfragen auf `Posting` müssen diesen Join-Filter verwenden. Direkte Abfragen ohne Ownership-Check sind nicht zulässig.

### S-3 – Benchmark-Ownership beim Laden

Die Einstellung `BenchmarkSecurityId` (eine Wertpapier-ID aus dem eigenen Bestand) wird beim Setzen auf Ownership geprüft. **Zusätzlich** wird beim Laden der Benchmark-Kursdaten erneut geprüft:

```
Security.OwnerUserId == aktuellerNutzer.Id
```

Dieser doppelte Check verhindert, dass ein manipulierter Einstellungswert Kursdaten eines fremden Wertpapiers abruft.

---

## Datenqualität und Kurslücken

### Forward-Fill bei fehlenden Kursen

Fehlen Kursdaten für einzelne Tage (Wochenenden, Feiertage, Datenlücken), wird der zuletzt bekannte Kurs bis zum nächsten vorhandenen Datenpunkt fortgeschrieben (**Forward-Fill**).

**Grenzwert:** Kurslücken von mehr als 30 aufeinanderfolgenden Tagen werden nicht stillschweigend gefüllt. In diesem Fall wird die betroffene Berechnungsperiode übersprungen und dem Nutzer ein Hinweis angezeigt (z. B. „Kursdaten ab [Datum] verfügbar").

### Mindestdaten für Berechnungen

| Kennzahl | Mindestvoraussetzung |
|----------|---------------------|
| Rendite-Box | ≥ 1 Kauf-Buchung + ≥ 1 Kursdatenpunkt |
| TWR | ≥ 2 Kursdatenpunkte in der Haltedauer |
| IRR | ≥ 1 Cashflow-Paar (Kauf + Marktwert oder Verkauf) |
| CAGR | ≥ 1 Tag Haltedauer + Anfangs- und Endwert > 0 |
| Volatilität | ≥ 2 aufeinanderfolgende Kursdatenpunkte |
| Max. Drawdown | ≥ 2 Kursdatenpunkte |

---

## Caching-Strategie

### Gültigkeitsdauer (TTL)

Alle berechneten Ergebnisse werden **1 Stunde** im Arbeitsspeicher des Servers gehalten. Der Cache ist nutzerspezifisch und wertpapierspezifisch.

### Cache-Schlüssel

```
ra:{datentyp}:{securityId}:{userId}[:{parameter}]
```

Beispiele:
- `ra:summary:{securityId}:{userId}` – kompakte Rendite-Box
- `ra:metrics:{securityId}:{userId}` – detaillierte Kennzahlen
- `ra:chart:{securityId}:{userId}:{zeitraum}` – Performance-Chart-Daten

### Cache-Invalidierung

Der Cache wird **sofort** gelöscht, wenn:

| Ereignis | Betroffene Cache-Einträge |
|----------|--------------------------|
| Neue Buchung für dieses Wertpapier | Alle `ra:*:{securityId}:{userId}` |
| Neuer Kursdatenpunkt für dieses Wertpapier | Alle `ra:*:{securityId}:{userId}` |
| Neuer Kursdatenpunkt für das Benchmark-Wertpapier | `ra:benchmark:{securityId}:{userId}` |

Nach der Invalidierung wird beim nächsten Aufruf alles neu berechnet. Der Nutzer bemerkt dabei keinen Unterschied – die Berechnung erfolgt transparent im Hintergrund.

### Verhalten nach Neustart

Bei einem Neustart des Servers gehen alle gecachten Ergebnisse verloren. Die erste Anfrage nach dem Neustart löst eine vollständige Neuberechnung aus, die einige Sekunden dauern kann.

---

## Benutzereinstellungen (Datenbankfelder)

Die Renditeanalyse-Einstellungen werden als zusätzliche Felder in der Nutzertabelle gespeichert:

| Feldname | Typ | Standard | Beschreibung |
|----------|-----|----------|-------------|
| `BenchmarkSecurityId` | GUID (optional) | `null` | ID des gewählten Benchmark-Wertpapiers. Muss dem Nutzer gehören. |
| `ShowSharpeRatio` | Boolean | `false` | Ob die Sharpe Ratio in der Kennzahlen-Ansicht angezeigt werden soll |
| `RiskFreeRate` | Dezimalzahl | `0` | Risikofreier Zinssatz für die Sharpe-Ratio-Berechnung (z. B. 0,04 = 4 %); muss ≥ 0 sein |

**Validierung:** Ein negativer `RiskFreeRate`-Wert wird mit einer Fehlermeldung abgewiesen. Die Einstellung kann nicht gespeichert werden, bis der Wert korrigiert ist.

**Datenbankmigration:** Neue Felder werden über zwei EF-Core-Migrationen hinzugefügt:
1. `AddReturnAnalysisSettingsToUser` – fügt die drei Felder in die Nutzertabelle ein
2. `AddReturnAnalysisPerformanceIndexes` – legt Datenbankindizes auf `SecurityPrices(SecurityId, Date)` und `Postings(SecurityId, BookingDate)` an

---

## Dividenden und Steuern

### Dividenden-Zuordnung

Dividenden werden dem `BookingDate` des zugehörigen `Posting`-Eintrags mit `SecurityPostingSubType.Dividend` zugeordnet. Dieses Datum entspricht dem Zahlungsdatum (nicht dem Ex-Dividende-Tag, es sei denn, beide fallen zusammen).

### Steuer-Zuordnung (Quellensteuer)

Steuer-Buchungen (`SecurityPostingSubType.Tax`) mit derselben `GroupId` wie eine Dividenden-Buchung gelten als Quellensteuer dieser Dividende. Sie werden bei der Nettorendite-Berechnung abgezogen.

Steuer-Buchungen **ohne** zugehörige Dividenden-Buchung (eigenständige Steuerbuchungen) fließen als separate negative Cashflows in die IRR-Berechnung ein, werden aber nicht zur FIFO-Kostenbasis addiert.

### Gebühren-Zuordnung

Gebühren-Buchungen (`SecurityPostingSubType.Fee`) mit derselben `GroupId` wie ein Kauf werden zur FIFO-Kostenbasis addiert. Eigenständige Gebühren-Buchungen ohne Kauf-Bezug fließen als separate negative Cashflows in die IRR-Berechnung ein.

---

## Häufige Fragen (Sachbearbeiter)

**Warum kann der TWR leicht vom theoretischen Wert abweichen?**
Die verwendete Modified-Dietz-Methode ist eine Näherung der exakten Sub-Perioden-Verkettung. Bei der ersten Kaufperiode (Anfangsbestand = 0) wird die Subperiode übersprungen. Die Abweichung beträgt in der Praxis weniger als 0,1 % und ist für Entscheidungszwecke vernachlässigbar.

**Was passiert, wenn ein Nutzer mehr Stücke verkauft, als je verbucht wurden?**
Das System gibt eine Warnmeldung zurück (`OversellWarning`) und berechnet Realized Gains nur auf Basis des vorhandenen FIFO-Bestands. Die Berechnung bricht nicht ab. Der Nutzer sieht einen Hinweistext in der Oberfläche.

**Warum wird der IRR manchmal als „–" angezeigt?**
Der IRR-Algorithmus konvergiert nach maximal 100 Iterationen. Wenn keine Lösung gefunden wird (z. B. bei einem reinen Kauf-Portfolio ohne Verkäufe oder Dividenden), gibt das System `null` zurück. Der Nutzer sieht „–" mit erklärendem Tooltip.

**Kann der Benchmark auf ein Wertpapier eines anderen Nutzers gesetzt werden?**
Nein. Beim Setzen und beim Laden der Benchmark-Daten wird die Eigentumsregel (`Security.OwnerUserId == userId`) geprüft. Eine Manipulation des Einstellungswerts würde spätestens beim Laden der Kursdaten abgefangen.

**Werden Kursberechnungen nach einem Serverneustart erneut ausgeführt?**
Ja. Der Cache liegt ausschließlich im Arbeitsspeicher und geht bei einem Neustart verloren. Die erste Anfrage eines Nutzers löst eine vollständige Neuberechnung aus. Dies ist bewusst so gestaltet (keine persistente Cache-Datenbank in Phase 1).

---

## Verwandte Dokumente

- [F017 – Renditeanalyse (Anwender-Dokumentation)](./F017-renditeanalyse.md)
- [F006 – Wertpapier-Verwaltung (Domäne)](./F006-wertpapier-verwaltung-domain.md)
- [F003 – Ausgabenverwaltung (Posting-Entität)](./F003-ausgabenverwaltung-domain.md)
- [F007 – Wertpapierpreise (Infrastruktur)](./F007-wertpapierpreise-infrastructure.md)
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md)
