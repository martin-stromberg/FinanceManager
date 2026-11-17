# Umsetzungsplan: Nachbesserungen Sparplan↔Konto-Verknüpfung

## Übersicht

Vier Pakete: (1) Info-Button + Savings-only-Auswahl im Verknüpfungs-Panel, (2) Symbolbuttons/Saldospalte/Ein-Link-Darstellung + „Eigenanteil" in Kopfdaten und Übersicht, (3) Bauspar-Demodaten mit Zinsen/Budget/Report-Favorit, (4) Report-Quellenoption `SavingsPlanPostingSource` (Planposten vs. verknüpfte Bankkontoposten).

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Verknüpfung Sparplan↔Bankkonto (Darstellung in Karte, Liste, Report, Demodaten) |
| Auslösende Ereignisse | Nutzerinteraktion (Info, Auswahl, Entfernen, Kontoaufruf, Reporteinstellung), Demodaten-Lauf |
| Erwartete Wirkung | Erklärtext, Savings-only-Auswahl (UI+Server), Symbolbuttons, Saldospalte ≥2 Links, „Eigenanteil"-Feld bei Links, Bauspar-Demodatensatz, Report-Quellenwahl |
| Umgang mit fehlenden Objekten | Ohne Links unverändert; Pläne ohne Links im Report weiterhin Planposten; Demodaten nur bei Neuinitialisierung |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Bei genau einer Verknüpfung zeigt die Zeile nur den Konto-Aufruf-Button (kein Entfernen, keine Saldospalte) | Wörtliche Rückmeldung („nur der Symbolbutton zum Bankkonto"); Saldo steht bereits im Kopf | bestätigt (wörtliche Anforderung aus continue.md, 2026-11-08) |
| 2 | Keine zusätzliche „Eigenanteil"-Spalte in der Übersichtsliste | „Dieselbe Saldoregel" betrifft den ausgewiesenen Saldo, nicht alle Kopffelder | bestätigt (Ableitung, 2026-11-08) |
| 3 | Home-KPIs/MCP-Default ohne explizite Wahl nutzen `PlanPostings` | Option nur bei ausdrücklicher Wahl wirksam | bestätigt (Ableitung, 2026-11-08) |

## Designentscheidungen

| Komponente | Gewählter Ansatz | Begründung |
|------------|------------------|------------|
| Report-Quelle | Neuer Enum `SavingsPlanPostingSource { PlanPostings, LinkedAccountPostings }` (persistiert als int auf `ReportFavorite`) | Drückt die Zwei-Wege-Wahl aus, erweiterbar, konsistent zu `PostingKind`-Enums in Shared Dtos |
| Umattribution im Report | In `QueryAsync` werden `SavingsPlan`-Rohzeilen verknüpfter Pläne durch `Bank`-Aggregatzeilen der verknüpften Konten ersetzt (gleiche `sourcePeriod`/`DateKind`) | Wiederverwendung des Aggregat-Flusses, keine neue Query-Pipeline |
| Kontoaufruf | Symbolbutton → `Vm.OpenLinkedAccount(id)` → `RaiseUiActionRequested("OpenAccountDetails", id)` | Vorhandener Handler in `CardPage.razor` (Z. 237) navigieren zu `/card/accounts/{id}?back=…` |
| Eigenanteil | `SavingsPlanDto.OwnAmount` (nullable), nur bei Links befüllt (Summe Planposten bis heute) | Kartenfeld nur bei Vorhandensein; ohne Links unverändert |

## Programmabläufe

### Verknüpfung anlegen (Savings-only)

1. `LoadLinkedAccountsAsync` filtert `LinkableAccounts` auf `AccountType.Savings`.
2. `AddLinkAsync` (Service) lehnt `account.Type != Savings` mit `ArgumentException` ab → Controller 400.

### Saldo + Eigenanteil in Kopfdaten

1. `GetAsync` lädt Links; bei Links `OwnAmount` = Summe `PostingKind.SavingsPlan`-Posten bis heute.
2. `BuildCardRecordAsync` ergänzt Feld `Card_Caption_SavingsPlan_OwnAmount` nur wenn `dto.OwnAmount.HasValue`.

### Report mit Quelle „verknüpfte Bankkontoposten"

1. Nutzer wählt `PostingKind.SavingsPlan` → Option sichtbar → `LinkedAccountPostings` wählen → `ReportAggregatesQueryRequest` → `ReportAggregationQuery`.
2. `QueryAsync`: Links des Owners laden; verknüpfte Konten → `Bank`-Aggregatzeilen (gleiche Periode/DateKind) auf Plan-Ids umattribuieren; `SavingsPlan`-Zeilen verknüpfter Pläne ersetzen; unverknüpfte Pläne behalten Planposten.
3. Favorit persistiert die Quelle (`ReportFavorite.SavingsPlanPostingSource`).

### Demodaten

1. `CreateDemoDataSetAsync`: Kontakt „Musterbausparkasse" (Banken), Konto „Bausparkonto" (`Savings`), Plan „Bausparen" (`Recurring`, `Monthly`, Zielbetrag/Datum, Kategorie Anlage, Vertragsnummer), Link via `ISavingsPlanAccountLinkService`, Budgetzweck „Bausparen" (`BudgetSourceType.SavingsPlan`) + Monatsregel.
2. `CreateMonthlyPostingPlanAsync`: monatlich Giro −X (self, planId → informeller Planposten) + Bausparkonto +X; laufende Salden für Giro/Bausparkonto mitführen; jedes Dezember (gebuchte Monate) Zinsgutschrift 1 % auf beide Salden (Bankkontakt, letzter Geschäftstag).
3. `EnsureDefaultReportFavoritesAsync`: Favorit „Sparplanbuchungen (monatlich)" `PostingKind.SavingsPlan`, `Month`, Filter auf „Bausparen", `SavingsPlanPostingSource.LinkedAccountPostings`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `SavingsPlanPostingSource` | Enum (Shared.Dtos.Reports) | Postenquelle für Sparpläne in Reports |

## Änderungen an bestehenden Klassen

### `SavingsPlanLinkedAccountsPanel.razor`
- Info-Button (`#info`) im Header → aufklappbarer Erklärtext (4 Aufzählungen, lokalisiert).
- Saldospalte (`account.CurrentBalance`) nur bei >1 Link; Symbolbutton Kontoaufruf (`#external`) immer; Symbolbutton Entfernen (`#unlink`) nur bei >1 Link.

### `SavingsPlanCardViewModel`
- `LinkableAccounts` Filter `Type == AccountType.Savings`; neue Methode `OpenLinkedAccount(Guid)`; `Card_Caption_SavingsPlan_OwnAmount`-Feld bei Links.

### `SavingsPlanAccountLinkService`
- `AddLinkAsync`: `ArgumentException` bei `account.Type != AccountType.Savings`.

### `SavingsPlanService.GetAsync` / `SavingsPlanDto`
- `GetAsync`: `OwnAmount` befüllen (nur bei Links); `SavingsPlanDto`: Property + optionaler Ctor-Param `ownAmount`.

### `ReportFavorite` + `ReportFavoriteService` + Report-DTOs
- `ReportFavorite`: Property `SavingsPlanPostingSource`, `Update(...)`+Param, `ReportFavoriteBackupDto`+Feld, `ToBackupDto`/`AssignBackupDto`.
- `ReportFavoriteService`: Durchreichen bei List/Get/Create/Update.
- `ReportFavoriteDto`, `ReportFavoriteCreateRequest`, `ReportFavoriteUpdateRequest`, `ReportFavoriteCreateApiRequest`, `ReportFavoriteUpdateApiRequest`, `ReportAggregatesQueryRequest`, `ReportAggregationQuery`: optionaler Parameter/Property `SavingsPlanPostingSource` (Default `PlanPostings`).

### `ReportAggregationService`
- Substitution der SavingsPlan-Rohzeilen für verknüpfte Pläne bei `LinkedAccountPostings` (nach `rawAnon`-Aufbau).

### `ReportsController`, `FmReportingAggregateTool`, `FmReportsConfigurationManageTool`
- Option aus Request/Favorit in die Query reichen; MCP-Schema/Argument `savingsPlanPostingSource`.

### `ReportDashboard.razor` + `ReportDashboardViewModel`
- VM: Property `SavingsPlanPostingSource`; `LoadAsync`/`SaveFavoriteAsync`/`UpdateFavoriteAsync`/`SubmitFavoriteDialogAsync` optionaler Parameter.
- Page: Select „Postenquelle" (`plan`/`linked`) sichtbar wenn `SavingsPlan` in `_selectedKinds`; `ApplyFavorite` setzt Wert; Submit reicht ihn weiter.

### `DemoDataService`
- `ISavingsPlanAccountLinkService` injizieren; Bauspar-Objekte, monatliche Buchungen, Zinsen, Budget, Report-Favorit (wie oben).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung |
|----------------|----------------------------|--------------|
| `AddReportFavoriteSavingsPlanPostingSource` | `ReportFavorites.SavingsPlanPostingSource` (int, default 0) | Neue Report-Option; per `dotnet ef` generiert |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Link-Konto | `Type == Savings` | `ArgumentException` → HTTP 400 |

## Seiteneffekte und Risiken

- **Altdaten-Links auf Nicht-Spar-Konten:** Bestehende Links bleiben bestehen; nur neue Links werden eingeschränkt.
- **Mehrfachzuordnung:** Ein Konto für mehrere Pläne zählt seine Bankposten je Plan (konsistent zur Saldo-Additivität).
- **Backup:** `ReportFavoriteBackupDto`-Erweiterung erfordert Mitpflege von `AssignBackupDto`; alte Backups ohne Feld deserialisieren mit Default.

## Umsetzungsreihenfolge

1. Enum + `ReportFavorite`/`Update`/Backup-DTO (Voraussetzung: keine)
2. Migration `dotnet ef` (Voraussetzung: 1)
3. DTOs + `ReportFavoriteService` + `ReportsController` + `ReportAggregationService` (Voraussetzung: 1)
4. `ReportDashboardViewModel` + `ReportDashboard.razor` + ResX (Voraussetzung: 3)
5. MCP-Tools (Voraussetzung: 3)
6. `SavingsPlanAccountLinkService` + VM-Filter + Panel + `SavingsPlanDto.OwnAmount` + `GetAsync` + Kartenfeld + ResX (Voraussetzung: keine)
7. `DemoDataService` (Voraussetzung: 1–3)
8. Tests (Voraussetzung: alle vorherigen)

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Prüfung |
|--------------------|------------|---------|
| `AddLinkAsync_GiroAccount_Throws` | `SavingsPlanAccountLinkServiceTests_SavingsOnly` | Ablehnung Nicht-Spar-Konto |
| `LoadLinkedAccountsAsync_NonSavingsAccount_NotLinkable` | `SavingsPlanCardLinkTests` | UI-Filter |
| `GetAsync_LinkedPlan_OwnAmountIsPlanPostingSum` / `GetAsync_UnlinkedPlan_OwnAmountNull` | `SavingsPlanServiceTests` (Erweiterung, Thema Saldo) | Eigenanteil |
| `QueryAsync_LinkedSource_UsesLinkedBankAggregates` / `QueryAsync_LinkedSource_UnlinkedPlanKeepsPlanPostings` | `ReportAggregationServiceSavingsPlanSourceTests` | Quellenoption |
| Favorit-Persistenz der Option | `ReportFavoriteServiceTests` (Erweiterung) | Create+Get Roundtrip |
| `CreateDemoData_*_CreatesBausparSet` | `ApiClientDemoDataTests` (Erweiterung) | Konto, Plan, Link, Favorit, Buchungen, Zinsen, Budget |

### Betroffene bestehende Tests

| Test / Testklasse | Grund |
|-------------------|-------|
| `ReportFavorite`-Aufrufer der `Update`-Signatur | Signaturerweiterung |
| Backup-Tests `ReportFavorite` | BackupDto-Feld |

### E2E-Tests (primärer Funktionsnachweis)

Das Projekt nutzt für UI-Flüsse keine browserbasierten E2E-Tests für diese Panels (bestehende `FinanceManager.Tests.E2E` decken Budget-Reports über API ab); der Feature-Branch wurde bisher über ViewModel-, Service- und API-Integrationstests abgesichert. Gleiche Abdeckungsstrategie: Konto-Aufruf/Panel-Verhalten auf VM-Ebene, Rest-Integration über `ApiClientDemoDataTests`. Als reale Abnahme wird der Demodaten-Fluss über die öffentliche API geprüft (erzeugt Buchungen/Links/Report). Begründung: kein UI-Automation-Setup für Blazor-Karten im Repo vorhanden.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Verknüpfung | Nutzer öffnet Sparplankarte | Plan existiert, Sparkonto+Giro vorhanden | Info-Button → Erklärtext; Dropdown zeigt nur Sparkonten; Link anlegen | Erklärung sichtbar; Giro nicht wählbar; Zeile mit Aufruf-Button | Unit/VM-Tests + Integr.-Test |
| 2 | Mehrere Links | ≥2 Links vorhanden | Saldospalte + Entfernen-Button pro Zeile | Salden sichtbar, Entfernen symbolisch | VM-Test/Markup |
| 3 | Eigenanteil | Plan mit Link + Planposten | Kopfdaten: Saldo=Kontosumme, Eigenanteil=Postensumme | Service-Test |
| 4 | Report-Quelle | Report-Einstellung | Plan mit Link + Bankposten | Quelle „verknüpfte Bankkontoposten" wählen → Aggregation aus Bankposten | Aggregations-Test |
| 5 | Demodaten | Demodaten erzeugen | neuer Nutzer | Bausparkonto/Plan/Link/Buchungen/Zinsen/Budget/Favorit vorhanden | `ApiClientDemoDataTests` |

## Offene Punkte

Keine.
