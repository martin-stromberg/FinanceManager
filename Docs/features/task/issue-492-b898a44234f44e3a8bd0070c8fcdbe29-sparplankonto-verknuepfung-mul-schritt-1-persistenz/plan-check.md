# Plan-Prüfung – Schritt 1: Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung

**Geprüfte Eingaben:** `requirement.md`, `inventory.md`, `plan.md` (dieses Verzeichnis)

## Ergebnis

**Status:** Plan vollständig

## Abgleich Anforderung ↔ Planelemente

| Anforderung | Abdeckung im Plan | Abgedeckt |
|---|---|---|
| FA-1: Explizite Join-Entity, keine Shadow-m:n | Element 1 (`SavingsPlanAccountLink : Entity`, Vorbild `AccountLinkedIban`) | Ja |
| FA-1: `SavingsPlanId`, `AccountId`, `OwnerUserId`, Erstellungs-/Metadaten | Element 1 (Felder + `Entity`-Basisklasse `CreatedUtc`/`ModifiedUtc`) | Ja |
| FA-1: eindeutige Paarung, eigene Tabelle | Element 2 (`ToTable`, Unique-Index `(SavingsPlanId, AccountId)`) | Ja |
| FA-1: Migration per `dotnet ef` am etablierten Ort | Element 3 (CLI-Aufruf, Ort `Migrations/` neben Snapshot) | Ja |
| FA-1: keine Verwechslung mit `SavingsPlanExpectation` | Element 1 (Benennungsentscheidung dokumentiert) | Ja |
| FA-2: Setzen | Element 4/5 (`AddLinkAsync`) | Ja |
| FA-2: Auflisten beider Richtungen | Element 4/5 (`GetLinkedAccountIdsAsync`, `GetLinkedSavingsPlanIdsAsync`) | Ja |
| FA-2: Entfernen | Element 4/5 (`RemoveLinkAsync`) | Ja |
| FA-2: Owner-Scoping (nur gleicher Eigentümer) | Element 5 (Plan und Konto jeweils gegen `ownerUserId` geprüft → Cross-Owner strukturell ausgeschlossen; `OwnerUserId` redundant am Link) | Ja |
| FA-2: Duplikat-Abweisung | Element 5 (`AnyAsync`-Prüfung → `InvalidOperationException`) + Unique-Index | Ja |
| FA-2: n:m-Semantik (mehrere Konten/Pläne) | Element 5 (keine Kardinalitätsbeschränkung; Tests belegen beide Richtungen) | Ja |
| AK 1: eigene Tabelle, eindeutige Paarung, EF-Migration | Elemente 1–3 | Ja |
| AK 2: Setzen/Auflisten/Entfernen | Elemente 4–5 + Tests | Ja |
| AK 3: Fremd-Owner + Duplikate abgewiesen | Element 5 + Tests `*_OtherOwner_*`, `*_DuplicatePair_*` | Ja |
| AK 4: Mehrfachverknüpfung beide Richtungen | Tests `*_MultipleAccountsPerPlan`, `*_MultiplePlansPerAccount` | Ja |
| AK 5: vollständige Service-Tests | Element 7 (13 Tests in 3 Klassen) | Ja |
| Nicht-Ziel: keine API/UI/MCP/Analyse/Backup-Integration | Plan berührt ausschließlich Domain/Infrastructure/Application + Tests; `*BackupDto` nur als Record (Konvention), keine Export-/Import-Verdrahtung | Eingehalten |
| Nicht-Ziel: keine Datenmigration | Migration erzeugt nur neue Tabelle | Eingehalten |

## Testbedarf vs. Plan

- Geforderte fachliche Regeln (Owner-Scoping, Duplikat, beide List-Richtungen, n:m, Entfernen,
  Cascade) sind durch konkrete Testmethoden abgedeckt; Testklassen folgen der
  `{Class}Tests_{Topic}`-Konvention mit < 5 Methoden je Topic-Klasse.
- Kein UI-/E2E-Testbedarf (Schritt hat keinen Benutzerfluss). Als reale End-to-End-Abnahme dient
  `dotnet ef database update` gegen eine echte SQLite-Datei (Persistenz-Wirkung jenseits
  `EnsureCreated`) plus Service-Tests gegen den echten `AppDbContext` — kein UI/CLI/externer
  Dienst betroffen.

## Hinweise

- `RemoveLink_DeletedPlan_CascadesLinks` setzt SQLite-FK-Enforcement voraus; EF Core aktiviert
  `PRAGMA foreign_keys` bei `Microsoft.Data.Sqlite` standardmäßig — sonst wird der Test an der
  Stelle auf EF-Tracking-Verhalten (geladene Dependents) zurückgeschnitten.
- `bankContactId` der in Tests erzeugten `Account`-Entities verweist auf keinen `Contact`;
  `EnsureCreated` erzeugt keinen FK-Zwang darauf (Konvention der Bestandstests).
