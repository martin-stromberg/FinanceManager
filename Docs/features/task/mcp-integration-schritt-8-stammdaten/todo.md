# Todo – EP-08 Stammdaten-Pflege (Issue #486)

- [x] Spezifikation & Bestandscode analysieren (Katalog §3.4/§4.5, Backlog F-0801…F-0803, Service-Oberflächen, Audit-Kopplung)
- [x] `McpMasterDataGuards` — Referenzauflösung, Plausibilität, Record-Write-Filter, `ApprovalRequiredAsync`, Delta-Helfer
- [x] T22 `fm_contacts_manage` (create/update/addAlias/removeAlias; merge+delete approval-required)
- [x] T23 `fm_accounts_manage` (create/update/linked-IBANs; delete approval-required)
- [x] T24 `fm_savings_plans_manage` (create/update; archive approval-required)
- [x] T25 `fm_securities_manage` (create/update; archive approval-required)
- [x] T26 `fm_budgets_manage` (purpose/rule/override/category; deletes approval-required)
- [x] T27 `fm_categories_manage` (vier Kategorietypen; delete approval-required)
- [x] T28 `fm_attachments_manage` (upload/url/reference/update/setCategory/reassign; delete approval-required)
- [x] T29 `fm_reports_configuration_manage` (reportFavorite/homeKpi; deletes approval-required)
- [x] DI-Registrierung (8 Descriptor + 8 Handler)
- [x] Tests `McpMasterDataWriteToolsTests` (39 Fälle)
- [x] Build + dotnet test grün (1788/1788)
- [x] Lifecycle-Artefakte (requirement/plan/review/test-results/todo)
- [ ] Commit `feat: Stammdaten-Pflege - schreibvorbereitende Tools T22-T29` + Push

## Folgepunkte (nächste Schritte, nicht Teil dieses Commits)
- EP-09: `AiActionRequest`-Queue an `ApprovalRequiredAsync` anschließen
  (Parameter-Snapshot in `parameters` bereits vorhanden; `RelatedRequestId` im
  Audit-Contract vorgesehen). Danach: Buchungs-/Storno-Tools T20/T21/T31 (EP-10).
