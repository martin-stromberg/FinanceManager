# UI, Routing und bestehende Listenlogik

## Einstiegspunkt

| Datei | Befund |
|---|---|
| `FinanceManager.Web/Components/Pages/ListPage.razor` | Generische Route `@page "/list/{Kind}/{SubKind?}/{Id?}"`; rendert Ribbon, Titel und `GenericListPage`. |
| `FinanceManager.Web/ViewModels/ListViewModelFactory.cs` | Mappt `Kind == "accounts"` auf `BankAccountListViewModel`. |
| `FinanceManager.Web/Components/Pages/GenericListPage.razor` | Rendert Suche, Tabelle, mobile Karten, Lade-/Leerzustände und Infinite-Scroll-Sentinel. |
| `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs` | Lädt Accounts in Seiten zu 50, baut die Tabellenspalten und stellt Ribbon-Aktionen bereit. |
| `FinanceManager.Web/ViewModels/Accounts/AccountListItem.cs` | Tabellenmodell; enthält ID, Name, Typ, IBAN, `CurrentBalance` und Navigation zum Account-Card-View. |
| `FinanceManager.Web/Components/Pages/GenericCardPage.razor` | Bestehender generischer Detailfluss, für die Infokachel voraussichtlich unverändert. |

## Relevante UI-Konventionen

- Die Liste ist interaktiv serverseitig und aktualisiert sich über `StateChanged` des Providers.
- Tabellenzeilen und mobile Karten verwenden denselben `Records`-Datenbestand.
- Geldbeträge werden in der Tabelle als Currency mit der aktuellen Kultur formatiert.
- Das UI nutzt CSS-Variablen wie `--border`, `--muted` und `--chart-*`; eine neue Kachel sollte diese vorhandenen Theme-Konventionen übernehmen.
- Die Anzeige muss neben der Tabelle funktionieren und darf Suche, Paging, Zeilenklicks sowie Ribbon-Aktionen nicht blockieren.

## Voraussichtliche Erweiterungspunkte

- Kontenspezifische Statistikdaten am Provider oder über ein separates `BankAccountStatisticsViewModel`.
- `ListPage.razor` beziehungsweise eine kontenspezifische Komposition, damit die Statistik nur bei `Kind == "accounts"` erscheint.
- Responsive Layout-CSS für Tabelle und Statistikbereich.
- Neue deutsche und englische Ressourcen in `FinanceManager.Web/Resources/Components/Pages/Accounts.*.resx` und/oder `Pages.*.resx`.
