# F007 – Wertpapierpreise

## Einleitung

Diese Funktion aktualisiert Ihre Wertpapierkurse automatisch.  
Ein behobener Fehlerfall betrifft AlphaVantage mit dem Hinweis `Invalid API call` bei `TIME_SERIES_DAILY`.  
Sie sehen dazu klare Hinweise statt unverständlicher Rohtexte.  
So erkennen Sie schnell, ob Sie warten oder das Symbol korrigieren müssen.  
Ihre letzte bekannte Kurslage bleibt erhalten.

## Wer nutzt es?

Diese Funktion nutzen Fachanwender aus Buchhaltung und Vermögensübersicht.  
Sie prüfen regelmäßig den aktuellen Stand ihrer Bestände.  
Auch neue Mitarbeitende nutzen die Hinweise, um Fehler schnell einzuordnen.

## Schritt-für-Schritt-Anleitung

1. Sie öffnen **Wertpapiere** und starten **Kurse aktualisieren**.  
2. Sie prüfen die Hinweise nach dem Lauf.  
3. Sie sehen bei einem dauerhaften Problem den Hinweis **Kursabruf fehlgeschlagen** mit dem Zusatz **Symbol prüfen**.  
4. Sie öffnen das betroffene Wertpapier und korrigieren das Symbol.  
5. Sie klicken **Speichern** und starten den Abruf erneut.  
6. Sie sehen bei einem kurzfristigen Problem einen Hinweis zum späteren Neustart.  
7. Sie warten in diesem Fall und starten den Abruf später erneut.  
8. Sie sehen dabei niemals einen eingeblendeten Schlüssel oder vertrauliche Zugangsdaten.

## Beispiel

Sie aktualisieren Kurse für drei Wertpapiere.  
Beim ersten Wertpapier ist das Symbol falsch hinterlegt.  
Sie erhalten einen klaren Hinweis mit **Symbol prüfen**.  
Beim zweiten Wertpapier gibt es kurzzeitig ein Verbindungsproblem.  
Der Lauf macht trotzdem weiter und aktualisiert das dritte Wertpapier erfolgreich.

## Was passiert im Hintergrund?

Die Anwendung trennt Fehler in klare Gruppen.  
Kurzfristige Probleme bei Verbindung oder Anbieter gelten als vorübergehend.  
Dauerhafte Probleme wie ein falsches Symbol gelten als zu korrigieren.  
Bei vorübergehenden Problemen versucht der Abruf erneut und läuft mit anderen Wertpapieren weiter.  
Wenn das Tageslimit erreicht ist, stoppt der laufende Durchgang sofort.

Für die Diagnose speichert die Anwendung Fehlerklasse, sichere Nutzernachricht, Zeitpunkt und bereinigte Details.  
Rohe Anbietertexte werden nie direkt angezeigt.  
Ein API-Schlüssel wird weder im Hinweis noch in gespeicherten Details offengelegt.

## Häufige Fragen (FAQ)

**F: Was sehe ich bei einem Fehler?**  
A: Sie sehen einen klaren Hinweis wie **Kursabruf fehlgeschlagen** mit kurzer Handlungsanweisung.

**F: Kann ein API-Schlüssel in einem Hinweis sichtbar werden?**  
A: Nein. Hinweise und gespeicherte Details zeigen keinen API-Schlüssel.

**F: Woran erkenne ich ein dauerhaftes Problem?**  
A: Hinweise wie **Symbol prüfen** zeigen, dass eine Eingabe angepasst werden muss.

**F: Woran erkenne ich ein vorübergehendes Problem?**  
A: Hinweise zum späteren Neustart zeigen ein kurzfristiges Problem bei Anbieter oder Verbindung.

**F: Was passiert beim Erreichen des Tageslimits?**  
A: Der laufende Abruf stoppt sofort. Sie starten später einen neuen Lauf.

## Nachweise und Testergebnis

- API-Artefakt: [docs/api/SecuritiesController.md](../../api/SecuritiesController.md)
- Flow-Artefakt: [docs/flows/security-price-worker.md](../../flows/security-price-worker.md)
- Code-Artefakte:
  - [FinanceManager.Web/Services/SecurityPriceWorker.cs](../../../FinanceManager.Web/Services/SecurityPriceWorker.cs)
  - [FinanceManager.Web/Services/AlphaVantage.cs](../../../FinanceManager.Web/Services/AlphaVantage.cs)
  - [FinanceManager.Web/Services/AlphaVantagePriceProvider.cs](../../../FinanceManager.Web/Services/AlphaVantagePriceProvider.cs)
- Test-Artefakte:
  - [FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs](../../../FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs)
  - [FinanceManager.Tests/Web/Services/AlphaVantagePriceProviderRetryTests.cs](../../../FinanceManager.Tests/Web/Services/AlphaVantagePriceProviderRetryTests.cs)
  - [FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs](../../../FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs)
- Dokumentations-/Lifecycle-Artefakt: [docs/documentation-plan.md](../../documentation-plan.md)
- Testergebnis: **Grün** – 39 von 39 relevanten Tests erfolgreich.

## Verwandte Funktionen

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
- [F017 – Backfill-Fehlerbenachrichtigung](../../../Docs/business/features/F017-backfill-fehlerbenachrichtigung.md)
- [F007 – Wertpapierpreise (Infrastructure-Perspektive)](./F007-wertpapierpreise-infrastructure.md)
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md)
