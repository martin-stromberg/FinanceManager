# Language Settings E2E Tests - Zusammenfassung

## Dateien erstellt

### 1. Test-Implementierung
\FinanceManager.Tests.E2E/Tests/ProfileSettings/ProfileSettingsLanguageTests.cs\
- 5 Testmethoden
- Page Object Model für SetupProfileTab
- Helper-Methode zum JWT-Decode

### 2. Test-Report
\Docs/features/task-issue-219-language-settings/e2e-test-report.md\
- Detaillierte Dokumentation aller Tests
- Ausführungsanleitung
- Wartungsrichtlinien

---

## Test-Szenarios (5 Tests)

1. **ChangeLanguage_ToEnglish_SavesAndApplies**
   - ✅ Verifiziert: Sprachänderung auf Englisch wird gespeichert und angewendet
   - Szenario: Benutzer ändert Sprache, speichert, Page neu laden → UI in Englisch

2. **NewUser_WithoutLanguagePreference_UsesDefaultCulture**
   - ✅ Verifiziert: Standard-Kultur (Deutsch) wird ohne Einstellung verwendet
   - Szenario: Neuer Benutzer ohne PreferredLanguage → UI in Deutsch (nicht Browser-Sprache)

3. **ChangeLanguage_UpdatesAuthCookie_WithNewJwtToken**
   - ✅ Verifiziert: JWT-Token wird nach Sprachänderung aktualisiert
   - Szenario: Benutzer ändert Sprache → Auth-Cookie mit neuem JWT + pref_lang Claim

4. **ChangeLanguage_ToAutomatic_RespectsBrowserLanguage**
   - ⚠️ Verifiziert: Auto-Modus funktioniert (optional AC3)
   - Szenario: Benutzer wählt \"Automatisch\" → Browser-Sprache wird beachtet

5. **LanguagePreference_PersistedAcrossSessions**
   - ✅ Verifiziert: Spracheinstellung bleibt über Sessions erhalten
   - Szenario: Session 1 Sprache auf Englisch → Session 2 immer noch Englisch

---

## Page Object Model

**SetupProfileTabPageObject** - Kapselt alle UI-Interaktionen

Methoden:
- SelectLanguageAsync(languageCode)
- ClickSaveAsync()
- VerifySuccessMessageDisplayedAsync()
- GetSelectedLanguageAsync()
- WaitForPageReadyAsync()

---

## Helper

**ExtractJwtClaim(token, claimName)**
- Dekodiert JWT Payload (base64-url)
- Extrahiert Claim-Werte
- Verwendet: pref_lang Claim-Verifikation

---

## Testierte Sprachstring

| Deutsch | English |
|---------|---------|
| Sprache | Language |
| Speichern | Save |
| Einstellungen gespeichert | Settings saved |

---

## Build & Ausführung

### Compilation
\\\ash
dotnet build FinanceManager.Tests.E2E
\\\

### Tests ausführen
\\\ash
cd FinanceManager.Tests.E2E
dotnet test --filter \"ProfileSettingsLanguageTests\"
\\\

### Spezifischer Test
\\\ash
dotnet test --filter \"Name=ChangeLanguage_ToEnglish_SavesAndApplies\"
\\\

---

## Status

✅ **Tests erstellt und kompilierbar**
✅ **Alle 4 Akzeptanzkriterien abgedeckt**
✅ **POM-Pattern korrekt implementiert**
✅ **JWT-Verifizierung möglich**
⚠️ **Noch nicht ausgeführt** (Test-Umgebung erforderlich)

---

## Framework & Dependencies

- **Test-Framework:** xUnit
- **Browser:** Playwright (Chromium)
- **Assertions:** FluentAssertions
- **Test-Helpers:** AuthGateway, TestUserSeeder, PlaywrightWebAppFixture

---

## Nächste Schritte

1. Tests ausführen: \dotnet test --filter \"ProfileSettingsLanguageTests\"\
2. HTML-Selektoren überprüfen (bei Fehlschlag)
3. Timeouts anpassen (bei Bedarf)
4. Erfolgsmeldung anzeigen

---

**Erstellt:** 2026-08-01 09:43 UTC
**Status:** Bereit zur Ausführung
