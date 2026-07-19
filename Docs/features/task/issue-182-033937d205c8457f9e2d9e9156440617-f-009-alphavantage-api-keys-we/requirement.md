# Requirement: F-009 - AlphaVantage API Keys werden als normale User-Eigenschaft gespeichert

## Metadaten

| Feld | Wert |
|------|------|
| Aufgaben-ID | 033937d2-05c8-457f-9e2d-9e9156440617 |
| Branch | task/issue-182-033937d205c8457f9e2d9e9156440617-f-009-alphavantage-api-keys-we |
| Erstellt | 2026-07-16 |
| Status | Risiko |
| Schweregrad | Mittel |
| Prioritaet | P2 |
| Bereich | Secret Handling, Datenschutz |
| Standardbezug | OWASP Top 10 A02 Cryptographic Failures, ASVS Data Protection |
| Eintrittswahrscheinlichkeit | Mittel |

## Ausgangslage

AlphaVantage API Keys werden aktuell als normale Benutzereigenschaft gespeichert. Dadurch koennen externe API Keys bei Zugriff auf Datenbank, Backups, Logs oder Debug-Ausgaben potenziell im Klartext sichtbar und direkt weiterverwendbar sein.

## Nachweis

- `FinanceManager.Domain/Users/User.AlphaVantage.cs:12-27` speichert `AlphaVantageApiKey` als `String`.
- `FinanceManager.Infrastructure/AppDbContext.cs:124` konfiguriert fuer den Wert nur `HasMaxLength(120)`.
- `FinanceManager.Web/Controllers/UserSettingsController.cs:110-127` setzt und teilt Keys ohne erkennbare Verschluesselung at rest.
- Matrixbezug: `endpoint-service-matrix.md`, Zeile `UserSettingsController`.

## Angriffsszenario

Ein Datenbank- oder Backup-Leak enthaelt verwendbare AlphaVantage API Keys. Angreifer koennen die Keys auslesen und gegen die externe AlphaVantage API verwenden.

## Zielzustand

AlphaVantage API Keys duerfen nicht mehr als Klartextwert in persistenten Benutzerdaten gespeichert werden. Gespeicherte Keys muessen at rest geschuetzt sein. Zugriff, Verwendung und administratives Teilen muessen nachvollziehbar und angemessen dokumentiert sein.

## Funktionale Anforderungen

1. Die Anwendung speichert AlphaVantage API Keys at rest verschluesselt, z. B. mit ASP.NET Core Data Protection oder einer geeigneten KMS-/Vault-Integration.
2. Bestehende Funktionen zum Setzen, Aktualisieren, Teilen und Verwenden von AlphaVantage API Keys bleiben fachlich erhalten.
3. Der Klartext-Key wird nur fuer den technisch notwendigen Moment zur Verwendung oder Verschluesselung im Speicher verarbeitet.
4. API Keys werden nicht in Logs, Debug-Ausgaben, Exceptions, Auditdaten oder UI-Ausgaben im Klartext ausgegeben.
5. Admin-Key-Sharing ist fachlich dokumentiert und technisch so umgesetzt, dass Zugriffe nachvollziehbar bleiben.
6. Bestehende gespeicherte Klartextwerte werden beim naechsten Schreiben oder durch eine Migration in den geschuetzten Speicherzustand ueberfuehrt, sofern technisch moeglich.

## Nicht-funktionale Anforderungen

1. Die Loesung schuetzt Datenbank- und Backup-Inhalte gegen direkte Klartext-Offenlegung der AlphaVantage API Keys.
2. Die Loesung folgt OWASP Top 10 A02 Cryptographic Failures und ASVS Data Protection.
3. Die Verschluesselungs- oder Secret-Handling-Komponente ist zentral gekapselt und nicht ad hoc in Controllern verteilt.
4. Fehler beim Entschluesseln oder Lesen eines Keys werden kontrolliert behandelt und fuehren nicht zur Offenlegung sensibler Werte.

## Akzeptanzkriterien

1. In der Datenbank wird fuer AlphaVantage API Keys kein verwendbarer Klartext-Key mehr gespeichert.
2. `UserSettingsController` oder nachgelagerte Services persistieren AlphaVantage API Keys nur noch ueber die geschuetzte Secret-Handling-Komponente.
3. Tests belegen, dass gespeicherte Werte nicht dem eingegebenen Klartext-Key entsprechen und bei autorisierter Verwendung korrekt wieder nutzbar sind.
4. Tests oder Codepruefungen belegen, dass AlphaVantage API Keys nicht im Klartext geloggt oder in Fehlerantworten ausgegeben werden.
5. Die Dokumentation beschreibt Admin-Key-Sharing, Zugriffsschutz, Backup-Auswirkungen und Auditierbarkeit.

## Abgrenzung

- Die Anforderung verlangt keine Aenderung des AlphaVantage-Funktionsumfangs.
- Die Anforderung verlangt keine neue externe Vault- oder KMS-Infrastruktur, wenn eine lokal passende Data-Protection-Loesung im Projekt angemessen umgesetzt werden kann.
- Die Anforderung verlangt keine Rotation bereits kompromittierter API Keys; sie sollte jedoch als betrieblicher Hinweis dokumentiert werden.

## Pruefhinweise

- Pruefen, ob `AlphaVantageApiKey` im Domainmodell weiterhin als Klartext-Property verwendet wird oder durch eine geschuetzte Persistenz-/Service-Schicht ersetzt werden muss.
- Pruefen, ob EF-Core-Konfiguration, Migrationen und bestehende Daten kompatibel angepasst werden muessen.
- Pruefen, ob Admin-Key-Sharing und Zugriffe auf geteilte Keys auditierbar sind oder dokumentiert nachgezogen werden muessen.
- Pruefen, ob Backups nach Umsetzung keine verwendbaren Klartext-Keys mehr enthalten und bestehende Backup-Risiken dokumentiert sind.
