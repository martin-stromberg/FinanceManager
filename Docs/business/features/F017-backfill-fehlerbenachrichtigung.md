# F017 – Backfill-Fehlerbenachrichtigung

## Einleitung

Diese Funktion informiert Sie bei Problemen im Nachlade-Lauf für Wertpapierkurse.  
Sie erhalten eine klare Meldung, wenn ein Problem nicht nur kurzzeitig ist.  
So wissen Sie sofort, ob Sie handeln müssen oder später neu starten können.  
Der Lauf verarbeitet trotz einzelner Fehler weitere Wertpapiere weiter.

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in der Vermögensübersicht und im Support.  
Sie prüfen nach einem Nachlade-Lauf, ob Handlungsbedarf besteht.  
Stakeholder sehen damit, dass Fehler sichtbar und nachvollziehbar behandelt werden.

## Schritt-für-Schritt-Anleitung

1. Sie starten den Nachlade-Lauf für Wertpapierkurse.  
2. Sie prüfen nach dem Lauf die Hinweise auf der Startseite.  
3. Sie sehen bei dauerhaften Problemen die Meldung **Kursabruf fehlgeschlagen**.  
4. Sie öffnen das betroffene Wertpapier und prüfen die Stammdaten.  
5. Sie korrigieren bei Bedarf das Symbol und klicken **Speichern**.  
6. Sie starten den Nachlade-Lauf danach erneut.  
7. Sie starten bei kurzfristigen Problemen den Lauf später neu.

## Beispiel

Sie laden Kurse für zehn Wertpapiere nach.  
Bei einem Wertpapier ist das Symbol ungültig.  
Sie sehen sofort **Kursabruf fehlgeschlagen** mit einer klaren Handlungsempfehlung.  
Die übrigen Wertpapiere werden im selben Lauf trotzdem weiter bearbeitet.

## Was passiert im Hintergrund?

Die Anwendung unterscheidet kurzfristige und dauerhafte Probleme.  
Bei kurzfristigen Problemen erhalten Sie keine zusätzliche Meldung.  
Bei dauerhaften Problemen wird ein Hinweis für Sie auf der Startseite erstellt.  
Beim Tageslimit endet der laufende Durchgang sofort, ohne neue Hinweis-Erstellung.

## Häufige Fragen (FAQ)

**F: Wann sehe ich die Meldung _Kursabruf fehlgeschlagen_?**  
A: Wenn ein dauerhafter Fehler beim Kursabruf erkannt wurde.

**F: Bekomme ich bei jedem Problem eine neue Meldung?**  
A: Nein. Bei kurzfristigen Verbindungsproblemen erscheint keine neue Meldung.

**F: Was passiert beim Tageslimit des Anbieters?**  
A: Der laufende Nachlade-Lauf stoppt sofort. Sie starten später neu.

**F: Bricht ein Fehler den ganzen Lauf immer ab?**  
A: Nein. Einzelne Fehler stoppen nicht alle anderen Wertpapiere.

**F: Ist dieses Verhalten getestet?**  
A: Ja. Die Meldungslogik und das Weiterlaufen bei Fehlern sind durch Tests abgedeckt.

## Nachweise und Teststatus

- Planungsgrundlage:
  - [Requirements](../../requirements/security-price-backfill-notification-alignment.md)
  - [Architektur-Blueprint](../../architecture/security-price-backfill-notification-alignment.md)
  - [ERM-Analyse](../../architecture/security-price-backfill-notification-erm.md)
  - [Architektur-Review](../../improvements/security-price-backfill-notification-review.md)
  - [Planungsübersicht](../../security-price-backfill-notification-planning-overview.md)
- Umsetzung:
  - [SecurityPricesBackfillExecutor](../../../FinanceManager.Web/Services/SecurityPricesBackfillExecutor.cs)
  - [SecurityPriceProviderErrorUserMessageBuilder](../../../FinanceManager.Web/Services/SecurityPriceProviderErrorUserMessageBuilder.cs)
- Tests:
  - [SecurityPricesBackfillExecutorNotificationTests](../../../FinanceManager.Tests/Web/Services/SecurityPricesBackfillExecutorNotificationTests.cs)
  - [SecurityPriceProviderErrorUserMessageBuilderTests](../../../FinanceManager.Tests/Web/Services/SecurityPriceProviderErrorUserMessageBuilderTests.cs)
  - [Testlücken-Liste](../../tests/backfill-fehlerbenachrichtigung-testluecken.md)
- Aktueller Stand: Kernfälle für Meldung, Nicht-Meldung und Weiterlauf sind umgesetzt. 8 relevante Tests laufen erfolgreich.

## Verwandte Funktionen

- [F007 – Wertpapierpreise](./F007-wertpapierpreise.md)
- [F013 – Benachrichtigungen](./F013-benachrichtigungen.md)
- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
