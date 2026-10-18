← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Statistikfeld „Umkehrsignale"

## Zweck

Das Statistikfeld „Umkehrsignale" ergänzt die Kennzahlen auf der
Wertpapierkarte (`/card/securities/{id}`, direkt unterhalb der Box
„Goldenes Kreuz") um eine bärische Trendanalyse. Es erkennt Umkehrsignale
nach unten und fasst sie zu einem Gesamt-Trendstatus zusammen.

## Erkannte Signale

| Signal | Erklärung | Stärke |
|--------|-----------|--------|
| **Todeskreuz** | Der 50-Tage-Durchschnitt ist unter den 200-Tage-Durchschnitt gefallen; das Kreuzungsdatum wird als „Death-Cross-Date" ausgegeben. | stark |
| **Bruch der 200-Tage-Linie** | Der Schlusskurs liegt unter der langfristigen Linie. Ab 5 aufeinanderfolgenden Tagen darunter gilt der Bruch als „strukturelle Trendwende" (stark), sonst als „kurzfristige Schwäche" (schwach). | schwach/stark |
| **Bärische Marktstruktur** | Niedrigere Hochs und niedrigere Tiefs in der Kursentwicklung: „beginnender Abwärtstrend" (stark), „Trendverlust" (mittel) oder „Seitwärtsphase mit Abwärtstendenz" (schwach). | schwach–stark |
| **Bärisches MACD-Crossover** | Die MACD-Linie (EMA12 − EMA26) hat die Signallinie (EMA9) von oben nach unten gekreuzt. | mittel |
| **Bärische RSI-Divergenz** | Der Kurs erreicht ein höheres Hoch, der RSI (Wilder, 14 Perioden) ein niedrigeres – Momentumverlust. | mittel |

## Trendstatus und Score

Jedes Signal trägt je nach Stärke 1 (schwach), 2 (mittel) oder 3 (stark)
Punkte zum Signal-Score bei:

| Status | Bedingung |
|--------|-----------|
| **Trend intakt** | Kein Umkehrsignal (Score 0) |
| **Trend geschwächt** | Score 1–3 |
| **Trendwende wahrscheinlich** | Score ≥ 4 |

## Wann wird das Feld angezeigt?

Die Box erscheint **nur**, wenn mindestens ein Umkehrsignal vorliegt
(Trendstatus „geschwächt" oder „Trendwende wahrscheinlich"). Bei intaktem
Trend, zu wenig Kursdaten oder Fehlern beim Laden bleibt sie ausgeblendet.
Über die Info-Schaltfläche öffnet sich ein Seitenpanel mit
Bedeutungserklärungen je Signal, der Statuserklärung und der Berechnungsformel.

## Benachrichtigungen

Nach dem Speichern neuer Kurse wertet der Hintergrund-Dienst die Analyse
erneut aus. Verschlechtert sich der Trendstatus, wird einmalig pro Status und
Zyklus ein Hinweis auf der Startseite erzeugt (der Zyklus beginnt neu, sobald
der Trend wieder „intakt" ist). Die Hinweise lassen sich im Setup unter
„Benachrichtigungen" über die Checkbox „Trendwende-Hinweise aktivieren"
pro Benutzer ein- und ausschalten (Standard: aktiviert).

## Hinweis

Umkehrsignale sind statistische Hinweise, keine Garantie für fallende Kurse.
Insbesondere in Seitwärtsphasen und bei geringer Liquidität sind Fehlsignale
möglich.
