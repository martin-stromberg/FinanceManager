# Testergebnisse – EP-09 Teil A

## Neue Tests (53)

| Datei | Inhalt |
|---|---|
| `Mcp/AiActionRequestTests.cs` (17) | Zustandsmaschine: alle erlaubten Übergänge, verbotene Übergänge (Terminal, Rejected→Approved, Pending→Executed, unclaimed), Claim/ReleaseClaim, WithdrawBySystem, Expire aus Pending+Approved, PreApproved-Fabrik, BackupDto-Roundtrip (Claim gelöscht) |
| `Mcp/McpApprovalServiceTests.cs` (13) | Enqueue Pending + gekoppeltes ApprovalRequested-Audit + Notification; IdempotencyKey-Dedup; Pending-Limit → `Err_Mcp_PendingLimit`; PreApproved → sofort Approved ohne Notification; Approve/Reject/Cancel + Audit; Owner-Isolation (Get/List/Fremd-Approve); CountPending; WithdrawOpenForAccess (claimed/terminal unberührt) |
| `Mcp/McpApprovalGateTests.cs` (10) | Entscheidungsmatrix Klasse×Modus (lesend/vorschlagend direkt, schreibvorbereitend PerAction→Request/PreApproved→direkt, genehmigungspflichtig immer Request), Write-Stop-Denial je Klasse, Pending-Limit `Err_Mcp_PendingLimit`, Fremd-Pending zählt nicht |
| `Mcp/AiActionExecutionEngineTests.cs` (13) | Ausführung über `IMcpToolHandler` im `ApprovedExecution`-Modus (Mode+Access verifiziert), Executed+ResultJson+Audit-Kette; Pending/claimed/terminal nicht gepickt; stale Claim reclaim (AttemptCount 2); McpToolException → klassifizierte FailureReason; unbekannte Exception → `Err_Internal`; widerrufener Access → Withdrawn; verlorenes Scope → `Err_RightsRevoked`; Write-Stop → Claim-Release, Approved bleibt; Expiry-Sweep Pending+Approved→Expired, claimed/terminal/future unberührt, ApprovalExpired-Audit |

## Lauf

```
dotnet test FinanceManager.Tests — gesamt: 1851, fehlgeschlagen: 0, erfolgreich: 1851
dotnet build FinanceManager.sln — 0 Fehler (nur vorbestehende Warnungen)
```

Hinweis: Engine-Tests laufen gegen SQLite in-memory (`EnsureCreated`), weil der
atomare Claim `ExecuteUpdateAsync` benötigt — der EF-InMemory-Provider
unterstützt ihn nicht.
