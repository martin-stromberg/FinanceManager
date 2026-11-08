# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium (aus continue.md) | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------------------------|-------------------|----------------------|--------|
| Info-Button mit Zweck-Erklärung (Saldo/Inflow/Informal/Fallback) | Panel-Header + aufklappbarer Text, ResX | VM-/Markup-Prüfung | Abgedeckt |
| Nur `Savings`-Konten wählbar + Tests | VM-Filter + Service-Ablehnung | `SavingsPlanAccountLinkServiceTests_SavingsOnly`, `SavingsPlanCardLinkTests` | Abgedeckt |
| Entfernen- + Konto-Aufruf-Symbolbuttons | Panel-Markup + `OpenLinkedAccount`/`OpenAccountDetails` | VM-Test | Abgedeckt |
| Saldospalte bei >1 Link; bei 1 Link nur Konto-Aufruf | Panel-Bedingung | Markup/VM-Test | Abgedeckt |
| Kopfdaten: Saldo=Kontosumme + „Eigenanteil"=Postensumme; ohne Links unverändert | `OwnAmount` in `GetAsync`, Kartenfeld | `SavingsPlanServiceTests`-Erweiterung | Abgedeckt |
| Übersicht gleiche Saldoregel | via `AnalyzeAsync` bereits gegeben | Regression durch List-Nutzung der Analyse (Bestand) | Abgedeckt |
| Demodaten Bauspar (Konto, Plan, Link, Buchungen, 1 %-Zinsen beidseitig, Budget, Report) | `DemoDataService`-Erweiterung | `ApiClientDemoDataTests`-Erweiterung | Abgedeckt |
| Report-Option Postenquelle + Demo-Report | Enum + Persistenz + Aggregation + UI + Favorit | `ReportAggregationServiceSavingsPlanSourceTests`, `ReportFavoriteServiceTests` | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss | Geplanter E2E-Test | Status |
|---------------|--------------------|--------|
| Verknüpfung/Panel-Interaktion | VM-Tests + API-Integration (kein Blazor-E2E-Setup im Repo — begründet im Plan) | Nicht erforderlich mit Begründung (Abdeckung über VM/Integration) |
| Demodaten/Report | `ApiClientDemoDataTests` über öffentliche API | Abgedeckt |

## Hinweise

- Scope-Ausschluss 1 (Ein-Link-Darstellung) ist bewusste, wörtlich geforderte Einschränkung.
- Migration ausschließlich per `dotnet ef` erzeugen.
