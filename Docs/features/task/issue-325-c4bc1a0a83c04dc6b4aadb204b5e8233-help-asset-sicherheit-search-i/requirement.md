### Fachliche Zusammenfassung

Die Help-Asset-Sicherheit muss gewährleisten, dass `search-index.json` für jede unterstützte Sprache als statisches Help-Asset vorhanden und im Manifest `help-assets.sha256` enthalten ist. Manipulationen an diesem Asset müssen über `HelpSecurityMiddleware` erkannt und mit `NotFound` beantwortet werden. Der aktuelle Fallback von `HelpController.GetSearchIndex`, der bei fehlender statischer Datei einen Index dynamisch erzeugt und `OK` liefert, darf die Manifest-/Hash-Prüfung nicht umgehen.

Die Build- und Testausführung muss die generierten Help-Assets sowie das verwendete Content-Root konsistent verwenden.

### Betroffene Klassen und Komponenten

- `HelpController`, insbesondere `GetSearchIndex` und `GenerateSearchIndex`
- `HelpSecurityMiddleware`
- Generator bzw. Build-Target für `search-index.json` und `help-assets.sha256`
- `TestWebApplicationFactory` und deren Content-Root-Konfiguration
- Help-Assets unter `FinanceManager.Web/wwwroot/help/{language}/`
- `help-assets.sha256` als Manifest der geschützten Assets
- Integrationstests, insbesondere `HelpSecurityMiddlewareTests.HelpAssetHttpRequest_IsBlockedWhenManifestedFileIsManipulated`
- Bestehende Help-Tests wie `HelpControllerSecurityTests` und `HelpAssetIntegrityValidatorTests`

### Implementierungsansatz

Zunächst wird der Build- und Testpfad für Help-Assets nachvollzogen und so korrigiert, dass `search-index.json` für alle unterstützten Sprachen vor Test- bzw. App-Start erzeugt und zusammen mit den übrigen Assets in `help-assets.sha256` aufgenommen wird. Die Middleware muss dabei dieselbe physische Datei prüfen, die der Controller verwendet.

Zusätzlich wird geprüft, ob `TestWebApplicationFactory` den Content-Root des Webprojekts verwendet, damit Manipulationen am Suchindex am tatsächlich geladenen Asset wirksam werden. `GetSearchIndex` muss bei fehlendem oder nicht freigegebenem statischem Suchindex entweder `NotFound` liefern oder den Zugriff so an die bestehende Integritätsprüfung anbinden, dass kein ungeschützter `OK`-Fallback entsteht. Die konkrete Variante ist anhand der bestehenden Help-Asset-Verträge und Tests festzulegen.

Die Regression wird durch Tests für vorhandene, manipulierte und fehlende `search-index.json`-Dateien in mindestens den unterstützten Sprachen abgesichert.

### Konfiguration

Es ist keine neue Laufzeitkonfiguration vorgesehen. Die Liste der unterstützten Sprachen und die Erzeugung der Help-Assets bleiben Bestandteil der bestehenden Build-/Content-Pipeline; falls bereits eine zentrale Sprachkonfiguration existiert, muss sie für die Manifestgenerierung wiederverwendet werden.

### Offene Fragen

- Soll bei fehlendem `search-index.json` ausschließlich `NotFound` geliefert werden, oder soll der Build die Datei zwingend erzeugen und der Controller-Fallback vollständig entfallen?
- Ist der Content-Root-Fehler nur testbezogen, oder muss die Produktionskonfiguration ebenfalls angepasst werden?
- Welche Sprachen gelten verbindlich als unterstützt und müssen daher im Build-Target und im Manifest berücksichtigt werden?
