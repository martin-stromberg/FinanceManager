# Anforderung – PR-Review-Feedback Iteration 3 (Fokus: Variante B)

Quelle: PR-Kommentar https://github.com/martin-stromberg/FinanceManager/pull/592#issuecomment-7199384997
Datum: 2026-11-26
Ziel-Branch: `task/issue-590-...-schritt-2-variante-b` (PR #591)
Antwortkanal: PR #592 (Ereignisursprung)

## Übersetzte Anforderungen

Der Anwender lenkt den Fokus auf Design-Variante B. Drei Anforderungen:

### R1 — Auf-/zuklappbares Navigationsmenü
- Die Navigation soll zwischen zwei Modi umschaltbar sein:
  - **Aufgeklappt („normale Ansicht"):** Symbole + Namen der Menüpunkte.
  - **Zugeklappt:** nur die jetzige Icon-Spalte (Rail, 72 px).
- Der gewählte Anzeigemodus wird im `localStorage` persistiert und beim
  nächsten Besuch wiederhergestellt.

### R2 — Überlaufendes Eigenschaftspanel auf Kartenansichten
- Auf den Kartenansichten (Kontakte, Sparpläne, u. a.) ragt das Panel mit den
  Entitätseigenschaften über den rechten Rand des Gesamt-Inhaltepanels hinaus.
- Das Panel muss innerhalb des Content-Panels bleiben.

### R3 — Menüband ohne horizontale Scrollbars (Issue #586)
- Durch die vereinheitlichte Symbolgröße in der Top-Action-Bar treten
  horizontale Scrollbars häufiger auf. Diese müssen verschwinden.
- Aktionsbuttons müssen sich dem verfügbaren Platz anpassen.
- Aktionen, die ohnehin deaktiviert sind, dürfen ausgeblendet werden.
- Erlaubte Mittel: Aufteilung der Aktionen auf Register/Tabs mit Gruppierungen
  wie bisher; wieder unterschiedliche Aktionsbutton-Größen.
- **Explizit gewünscht:** Bestandsaufnahme aller Menübänder mit Auflistung der
  enthaltenen Aktionsgruppen und Aktionen; Ergebnis geht zurück an den
  Anwender zur Designplanung in Stitch (als PR-Kommentar/Artefakt).

## Bezüge
- Issue #590 (Design-Überarbeitung), Issue #586 (responsives Ribbon)
- Variante-B-Design: `Docs/design/proposals/variantB/`
