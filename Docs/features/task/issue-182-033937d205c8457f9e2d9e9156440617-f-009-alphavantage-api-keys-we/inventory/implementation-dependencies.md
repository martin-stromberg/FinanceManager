# Implementierungsrelevante Abhaengigkeiten

## Projektstruktur

Die betroffenen Projekte sind:

- `FinanceManager.Domain`: User-Entity und AlphaVantage-Property.
- `FinanceManager.Infrastructure`: EF-Konfiguration, Migrationen, ggf. Data-Protection-/Secret-Service, Backup-Service.
- `FinanceManager.Web`: Controller, Resolver, DI, UI/ViewModel, AlphaVantage-HTTP-Client.
- `FinanceManager.Shared`: DTOs fuer Profil-Settings.
- `FinanceManager.Tests` und `FinanceManager.Tests.Integration`: Verifikation.

## Moegliche Platzierung Der Secret-Komponente

Option A: Infrastructure

- Vorteil: Nahe an Persistenz und Data Protection.
- Nachteil: `FinanceManager.Web` nutzt den Resolver bereits dort; Controller im Web-Projekt muesste Infrastructure-Service injizieren.

Option B: Web Services

- Vorteil: Data Protection ist ein ASP.NET-Core-Web-Service und DI-Konfiguration liegt in `ProgramExtensions`.
- Nachteil: Secret-Handling bleibt im Web-Projekt, obwohl Persistenzrisiko im Infrastructure-Modell liegt.

Pragmatisch passt eine kleine Web-Service-Komponente, wenn sie nur AlphaVantage betrifft. Wenn spaeter weitere User-Secrets folgen, ist ein allgemeiner Infrastructure/Application-Abstraktionsschnitt sinnvoller.

## Data Protection

Falls ASP.NET Core Data Protection genutzt wird, sind relevant:

- `IDataProtectionProvider.CreateProtector("FinanceManager.AlphaVantageApiKey.v1")`
- `Protect(string plaintext)`
- `Unprotect(string protectedData)`

Die Umsetzung sollte ein eigenes Formatpraefix speichern, damit alte Klartextwerte und neue Ciphertexte unterscheidbar sind. Ohne Praefix ist Lazy-Migration fehleranfaellig.

## Spaltenlaenge

Die aktuelle Persistenzlaenge 120 reicht voraussichtlich nicht fuer Data-Protection-Payloads. Umsetzungsoptionen:

- `AlphaVantageApiKey` auf groessere Laenge setzen, z. B. 2048 oder providerabhaengig unbounded.
- Neue Spalte `AlphaVantageApiKeyProtected` einfuehren und alte Spalte migrieren/entfernen.
- Bestehende Property semantisch umdeuten, aber EF-Laenge erhoehen und Dokumentation/Kommentare anpassen.

Die einfachste kompatible Variante ist: bestehende Property weiterverwenden, Laenge erhoehen, Formatpraefix einfuehren.

## Migration Bestehender Werte

Varianten:

- Beim naechsten Schreiben: einfach, erfuellt die Mindestformulierung teilweise, aber alte Keys bleiben bis dahin im Klartext.
- Beim naechsten Lesen/Verwenden: reduziert Risiko schneller, erfordert schreibenden Kontext im Resolver oder separaten Service.
- Einmalige EF-Migration kann nicht ohne Zugriff auf Data-Protection-Services verschluesseln, sofern nur C#-Migration/SQL genutzt wird.
- Startup-Migration-Service: koennte bestehende Werte beim Start schuetzen, braucht sorgfaeltige Fehlerbehandlung und Key-Ring-Verfuegbarkeit.

Fuer eine robuste Umsetzung bietet sich Lazy-Reprotect beim erfolgreichen Resolver-Lesen an, sofern der Resolver tracking-faehig erweitert oder ein separater Secret-Migration-Service genutzt wird.

## Admin-Key-Sharing

Bestehendes Verhalten:

- Admins koennen `ShareAlphaVantageApiKey` setzen.
- Resolver nimmt den ersten teilenden Admin nach `UserName`.
- Normale Nutzer duerfen Sharing nicht aktivieren.

Nachvollziehbarkeit ist aktuell nur indirekt ueber Datenzustand moeglich. Falls technische Auditierbarkeit gefordert ist, braucht es strukturierte Logs ohne Key:

- Nutzer-ID des anfragenden Nutzers.
- Ob persoenlicher oder geteilter Key verwendet wurde.
- ID des Admin-Users bei geteiltem Key, falls datenschutzrechtlich akzeptiert.
- Kein API-Key-Wert, keine URL mit Query.

## Nicht Zu Aendernde Flaechen

- Der fachliche AlphaVantage-Funktionsumfang soll gleich bleiben.
- UI muss den gespeicherten Key nicht anzeigen.
- Externe Vault-/KMS-Infrastruktur ist nicht zwingend noetig.
- Die Anwendung muss keine Rotation kompromittierter Keys automatisieren; Dokumentationshinweis reicht laut Anforderung.
