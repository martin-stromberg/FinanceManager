# Abnahmeprüfung – Entwicklungsschritt 1 (EP-01)

Geprüfter Stand: Branch `task/mcp-integration-schritt-1-fundament`, Korrektur-Commits
`9e5059c0` (fix: untrusted-data-Kennzeichnung und Scope-Validierung) und `8a0f11f4`
(Archivierung Runde-1-Bericht). Runde 2 nach Runde 1 (`acceptance-schritt-1.1.md`).
Prüfer: unabhängige Abnahme (nur Bericht, keine Änderungen).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

- [ ] Keine. Alle drei Punkte aus Runde 1 sind aufgelöst bzw. als dokumentierte Einschränkung zugeordnet.

## Hinweise

- **Runde 1, Punkt 2 – `untrusted_data`-Kennzeichnung (behoben):**
  T32 `fm_capabilities_describe` legt die benutzerbelegten Felder `name`, `description`,
  `allowedIpRanges` ausschließlich im `untrusted_data`-Wrapper unter `access`
  (`FmCapabilitiesDescribeTool.cs:78-83`); Systemfelder (`id`, `level`, `approvalMode`,
  `status`, `expiresUtc`, `isExpired`, `metaScope`) bleiben top-level. T33 `fm_task_status`
  verpackt `message`, `errorDetail`, `warnings`, `errors` je Task im Wrapper
  (`FmTaskStatusTool.cs:84-90`); `id`, `type`, `status`, `processed`, `total` und die
  Zeitstempel bleiben top-level. Kommentare verweisen auf sicherheitskonzept.md §5.2.
- **Runde 1, Punkt 3 – „Kein Tool ohne Scope" (behoben):** `McpToolRegistry`-Konstruktor
  validiert jeden `IMcpToolDescriptor` fail-fast: genau eines von `RequiredScopes`
  (≥1 Tupel) oder nicht-leerem `MetaScope` muss deklariert sein, sonst
  `InvalidOperationException` (`McpToolRegistry.cs:66-76`; XOR-Check `hasScopes ==
  hasMetaScope` deckt beide Lücken ab – weder Scope-los noch beides, wobei „beides" die
  Tupel-Prüfung im Dispatcher, `McpToolDispatcher.cs:86`, still umgehen würde). Start-up-
  Auflösung: `ProgramExtensions.cs:577` löst die Singleton-Registry (`ProgramExtensions.cs:440`)
  beim App-Start auf – ein ungültiger Katalog bricht den Start ab, nicht erst den ersten
  `/mcp`-Aufruf. Testabdeckung in `McpToolRegistryTests.cs`: wirft bei fehlendem Scope
  (Zeile 38), wirft bei beidem (Zeile 47), akzeptiert genau-eine-Variante und den
  Live-Katalog (Zeilen 59/76), Wrapper-Verifikation für T32/T33 (Zeilen 91/132),
  Ownership-Filter (Zeile 169).
- **Runde 1, Punkt 1 – SSE-Upgrade (dokumentierte Einschränkung, keine Abweichung):**
  `GET /mcp` bleibt bewusst 405; Request/Response + `fm_task_status`-Polling ist der
  Frühphasen-Pfad (architekturkonzept.md §3.1/§3.2, eigener Transport-Layer statt
  MCP-C#-SDK). Das SSE-Upgrade ist als F-1101 im Backlog von EP-11 eingeplant
  (`product-backlog.md:204`, `project-plan.md:84`).
- **Korrektur-Umfang:** `9e5059c0` = 8 Dateien, +314/−14 (2 Tool-Handler, Registry,
  Interface-Doc, Startup-Auflösung, 197 Zeilen neue Tests, review.md-Nachtrag,
  Berichts-Archivierung); `8a0f11f4` = reine Umbenennung des Runde-1-Berichts.
  Keine Scope-Überschreitung, keine unerwünschten Seitenänderungen; Working Tree sauber.
- **Teststand:** `dotnet test FinanceManager.Tests --filter Mcp`: **68/68 bestanden**
  (0 fehlgeschlagen; Runde 1: 61 + 7 neue Registry-/Wrapper-Tests). Feature-Abgleich
  F-0101–F-0108 aus Runde 1 (`acceptance-schritt-1.1.md`) unverändert gültig – die
  Korrekturen betreffen nur die drei gemeldeten Punkte.
- **Verbindlichkeit ab EP-02:** Die `untrusted_data`-Konvention ist für die Fach-Adapter
  (T01–T31) ab EP-02 weiter durchzuziehen (review.md, „Korrekturen Abnahme 1").
- **Nicht-E2E:** UI-Flows weiterhin nur manuell abgedeckt (Hinweis aus Runde 1 bleibt).
