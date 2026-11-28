# Projektplan-Prüfung (Durchlauf 2)

## Ergebnis

**Status:** Projektplan vollständig

## Befund-Nachprüfung

| Befund | Behoben | Nachweis |
|---|---|---|
| 1 — Payload-Erkennungsregel ambig / `account/{guid}`-Regression | ja | Geschlossene Präfix-Whitelist `card/`, `list/`, `reports/` (Ordinal, Ausschluss `:`-Schema und `//`-Start); Regressionskriterium explizit: `account/{guid}` → `list/postings/account/{guid}`. Code-Verifikation: `PostingsCardViewModel.cs:124–134` sendet `account/`/`contact/`/`savings-plan/`/`security/` (kein Whitelist-Präfix); `card,{id}` ist Komma-Format unberührt; `BudgetPurposeCardViewModel.cs:481` (`card/budget/categories/{id}`) wird korrekt mitgefixt. |
| 2 — Präzedenz vs. `?back=` | ja | Whitelist-Check nach `?back=`, vor GUID-/Legacy-Auswertung; „`?back=` gewinnt vor dem Payload" in den Akzeptanzkriterien. Konsistent mit #606–608. |
| 3 — Recurring-Rückwärts-Fälligkeiten nicht implementierbar | ja | `AddIntervalWithMonthEndRule` per Richtungsparameter verallgemeinern oder `SubtractIntervalWithMonthEndRule` ergänzen + sichtbar machen; Abbruchregeln rückwärts ≥ `RangeFrom` und ≥ `CreatedUtc`, vorwärts ≤ `RangeTo`; `SavingsPlanDto.CreatedUtc` existiert (non-nullable). Terminierung gesichert. |
| 4 — Query-Teil in InitBack-Payloads | ja | Query-Teil bleibt Teil des Ziels (InitBack kann `?back=`-Ketten enthalten). |
| 5 — Delivery-/PR-Schritt fehlt | ja | Neue Sektion „Abschluss / Delivery": Push, PR gegen `staging` mit `Closes #614` + `Closes #599`–`#610`, fail-fast Überwachung. |
| 6 — E2E-Nachweis fälschlich Schritt-Muss | ja | Gebündelt auf Projektebene. |

## Abgleich Anforderung ↔ Entwicklungsschritte

Alle zwölf Issues (#599–#610) vollständig den Schritten 1–5 zugeordnet und abgedeckt;
Nicht-Ziele eingehalten; Abhängigkeiten zyklenfrei; keine neuen Enum-Member/persistierten
Felder (`EnumCoverageTests`/Backup-Regeln nicht tangiert).

## Verbleibende Punkte

Keine.

## Hinweise

- Terminierung der Recurring-Iteration verifiziert (streng monotone ≥1-Monats-Schritte,
  beidseitig begrenzt). Detail für Lifecycle: `Type = Recurring` mit `Interval == null` →
  Nicht-Treffer oder `TargetDate`-Only-Fallback (definiert).
- Residual-Trade-off der Check-Position: `BudgetPurposeCardViewModel.cs:481` nutzt den
  `"Back"`-Kanal für eine Linked-Aktion; bei gesetztem `?back=` auf der Purpose-Karte gewinnt
  `?back=`. Enge Randbedingung, dokumentierte Entscheidung ist die bessere Wahl gegenüber
  der Alternative (Payload vor `?back=` ignorierte das `?back=`-Ziel bei #606–608).
- Optional: Präfixvergleich `OrdinalIgnoreCase` und zusätzliche `IsSameOriginUri`-Prüfung
  des Whitelist-Ziels als Härtung — nicht schlechter als Ist-Verhalten.
