# Usability-Review

**Status:** Keine Befunde

- Info-Button: sichtbares `#info`-Symbol im Panel-Header, `title`-Text lokalisiert, `aria-expanded` gesetzt; Erklärtext deckt alle vier geforderten Aspekte ab (Saldo-Quelle, Inflow-Basis, informelle Belegung, Fallback ohne Links).
- Kontoauswahl: Dropdown zeigt nur Sparkonten (Name + IBAN als Plain-Text-Auswahl); Nicht-Spar-Konten sind weder wählbar noch serverseitig linkbar (400).
- Symbolbuttons: Entfernen (`#unlink`) und Konto-Aufruf (`#external`) mit lokalisiertem `title`, `icon-btn small`-Konvention wie im restlichen UI; Klick-Verhalten: OpenAccountDetails-Navigation mit Return-Url bereits in `CardPage.razor` verdrahtet.
- Saldospalte erscheint nur bei >1 Link; bei genau einem Link nur der Öffnen-Button (wörtliche Anforderung).
- Reporteinstellung „Sparplan-Postenquelle" erscheint nur, wenn `SavingsPlan` gewählt ist; Wechsel löst Reload aus; Wert wird in Favoriten persistiert und beim Laden wiederhergestellt.
- Jede `RaiseUiActionRequested`-Aktion hat einen Handler: `OpenAccountDetails` ist in `CardPage.razor` (Zeile ~237) implementiert.

Manuelle E2E-Abnahme: im Rahmen der API-Integrationstests (Demodaten-Fluss) abgesichert; kein Blazor-E2E-Setup im Repo — siehe plan.md (Begründung).
