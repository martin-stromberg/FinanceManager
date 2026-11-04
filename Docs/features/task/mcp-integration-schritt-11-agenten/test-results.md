# EP-11 — Testergebnisse

## Build
`dotnet build FinanceManager.sln` — 0 Fehler.

## Unit-Tests (FinanceManager.Tests)
`dotnet test FinanceManager.Tests` — **1918/1918 grün**.

Neue Tests:
- `McpSessionHubTests` (6): Fan-out-Isolation je Zugang, Multi-Subscriber,
  Cap, Dispose/Completion, Bounded-Overflow (DropOldest, frischeste Event
  überlebt), Session-Semantik unverändert.
- `McpAuditRetentionExecutorTests` (8): Stufen-Löschung (Short/Long-Tier),
  Marker-Eintrag mit Ranges, Verify intakt nach dokumentierter Kürzung,
  unerklärte Lücke = Bruch, nichts innerhalb der Frist, Marker immun gegen
  spätere Passes, terminale Requests vs. offene, Multi-Owner-Ketten,
  Range-Kompression.
- `AiActionExecutionEngineTests` (+3): Publisher sieht Executing→Executed,
  Failed mit Err_-Reason, Expired beim Sweep.
- `McpApprovalServiceTests` (+1): `GetAccessMonitoringAsync` aggregiert
  Queue-Tiefe/24h-Outcomes/Fehlerrate; Fremd-Zugriff → null.

## Integration (FinanceManager.Tests.Integration)
`dotnet test --filter McpEndpointTests` — **7/7 grün**.
- `Get_WithoutSseAccept_Returns405` (PAT, kein SSE → 405)
- `Get_SseWithoutSession_Returns404` (PAT+SSE-Accept, keine Session → 404)
- `Get_SseStream_OpensAndDeliversOwnRequestStatus` (Stream öffnet 200
  text/event-stream; Hub-Publish → `request-status`-Frame mit RequestId)
- Bestehend: 503 Kill-Switch, 401-Uniformität, PAT-Flow, JWT-Ablehnung.
