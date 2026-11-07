# Anforderung – Schritt 1: Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung

**Issue:** #492
**Projektschritt:** 1 von 10 (verbindlicher Auftrag aus `Docs/projects/task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul/project-plan.md`)
**Basisbranch:** `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul`

## Ziel

Ein Sparplan↔Bankkonto-Verknüpfung als explizite n:m-Beziehung persistent und fachlich verwaltbar machen.
Dieser Schritt legt ausschließlich das Datenmodell, die Migration und die fachlichen Service-Operationen;
die Auswirkungen auf Plananalyse (Schritt 2), API (Schritt 3), MCP/UI (Schritte 6–8) und Backup (Schritt 9)
sind bewusst nicht Teil dieses Schritts.

## Fachliche Anforderungen

### FA-1 Persistenz der Verknüpfung

- Neue explizite Join-Entity zwischen `SavingsPlan` und `Account` (kein EF-Shadow-m:n, konsistent mit der
  Projektkonvention expliziter Beziehungs-Entities, Vorbild `AccountLinkedIban`).
- Felder mindestens: `Id`, `SavingsPlanId`, `AccountId`, `OwnerUserId` (redundant, konsistent mit allen
  anderen Entities; vereinfacht Owner-Prüfung, spätere Backup-Einsammlung und Indizes) sowie
  Erstellungs-/Metadaten (`CreatedUtc`/`ModifiedUtc` über die `Entity`-Basisklasse).
- Die Paarung `(SavingsPlanId, AccountId)` ist eindeutig (Unique-Index) — keine Duplikat-Links.
- Benennung bewusst so gewählt, dass keine Verwechslung mit der bestehenden
  `SavingsPlanExpectation`-Kopplung am `Account` entsteht.
- Migration ausschließlich per `dotnet ef migrations add` erzeugt, am etablierten Migrationsort
  (neben `AppDbContextModelSnapshot.cs` in `FinanceManager.Infrastructure/Migrations/`).

### FA-2 Fachliche Verwaltungsoperationen (Service-Ebene)

- **Setzen** einer Verknüpfung zwischen Plan und Konto.
- **Auflisten** in beiden Richtungen: verknüpfte Konten eines Plans sowie verknüpfte Pläne eines Kontos.
- **Entfernen** einzelner Verknüpfungen.
- Verknüpfungen sind nur zwischen Plan und Konto **desselben Eigentümers** zulässig (owner-scoped).
- Duplikate werden fachlich abgewiesen (nicht nur über den Unique-Index abgesichert).
- n:m-Semantik: mehrere Konten pro Plan und mehrere Pläne pro Konto (Sammelkonto) sind zulässig.

## Akzeptanzkriterien

1. Die Join-Entity liegt in einer eigenen Tabelle (per `dotnet ef` erzeugte Migration); die Paarung
   (Plan, Konto) ist eindeutig.
2. Verknüpfungen lassen sich setzen, auflisten (Konten eines Plans bzw. Pläne eines Kontos) und einzeln
   entfernen.
3. Links zwischen Plan und Konto unterschiedlicher Eigentümer sowie Duplikate werden abgewiesen.
4. Ein Plan kann mehrere Konten und ein Konto mehrere Pläne bedienen.
5. Der Schritt ist über Service-Tests vollständig belegt (xUnit + SQLite in-memory, Projektkonvention).

## Explizite Nicht-Ziele dieses Schritts

- Keine API-Endpunkte, keine DTO-/ApiClient-Erweiterung, keine UI, keine MCP-Tools (folgen in Schritt 3+).
- Keine Auswirkung auf `accumulatedAmount`/Plananalyse (Schritt 2) und auf die Budgetsicht.
- Keine Backup-/Restore-Integration der Join-Entity (Schritt 9); der `*BackupDto`-Record wird jedoch
  konventionsgemäß bereits am Entity-Typ mitgeführt.
- Keine Datenmigration bestehender Daten.
