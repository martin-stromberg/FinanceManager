# Anforderung: Attachment-Download-Tokens und Attachment-Upload absichern

## Metadaten

- Aufgaben-ID: `4b7868f6-67ec-4e2a-ad1a-3eb264ad45e8`
- Branch: `task/issue-181-4b7868f667ec4e2aad1a3eb264ad45e8-f-007f-008-attachment-download`
- Erstellt: 2026-07-16
- Umfang: F-007, F-008

## Ausgangslage

Attachment-Downloads verwenden kurzlebige anonyme Download-Tokens, die aktuell als Query-Parameter akzeptiert werden. Die Request-Logging-Middleware protokolliert Pfad und Query-String gemeinsam, wodurch diese Tokens in Datei- oder Konsolenlogs landen koennen.

Attachment-Uploads erlauben sehr hohe Request-Groessen und verlassen sich bei der Typpruefung auf den vom Client gelieferten `Content-Type`. Die gespeicherten Attachment-Metadaten und Bytes werden unveraendert persistiert und beim Download mit dem gespeicherten Content-Type ausgeliefert.

## Ziel

Die Attachment-Funktion muss so gehaertet werden, dass Download-Tokens nicht in Logs offengelegt werden und hochgeladene Dateien nicht allein anhand clientgelieferter Header als sicher behandelt oder inline mit riskantem Content-Type ausgeliefert werden.

## Funktionale Anforderungen

### F-007: Sensitive Query-Parameter in Logs redigieren

- Die Request-Logging-Middleware muss sensitive Query-Parameter vor jeder Protokollierung redigieren.
- Der Parameter `token` muss mindestens redigiert werden, unabhaengig von Gross-/Kleinschreibung.
- Pfad und Query-String duerfen weiterhin fuer Diagnosezwecke protokolliert werden, aber sensitive Werte muessen durch einen konstanten Platzhalter ersetzt werden.
- Die Redigierung muss fuer alle Logpfade gelten, die aktuell `Request.Path + Request.QueryString` verwenden.
- Attachment-Download-Tokens duerfen nach der Aenderung nicht mehr im Klartext in Datei- oder Konsolenlogs erscheinen.

### F-008: Attachment-Upload und Download-Content-Handling haerten

- Die Upload-Groessenbegrenzung auf Controller-/Serverebene muss an eine realistische maximale Attachment-Groesse gekoppelt werden und darf nicht `long.MaxValue` verwenden.
- Die fachliche Groessenpruefung muss erhalten bleiben.
- Der erlaubte Content-Type darf nicht ausschliesslich aus `file.ContentType` abgeleitet werden.
- Fuer relevante erlaubte Dateitypen muss eine serverseitige Inhaltsvalidierung anhand von Magic Numbers oder vergleichbaren Signaturen ergaenzt werden.
- Der gespeicherte oder ausgelieferte Content-Type muss aus serverseitig validierten Informationen stammen oder auf einen sicheren Fallback gesetzt werden.
- Downloads muessen sicherstellen, dass riskante Inhalte nicht inline ausgefuehrt werden.
- Attachment-Downloads muessen mit sicherem `Content-Disposition: attachment` ausgeliefert werden, sofern kein explizit sicherer Inline-Fall begruendet und implementiert ist.

## Nicht-funktionale Anforderungen

- Die Aenderungen muessen mit OWASP Top 10 A09, A04, A05 sowie ASVS Data Protection und ASVS File Handling vereinbar sein.
- Bestehende gueltige Attachment-Uploads und -Downloads sollen fuer erlaubte Dateitypen weiterhin funktionieren.
- Fehlerfaelle muessen fuer Benutzer verstaendlich bleiben, ohne sensitive Details preiszugeben.
- Die Implementierung muss automatisiert testbar sein.

## Akzeptanzkriterien

- Ein Request auf einen Attachment-Download mit `?token=<wert>` erzeugt keinen Logeintrag, der `<wert>` im Klartext enthaelt.
- Query-Parameter mit Namen `token`, `Token` oder anderer Gross-/Kleinschreibung werden gleich behandelt und redigiert.
- Nicht-sensitive Query-Parameter bleiben im Log erkennbar, soweit sie nicht als sensitiv klassifiziert sind.
- Uploads oberhalb der konfigurierten maximalen Attachment-Groesse werden fruehzeitig abgewiesen.
- Uploads mit manipuliertem oder nicht passendem `Content-Type` werden abgewiesen oder mit einem sicheren Content-Type behandelt.
- Downloads verwenden keinen ungeprueften clientgelieferten Content-Type fuer die Antwort.
- Downloads setzen `Content-Disposition: attachment` fuer gespeicherte Attachments.
- Automatisierte Tests decken Token-Redigierung, Upload-Groessenlimit, Content-Type-/Signaturvalidierung und Download-Header ab.

## Referenzen im Code

- `FinanceManager.Web/Controllers/AttachmentsController.cs:118-123`: aktuelles Request-Groessenlimit
- `FinanceManager.Web/Controllers/AttachmentsController.cs:143-148`: fachliche Groessenpruefung
- `FinanceManager.Web/Controllers/AttachmentsController.cs:150-157`: Pruefung von `file.ContentType`
- `FinanceManager.Web/Controllers/AttachmentsController.cs:232-244`: Erzeugung von Download-Tokens
- `FinanceManager.Web/Controllers/AttachmentsController.cs:260-289`: Annahme des Tokens als Query-Parameter und Download-Auslieferung
- `FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs:50-57`: Logging von Pfad und Query-String
- `FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs:62-69`: Logging von Pfad und Query-String
- `FinanceManager.Infrastructure/Attachments/AttachmentService.cs:63-75`: unveraenderte Speicherung von Dateiname, Content-Type und Bytes

## Risiken und Annahmen

- Bestehende Clients koennen weiterhin Download-Links mit Query-Token verwenden; diese Anforderung verlangt primaer Log-Redigierung, nicht zwingend eine Protokollaenderung weg von URL-Tokens.
- Eine vollstaendige Malware-Erkennung ist nicht Teil der Anforderung.
- Die konkret erlaubten Dateitypen und Maximalgroessen sind aus der bestehenden Anwendungskonfiguration oder aus dem aktuellen fachlichen Verhalten abzuleiten.
- Zusaetzliche Einschraenkungen koennen bestehende Dateien betreffen, wenn deren gespeicherter Content-Type bisher ungeprueft uebernommen wurde.

## Offene Punkte

- Keine.
