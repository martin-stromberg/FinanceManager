# F007 – Wertpapierpreise

## Einleitung

Diese Funktion aktualisiert Ihre Wertpapierkurse automatisch.  
Wenn der Kursabruf scheitert, sehen Sie einen klaren Hinweis statt unverständlicher Fehltexte.  
So erkennen Sie schnell, ob Sie warten oder etwas korrigieren müssen.  
Ihre letzte bekannte Kurslage bleibt dabei erhalten.

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

## Beispiel

Sie aktualisieren Kurse für drei Wertpapiere.  
Beim ersten Wertpapier ist das Symbol falsch hinterlegt.  
Sie erhalten einen klaren Hinweis mit **Symbol prüfen**.  
Beim zweiten Wertpapier gibt es kurzzeitig ein Verbindungsproblem.  
Der Lauf macht trotzdem weiter und aktualisiert das dritte Wertpapier erfolgreich.

## Was passiert im Hintergrund?

Die Anwendung trennt Fehler in zwei Gruppen.  
Kurzfristige Probleme beim Datenanbieter oder bei der Verbindung gelten als vorübergehend.  
Dauerhafte Probleme wie ein falsches Symbol gelten als zu korrigieren.  
Bei vorübergehenden Problemen läuft der Abruf für andere Wertpapiere weiter.  
Wenn das Tageslimit des Anbieters erreicht ist, stoppt der laufende Durchgang sofort.

Für die Diagnose speichert die Anwendung: Fehlerklasse, sichere Nutzernachricht, Zeitpunkt und bereinigte Anbieterdetails.  
Rohe Anbietertexte werden nie direkt an Nutzer gezeigt.  
So bleiben Hinweise verständlich und enthalten keine irreführenden Rohdaten.

Technische Nachweise:
- [FinanceManager.Web/Services/SecurityPriceWorker.cs](../../../FinanceManager.Web/Services/SecurityPriceWorker.cs)
- [FinanceManager.Web/Services/AlphaVantage.cs](../../../FinanceManager.Web/Services/AlphaVantage.cs)
- [FinanceManager.Web/Services/PriceProviderErrorClass.cs](../../../FinanceManager.Web/Services/PriceProviderErrorClass.cs)
- [FinanceManager.Domain/Securities/Security.cs](../../../FinanceManager.Domain/Securities/Security.cs)
- [FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs](../../../FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs)
- [FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs](../../../FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs)
- [FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs](../../../FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs)

## Häufige Fragen (FAQ)

**F: Was sehe ich bei einem Fehler?**  
A: Sie sehen einen klaren Hinweis wie **Kursabruf fehlgeschlagen** mit kurzer Handlungsanweisung.

**F: Warum sehe ich nicht den Originaltext des Anbieters?**  
A: Originaltexte sind oft unklar. Die Anwendung zeigt deshalb kurze, sichere Hinweise.

**F: Woran erkenne ich ein dauerhaftes Problem?**  
A: Hinweise wie **Symbol prüfen** zeigen, dass eine Eingabe angepasst werden muss.

**F: Woran erkenne ich ein vorübergehendes Problem?**  
A: Hinweise zum späteren Neustart zeigen ein kurzfristiges Problem bei Anbieter oder Verbindung.

**F: Was passiert beim Erreichen des Tageslimits?**  
A: Der laufende Abruf stoppt sofort. Sie starten später einen neuen Lauf.

## Verwandte Funktionen

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
- [F007 – Wertpapierpreise (Infrastructure-Perspektive)](./F007-wertpapierpreise-infrastructure.md)
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md)
