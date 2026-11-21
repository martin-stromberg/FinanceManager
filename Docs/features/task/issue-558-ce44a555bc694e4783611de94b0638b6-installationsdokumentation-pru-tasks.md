# Tasks: Server-Installationsdokumentation (Issue #558)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Dokumentation | `Docs/help/systemverwaltung-und-setup/server-installation.md` anlegen: Header-Konvention, Voraussetzungen, Artefakt-Bezug (Release-ZIP oder `dotnet publish`), Service-Account, Verzeichnislayout mit Schreibpfaden | Offen | — |
| 2 | Dokumentation | Basisteil: Environment-Datei mit Pflichtwerten (`ASPNETCORE_ENVIRONMENT`, `Jwt__Key`/`Issuer`/`Audience`/`LifetimeMinutes`) und `ValidateOnStart`-Hinweis beschreiben | Offen | — |
| 3 | Dokumentation | Basisteil: vollständige systemd-Unit mit Hardening (`User=`, `ProtectSystem=strict`, `ReadWritePaths`, `PrivateTmp`, `NoNewPrivileges`), `EnvironmentFile`, `Updates__ServiceName`-Hinweis und Aktivierung/`journalctl`/Smoke-Test `/health` | Offen | — |
| 4 | Dokumentation | Pfad A „ohne Reverse-Proxy": Listener (`Kestrel__Endpoints__Http__Url`/`ASPNETCORE_URLS`, Production-Default Port 5003), Firewall, `AllowedHosts`, `UseHttpsRedirection`/`UseHsts`-Hinweise | Offen | — |
| 5 | Dokumentation | Pfad B „mit Reverse-Proxy": Loopback-Bindung, Referenz-Proxy-Konfiguration (Caddy + nginx-Kurzhinweis), `X-Forwarded-*`, `ReverseProxy__KnownProxies`/`KnownNetworks`, `PathBase=/finance` + Domain-Root-Hinweis, `AllowedHosts`, End-to-End-Prüfung | Offen | — |
| 6 | Dokumentation | Abschlussabschnitt „Nächste Schritte" mit Verlinkung auf `installation.md` (und optional `troubleshooting.md`) | Offen | — |
| 7 | Verankerung | `index.md`-Inhaltsliste um `- [Server-Installation](server-installation.md)` vor „Installation & Konfiguration" ergänzen | Offen | — |
| 8 | Verankerung | `installation.md` Abschnitt „Voraussetzungen" um Rückverweis auf `server-installation.md` ergänzen | Offen | — |
| 9 | Verankerung | `bereitstellung.md` Abschnitt „Build und Veröffentlichungsartefakt" um Querverweis auf `server-installation.md` ergänzen | Offen | — |
| 10 | Code | `"server-installation.md"` in `HelpContentCatalog.TechnicalOnlyDocumentNames` (`HelpContentCatalog.cs:93-101`) ergänzen; kein `Topics`-Eintrag | Offen | — |
| 11 | Verifikation | `dotnet build FinanceManager.Web` (Help-Indizes/`help-assets.sha256` regenerieren) und `HelpContentCatalogTests` ausführen; gedanklicher Durchlauf beider Installationspfade | Offen | — |
