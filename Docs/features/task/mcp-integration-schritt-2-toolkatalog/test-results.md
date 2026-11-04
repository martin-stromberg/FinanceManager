# Testergebnisse – EP-02 Read-Only-Toolkatalog

## Build
```
dotnet build FinanceManager.sln  →  erfolgreich, 0 Fehler
```
(XML-Dokumentation vollständig — CS1591 ist Warning-as-Error; keine Stubs.)

## Unit-Tests (`FinanceManager.Tests`)
```
dotnet test FinanceManager.Tests →  1609 erfolgreich, 0 fehlgeschlagen, 0 übersprungen (38 s)
dotnet test FinanceManager.Tests --filter "FullyQualifiedName~Mcp" →  87 erfolgreich
```

## Abgedeckte Kernfälle (neu, `Mcp/McpReadOnlyToolsTests.cs`)
- **Registry:** 15 Deskriptoren valide (Scope/Meta-Scope exklusiv, `fm_*`-Namen, Klasse
  `lesend`, valides JSON-Schema, `katalog-v1`); exakte Scope-Tupel je Tool aus
  scope-und-toolkatalog.md §3.1/§3.5.
- **Paging-Clamp:** `take` → 1–200, `skip` → ≥0 (`McpToolArguments`).
- **Fehlerbild:** malformed Args → `Err_Mcp_InvalidArguments`; T02 verlangt genau einen
  Kontext-Selektor.
- **untrusted_data:** Accounts (name/iban), Postings (subject/recipientName/description),
  Attachments (fileName + contentBase64 mit contentType/sha256), Drafts (fileName/description/
  Entry-Texte), Validierungsmeldungen, Import-Texte — jeweils ausschließlich im Wrapper.
- **Fremddaten:** Fremdkonto/Fremdanhang/Fremdentwurf → leere Menge bzw. `null` ohne
  Fehlerunterscheidung; T30 normalisiert „not found"/„not authorized" → `found:false`.
- **Record-Filter:** Kontotyp-Whitelist (Savings) blendet Giro-Bankpostings aus
  (`McpRecordScopeFilter` + Auflösung `AccountId`→`Account.Type`).
- **T08-Service:** `StatementImportQueryService` (EF InMemory): Besitz via
  `AccountId`→`Account.OwnerUserId`, Entries folgen Import-Besitz, Fremd→leer,
  `AccountType` denormalisiert, Take-Clamp.

## Integrationstests
Nicht erweitert: Der `/mcp`-Pfad ist hinter PAT-Auth + `Mcp:Enabled`-Flag; die fachliche
Korrektheit der Adapter ist auf Handler-/Service-Ebene (obige Tests) verifiziert.
Endpunkt-E2E (Token → tools/call) ist Kandidat für die Abnahme-Session.
