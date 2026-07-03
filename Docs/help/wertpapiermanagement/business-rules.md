← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Business Rules

## Pflichtfelder beim Wertpapier

**Beschreibung:** Für ein Wertpapier sind Name, Kennung und Währung zwingend.

**Bedingungen:**
- Eingaben dürfen nicht leer sein.

**Verhalten:**
- Gültige Eingaben: Wertpapier wird erstellt/aktualisiert.
- Ungültige Eingaben: Vorgang wird abgebrochen.

**Umsetzung:** `Security.Update`.

## Preisfehler wird explizit markiert

**Beschreibung:** Fehler bei Kursabrufen werden am Wertpapierzustand gespeichert.

**Bedingungen:**
- Externer Abruf/Import meldet Fehler.

**Verhalten:**
- Wertpapier setzt `HasPriceError`, `PriceErrorMessage`, `PriceErrorSinceUtc`.
- Nach erfolgreicher Aktualisierung kann der Fehlerzustand entfernt werden.

**Umsetzung:** `Security.SetPriceError` und `Security.ClearPriceError`.
