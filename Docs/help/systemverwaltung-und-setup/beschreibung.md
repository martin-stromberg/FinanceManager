← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Beschreibung

## Zweck

Der Bereich stellt Betriebs- und Administrationsfunktionen bereit: Benutzer, Rollen, Login, Profile, Benachrichtigungen, Backup/Restore und Systemschutz.

## Funktionsweise

Die Setup-Seite bündelt persönliche Einstellungen, Benachrichtigungen, Importvorgaben, Anhänge, Backups, Updates, Sicherheitsangaben und Berichtseinstellungen. Einige Abschnitte sind nur für Administratoren sichtbar.

Benutzer können ihr Profil, Spracheinstellungen und Benachrichtigungen pflegen.
Administratoren können Benutzer verwalten, Backups erstellen oder wiederherstellen,
Updates prüfen und Sicherheitsinformationen für die öffentliche Kontaktaufnahme
pflegen.

Backups werden als Archivdateien verwaltet. Eine Wiederherstellung ersetzt
vorhandene Daten und ist deshalb eine besonders riskante Aktion. Vor dem Start
muss der Benutzer den exakten Backup-Dateinamen in einem Bestätigungsdialog
eingeben. Bei falscher oder fehlender Bestätigung wird keine Wiederherstellung
gestartet.

AlphaVantage-Schlüssel aus dem Benutzerprofil werden geschützt gespeichert.
Die Profilansicht zeigt nur an, ob ein Key vorhanden ist; der gespeicherte Wert
wird nicht im Klartext angezeigt. Administratoren können ihren Key weiterhin
zur gemeinsamen Nutzung freigeben. Andere Benutzer verwenden diesen geteilten
Key als Fallback für Kursabrufe, ohne den Klartext einsehen zu können.

Die Einstellungsseite verwendet ein Akkordeon-Layout: Sektionen können einzeln auf- und zugeklappt werden. Die Ribbon-Aktionsleiste zeigt die wichtigsten Aktionen dauerhaft an, unabhängig davon, welche Sektion gerade geöffnet ist.

Die `UploadBackup`-Aktion klappt die Backup-Sektion automatisch auf, falls sie beim Klick auf den Ribbon-Button noch geschlossen ist, bevor der Datei-Picker geöffnet wird.

Aktive Hintergrundtasks werden in der Benutzeroberfläche über ein Statuspanel angezeigt. Dieses Panel fragt laufende und wartende Tasks nur ab, wenn ein authentifizierter Benutzerkontext vorhanden ist. Nicht angemeldete Benutzer starten keine wiederkehrende Statusabfrage gegen `/api/background-tasks/active`. Falls ein bereits gestartetes Panel vom API-Client dennoch `401 Unauthorized` erhält, beendet es seine Polling-Schleife für die aktuelle Komponenteninstanz und blendet die Task-Anzeige aus.

## Aktive Sitzungsverlängerung und JWT-Refresh

Die Anwendung hält aktive Sitzungen für authentifizierte Browser automatisch aufrecht. Wenn ein JWT kurz vor dem Ablauf steht, startet der Client einen Keepalive-Request auf `/api/auth/keepalive`, und das Backend erneuert das Token als neues `FinanceManager.Auth`-Cookie, ohne dass der Benutzer manuell erneut anmelden muss.

Diese Validierung erfolgt serverseitig mit `JwtRefreshMiddleware` und `JwtRefreshService`: Nur aktive Benutzer mit gültigem `security_stamp`, unveränderter Admin-Rolle und aktuellem Benutzerstatus werden berücksichtigt. Ein veralteter oder fachlich invalidierter Token führt nicht zu einem absichtlichen Redirect auf aktiven Seiten, sondern wird sauber verworfen und erst bei einem echten Auth-Verlust als Login-Redirect behandelt.

## Beispiele

- Ein Administrator legt Benutzer an oder setzt Passwörter zurück.
- Ein Administrator deaktiviert einen Benutzer oder entzieht die Admin-Rolle;
  vorhandene Tokens werden danach nicht mehr akzeptiert.
- Ein Benutzer pflegt Import- und Benachrichtigungseinstellungen.
- Ein Benutzer hinterlegt einen AlphaVantage API Key; die Anwendung speichert
  nur den geschuetzten Persistenzwert.
- Ein Administrator gibt seinen AlphaVantage API Key frei; andere Benutzer
  koennen Kursabrufe darueber ausfuehren, sehen den Key aber nicht im Klartext.
- Ein Backup wird erstellt, als ZIP heruntergeladen und später nach Dateinamen-Bestätigung als Hintergrundtask wiederhergestellt.
- Ein Administrator prueft auf ein Self-Update, kontrolliert Paketmetadaten und
  startet die Installation nach Downtime-Bestaetigung.
- Ein angemeldeter Benutzer startet einen Hintergrundtask und sieht Fortschritt, Warteschlange sowie Abbrechen- oder Entfernen-Aktionen im Statuspanel.
- Ein Benutzer arbeitet weiter auf geschützten Seiten; Tastatur-, Mausklick- und Quick-Edit-Interaktionen lösen automatische Keepalive-Requests aus und verlängern die Session ohne sichtbaren Login.
- Ein Benutzer wird deaktiviert oder erhält einen neuen `security_stamp`; der nächste Refresh wird abgelehnt und die Sitzung endet fachlich sauber.

Die Update-Sektion zeigt Quelle, Status, Release Notes und die Metadaten der
verfügbaren Aktualisierung. Administratoren können die automatische Prüfung
aktivieren, Vorabversionen berücksichtigen, ein tägliches Prüfzeitfenster
festlegen, eine geplante Uhrzeit eintragen und den Dienstnamen pflegen. Ein
manueller Installationsstart verlangt eine Ausfallzeit-Bestätigung. Nach dem
Start zeigt die Oberfläche eine Warteseite und lädt neu, sobald die Anwendung
wieder erreichbar ist.

## Spracheinstellung (Anzeigesprache)

Benutzer können ihre bevorzugte Anzeigesprache im Profil-Tab der Einstellungsseite festlegen.
Unterstützte Sprachen sind aktuell **Deutsch (de)**, **Englisch (en)** und **Automatisch**.

### Anzeigesprachen-Modi

| Einstellung | Verhalten |
|-------------|-----------|
| **Automatisch** | Die Sprache wird aus dem `Accept-Language`-Header des Browsers ermittelt |
| **Deutsch (de)** | Die Oberfläche erscheint immer auf Deutsch |
| **Englisch (en)** | Die Oberfläche erscheint immer auf Englisch |

### Sprachänderung & sofortige Wirkung

Wenn ein Benutzer die Sprache speichert, lädt die Seite automatisch neu. Die
neue Sprache ist danach unmittelbar in der gesamten Benutzeroberfläche sichtbar
— ohne erneuten Login.

### Beispiele

- Ein Benutzer stellt **Englisch** als Anzeigesprache ein. Nach dem Speichern erscheinen alle Texte auf Englisch — unabhängig von der Browser-Spracheinstellung.
- Ein Benutzer stellt **Automatisch** ein. Die Anwendung erkennt seine Browser-Sprache (z.B. Englisch) und zeigt die Oberfläche entsprechend an.
- Die Spracheinstellung bleibt nach einem erneuten Login erhalten. Die Browser-Sprache überschreibt eine gespeicherte **Automatisch**-Einstellung nicht.

## security.txt (RFC 9116)

Der Setup-Bereich enthält eine eigene Einstellungssektion **security.txt**, die ausschließlich für Benutzer mit der Rolle `Admin` sichtbar ist. Hierüber werden alle konfigurierbaren RFC-9116-Direktiven gepflegt.

### Öffentliche Ausgabeadressen

Nach erfolgreicher Konfiguration sind folgende Adressen ohne Anmeldung erreichbar:

| Adresse | Format |
|----------|--------|----------|
| `/security.txt` | Text |
| `/.well-known/security.txt` | Text |
| `/.well-known/security.md` | Markdown |
| `/.well-known/security.html` | HTML |

Solange das Pflichtfeld **Kontakt** noch nicht konfiguriert ist, bleiben die vier öffentlichen Ausgaben mit einer erklärenden Fehlermeldung deaktiviert.

### Konfigurierbare Direktiven

| Feldbezeichnung (UI) | RFC-9116-Direktive | Pflicht |
|----------------------|--------------------|---------|
| Kontakt | `Contact` | Ja |
| Ablaufdatum | `Expires` | Ja |
| Verschlüsselung | `Encryption` | Nein |
| Danksagungen | `Acknowledgments` | Nein |
| Bevorzugte Sprachen | `Preferred-Languages` | Nein |
| Richtlinie | `Policy` | Nein |
| Jobs | `Hiring` | Nein |
| Canonical | `Canonical` | Nein |

Die Direktive `Canonical` kann im Setup-Bereich als vollständige HTTPS-URL gepflegt werden. Ist das Feld leer, wird weiterhin automatisch `<Api:BaseAddress>/.well-known/security.txt` als Fallback verwendet.

### Beispiele

- Ein Administrator trägt `mailto:security@example.com` als Kontakt ein und setzt ein Ablaufdatum in der Zukunft. Nach dem Speichern ist `/.well-known/security.txt` öffentlich erreichbar.
- Ein Sicherheitsforscher ruft `/.well-known/security.html` auf und erhält eine strukturierte HTML-Seite mit allen konfigurierten Direktiven.

## Einschränkungen

- Administrative Bereiche erfordern entsprechende Berechtigungen.
- Restore- und Aggregatjobs laufen asynchron und sind statusbasiert zu überwachen; die automatische Statusabfrage erfolgt nur für authentifizierte Benutzer.
- Backup-Uploads sind auf 100 MB komprimiert, 250 MB entpackte NDJSON-Daten, einen ZIP-Eintrag und ein maximales Kompressionsverhältnis von 25 begrenzt.
- Die Lesbarkeit verschluesselt gespeicherter AlphaVantage API Keys haengt vom
  passenden ASP.NET-Core-Data-Protection-Key-Ring ab.
- Self-Updates beenden die laufende Anwendung kurzzeitig. Der Start wird
  abgelehnt, wenn Paket, Lock, ZIP-Struktur oder Service-/EXE-Ziel nicht
  eindeutig valide sind.
- Der administrative Lock-Reset ist ein Betriebswerkzeug fuer manuell
  gepruefte Haengefaelle. Die Anwendung loescht nur alte Locks und zeigt bei
  fehlgeschlagenem Reset den konkreten Grund statt pauschal eine laufende
  Installation zu melden.
- Die Anzeigesprache gilt für die gesamte Benutzeroberfläche. Im Modus „Automatisch"
  wird die Browser-Sprache berücksichtigt; es werden nur die unterstützten Sprachen
  Deutsch und Englisch angeboten.
- Die `security.txt`-Direktive `Contact` akzeptiert nur einen einzelnen Wert (URI oder mailto). Mehrfacheinträge gemäß RFC 9116 werden aktuell nicht unterstützt.
- Die `Canonical`-Direktive akzeptiert optional nur absolute HTTPS-URLs ohne Query/Fragment und ohne localhost/Loopback-Host. Bei leerem Feld wird `Api:BaseAddress` als Fallback verwendet.
