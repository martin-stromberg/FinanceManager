# Offene Rückmeldungen

Status 2026-11-08: Alle Punkte umgesetzt (siehe plan.md / review.md / test-results.md).

Erstellt am: 2026-11-08

## UX-Rückmeldung

- [x] Im Panel zur Sparplan↔Konto-Verknüpfung einen Info-Button ergänzen.
- [x] Der Info-Button soll erklären, wofür diese Verknüpfung gedacht ist:
  - verknüpfte Konten liefern den tatsächlichen Plan-Saldo,
  - Konto-Inflow dient als Grundlage für Rhythmus- und Defizitanalyse,
  - Funding-Postings auf anderen Konten können als informelle Belegquelle gekennzeichnet werden,
  - ohne verknüpfte Konten bleibt die bisherige Posting-basierte Betrachtung aktiv.
- [x] Umsetzung erst nach weiterer fachlicher Rückmeldung bzw. Freigabe. (freigegeben durch Auftrag)
- [x] Bei der Kontoverknüpfung dürfen ausschließlich Konten vom Typ „Sparkonto“ auswählbar sein.
- [x] Die Einschränkung auf Sparkonten muss durch Tests abgesichert werden.

## UX-Verfeinerung Verknüpfungsliste und Saldoanzeige

- [x] Der Button zum Entfernen einer Kontoverknüpfung soll als Symbolbutton dargestellt werden.
- [x] Jede verknüpfte Bankkonto-Zeile soll einen Symbolbutton zum Aufruf des Bankkontos erhalten.
- [x] Bei mehreren Verknüpfungen sollen die Salden der Bankkonten als eigene Spalte in der Auflistung angezeigt werden.
- [x] Bei nur einer Verknüpfung soll nur der Symbolbutton zum Bankkonto gezeigt werden.
- [x] In den Kopfdaten des Sparplans soll bei vorhandenen Verknüpfungen die Summe der Bankkontosalden als Saldo angezeigt werden.
- [x] Zusätzlich soll der bisherige Saldo aus den Sparplanposten als „Eigenanteil“ ausgewiesen werden.
- [x] Ohne Verknüpfung bleibt die bisherige Saldoausgabe unverändert.
- [x] In der Sparplanübersicht muss die Saldoanzeige denselben Regeln folgen wie in den Kopfdaten.

## Demodaten

- [x] Demodaten um ein Bankkonto für einen Bausparvertrag erweitern.
- [x] Demodaten um einen Sparplan „Bausparen“ erweitern.
- [x] Das Bausparkonto mit dem Sparplan „Bausparen“ verknüpfen.
- [x] Monatliche Buchungen vom Girokonto auf das Bausparkonto erfassen.
- [x] Zum Jahresende Zinsbuchungen in Höhe von 1 % des jeweiligen Saldos erfassen.
- [x] Die Zinsbuchungen sollen sowohl auf dem Girokonto als auch auf dem Bausparkonto saldenwirksam berücksichtigt werden.
- [x] Für die monatlichen Einzahlungen auf das Bausparkonto soll ein Budget in den Demodaten hinterlegt werden.
- [x] In den Demodaten soll ein weiterer Report erfasst werden, der die monatlichen Sparplanbuchungen zeigt.
- [x] Bei Sparplänen mit Kontoverknüpfung muss der Report die Bankkontoposten berücksichtigen können.
- [x] Die Reporteinstellungen sollen um eine neue Option erweitert werden, die gesetzt werden kann, wenn Sparpläne ausgegeben werden sollen.
- [x] Über diese Option soll der Anwender die Postenquelle für die ausgegebenen Sparpläne wählen können, insbesondere zwischen Sparplanposten und Posten der verknüpften Bankkonten.
