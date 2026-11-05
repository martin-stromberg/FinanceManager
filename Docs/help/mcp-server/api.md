← [Zurück zur Übersicht](index.md)

# MCP-Server — API

## Übersicht

Es existieren zwei Schnittstellengruppen: der öffentliche MCP-Endpunkt `/mcp` (JSON-RPC 2.0 über Streamable HTTP, `McpEndpointExtensions`) sowie die Verwaltungs-APIs `McpAccessesController`, `AiActionRequestsController` und `McpAuditController` (JWT-geschützt wie die übrige REST-API). Ein `UserId`-Parameter existiert an keiner Stelle — der Besitzer wird aus dem Token beziehungsweise der Anmeldung aufgelöst.

## MCP-Endpunkt `/mcp`

### `POST /mcp`

**Beschreibung:** Nimmt einzelne oder gebündelte JSON-RPC-2.0-Nachrichten entgegen. Unterstützte Methoden:

- `initialize` — liefert Protokollversion und Server-Informationen inklusive `catalogVersion` und legt eine Transportsitzung an (`Mcp-Session-Id`-Antwort-Header).
- `ping` — Lebenszeichen.
- `tools/list` — Werkzeugkatalog (siehe unten).
- `tools/call` — Werkzeugausführung; erfolgreiche Aufrufe liefern `content` plus `structuredContent`, fachliche Werkzeugfehler `isError: true`.
- Notifications (z. B. `notifications/initialized`) werden ohne Antwort still angenommen.

**Authentifizierung:** `Authorization: Bearer fm_mcp_<hint>_<secret>` über das Scheme `McpPat`. Cookies und JWT werden nicht akzeptiert.

**Begrenzungen:** Request-Body maximal 1 MiB; Rate-Limit am Endpunkt sowie je Werkzeugklasse (Konfiguration `Mcp:RateLimiting`).

**Fehler (JSON-RPC-Codes):**

|| Code | Bedeutung |
||---|-----------|
|| `-32700` | Parse error (ungültiges JSON) |
|| `-32600` | Invalid request (u. a. Body zu groß, leere Batch) |
|| `-32601` | Method not found |
|| `-32602` | Invalid params (fehlender Werkzeugname u. a.) |
|| `-32603` | Internal error |
|| `-32000` | MCP-Protokollfehler mit `data.code` (Scope-/Deckel-Verweigerung, deaktivierter Schreibzugriff u. a.) |

### `GET /mcp` (Server-Sent Events)

**Beschreibung:** Mit `Accept: text/event-stream` und gültiger `Mcp-Session-Id` öffnet der Aufruf einen Ereignisstrom, der Statusänderungen der eigenen Anforderungen liefert (`request-status`-Events mit `requestId`, `status`, `toolName`, `actionKey`, `correlationId`, `failureReason`, `utc`) plus periodischem Heartbeat. Nur Anforderungen des authentifizierten Zugangs werden gesendet.

**Fehler:** `405` ohne SSE-Accept-Header; `404` bei unbekannter/abgelaufener Sitzung; `429` bei überschrittener Abonnentenzahl je Zugang.

### `DELETE /mcp`

**Beschreibung:** Beendet die Transportsitzung (`Mcp-Session-Id`).

### `tools/list`-Antwortschema

Je Werkzeug werden ausgegeben: `name` (`fm_*`), `description`, `inputSchema` (JSON Schema), `deprecated`, `replacedBy`, `sinceVersion`. Die Liste spiegelt den serverseitigen Katalog; die tatsächliche Ausführbarkeit eines Werkzeugs hängt zusätzlich von Scopes, Sicherheitsklasse und den globalen Schaltern ab.

### Meta-Werkzeug `fm_capabilities_describe`

Liefert das effektive Profil des eigenen Zugangs: Stufe, Scopes samt Filtern, Genehmigungsmodus, Status und Ablauf sowie die Katalogversion. Freitexte (Name, Beschreibung) werden als `untrusted_data` markiert ausgegeben.

### Werkzeug-Überblick nach Sicherheitsklasse

| Klasse | Beispiele |
|--------|-----------|
| `Lesend` | `fm_accounts_overview`, `fm_postings_query`, `fm_statement_drafts_read`, `fm_statement_draft_validate`, `fm_statement_imports_review`, `fm_budget_overview`, `fm_reporting_aggregate`, `fm_savings_plan_analysis`, `fm_securities_analysis`, `fm_contacts_lookup`, `fm_configuration_read`, `fm_attachments_read`, `fm_posting_reversal_check`, `fm_task_status`, `fm_capabilities_describe`, `fm_admin_status` |
| `Vorschlagend` | `fm_statement_draft_analyze`, `fm_statement_draft_prepare_booking`, `fm_contacts_suggest`, `fm_budget_rules_suggest` |
| `Schreibvorbereitend` | `fm_statement_draft_complete`, `fm_statement_draft_edit`, `fm_statement_draft_import`, `fm_contacts_manage`, `fm_accounts_manage`, `fm_budgets_manage`, `fm_savings_plans_manage`, `fm_securities_manage`, `fm_attachments_manage`, `fm_categories_manage`, `fm_reports_configuration_manage`, `fm_admin_updates`, `fm_admin_backups`, `fm_admin_notifications`, `fm_admin_settings` |
| `Genehmigungspflichtig` | `fm_statement_draft_book`, `fm_statement_draft_discard`, `fm_posting_reverse` |

Verbindliche Referenz ist stets die Laufzeit-Ausgabe von `tools/list` (Katalogversion `katalog-v<n>`); veraltete Werkzeuge sind dort über `deprecated`/`replacedBy` gekennzeichnet.

### Administrative Werkzeuge (`fm_admin_*`)

Die administrativen Werkzeuge T34–T38 sind zusätzlich zum Scope-Tupel `(Administration, …)` auf die Administratorrolle des Zugangsbesitzers beschränkt: Ein Zugang eines Benutzers ohne Admin-Flag erhält `Err_Mcp_AdminRequired` — bei interaktiven Aufrufen wie bei genehmigten Warteschlangen-Ausführungen.

| Werkzeug | Operationen | Wirkung |
|----------|-------------|---------|
| `fm_admin_status` | – (nur lesend) | Installationsstatus: Version, Update-Stand, letztes Backup, Benachrichtigungszähler, MCP-Schalter |
| `fm_admin_updates` | `status`, `check` | Update-Status bzw. manuelle Update-Prüfung (inkl. Paket-Download) |
| `fm_admin_updates` | `install`, `schedule` | Update-Installation (`confirmDowntime` erforderlich, startet die Anwendung neu) bzw. geplante Installationszeit — beide **genehmigungspflichtig** (`AiActionRequest`), Scope `(Administration, Änderung)` |
| `fm_admin_backups` | `list` | Gespeicherte Sicherungen des Besitzers (Paging) |
| `fm_admin_backups` | `create` | Neue Datensicherung anstoßen — **genehmigungspflichtig**, Scope `(Administration, Erstellung)`; Wiederherstellung/Upload/Download/Löschen werden nicht exponiert |
| `fm_admin_notifications` | `list`, `dismiss` | Eigene aktive Benachrichtigungen; `dismiss` quittiert eine eigene/globale Benachrichtigung direkt (unkritische Wirkung), Scope `(Administration, Änderung)` |
| `fm_admin_settings` | `get` | Admin-Einstellungen der Einrichtung lesen (`mcp`, `updates`, `wellKnown`) |
| `fm_admin_settings` | `set` | Begrenzte Felder ändern (`mcp.enabled`, `mcp.writeAccessEnabled`, `updates.enabled`, `updates.includePrereleases`, `wellKnown.changePasswordUrl`) — **genehmigungspflichtig**, Scope `(Administration, Änderung)` |

## Verwaltungs-API `/api/mcp-accesses`

Alle Operationen sind strikt auf den eigenen Benutzer begrenzt (JWT); Klartext-Token erscheinen ausschließlich in den Anlage- und Rotationsantworten.

| Methode | Pfad | Beschreibung |
|---------|------|--------------|
| `GET` | `/api/mcp-accesses/status` | Feature-Status (`Enabled`) zum Ein-/Ausblenden der Oberfläche |
| `GET` | `/api/mcp-accesses` | Eigene Zugänge (`q`, `skip`, `take` ≤ 200) |
| `GET` | `/api/mcp-accesses/{id}` | Zugangsdetail (`404` bei Fremdbezug) |
| `POST` | `/api/mcp-accesses` | Zugang anlegen; Antwort enthält das Klartext-Token genau einmal (`201`) |
| `PUT` | `/api/mcp-accesses/{id}` | Stammdaten, Stufe, Genehmigungsmodus, Ablaufdatum, IP-Bindung |
| `PUT` | `/api/mcp-accesses/{id}/scopes` | Scope-Profil ersetzen (nur Stufe `Individuell`) |
| `PUT` | `/api/mcp-accesses/{id}/filters/{scopeId}` | Filterdimensionen einer Scope-Zeile setzen |
| `POST` | `/api/mcp-accesses/{id}/activate` | Zugang aktivieren (sofort wirksam) |
| `POST` | `/api/mcp-accesses/{id}/deactivate` | Zugang deaktivieren (sofort wirksam) |
| `POST` | `/api/mcp-accesses/{id}/rotate-token` | Token rotieren; neuer Klartext einmalig in der Antwort |
| `DELETE` | `/api/mcp-accesses/{id}` | Zugang widerrufen (optionaler Grund im Body) |
| `GET` | `/api/mcp-accesses/levels` | Stufendefinitionen inklusive Standard-Scope-Profile |
| `GET` | `/api/mcp-accesses/options` | UI-Optionen: Scope-Matrix, Filterdimensionen, Kategorielisten, TLS-Flag |

## Genehmigungs-API `/api/mcp-approvals`

| Methode | Pfad | Beschreibung |
|---------|------|--------------|
| `GET` | `/api/mcp-approvals` | Eigene Anforderungen, neueste zuerst (Filter `status`, `accessId`, `toolName`; Paging) |
| `GET` | `/api/mcp-approvals/count` | Anzahl offener Anforderungen (Badge des Navigationseintrags) |
| `GET` | `/api/mcp-approvals/{id}` | Detail inklusive eingefrorenem Parametersatz, Auswirkungsvorschau und Begründungstext |
| `POST` | `/api/mcp-approvals/{id}/approve` | Genehmigen (`Pending → Approved`, optionaler Kommentar) |
| `POST` | `/api/mcp-approvals/{id}/reject` | Ablehnen (`Pending → Rejected`, optionaler Grund) |
| `POST` | `/api/mcp-approvals/{id}/cancel` | Eigene noch offene Anforderung zurückziehen |
| `POST` | `/api/mcp-approvals/batch` | Stapelentscheidung über 1–50 Anforderungen, Ergebnis je Element |
| `GET` | `/api/mcp-approvals/monitoring/{accessId}` | Agenten-Monitoring eines eigenen Zugangs (Warteschlangentiefe, 24-Stunden-Ergebniszähler) |

## Audit-API `/api/mcp-audit`

Reine Lese-API des eigenen Protokolls; ein Änderungspfad existiert nicht.

| Methode | Pfad | Beschreibung |
|---------|------|--------------|
| `GET` | `/api/mcp-audit` | Eigene Einträge (Filter `from`, `to`, `category`, `mcpAccessId`, `correlationId`, `toolName`, `result`; Paging) |
| `GET` | `/api/mcp-audit/{id}` | Eintragsdetail inklusive Nutzlast und Feld-Deltas |
| `GET` | `/api/mcp-audit/verify` | Integritätsprüfung der eigenen Hash-Kette |
| `GET` | `/api/mcp-audit/export` | Export `format=csv\|json` (max. 5.000 Zeilen; der Export selbst wird protokolliert) |
| `GET` | `/api/mcp-audit/usage/{accessId}` | Nutzungsübersicht eines eigenen Zugangs (Zähler, Top-Werkzeuge, letzte Aufrufe) |
| `GET` | `/api/mcp-audit/security-overview` | Installationsweite Sicherheitsübersicht, nur Metadaten — Rolle `Admin`, sonst `403` |
| `GET` | `/api/mcp-audit/settings` | Globale Schalter lesen — Rolle `Admin` |
| `PUT` | `/api/mcp-audit/settings` | Globale Schalter `Enabled`/`WriteAccessEnabled` setzen — Rolle `Admin` |

## Fehler

Typische Fehler: `401` bei ungültigem, deaktiviertem oder abgelaufenem Token; `404` bei fremden oder fehlenden Ressourcen (bewusst keine `403`-Unterscheidung im Benutzerkontext); `429` bei Überschreitung des Rate-Limits; `503` bei global deaktiviertem MCP-Endpunkt; `400` bei Validierungsfehlern (`Err_Mcp_InvalidArguments`, `Err_Invalid_format`).
