# Übersetzte Anforderung – EP-05 Individuelle Rechte & Filter (Issue #483)

## Ausgangslage
Auf dem MCP-Fundament (Schritt 1: Zugänge, Token, Kill-Switches), dem Toolkatalog
(Schritt 2), der Audit-Grundlage (Schritt 3, `McpAuditWriter` + AccessEvent-Deltas)
und den Vorschlags-Tools (Schritt 4) wird jetzt die **Freigabestufe Individuell**
produktiv: explizite Scope-Tupel je Zugang, Matrix-Editor in der Zugangskarte,
Filterverwaltung über die fünf Filterdimensionen und die vervollständigte
Genehmigungsmodus-Verwaltung — F-0501…F-0504 aus
[product-backlog.md](../../../planung/mcp-integration/product-backlog.md) EP-05.

**Normative Quellen:**
`berechtigungsmodell.md` §3.2 (individuelle Anpassung), §4 (Konfliktverhalten,
OR/AND-Semantik), §5 (nur einschränken — Stufe-0-Owner, AccountShare-/weitere Deckel);
`ui-konzept.md` §4.3 (Freigabestufen + Matrix-Editor + Implikationshilfe +
Genehmigungsmodus), §4.4 (Filterverwaltung); `approval-konzept.md` §3.4
(vorab-genehmigter Modus) / §5 (Ausführungsbezug, Kill-Switch-Relation);
`scope-und-toolkatalog.md` (F3-Matrix).

## Vorhandene Basis (wird genutzt)
- `McpAccessService.SetScopesAsync`/`SetFiltersAsync` + `PUT …/scopes|filters/{scopeId}`
  + `levels`/`options` (Matrix, Kategorielisten, TLS-Flag) — bestanden, Tests ergänzt.
- `McpScopeMatrix` (F3-Wahrheit) + `McpLevelProfiles` (Standardprofile).
- `McpScopeEvaluator` Stufen 0–3 inkl. `AccountShare`-Deckel (laufzeitseitig, bereits
  produktiv) und `McpRecordScopeFilter` (Datensatzfilter, AND/OR-Semantik).
- UI `/list/mcp-accesses` + `/card/mcp-accesses/{id}` mit `McpAccessExtrasPanel`
  (bisher nur lesende Rechte-Anzeige) und `McpAccessCreateWizard` (Genehmigungsmodus-
  Radio ab Stufe Sachbearbeiter).
- AccessEvent-Audit `ScopesChanged`/`FiltersChanged` mit Vorher/Nachher-Delta.

## Funktionale Anforderungen

### FA-1: `PUT /api/mcp-accesses/{id}/scopes` vollenden (F-0501)
- Ersetzt das persistierte Profil eines Individuell-Zugangs; Validierung gegen die
  F3-„✓"-Matrix (`Err_Mcp_InvalidScope`) — nur Einschränkung, nie Erweiterung.
- **Implikationsregel nachgezogen:** jede Rechteart ≠ `Lesen` verlangt eine
  `Lesen`-Zeile auf demselben Datentyp im selben Profil
  (ui-konzept.md §4.3.2 — „Änderung ohne Lesen ist fachlich unsinnig"; die API lehnt
  unsinnige Kombinationen ab).
- Audit `ScopesChanged` mit Tupel-Delta (existiert); Standardstufen bleiben zeilenlos.

### FA-2: Rechte-Matrix-Editor (F-0502)
- 18 Datentypen × 7 Rechtearten; Zellen nur aktivierbar, wo die serverseitige
  Matrix (`GET …/options` → `AllowedRights`) die Kombination erlaubt; „—"-Zellen
  gesperrt mit Kurzbegründung (Buchung nur auf Entwürfen; Buchungen schreibgeschützt;
  Importe revisionssicher; Kontotypen Aufzählwerte).
- Stufe ≠ Individuell: nur lesend (Effektiv-Anzeige des Stufenprofils) +
  „Von Vorlage anpassen" (wechselt zu Individuell, seedet das bisherige Profil).
- Stufe Individuell: Zellen editierbar; Implikationshilfe (höherwertiges Recht
  selektiert `Lesen` automatisch, Entfernen von `Lesen` kaskadiert); Speichern via
  `IApiClient` PUT scopes; `IConfirmationService` (Warning) bei
  rechtserweiternden Änderungen; Stufenwechsel weg von Individuell verwirft
  Anpassungen (Bestehende Downgrade-Bestätigung greift).
- Bestehende Filterbelegung beibehaltener Tupel bleibt beim Speichern erhalten
  (kein stilles Verwerfen).

### FA-3: Filterverwaltung (F-0503)
- Je persistierter Scope-Zeile die 5 Dimensionen (Kontotypen; Kontakt-,
  Wertpapier-, Sparplan-, Anhangkategorien) mit Mehrfachauswahl — Kategorien
  namensbasiert aus `GET …/options` (Namen→Id-Auflösung); unbekannte bereits
  gesetzte Ids bleiben sichtbar/entfernbar.
- Speichern je Zeile via `IApiClient` PUT filters; Audit `FiltersChanged` (existiert).
- Klartext-Semantik: leer = unbeschränkt; Dimensionen UND-verknüpft, Zeilen
  OR-verknüpft; unklassifizierte Datensätze werden von gesetzten Filtern nicht
  erfasst; indirekte Wirkung (abhängige Daten) erklärt.
- Filter-Editor nur bei persistierten Zeilen (Stufe Individuell); bei ausstehenden
  Matrix-Änderungen gesperrt.

### FA-4: Genehmigungsmodus-Verwaltung (F-0504)
- Anzeige in der Rechte-Section: Modus + Klartext + Kill-Switch-Relation
  (`Mcp:WriteAccess:Enabled` kann selbst vorab genehmigte Ausführung global
  zurückhalten — der Modus erweitert nie Rechte).
- Bearbeitung weiterhin über das Karten-Feld `ApprovalMode`; Wechsel auf
  „dauerhaft vorab genehmigt" bei Profil mit `Buchung`/`Löschung` erfordert
  `IConfirmationService` (Critical). Wizard zeigt denselben Kill-Switch-Hinweis.
- Persistierung + AccessEvent-Delta (existiert).

## Abgrenzung
- Keine Ausführungswirkung des Genehmigungsmodus (Approval-Queue = EP-08); nur die
  Verwaltung + Hinweise.
- Kein Multi-Zeilen-Editor je Tupel — vorhandene Mehrfachzeilen werden beim
  Speichern verbatim erhalten, neue Tupel entstehen ungefiltert.
