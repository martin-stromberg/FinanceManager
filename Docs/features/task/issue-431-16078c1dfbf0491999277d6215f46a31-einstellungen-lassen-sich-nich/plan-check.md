# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Kontoauszüge: `MaxEntriesPerDraft`, `MonthlySplitThreshold`, `MinEntriesPerDraft` lösen Speichern-Aktivierung aus | `Validate()` um `RaiseStateChanged()` ergänzt; VM via `CreateSubViewModel` verdrahtet | bUnit `AllSections_EachVisibleField_TogglesSaveButton`, E2E `Statements_Fields_ToggleSaveButton` | Abgedeckt |
| Kontoauszüge: `MassImportDialogPolicy`, `KnownContactAutoCreateEnabled` lösen Speichern-Aktivierung aus | `@bind:after` → `OnChanged()` auf beiden Feldern | dieselben Tests | Abgedeckt |
| Renditeanalyse: `SelectedBenchmarkSecurityId`, `ShowSharpeRatio`, `RiskFreeRate` lösen Speichern-Aktivierung aus | `CreateSectionViewModel` verdrahtet `StateChanged`-Forwarding | bUnit `AllSections_EachVisibleField_TogglesSaveButton`, E2E `ReturnAnalysis_Fields_ToggleSaveButton` | Abgedeckt |
| Rücksetzen auf gespeicherten Wert deaktiviert Speichern wieder | `RecomputeDirty()` in allen Pfaden | beide Tests prüfen Aktivieren und Deaktivieren | Abgedeckt |
| Dynamischer Test über alle Einstellungsfelder, berücksichtigt neue Felder automatisch | bUnit-Test iteriert alle Sektionen aus `SettingSections`, enumeriert sichtbare `input`/`select`, klassifiziert dirty-aggregiert vs. Ausnahme; neue Sektionen → Fehlschlag | `AllSections_EachVisibleField_TogglesSaveButton` + Hilfsmethoden | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Feldänderung „Kontoauszüge" aktiviert/deaktiviert „Speichern" | `SetupDirtyDetectionPlaywrightTests.Statements_Fields_ToggleSaveButton` | Abgedeckt |
| Feldänderung „Renditeanalyse" aktiviert/deaktiviert „Speichern" | `SetupDirtyDetectionPlaywrightTests.ReturnAnalysis_Fields_ToggleSaveButton` | Abgedeckt |

## Hinweise

- Vorangegangene Prüfung (`plan-check.1.md`) hatte den eingeschränkten Testumfang sowie fehlende Regeln für bedingt sichtbare Felder (`hidden`) und nicht aggregierte Sektionen beanstandet; beides ist im aktualisierten Plan adressiert.
