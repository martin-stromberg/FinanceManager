# Todo – Schritt 3: Sicherheits-, Audit- und Approval-Konzept

- [x] Projektverzeichnis lesen (project-plan.md Schritt 3 inkl. Akzeptanzkriterien, requirement.md F8–F10, inventory.md, Offene Punkte → Behandlung Annahme 2)
- [x] Vorgängerdokumente lesen und Kanten übernehmen (fachkonzept.md: Token-Lifecycle/Freigabestufen/Genehmigungsmodus; berechtigungsmodell.md: Prüfstufen 0–3/Meta-Scope; scope-und-toolkatalog.md: 4 Sicherheitsklassen, Übersichtstabelle, fm_task_status)
- [x] Branch verifiziert (`task/issue-470-…-schritt-3-sicherheit`)
- [x] Code-Anker verifiziert (`Pbkdf2IdentityPasswordHasher`, `IAlphaVantageSecretProtector` `dp:v1:`, `IpBlockMiddleware`/`IpBlock`, `RateLimitPolicies.AuthSensitive` FixedWindow, `RequestLoggingMiddleware` token-Redaction, `INotificationWriter` SystemAlert/EventDriven, `IBackgroundTaskManager` In-Memory, `OnTokenValidated` Security-Stamp, `BackgroundTasks:Enabled`, `AllowedHosts:"*"`, `ReverseProxy:KnownProxies`, `NotificationType`/`NotificationTarget`)
- [x] `Docs/planung/mcp-integration/sicherheitskonzept.md` erstellen (PAT-Auth, Autorisierung Stufen 0–3, Prompt-Injection, Notfallmaßnahmen, Rate Limiting, HTTP-LAN-Behandlung)
- [x] `Docs/planung/mcp-integration/approval-konzept.md` erstellen (Approval-Katalog, `AiActionRequest` + Zustandsmodell, Workflow, persistente Queue, Idempotenz, Fehlerbehandlung, Genehmigungscenter-UX)
- [x] `Docs/planung/mcp-integration/audit-konzept.md` erstellen (Ereigniskatalog, Detailtiefe, Retention, Auswertbarkeit, Verknüpfungskette, WORM/Hash-Verkettung)
- [x] Lifecycle-Artefakte anlegen (requirement.md, plan.md, review.md, test-results.md, todo.md)
- [x] Selbstprüfung gegen Akzeptanzkriterien (F8-Abdeckung, F9-Abdeckung, F10-Zustände, Konsistenz Approval-Liste ↔ Toolkatalog, Code-Referenzen real)
- [x] Commit `docs: Schritt 3 - Sicherheits-, Audit- und Approval-Konzept (MCP, Issue #470)` (ohne Push/Merge)
