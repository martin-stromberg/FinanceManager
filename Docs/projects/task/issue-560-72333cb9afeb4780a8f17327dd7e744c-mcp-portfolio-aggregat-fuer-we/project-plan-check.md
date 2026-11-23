# Projektplan-Prüfung

## Ergebnis

**Status:** Projektplan vollständig

Alle vier Befunde aus der ersten Prüfung (archiviert als `project-plan-check.1.md`) sind korrekt behoben; die übernommenen Hinweise (`PreApprovalKind`-Enum, race-sicheres FX-Lazy-Fill + Unique-Index-Hinweis) sind eingearbeitet. Keine neuen blockierenden Widersprüche; drei kleinere Redaktionspunkte aus der zweiten Prüfung wurden anschließend ebenfalls bereinigt (DecisionNote-Verweis in Vorgehensentscheidung 5, Unique-Index-Doppelangabe, `dismiss`-Akzeptanzkriterium).

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung | Schritt | Abgedeckt? | Anmerkung |
|---|---|---|---|
| Auftrags-Deliverables (Statuskommentare je Issue, Screenshots, gemeinsamer PR, Closing-Tags) | Abschlussphase | ja | In Übersicht verankert. |
| #562 Transparenz (`priceErrorMessage`/`priceErrorSinceUtc`, Tool-Doku) | 1 | ja | untrusted-Platzierung, drei Auffälligkeiten im Descriptor, DTO-Test; kein Backup-Bedarf. |
| #560 `includeStatistics` inkl. Record-Scope auf Statistik, `skip`/`take`-Unabhängigkeit | 2 | ja | Explizite Vorgabe: Record-Filter auf ungepagte Menge vor Pagination; Batch-Preislookup über volle ID-Menge. Gegen Code verifiziert. |
| #561 EUR-Gegenwert (`valueEur`, `fxRate`, Division statt Multiplikation, graceful null, Cache) | 3 | ja | Globale `CurrencyExchangeRate`, Lazy-Fill, Fallback ≤ Kursdatum, Offline→`null`, Backup-Sektion mit Guard. |
| #561 Race-sicheres Lazy-Fill + Unique-Index | 3 | ja | Sperre/In-Flight-Dedupe und Index-Varianten benannt. |
| #578 Genehmigungseinheit `(DataType, Right)`, freigebbar Read/Complete/Modify/Create, nicht Suggest/Book/Delete | 4 | ja | Konsistent in Umfang, Akzeptanz und Offene Punkte. |
| #578 `dismiss` als genehmigungspflichtige Operation `(Administration, Modify)` | 4 | ja | Bewusste Katalogisierung; Pending ohne Grant, auto-Approved mit Grant. |
| #578 Grant-Entzug zieht grant-basiert auto-genehmigte Requests zurück | 4 | ja | Präzedenz `WithdrawScopeUncoveredForAccessAsync`/`WithdrawBySystem` verifiziert. |
| #578 `PreApprovalKind`-Enum statt DecisionNote-String | 4 | ja | Persistiert, BackupDto-Default, EnumCoverageTests. |
| #578 Audit/UI/Persistenz-Backup-Kompatibilität | 4 | ja | `ApprovalGrantsChanged`-Event, DTO/Update-Pfad, Wizard + Grant-Matrix, Resx, Migration + Backup-Defaults; MCP-Import-Ausnahme dokumentiert. |

## Abhängigkeitsprüfung

Azyklisch: Schritt 1 ohne Abhängigkeiten; 2 → 1 (gleiche Datei); 3 → 2 (Statistik-Vertrag); 4 ohne fachliche Abhängigkeit (zeitlich nach 3). Übersichtstabelle deckungsgleich mit den Schritt-Blöcken. Jede Schrittbeschreibung trägt Umfang, Akzeptanzkriterien, Abhängigkeiten und Bereiche und ist als eigenständige Lifecycle-Eingabe geeignet.

## Fehlende oder unvollständige Punkte

Keine.

## Hinweise

- Die Umordnung „Record-Filter vor Pagination" in Schritt 2 ist eine echte, bewusste Verhaltenskorrektur (Seiten füllen sich danach korrekt); im Step-Plan als solche dokumentieren.
- `Granular`-Gate ist konservativ: nicht katalogisierte `actionKey` → `RequiresApproval` → Queue mit Grant-Prüfung im `EnqueueAsync`; `PerAction`-Semantik als Fallback erhalten.
- `PreApprovalKind` und `McpApprovalMode.Granular` benötigen neben `EnumCoverageTests` eine Test-Referenz (`.githooks/enum-coverage-check.py`) — im Umfang berücksichtigt.
- `PreApprovalKind` ist neues Feld an bestehender Entität: `ToBackupDto`/`AssignBackupDto` auf `AiActionRequest` im Step-Plan benennen (AGENT.md-Konvention).
- `fm_admin_status` ist `Lesend` und benötigt korrekterweise keinen Katalog-Eintrag.
