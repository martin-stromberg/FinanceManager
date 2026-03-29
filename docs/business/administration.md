# Administration (Business & Features)

Dieses Dokument beschreibt den Administrationsbereich: Benutzerverwaltung, IP‑Sperren, globale Einstellungen, Demo‑Daten und Backup‑Verwaltung.

Funktionen
- Benutzerverwaltung: Anlegen, Bearbeiten, Rollen (Admin/User), Sperren/Entsperren, Password reset
- IP‑Sperrliste: Sperren von IPs bei wiederholten fehlgeschlagenen Logins; Whitelist-Mechanismus
- System‑Einstellungen: Feature‑Flags, Hintergrundjob Konfiguration, API‑Keys (nur Admin freigeben)
- Demo‑Daten: Erzeugung / Löschen von Demo‑Daten für Testbenutzer
- Backups: Erstellen / Liste / Restore (Restore ist restriktiv, ggf. nur Offline möglich)

Sicherheit
- Admin Endpoints nur für Benutzer mit Admin Rolle
- Audit Log: Admin Aktionen protokollieren (wer, wann, was)

API
- `AdminController` — Status, seed-demo-data, clear-cache
- `UsersController` — extended admin operations
- `BackupsController` — admin restore

UI
- Admin Dashboard mit Übersicht (User count, pending tasks, last backup date)
- Actions in Admin area guarded and confirmed (Danger operations require confirmation modal)

Tests
- Unit & Integration: Admin role enforcement, seed data correctness, backup restore flow (dry-run)
