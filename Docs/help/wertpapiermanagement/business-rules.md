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

## AlphaVantage-Key-Aufloesung

**Beschreibung:** Kursabrufe verwenden bevorzugt den persoenlichen
AlphaVantage API Key des anfragenden Benutzers und fallen nur bei fehlendem
persoenlichem Key auf einen freigegebenen Admin-Key zurueck.

**Bedingungen:**
- Der anfragende Benutzer hat einen gespeicherten AlphaVantage API Key.
- Oder ein Administrator hat einen Key gespeichert und
  `ShareAlphaVantageApiKey` aktiviert.

**Verhalten:**
- Persoenlicher Key vorhanden: Der persoenliche Key wird fuer den Abruf
  verwendet.
- Kein persoenlicher Key, aber freigegebener Admin-Key vorhanden: Der
  Admin-Key wird als Shared-Fallback verwendet.
- Kein Key verfuegbar: Der AlphaVantage-Abruf kann nicht ausgefuehrt werden.
- Gespeicherte Keys werden vor der Nutzung entschluesselt; der Klartext wird
  nicht in Logs, Profilantworten oder UI-Ausgaben offengelegt.

**Umsetzung:** `AlphaVantagePriceProvider`, `AlphaVantageKeyResolver`,
`DataProtectionAlphaVantageSecretProtector`.
