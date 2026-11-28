# Usability-Review – Schritt 1: Back-Navigation

**Status:** Keine Befunde

Alle ursprünglich gemeldeten (niedrigschwelligen) Befunde wurden in derselben Iteration behoben:

- [x] ~~Inkonsistente Rückkehrkette bei Budget: „Budgetkategorie"-Aktion der Zweckkarte brach die Rückkehrkette (Kategoriekarte → Zurück landete auf der Kategorien-Übersicht)~~ — behoben: Die Aktion navigiert jetzt über den `OpenCategory`-Kanal; die Kategoriekarte erhält die Zweckkarte als `?back=`-Ziel (`BudgetPurposeCardViewModel`, `CardPage.razor` `case "OpenCategory"`).
- [x] ~~„Budgetkategorie"-Aktion verhielt sich bei gesetztem `?back=`-Kontext wie „Zurück" (Label-Versprechen gebrochen)~~ — behoben: Die Aktion läuft nicht mehr über den `Back`-Kanal und wird daher von der `?back=`-Präzedenz nicht mehr verschluckt.
- [x] ~~Fallback-Route `list/budget/rules` existiert nicht (kontextlose Regelkarte → endlos „Laden …")~~ — behoben: Regeln ohne Purpose-/Kategorie-Kontext fallen jetzt auf `list/budget/purposes` zurück (`BudgetRuleCardViewModel.LoadAsync`).

## Begründung aus Anwendersicht

Das Kernproblem ist gelöst: Der Zurück-Button der Budget-Regelkarte sowie aller vier MCP-Ansichten führt auf echte Zielseiten (`card/budget/purposes/{id}`, `card/budget/categories/{id}`, `card/setup`) statt auf ungültige Routen mit endlosem Ladezustand. Die Ketten Sparplankarte → „Kategorie" → „Zurück" und Zweckkarte → „Budgetkategorie" → „Zurück" kehren nachvollziehbar zur Ausgangskarte zurück, inklusive Weitergabe bestehender Rücksprungkontexte. Technische Kennungen werden der Anwenderin nicht in der Oberfläche ausgesetzt — GUIDs erscheinen nur in der Adresszeile, was dem bisherigen App-Verhalten entspricht.
