# Tests und Abdeckung

## Vorhandene relevante Tests

- `FinanceManager.Tests/Controllers/UserImportSplitSettingsControllerTests.cs:71-82` prueft neue User-/API-Defaults, darunter `OnMissingInformation`.
- `FinanceManager.Tests/Controllers/UserImportSplitSettingsControllerTests.cs:85-103` und `:125-143` pruefen das explizite Persistieren von `AlwaysConfirm`.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs:163-172` prueft den Default ueber den integrierten API-Pfad.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs:176-194` prueft Update und anschliessendes Lesen von `AlwaysConfirm`.
- `FinanceManager.Tests/Statements/MassImportOrchestratorTests.cs:124-147` prueft die fachliche Wirkung von `AlwaysConfirm` im Orchestrator.

Die vorhandenen Tests verwenden ueberwiegend InMemory- oder integrierte SQLite-Kontexte und validieren Controller/API-Verhalten. Sie zeigen, dass ein bereits gesetzter Wert `AlwaysConfirm` erhalten bleibt.

## Fehlende Abdeckung

Es gibt keinen gezielten Test fuer die EF-Modellmetadaten, insbesondere fuer Sentinel, `ValueGeneratedOnAdd` und den Modellvalidierungs-Warning. Ebenfalls fehlt ein relationaler Insert-Test, der einen neuen `User` mit explizitem `AlwaysConfirm` speichert und nach dem Reload verifiziert. Die Konstruktorpfade setzen den fachlichen Default indirekt teilweise nur ueber den Property-Initializer; dafuer existiert kein eigener Test je Konstruktor.

## Testbedarf fuer die Umsetzung

Die Korrektur sollte mindestens einen EF-Modelltest gegen `AppDbContext` und einen SQLite-Insert-/Reload-Test fuer `AlwaysConfirm` ergaenzen. Ein Regressionstest fuer den Standard `OnMissingInformation` sollte erhalten bleiben. Fuer bestehende Daten ist ein Test sinnvoll, der sowohl gespeichertes `0` als auch `1` unveraendert ueber eine Modellanpassung liest.

