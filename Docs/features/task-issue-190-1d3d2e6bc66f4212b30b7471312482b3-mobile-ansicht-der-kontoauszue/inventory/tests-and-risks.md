# Test- und Risiko-Inventar

## Vorhandene Tests

Relevante vorhandene Tests:

- `FinanceManager.Tests/ViewModels/StatementDraftsViewModelTests.cs` testet die Draft-Listen-ViewModels.
- `FinanceManager.Tests/Components/GenericListPageTests_MobileFilters.cs` testet generische Mobile-Filterdarstellung.
- `FinanceManager.Tests.E2E/Helpers/ListPageGateway.cs` kennt `.generic-list-mobile-card`.
- Import- und Navigation-E2E-Tests nutzen mobile Viewports bzw. mobile Selektoren fuer generische Listen.

Es gibt aktuell keinen spezifischen Test, der die mobile Darstellung von Kontoauszugseintraegen mit Kontakt/Sparplan/Wertpapier absichert.

## Empfohlene Tests

### ViewModel-Tests

Ergaenzen in oder neben `StatementDraftsViewModelTests.cs`:

- AlreadyBooked-Eintrag erzeugt Records mit `Muted = true`.
- Sparplanname erscheint in einem Record, wenn `SavingsPlanNames` fuer Entry vorhanden ist.
- Wertpapiername wird mit lokalisierter Buchungsart formatiert.
- Kontaktanzeige bevorzugt Kontaktname, wenn Kontakt nicht Bankkontakt und nicht Self ist.
- Empfaenger erscheint nur, wenn kein anzuzeigender Kontakt vorhanden ist.

Falls `StatementDraftEntriesListViewModel` intern bleibt, kann entweder ueber `StatementDraftCardViewModel.EmbeddedList` getestet oder die Sichtbarkeit gezielt angepasst werden.

### Component-/bUnit-Tests

Fuer `GenericListPage.razor` oder eine neue spezifische mobile Darstellung:

- Mobile Card enthaelt Datum und Betrag in einer gemeinsamen zweispaltigen Zeile.
- Lange Textwerte brechen innerhalb der mobilen Karte um.
- `muted-row` ist auf mobilen Karten sichtbar per Klasse vorhanden.

CSS-Eigenschaften selbst sind in bUnit nur begrenzt pruefbar. Wichtig ist, stabile Klassen/Struktur zu testen.

### E2E-/Playwright-Tests

Optional, aber fuer die Scroll-Anforderung sinnvoll:

- Mobile Viewport setzen, Kontoauszug mit sehr langem Dateinamen und Eintrag mit langen Texten oeffnen.
- Pruefen, dass `document.documentElement.scrollWidth <= window.innerWidth`.
- Pruefen, dass mobile Karte Sparplan und Wertpapier inklusive Buchungsart enthaelt.

## Risiken

- Eine generische Aenderung an `GenericListPage` kann alle Listen auf Mobile veraendern. Das ist riskant, weil viele Seiten dieselbe Komponente nutzen.
- Eine DTO-Erweiterung betrifft Shared API Client und Controller. Sie ist rueckwaertskompatibel, wenn optionale Record-Parameter am Ende ergaenzt werden, muss aber in allen Konstruktoraufrufen beachtet werden.
- Kontaktfilterung kann falsch werden, wenn Self-Kontakt nicht vorhanden ist. Bestehende Auth-/Setup-Logik legt Self-Kontakte an, trotzdem sollte die Anzeige robust gegen `null` sein.
- Lokalisierung der Buchungsart darf nicht per `ToString()` enden, wenn die UI Deutsch anzeigen soll.
