# Projektplan-Prüfung (Durchlauf 1, archiviert)

## Ergebnis

**Status:** Projektplan lückenhaft

## Festgestellte Lücken (vollständiger Bericht im Sitzungsprotokoll; hier kondensiert)

1. **Interner Widerspruch Schritt 4 (Grant-Rechte-Satz):** Umfang verlangte „Read/Suggest-Artige sowie Book/Delete nicht zulässig" — würde Read-Grants verbieten, Akzeptanzkriterium verlangt aber `(Administration, Read)`-Grant. Korrektur: freigebbar `Read|Complete|Modify|Create`, nicht freigebbar `Suggest|Book|Delete`.
2. **`dismiss` widersprach Requirement-Annahme:** Plan sagte „direkt-mit-Scope-Check wie heute"; unter PerAction queued der Aufruf heute faktisch am Gate. Katalogisiert als genehmigungspflichtige Operation `(Administration, Modify)` → bei Granular ohne Grant Pending, mit Grant dokumentiert auto-Approved.
3. **Prozess-Deliverables fehlten:** Statuskommentare je Issue und Screenshots als Beleg nicht verankert → als Abschlussarbeiten ergänzt.
4. **Schritt-2-Subtilität:** `McpRecordScopeFilter.ApplyAsync` läuft heute nach `Skip/Take` auf das Paging-Fenster; Statistik erfordert Umordnung (Record-Filter auf ungepagte Menge, danach Paging; Preis-Batch über volle sichtbare ID-Menge) — explizit im Schritt benannt.
5. **Hinweis übernommen:** DecisionNote-String als Diskriminator fragil → explizites `PreApprovalKind`-Feld am `AiActionRequest` (Enum, short-persistiert, BackupDto-Default).
6. **Hinweis übernommen:** FX Unique-Index um `Source` erweitern; parallele Lazy-Fills derselben Währung absichern.
7. **Hinweis dokumentiert:** MCP-Backup-Sektionen werden exportiert, nicht importiert (Ist-Konvention); Abweichung zur AGENT.md-Importregel in `datenmodell-erweiterungen.md` §10 zu begründen.

## Abgleich Anforderung ↔ Entwicklungsschritte

Nahezu vollständige Abdeckung aller Muss-Kriterien (#560: includeStatistics mit byCurrency/byCategory/positionsWithoutPrice/asOf; #561: valueEur/fxRate/EZB/Offline-null/Backup; #562: priceErrorMessage untrusted + Doku; #578: Modus/Granularität/Gate/Defaults/Deckel/Audit/Persistenz/UI/Tests). Verifizierte technische Annahmen: `EvaluateAsync` ohne actionKey (McpApprovalGate.cs:42), `GetLatestPricesAsync` vorhanden (SecurityPriceService.cs:126), Fehlerfelder auf Entity+BackupDto aber nicht SecurityDto, Admin-Sub-Op-Tabellen korrekt, `McpScopeMatrix` Administration→Read/Modify/Create.

## Abhängigkeitsprüfung

Azyklisch; alle Referenzen auf frühere Schritte; Schrittbeschreibungen als Lifecycle-Input geeignet (Schritt 4 mit genanntem Widerspruch).

## Hinweise

- Statistik-In-Memory-Ansatz schlüssig (volle Menge bereits geladen; einzige Zusatzabfrage Preis-Batch).
- FX-Design schlüssig (Division statt Multiplikation benannt; persistenter Cache mit dokumentierter OwnerUserId-Ausnahme; Präzedenz McpSettings/WellKnownSettings).
- `McpApprovalMode` via `HasConversion<short>` persistiert, EnumCoverageTests-Pinning + Hook + Resx-Pflege im Plan berücksichtigt.
