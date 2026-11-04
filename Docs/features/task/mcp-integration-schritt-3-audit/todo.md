# Todo – EP-03 Audit-Grundlage & Revisionssicherheit

- [x] Eingaben gelesen (audit-konzept, datenmodell §6–§8, architektur §7, product-backlog F-0301/F-0302, inventory)
- [x] `IMcpAuditWriter` erweitert (`StageAsync`, `VerifyAsync`) + `McpAuditChainVerification`-Result-Typen
- [x] `McpAuditWriter` (Kettenkopf + Semaphor, kanonisierte SHA-256-Kette, Secret-Redaction, Längenkappen, Bündel, Verify)
- [x] `NullMcpAuditWriter` an erweiterte Schnittstelle angepasst (bleibt für Tests/Referenz)
- [x] Gefilterter Unique-Index `SequenceNo WHERE OwnerUserId IS NULL` + Migration `AddMcpAuditGlobalChainIndex`
- [x] DI: `McpAuditWriter` registriert
- [x] `McpAccessService`: Stage-Then-Save-Kopplung aller schreibenden Verwaltungsaktionen (inkl. Retry-Detach)
- [x] `McpToolDispatcher`: Parameter-Payload, Warnlog statt Stillschweigen, `Err_Mcp_AuditFailed` für Schreibklassen
- [x] `McpAuthHandler`: Deny-Pfad-Audit in try/catch
- [x] Tests: `McpAuditWriterTests` (18), `McpAuditPipelineTests` (7) — 25 grün
- [x] `dotnet build FinanceManager.sln` grün; `dotnet test` 1636 + 155 grün
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [x] Commit + Push `task/mcp-integration-schritt-3-audit`

## Nachgelagert (andere EPs)
- [ ] `DataChange`/`ApprovalEvent`-Callsites in Write-Tool-Handlern (EP-06+) via `StageAsync` in der Wirktransaktion
- [ ] `AiActionRequest`-Zustandsketten + `RelatedRequestId`-Verknüpfung (EP-09)
- [ ] Audit-Ansicht/Export + „Integrität prüfen"-UI + Admin-Sicherheitssicht (EP-08)
- [ ] Rate-Limit-Aggregate + Notfallschalter-Ereignisse (SecurityEvent-Aggregation, EP-08+)
- [ ] Retention-Executor (`Mcp:Audit:RetentionDays:*`, `RetentionPurged`-Ereignis; Verify „ab diesem Eintrag"-Modus) (EP-11)
