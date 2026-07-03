← [Zurück zur Übersicht](index.md)

# Kontakte — Business Rules

## Aliaszugriff nur für Kontakt-Eigentümer

**Beschreibung:** Aliasnamen dürfen nur für Kontakte des angemeldeten Benutzers gelesen oder geändert werden.

**Bedingungen:**
- Kontakt existiert.
- Kontakt gehört zum aktuellen Benutzer.

**Verhalten:**
- Wenn Eigentümer passt: Aliasliste verfügbar.
- Sonst: Kein Zugriff auf Aliasdaten.

**Umsetzung:** `ContactService.ListAliases` mit Ownership-Prüfung.

## Kontaktzusammenführung mit Referenzumschreibung

**Beschreibung:** Beim Merge werden abhängige Referenzen auf den Zielkontakt umgebogen.

**Bedingungen:**
- Quell- und Zielkontakt sind gültig und benutzergebunden.

**Verhalten:**
- Zuordnungen werden auf den Zielkontakt übertragen.
- Quellkontakt wird aus aktiver Verwendung entfernt.

**Umsetzung:** `ContactsController` (`/merge`) mit Service-Logik zur Konsolidierung.
