# Implementierungs-Zusammenfassung: Bug-Fix Issue #219 — "Language settings not considered"

## Status
✅ **Vollständig implementiert, alle Tests grün**

---

## Problem

Trotz gesetzter Spracheinstellung (z.B. Englisch) zeigte die Anwendung immer Deutsch.
Außerdem: Wurde „Automatisch" gesetzt und danach neu eingeloggt, stand in den
Einstellungen plötzlich die konkrete Browser-Sprache statt „Automatisch".

---

## Ursachen & Korrekturen

### 1. Middleware-Reihenfolge (`ProgramExtensions.cs`)

`UseRequestLocalization` lief **vor** `UseAuthentication`. Damit war `HttpContext.User`
beim Auswerten der Kulturpräferenz noch nicht befüllt — alle authentifizierten Benutzer
fielen auf die Standard-Sprache zurück.

**Fix:** `UseRequestLocalization()` wird jetzt **nach** `UseAuthentication()` aufgerufen.

### 2. „Automatisch"-Modus (`UserPreferenceRequestCultureProvider.cs`)

Der Provider gab `"de"` als Fallback zurück, wenn keine explizite Einstellung vorlag.
Damit wurde die `Accept-Language`-Kette unterbrochen und Browser-Sprache nie ausgewertet.

**Fix:** Für unauthentifizierte Requests und Benutzer mit `null`-Einstellung (= Automatisch)
gibt der Provider `null` zurück und delegiert an den nächsten Provider (`Accept-Language`-Header).

### 3. Login überschreibt „Automatisch"-Einstellung (`UserAuthService.cs`)

Beim Login sendete das JavaScript die Browser-Sprache mit. `LoginAsync` überschrieb ein
`null`-`PreferredLanguage` (= „Automatisch") mit der erkannten Browser-Sprache. Nach dem
nächsten Login war in den Einstellungen die konkrete Sprache statt „Automatisch" zu sehen.

**Fix:** Die Logik zum Überschreiben von `PreferredLanguage` beim Login wurde entfernt.
Neue Benutzer erhalten ihre Sprache bereits bei der Registrierung — die Login-Überschreibung
war redundant und hat den Automatisch-Modus beschädigt.

---

## Geänderte Dateien

| Datei | Änderung |
|-------|----------|
| `FinanceManager.Web/ProgramExtensions.cs` | `UseRequestLocalization` nach `UseAuthentication` verschoben |
| `FinanceManager.Web/Infrastructure/UserPreferenceRequestCultureProvider.cs` | `null` zurückgeben statt `"de"` für Automatisch-Modus |
| `FinanceManager.Infrastructure/Auth/UserAuthService.cs` | Login überschreibt `PreferredLanguage` nicht mehr |
| `FinanceManager.Tests/Infrastructure/UserPreferenceRequestCultureProviderTests.cs` | 6 Unit-Tests aktualisiert (erwarten `null` statt `"de"`) |
| `FinanceManager.Tests.E2E/Tests/ProfileSettings/ProfileSettingsLanguageTests.cs` | 5 E2E-Tests neu implementiert |
| `Docs/help/systemverwaltung-und-setup/beschreibung.md` | Dokumentation aktualisiert |

---

## Test-Ergebnisse

| Test-Suite | Bestanden | Fehlgeschlagen |
|-----------|-----------|----------------|
| `UserPreferenceRequestCultureProviderTests` (Unit) | 6 | 0 |
| `ProfileSettingsLanguageTests` (E2E) | 5 | 0 |

---

## Provider-Kette (nach Fix)

```
UserPreferenceRequestCultureProvider
  → explizite Einstellung gesetzt? → ProviderCultureResult("de"/"en")
  → Automatisch / null?            → null (weiter zur nächsten)
           ↓
HeaderRequestCultureProvider (Accept-Language)
           ↓
Default Culture "de"
```
