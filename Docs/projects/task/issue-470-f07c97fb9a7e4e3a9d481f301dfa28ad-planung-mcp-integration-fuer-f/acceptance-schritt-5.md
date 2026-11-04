# Abnahmeprüfung – Entwicklungsschritt 5

Zweite Runde: Nachprüfung der Korrektur aus Runde 1
(`acceptance-schritt-5.1.md`) anhand Commit `08ae5dbf`
(„docs: Korrektur Schritt 5 - Planwert-Summen in Aufwandsschätzung").
Geprüftes Dokument: `Docs/planung/mcp-integration/aufwandsschaetzung.md`.

## Ergebnis

**Status:** Anforderung vollständig erfüllt

Die einzige Abweichung aus Runde 1 (falsche Planwert-Summen) ist
vollständig und ohne neue Inkonsistenzen korrigiert.

## Abweichungen

- Keine.

## Hinweise

**Nachgerechnete Summen (Stichprobe/vollständig):**

- **Baustein-Planwerte B1–B19:** 10+8+6+9+13+5+13+6+15+12+6+10+5+9+
  9+7+8+3+11 = **165 PT** – Zeile 70 „Planwert ≈ 165 PT" ist jetzt
  exakt (vorher ≈168). Band-Summe 136–200 PT unverändert exakt.
- **Phasen-Planwert P4 (Zeile 84):** B1-Teil ~3 + B10 12 + B11 6 +
  B17 8 + B18 3 + Testblock ~5 = **≈37 PT** – korrigiert von ≈39;
  die Angabe entspricht jetzt der Summe der Band-Mitten.
- **Phasen-Summe (Zeile 86):** 94+26+19+37+16 = **≈192 PT** –
  korrigiert von ≈194; rechnerisch exakt.
- **Personenmonate (Zeile 88–89):** 192/20 = **9,6 PM** – korrigiert
  von 9,7; Gesamtband 157–231 PT → 7,85–11,55 PM, Angabe
  „≈ 8–11,5 PM" weiterhin korrekt.
- **P1-Anteil (Zeile 89):** 94/192 = 48,96 % → **≈ 49 %** – korrigiert
  von 48 %, stimmig.
- **§4-Gesamtempfehlung (Zeile 123):** „Planwert ≈ 192 PT; konservativ
  200–230 PT" ist konsistent zur neuen Summe und zum oberen Bandende
  (231 PT ≈ 230). Keine neue Inkonsistenz.
- **Einzel-Planwerte** bleiben korrekt gerundete Band-Mitten
  (z. B. P1: 7+8+6+9+13+5+13+6+5+9+Test ~12,5 ≈ 94; P2: 6+9+7+3,5 ≈ 26;
  P3: 15+1,5+2,5 ≈ 19; P5: 11+2,5+2,5 ≈ 16).

**Diff-Umfang des Korrektur-Commits `08ae5dbf`:** ausschließlich
`aufwandsschaetzung.md` (5 Zeilen: Zeilen 70, 84, 86, 88–89, 123) und
`review.md` (Anpassung der Planwert-Nennung in den Review-Hinweisen
plus neuer Abschnitt „Korrekturen Abnahme 1"). Keine anderen Dateien
berührt; genau die in Runde 1 beanstandeten Stellen plus zugehörige
Folgewerte (PM, P1-Anteil, Gesamtempfehlung).

**Restwert-Abgleich:** Volltextsuche in `Docs/planung/mcp-integration/`
zeigt keine Alt-Werte mehr (168, 194, ≈39 für P4, 9,7, 48 % kommen nur
noch als „statt"-Werte im review.md-Korrekturabschnitt vor). Roadmap,
Product-Backlog und README-Index referenzieren die Summen nicht und
sind unverändert.

**Verweis auf Runde 1:** Alle übrigen Akzeptanzkriterien waren bereits
in `acceptance-schritt-5.1.md` als erfüllt festgestellt (Artefakte,
Phasenstruktur, Schätzmethodik, Backlog-Abdeckung aller 33 Tools,
Index-Vollständigkeit, Konsistenz zu den Vorgängerkonzepten); der
Korrektur-Commit berührt keine dieser Punkte.
