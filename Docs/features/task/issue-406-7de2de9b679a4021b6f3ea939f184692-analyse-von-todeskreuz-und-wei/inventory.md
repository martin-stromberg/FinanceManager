# Bestandsaufnahme: Analyse von Todeskreuz und weiteren Umkehrsignalen

Analysiert wurde der Bereich Wertpapier-Analyse (Golden Cross und Umgebung), bezogen auf die Anforderung aus `requirement.md` (Issue #406).

## Zusammenfassung

- Eine vollständig ausgebaute **Golden-Cross-Analyse** existiert bereits als Referenzarchitektur: reiner Rechenkern `GoldenCrossAnalyzer` (Application), DB-gebundener Service `GoldenCrossService` (Infrastructure), DTOs + `IApiClient`-Methode (Shared), REST-Endpunkt `SecuritiesController.GetGoldenCrossAsync`, UI-Widget `GoldenCrossWidget.razor` auf `CardPage.razor`, Lokalisierung über resx (de/en), Home-Page-Benachrichtigungen via `INotificationWriter` + `GoldenCrossNotifiedPhase` auf `Security`.
- **Keine** Bestandteile für Todeskreuz/Umkehrsignale vorhanden. Es gibt keinerlei MACD-, RSI- oder EMA-Berechnung im gesamten Codebestand.
- Swing-High/Low-Erkennung existiert als private Logik in `GoldenCrossAnalyzer.AnalyzeTrend` (`SwingLookaround = 3`) — nicht wiederverwendbar gekapselt, aber als Muster übertragbar.
- `SecurityPrice` liefert die Datenbasis (`SecurityId`, `Date`, `Close`); der GoldenCross-Service lädt `LongWindow * 3` Kurszeilen.
- `Security` besitzt `GoldenCrossNotifiedPhase` (persistierte Notification-Schwelle) — vergleichbares Feld für Umkehrsignale existiert nicht.
- Benachrichtigungs-Einstellung `User.GoldenCrossNotificationsEnabled` existiert; eine Entsprechung für Umkehrsignale fehlt.
- Dokumentation: `Docs/help/wertpapiermanagement/goldenes-kreuz.md` beschreibt das bestehende Feature aus Anwendersicht.

Test-Ausgangszustand: Unit-Tests 1397/1397 bestanden, Integrationstests 134/134 bestanden; E2E-Basislauf siehe [tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [UI](inventory/ui.md)
- [Tests](inventory/tests.md)
