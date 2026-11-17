# Reale Abnahme: Posteneinsicht im Bericht (Issue #510)

Datum: 2025-Iteration (Lifecycle-Iteration zu `review.md`, Punkt „Reale Abnahme")

## Machbarkeit

Die Abnahme erfolgte **echt** — nicht simuliert: Die vorhandene Playwright-E2E-Infrastruktur
(`FinanceManager.Tests.E2E`) startet die gebaute `FinanceManager.Web.dll` als echten Prozess
gegen eine Wegwerf-SQLite-Datenbank auf einem freien Port und steuert einen realen Browser
(gebündeltes Chromium, `PLAYWRIGHT_BROWSER_CHANNEL`-Fallback in
`PlaywrightWebAppFixture.LaunchBrowserAsync`). Zugangsdaten sind nicht manuell nötig:
`TestUserSeeder.EnsureUserAsync` legt Benutzer direkt in der Test-DB an,
`AuthGateway.LoginAsync` meldet über die echte Anmeldung an, Seed-Daten werden über
`AppDbContext` auf derselben SQLite-Datei und `PostingAggregateService.RebuildForUserAsync`
erzeugt, Berichtsvarianten über die echte `POST /api/report-favorites`-API.

Umgesetzt als dauerhafter Nachweis: `FinanceManager.Tests.E2E/Tests/Reports/ReportPostingsOverlayPlaywrightTests.cs`
(3 Facts). Ausführung:

```
dotnet test FinanceManager.Tests.E2E --filter "FullyQualifiedName~ReportPostingsOverlayPlaywrightTests"
→ 3/3 bestanden (~15 s inkl. Server- und Browser-Start)
```

## Abgedeckte Abnahmeszenarien (alle per echtem Klick auf den Symbolbutton der Zelle)

| Szenario (plan.md Schritt 11) | Nachweis im Test | Ergebnis |
|---|---|---|
| Entitätszeile, Spalte Betrag | `PostingsOverlay_EntityRowColumnsAndTotalRow_ShowCellPostings`: `td.amt-current` der `Account:`-Zeile → 2 Posten (100 + 50), Summe 150 = Zellwert | bestanden |
| Spalte Vorperiode | `td.amt-prev` → 1 Posten (−20) aus dem Vormonat | bestanden |
| Spalte Vorjahr | `td.amt-year` → 1 Posten (30) aus dem Vorjahresmonat | bestanden |
| Total-Zeile (`GroupKey = null`) | `tr.total-row td.amt-current` → 2 Posten, Summe 150 | bestanden |
| Type-Zeile + LinkedAccountPostings | `PostingsOverlay_LinkedSavingsPlanSource_TypeAndTotalRows`: `Type:SavingsPlan` → verlinkter Plan liefert Bankposten des Vertragskontos (Herkunft = Kontoname „E2E Vertragskonto"), unverlinkter Plan eigenen Planposten (755 = 700 + 55) | bestanden |
| Expandierte Entitätszeile unter Type | Expandierter `SavingsPlan:`-Kindknoten → nur der Bankposten des verlinkten Kontos (700) | bestanden |
| Type:Bank unbeeinflusst von Linked-Quelle | `Type:Bank` → 2 Bankposten (742) | bestanden |
| Total in Multi-Kind + Linked | Total-Zeile → 4 Posten, Summe 1497 (Union Bank + abgeleitete Planposten, identisch zur Aggregatsumme) | bestanden |
| Dividenden-Netting | `PostingsOverlay_NettedDividendRow_ShowsGroupMembers`: `Security:`-Zeile unter `IncludeDividendRelated` → Overlay listet Dividende + Gebühr der Gruppe (90 = 100 − 10 = Netto-Zellwert); Buy-Posten taucht weder im Netting-Bericht noch im Overlay auf | bestanden |

Je Szenario wurde zusätzlich die Öffnen/Schließen-Mechanik (Overlay erscheint, X-Button
schließt) und die Fußzeilensumme geprüft (kulturunabhängig: Ziffernvergleich der
`tfoot`-Summe gegen den erwarteten Betrag).

## Nicht über E2E abgenommen (stattdessen Unit-Abdeckung)

- Filter-Variante (Text-/Kategorie-/Subtypfilter) — abgedeckt durch
  `ReportPostingsQueryServiceTests` (Kategoriefilter unter `IncludeCategory`,
  `SecuritySubTypes`, Textfilter inkl. Dividenden-Anker-Textfilter) sowie
  `ReportTextFilterPlaywrightTests` (Textfilter-UI am selben Dashboard).
- Intervallfenster Quarter/HalfYear/Year × Current/Previous/YearAgo — Unit-Fenstertests
  (`QueryAsync_QuarterWindows_*`, `QueryAsync_HalfYearWindows_*`, `QueryAsync_YearWindows_*`).

## Auffälligkeiten

Keine — kein Produktionsfehler festgestellt. Hinweis (kein Fehler, dokumentierte Semantik):
Die Total-Zeile im Linked-Account-Modus listet die Bankposten verlinkter Konten zweifach
(einmal als Bank-Kind, einmal als abgeleitete Sparplan-Herkunft) — identisch zur
Summenbildung der Aggregation.
