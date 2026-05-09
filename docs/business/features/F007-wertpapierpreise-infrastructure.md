# F007 – Wertpapierpreise (Infrastructure-Perspektive)

## Einleitung

Diese Seite erklärt den Ablauf des Kursabrufs in einfacher Form.  
Der Schwerpunkt liegt auf der neuen Fehlerbehandlung.  
Nutzer erhalten klare Hinweise statt roher Anbietertexte.  
So können Fachbereiche schnell entscheiden: warten oder Daten korrigieren.

## Wer nutzt es?

Diese Seite hilft Produktverantwortlichen und fachnahen Stakeholdern.  
Sie brauchen eine klare Sicht auf Verhalten, Risiken und Diagnose.  
Auch Support-Teams nutzen diese Übersicht für Rückfragen aus dem Alltag.

## Schritt-für-Schritt-Anleitung

1. Sie starten den Kursabruf für mehrere Wertpapiere.  
2. Sie prüfen nach dem Lauf die Hinweise auf der Startseite.  
3. Sie unterscheiden den Hinweistext:  
   - **Symbol prüfen** steht für ein dauerhaftes Eingabeproblem.  
   - **Später erneut starten** steht für ein vorübergehendes Problem.  
4. Sie korrigieren bei dauerhaftem Problem das Symbol und speichern.  
5. Sie starten den Abruf erneut.  
6. Sie warten bei einem Tageslimit des Anbieters bis zum nächsten Lauf.

## Beispiel

Ein Lauf enthält fünf Wertpapiere.  
Beim ersten Wertpapier ist das Symbol falsch.  
Das System merkt sich den Fehler und zeigt einen klaren Nutzerhinweis.  
Beim zweiten Wertpapier tritt ein kurzer Verbindungsfehler auf.  
Der Lauf macht trotzdem mit den restlichen Wertpapieren weiter.

## Was passiert im Hintergrund?

Der Ablauf bewertet jeden Fehler mit einer festen Fehlerklasse.  
Vorübergehende Verbindungsprobleme lösen keinen dauerhaften Fehlerstatus aus.  
Dauerhafte Probleme am Symbol oder unklare Anbieterprobleme werden als Fehler am Wertpapier gespeichert.  
Bei Tageslimit stoppt der Lauf sofort, damit unnötige weitere Abrufe ausbleiben.  
Bei anderen Fehlern läuft der Durchgang mit dem nächsten Wertpapier weiter.

Gespeicherte Diagnosedaten:
- Fehlerklasse, zum Beispiel `INVALID_SYMBOL_OR_FUNCTION` oder `UNKNOWN_PROVIDER_ERROR`
- Sichere Nutzernachricht für die Oberfläche
- Zeitpunkt seit dem der Fehler aktiv ist
- Bereinigter Anbietertext, gekürzt und ohne Steuerzeichen

Warum keine rohen Anbietertexte im Nutzerhinweis:
- Rohe Texte sind oft technisch und missverständlich.
- Rohe Texte können uneinheitlich sein.
- Klare Standardhinweise führen schneller zur richtigen Aktion.

Absicherung durch Tests:
- Fehlerklassen aus Anbieterantworten:  
  [FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs](../../../FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs)
- Weiterlaufen bei nicht Tageslimit-Fehlern und Stopp bei Tageslimit:  
  [FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs](../../../FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs)
- Stabile Fehlercodes für Speicherung und Auswertung:  
  [FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs](../../../FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs)

Technische Artefakte:
- [FinanceManager.Web/Services/SecurityPriceWorker.cs](../../../FinanceManager.Web/Services/SecurityPriceWorker.cs)
- [FinanceManager.Web/Services/AlphaVantagePriceProvider.cs](../../../FinanceManager.Web/Services/AlphaVantagePriceProvider.cs)
- [FinanceManager.Web/Services/AlphaVantage.cs](../../../FinanceManager.Web/Services/AlphaVantage.cs)
- [FinanceManager.Web/Services/PriceProviderErrorClass.cs](../../../FinanceManager.Web/Services/PriceProviderErrorClass.cs)
- [FinanceManager.Domain/Securities/Security.cs](../../../FinanceManager.Domain/Securities/Security.cs)
- [FinanceManager.Infrastructure/Migrations/20260509072751_AddSecurityPriceErrorClassification.cs](../../../FinanceManager.Infrastructure/Migrations/20260509072751_AddSecurityPriceErrorClassification.cs)

## Häufige Fragen (FAQ)

**F: Was ist ein vorübergehendes Problem?**  
A: Kurzfristige Verbindungsprobleme. Sie starten den Abruf später erneut.

**F: Was ist ein dauerhaftes Problem?**  
A: Ein falsches oder ungeeignetes Symbol. Sie müssen den Eintrag korrigieren.

**F: Warum stoppt der Lauf beim Tageslimit sofort?**  
A: Weitere Abrufe wären im selben Lauf nicht erfolgreich.

**F: Welche Infos werden für Diagnose gespeichert?**  
A: Fehlerklasse, sichere Meldung, Zeitstempel und bereinigter Anbietertext.

**F: Wie wird sichergestellt, dass das Verhalten stabil bleibt?**  
A: Durch neue und aktualisierte Tests für Fehlerklassen und Laufsteuerung.

## Verwandte Funktionen

- [F007 – Wertpapierpreise](./F007-wertpapierpreise.md)
- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md)
