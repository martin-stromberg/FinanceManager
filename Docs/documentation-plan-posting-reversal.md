# Dokumentationsplan: Posting-Stornierung (Reversal)

> Erstellt: 2025-07  
> Branch: `140-buchung-rückgängig-machen`  
> Status: 🟡 In Bearbeitung  
> Feature: Posting-Stornierung – Fehlerhafte Buchungen durch Gegenbuchung rückgängig machen

---

## Phase 1 – Analyse-Ergebnisse (Agentenschwarm)

### API-Dokumentation

| Befund | Details |
|--------|---------|
| **Bestehende Datei** | `docs/api/PostingsController.md` – vorhanden, aber nur 16 Zeilen (sehr unvollständig) |
| **Fehlende Reversal-Endpunkte** | 2 neue Endpoints komplett undokumentiert |
| **Fehlende DTOs** | `ReversalResultDto`, `ReversalValidationDto` nicht in `models.md` |
| **CHANGELOG** | Nicht vorhanden |
| **Priorität** | 🔴 Hoch |

**Fehlende Endpoints:**

| HTTP | Route | Status |
|------|-------|--------|
| POST | `/api/postings/{id}/reverse` | ❌ Fehlt |
| GET | `/api/postings/{id}/validate-reversal` | ❌ Fehlt |

**Zu aktualisierende Dateien:**
- `docs/api/PostingsController.md` – alle Reversal-Endpoints ergänzen
- `docs/api/PUBLIC_API.md` – Postings-Sektion um Reversal-Endpoints erweitern
- `docs/api/models.md` – ReversalResultDto, ReversalValidationDto dokumentieren
- `docs/api/INDEX.md` – Postings-Zeile aktualisieren (GET → GET, POST)
- `CHANGELOG.md` – neu erstellen (Root-Verzeichnis)

---

### Flow-Dokumentation

| Befund | Details |
|--------|---------|
| **Bestehende Flows** | 9 Flows in `docs/flows/` (kein Reversal-Flow) |
| **Neu zu erstellen** | `docs/flows/posting-reversal-flow.md` |
| **Zu aktualisieren** | `docs/flows/README.md` – Index-Eintrag ergänzen |
| **Priorität** | 🟠 Mittel |

**Wesentliche Ablaufschritte (ermittelt aus Quellcode):**
1. Pre-Transaction Validierung (CanReverseAsync: Posting existiert, Benutzer autorisiert, nicht bereits storniert, keine Teilstornierung)
2. DB-Transaktion (ReadCommitted)
3. Original + Gruppenpostings laden
4. Stornierungspostings mit negativem Betrag erstellen
5. Original als storniert markieren (ReversedByPostingId)
6. StatementImport für Rekonziliation erstellen
7. Aggregates aktualisieren
8. Commit / Rollback bei Fehler

**Fehlerszenarien:** Posting nicht gefunden (400), bereits storniert (409), Gruppe teilweise storniert (409), nicht autorisiert (403), Transaktion schlägt fehl (500)

---

### Business-Dokumentation

| Befund | Details |
|--------|---------|
| **Bestehende Features** | F001–F018 in `docs/business/features/` |
| **Fehlt** | F019 für "Buchungsstornierung" – NICHT vorhanden |
| **Requirements** | `docs/requirements/FA-POST-001_Posting_Reversal.md` vorhanden (310 Zeilen) |
| **Priorität** | 🔴 Hoch |

**Zu erstellende Dateien:**
- `docs/business/features/F019-buchungsstornierung.md` – Endnutzer-Dokumentation (DE)
- `docs/business/features/F019-buchungsstornierung.en.md` – Endnutzer-Dokumentation (EN)

**Wesentliche Business-Regeln:**
- Nur eigene Postings stornierbar (Zugriffskontrolle)
- Bereits stornierte Postings können nicht erneut storniert werden
- Stornierungen können nicht selbst storniert werden (keine Rekursion)
- Gruppenstornierung: All-or-Nothing (alle Postings einer Gruppe werden gemeinsam storniert)
- Gegenbuchung: negativer Betrag, gleiches Datum, gleiche Referenzen

**Bekannte Einschränkungen:**
- Kein Bestätigungsdialog (direkte Stornierung)
- Bug: `GetRelatedPostingsAsync` mit `GroupId == Guid.Empty` (Test L21 mit Skip)

---

### README-Dokumentation

| Befund | Details |
|--------|---------|
| **Fehlende Features** | Reversal-Feature nicht in README.md erwähnt |
| **XML-Dok** | `IPostingReversalService`, `PostingReversalService`, alle DTOs – vollständig |
| **Controller** | `ReversePosting()` XML-Kommentar unvollständig (Transaktions-Semantik fehlt) |
| **Priorität** | 🟠 Mittel |

**Zu aktualisierende Dateien:**
- `README.md` – Abschnitt "Features / Für Nutzer" um Reversal ergänzen

---

## Phase 2 – Ausführungsplan (Parallele Agenten)

| Agent | Aufgabe | Zieldateien |
|-------|---------|-------------|
| `documentation-api` | Reversal-Endpoints + DTOs dokumentieren, CHANGELOG erstellen | `docs/api/PostingsController.md`, `docs/api/PUBLIC_API.md`, `docs/api/models.md`, `docs/api/INDEX.md`, `CHANGELOG.md` |
| `documentation-flow` | Mermaid-Ablaufdiagramm für Reversal-Prozess | `docs/flows/posting-reversal-flow.md`, `docs/flows/README.md` |
| `documentation-business` | F019-Feature-Dokumentation für Endnutzer (DE+EN) | `docs/business/features/F019-buchungsstornierung.md`, `docs/business/features/F019-buchungsstornierung.en.md` |
| `documentation-readme-writer` | README.md um Reversal-Feature ergänzen | `README.md` |

---

## Phase 3 – Ergebnis

> Abgeschlossen: 2025-07  
> Status: ✅ Vollständig

### Erstellt / Aktualisiert

| Datei | Aktion | Umfang |
|-------|--------|--------|
| `CHANGELOG.md` | ✅ Neu erstellt | 38 Zeilen – Keep-a-Changelog-Format, Unreleased-Sektion mit Reversal-Feature |
| `docs/api/PostingsController.md` | ✅ Vollständig überarbeitet | 495 Zeilen – alle 13 Endpoints inkl. Reversal dokumentiert |
| `docs/api/models.md` | ✅ Ergänzt | +ReversalResultDto, ReversalValidationDto, erweiterte PostingServiceDto |
| `docs/api/PUBLIC_API.md` | ✅ Ergänzt | POST /reverse + GET /validate-reversal mit Fehlerbeispielen |
| `docs/api/INDEX.md` | ✅ Aktualisiert | Postings-Eintrag: GET → GET, POST |
| `docs/flows/posting-reversal-flow.md` | ✅ Neu erstellt | 239 Zeilen – Mermaid-Diagramme, Fehlerszenarien, Technical Notes |
| `docs/flows/README.md` | ✅ Aktualisiert | Eintrag für neuen Reversal-Flow ergänzt |
| `docs/business/features/F019-buchungsstornierung.md` | ✅ Neu erstellt | 79 Zeilen – Deutsche Endnutzer-Dokumentation |
| `docs/business/features/F019-buchungsstornierung.en.md` | ✅ Neu erstellt | 79 Zeilen – Englische Endnutzer-Dokumentation |
| `README.md` | ✅ Ergänzt | Reversal-Feature in Features, API-Hinweis, Known Issues, Changelog-Eintrag |
| `docs/documentation-plan-posting-reversal.md` | ✅ Erstellt | Dieser Plan |

### Offene Punkte

- **Bug:** `GetRelatedPostingsAsync` mit `GroupId == Guid.Empty` (Test L21 mit Skip markiert) – dokumentiert in CHANGELOG Known Issues
- **Kein Bestätigungsdialog** – in F019 und README als bekannte Einschränkung dokumentiert
- **13 Pre-existing Test Failures** – nicht durch dieses Feature verursacht, in CHANGELOG erwähnt
- **Controller-XML-Kommentar** für `ReversePosting()`: Transaktions-Semantik-Hinweis könnte noch ergänzt werden (niedriger Prio)
