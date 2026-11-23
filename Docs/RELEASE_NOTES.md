# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- MCP: the read tool `fm_securities_analysis` now always returns a top-level `latestPrice` field `{ close, date }` per security — the most recently stored closing price and its date — in both the list and the single-security query, regardless of `includePrices`/`priceTake`; `latestPrice` is `null` when no price is stored and is still returned when `hasPriceError` is set (the flag remains an independent signal), so the previous `includePrices: true` + `priceTake: 1` workaround is no longer needed (issue #559).

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- MCP: Das Lesetool `fm_securities_analysis` liefert jetzt je Wertpapier ein immer vorhandenes Top-Level-Feld `latestPrice` `{ close, date }` — den zuletzt gespeicherten Schlusskurs samt Datum — in der Listen- wie in der Einzelabfrage, unabhängig von `includePrices`/`priceTake`; `latestPrice` ist `null`, wenn kein Kurs gespeichert ist, und wird auch bei gesetztem `hasPriceError` ausgegeben (das Flag bleibt eigenständiges Signal) — der bisherige Workaround `includePrices: true` + `priceTake: 1` entfällt damit (Issue #559).
