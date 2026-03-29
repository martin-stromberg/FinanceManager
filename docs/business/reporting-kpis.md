# Reporting & KPIs (Startseite Tiles)

Dieses Dokument beschreibt die Kachel‑basierten KPIs auf der Startseite, die Funktionsweise der Favoritenberichte und wie Kacheln mit aggregierten Werten befüllt werden.

Ziele
- Schnellzugriff auf relevante Auswertungen (Summen, Trends, Ziele)
- Konfigurierbare Favoriten (Reports) pro Nutzer
- Live‑/Near‑real‑time Aktualisierung mittels Cache / Background Jobs

Kachel‑Typen
- Single Metric: z. B. Kontostand, Monatsausgaben, Monatliche Sparrate
- Trend: Zeitreihen über letzten 12 Monate (Chart)
- Goal: Fortschritt gegenüber Ziel (z. B. Sparplan Ziel)

Favoritenberichte
- Nutzer kann Reports als Favorit speichern; Favoriten sind per Nutzer und konfigurierbar (Filter, Zeitraum, Accounts)
- Favoriten erscheinen als Kachel auf Startseite

Datenaktualisierung
- Aggregates für Perioden werden bei Posting‑Erstellung/Änderung aktualisiert (UpsertAggregates)
- Kachel‑Cache invalidiert bei relevanten Änderungen (Postings, BudgetRule Änderungen, SavingsPlan Änderungen)

API & Background
- `BudgetReportsController` liefert Report‑Daten
- `HomeKpisController` liefert Kachelkonfigurationen für Nutzer
- BackgroundJob / Cache Refresh Service aktualisiert Aggregate in Intervallen

UI Hinweise
- Kacheln sind responsive; Chart‑Kacheln erweitern beim Klick zur Detailseite
- Favoriten‑Konfiguration als Modal (Filter, Name, Icon)

Tests
- Unit: Aggregation correctness
- E2E: Kachel anzeigen → Filter anwenden → Drilldown zeigt korrekte Postings
