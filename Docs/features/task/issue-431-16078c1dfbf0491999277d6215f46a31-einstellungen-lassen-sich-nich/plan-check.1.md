# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Kontoauszüge: `MaxEntriesPerDraft`, `MonthlySplitThreshold`, `MinEntriesPerDraft` lösen Speichern-Aktivierung aus | `Validate()` um `RaiseStateChanged()` ergänzt; VM ist via `CreateSubViewModel` verdrahtet | bUnit `StatementsSection_AllFields_ToggleSaveButton`, E2E `Statements_Fields_ToggleSaveButton` | Abgedeckt |
| Kontoauszüge: `MassImportDialogPolicy`, `KnownContactAutoCreateEnabled` lösen Speichern-Aktivierung aus | `@bind:after` → `OnChanged()` auf beiden Feldern | dieselben Tests | Abgedeckt |
| Renditeanalyse: `SelectedBenchmarkSecurityId`, `ShowSharpeRatio`, `RiskFreeRate` lösen Speichern-Aktivierung aus | `CreateSectionViewModel` verdrahtet `StateChanged`-Forwarding | bUnit `ReturnAnalysisSection_AllFields_ToggleSaveButton`, E2E `ReturnAnalysis_Fields_ToggleSaveButton` | Abgedeckt |
| Rücksetzen auf gespeicherten Wert deaktiviert Speichern wieder | durch `RecomputeDirty()` in allen Pfaden abgedeckt | beide Tests prüfen Aktivieren **und** Deaktivieren | Abgedeckt |
| Dynamischer Test „für alle Einstellungsfelder", der neu hinzukommende Eingabefelder automatisch berücksichtigt | Plan sieht Enumerierung nur in den Sektionen `statements` und `returnanalysis` vor | — | **Lücke** (siehe unten) |

## Fehlende oder unvollständige Testanforderungen

- [ ] Die Anforderung verlangt einen Test „für alle Einstellungsfelder" — nicht nur für die aktuell defekten. Der Plan muss den dynamischen Test auf **alle** sichtbaren Setup-Sektionen mit Dirty-Aggregation ausdehnen (laut `HasPendingChanges`: profile, notifications, statements, update [Admin], returnanalysis, securitytxt, wellknown). Sektionen ohne Dirty-Aggregation (`attachments`, `backup`, `security`) besitzen Eingabefelder ohne Speichern-Semantik (z. B. `SetupSecurityTab` IP-Eingaben, `SetupBackupTab` Restore-Bestätigung, `SetupAttachmentCategoriesTab` Namensfelder mit eigener Save-Logik) und müssen im Test explizit als Ausnahmen klassifiziert werden — andernfalls schlägt eine vollständige Enumerierung fehl. Eine neu hinzukommende Sektion soll den Test standardmäßig scheitern lassen (Klassifizierungspflicht), damit die Abdeckung nicht unbemerkt lückenhaft wird.
- [ ] Bedingt sichtbare Felder: `MonthlySplitThreshold`/`MinEntriesPerDraft` sind je nach `Mode` per `hidden` ausgeblendet. Der Plan muss festlegen, dass der dynamische Test nur sichtbare Felder prüft bzw. den Modus so vorbereitet, dass die genannten Felder sichtbar sind (z. B. `Mode = MonthlyOrFixed` seeden).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Feldänderung „Kontoauszüge" aktiviert/deaktiviert „Speichern" | `SetupDirtyDetectionPlaywrightTests.Statements_Fields_ToggleSaveButton` | Abgedeckt |
| Feldänderung „Renditeanalyse" aktiviert/deaktiviert „Speichern" | `SetupDirtyDetectionPlaywrightTests.ReturnAnalysis_Fields_ToggleSaveButton` | Abgedeckt |
| Persistieren nach Aktivierung (Save tatsächlich speichert) | implizit durch bestehenden Fan-out-Test `GlobalSave_InvokesDirtySetupSectionSaves`; E2E-Speichern-Klick optional | Abgedeckt (Hinweis) |

## Fehlende oder unvollständige Planbestandteile

- [ ] Umsetzungsreihenfolge Schritt 1 muss den erweiterten Testumfang (alle Dirty-Sektionen + Ausnahmeklassifizierung + Sichtbarkeitsregel) aufnehmen.

## Hinweise

- Für die Enumerierung bietet sich an, über `vm.SettingSections` zu iterieren (alle sichtbaren Sektionen), statt Keys hart zu kodieren; die Klassifizierung „dirty-aggregiert vs. Ausnahme" erfolgt über eine explizite Ausschlussliste im Test.
