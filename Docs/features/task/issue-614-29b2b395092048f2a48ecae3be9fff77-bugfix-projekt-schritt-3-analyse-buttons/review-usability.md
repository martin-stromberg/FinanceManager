# Usability-Review — Schritt 3 (analyse-buttons, #603)

**Reviewer:** Review-Subagent (frischer Kontext, read-only)
**Ergebnis:** Mittlere Befunde behoben, niedrige behoben oder dokumentiert akzeptiert.

## Befunde und Auflösung

| # | Schwere | Befund | Auflösung |
|---|---|---|---|
| 2 | Mittel | Identische `«`/`»`-Icons für Monat/Jahr → im Icon-only-Modus (`hide-small-labels`) nicht unterscheidbar | **Behoben (Mitigation):** `Tooltip`-Parameter aller Nav-Aktionen befüllt (neue resx-Keys `Ribbon_Tip_*` de/en/neutral, in Übersicht + Bericht) — Hover/Title und aria-label klären die Ambiguität. Eigene Jahres-Symbole als Folge-Verbesserung denkbar |
| 3 | Mittel | LoadingBar lief in der Übersicht nicht über den Reload (Callback feuert nur Event; Bericht awaited `LoadAsync` direkt) | **Behoben:** `ListPage` injiziert `LoadingBarService` und wrapped `Reload`-/`ClearSearch`-Loads in `LoadingBar.RunAsync` — gilt für alle Listen gleichermaßen |
| 1 | Niedrig | `ThisYear` am Ende bricht die Spaltensymmetrie (Ribbon rendert 3 Items/Spalte) | **Behoben:** `ThisYear` auf Position 5 (zwischen `PrevYear` und `NextYear`) → beide Spalten lesen prev/this/next; die 5 gemeinsamen Buttons behalten relative Reihenfolge zum Bericht |
| 5 | Niedrig | `ThisMonth`/`ThisYear` setzen Range-Breite zurück (semantisch absoluter Sprung) | **Akzeptiert/dokumentiert:** „Aktuell"-Buttons sind absolute Sprünge — konsistent mit Bericht (`AsOfDate`-Anker) |
| 7 | Niedrig | `Tooltip`-Parameter ungenutzt; relative-vs-absolute Semantik unklar | **Behoben:** siehe #2 — Tooltips benennen die Verschiebe-Semantik explizit |
| 6 | Info | „Suche löschen" löscht auch den Zeitraum | **Akzeptiert:** pre-existing `ClearSearch`-Verhalten in `ListPage` (Such- UND Range-Reset), kein neuer Seiteneffekt |
| 9 | Info | Kein Reload-Guard bei schnellen Klicks | **Akzeptiert/dokumentiert:** siehe Code-Review #3 — pre-existing, letzter Load gewinnt |

## Positiv vermerkt

- Identische Id/Label/Icon/Size/Reihenfolge der 5 gemeinsamen Buttons zwischen Übersicht und Bericht
- Alle resx-Keys de/en/neutral vorhanden; `MobileShortcut` korrekt nur auf „Neu"
- Native Tastaturbedienbarkeit, `role="group"`/`toolbar` mit `aria-labelledby`
- Mobile: Labels immer sichtbar → Icon-Ambiguität dort nicht relevant; Touch-Targets ~44px
- Edge-Case-Behandlung in `ShiftRange` solide (Ankerableitung, Monatsende-Snap, Inversionsfreiheit, Überlauf-No-Op)

## Verifizierte Akzeptanz-Screenshots

- `s3-budget-overview-prev-month.png` — symmetrische Button-Spalten, Von/Bis = Oktober 2026
- `s3-budget-overview-prev-year.png` — Von/Bis = November 2025
- `s3-budget-overview-this-year.png` — Von/Bis = 01.01.–31.12.2026
- `s3-budget-report-analysis-buttons.png` — Bericht mit 5 Buttons, Periode 01.01.–31.12.2026 (Nächster-Monat-Shift)
