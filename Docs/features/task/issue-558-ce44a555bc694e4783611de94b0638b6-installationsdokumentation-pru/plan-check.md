# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Die `requirement.md` enthält keinen explizit benannten Akzeptanzkriterien-Abschnitt; die Kriterien wurden aus „Fachlicher Scope" und „Implementierungsansatz" abgeleitet.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Neue eigenständige Server-Installationsanleitung unter `Docs/help/systemverwaltung-und-setup/` | `server-installation.md` (Dateiname bestätigt, Offener Punkt #3), Umsetzungsschritte 1–4 | Akzeptanzszenarien 1–3 (Durchlauf), Szenario 5 (Build/Tests) | Abgedeckt |
| Zwei jeweils vollständige Pfade (ohne Proxy: HTTP direkt; mit Proxy: TLS-Terminierung, `PathBase`, `X-Forwarded-*`) | Basisteil (Schritte 1–8) + Pfad A (Schritte 1–5) + Pfad B (Schritte 1–6); Designentscheidung „Basisteil + zwei eigenständige Varianten-Abschnitte" sichert Vollständigkeit je Pfad | Akzeptanzszenario 1 (Happy Path ohne Proxy), Szenario 2 (Happy Path mit Proxy, inkl. `https://<host>/finance/health` und Client-IP/Schema-Kontrolle) | Abgedeckt |
| Leser ohne Vorwissen von „leerer Server" bis „läuft": Voraussetzungen, Artefakt-Bezug (Release-ZIP oder `dotnet publish`), Service-Account, Verzeichnis/Schreibpfade, Pflicht-Env (`ASPNETCORE_ENVIRONMENT`, `Jwt__*`), systemd-Unit mit Hardening, `Updates:ServiceName`, `/health`-Smoke-Test | Basisteil Schritte 1–8 vollständig abgebildet (inkl. `useradd --system`, `EnvironmentFile` mit `0600`, `ProtectSystem=strict`/`ReadWritePaths`/`PrivateTmp`/`NoNewPrivileges` mit Inline-Erklärung, `openssl rand -base64 48`, `journalctl`, Erststart-Schema per `ApplyMigrationsAndSeed`) | Akzeptanzszenario 1 (Ausgangszustand „leerer Linux-Server" → Dienst läuft, `financemanager.db` angelegt) | Abgedeckt |
| Pfad ohne Proxy: Listener/Port, Firewall, Erreichbarkeit, `UseHttpsRedirection`-/`UseHsts`-Verhalten, `AllowedHosts` | Pfad A Schritte 1–5 (Production-Default Port 5003 korrekt berücksichtigt, `ufw`/`firewalld` generisch, `AllowedHosts`-Einschränkung, Verweis auf Pfad B für TLS) | Akzeptanzszenario 1 | Abgedeckt |
| Pfad mit Proxy: Loopback-Bindung, TLS-Terminierung am Proxy mit Beispiel-Config, `X-Forwarded-*`-Weiterleitung, `PathBase`, `KnownProxies`/`KnownNetworks`, `AllowedHosts` | Pfad B Schritte 1–6 (Caddy-Referenz + nginx-Hinweisblock bestätigt, `PathBase=/finance` + Domain-Root-Hinweis, Warnung bei leeren Trusted Proxies mit `ForwardLimit=1`-Begründung) | Akzeptanzszenario 2; Fehlerszenario 4 (Forwarded-Header ohne Trusted-Proxy-Konfiguration) | Abgedeckt |
| Verlinkung neue Datei → `installation.md` (Folgeschritt) | Schritt 4 „Folgeschritt-Verlinkung abschließen" | Szenario 1 (Wirkung: Leser gelangt zum Folgeschritt) | Abgedeckt |
| Verlinkung `installation.md` → neue Datei (vorgelagerter Schritt), ohne Inhaltsduplikation | Schritt 6 (Abschnitt „Voraussetzungen" Zeilen 5–8, verifiziert); Scope-Ausschluss #2 | Szenario 1 (Ausgangszustand prüft Rückverweis) | Abgedeckt |
| Eintrag in `index.md`-Inhaltsliste | Schritt 5 (Eintrag vor „Installation & Konfiguration", Zeile 12 verifiziert) | Szenario 1 (Ausgangszustand prüft `index.md`-Listung) | Abgedeckt |
| `HelpContentCatalog.TechnicalOnlyDocumentNames` ergänzen; kein `Topics`/`SetupDocuments`-Eintrag | Schritt 8 (`HelpContentCatalog.cs:93–101` verifiziert — HashSet mit 6 Einträgen, `OrdinalIgnoreCase`); Designentscheidung begründet Einordnung | Szenario 5: `CatalogDocuments_ExistAndDoNotPublishTechnicalOnlyFiles` + `CatalogDocuments_DoNotContainTechnicalImplementationMarkers` + `Topics_ContainsReviewedUserHelpTopicsOnly` (13 Topics) bleiben grün | Abgedeckt |
| Keine Verweise auf andere/private Repositories; Anleitung steht für sich | Scope-Ausschluss #3 (bestätigt); alle systemd-/Proxy-Beispiele inline erklärt; Artefakt-Bezug verlinkt nur auf `bereitstellung.md` (eigenes Repo, kein Fremdverweis) | Gedanklicher Durchlauf | Abgedeckt |
| Querverweis aus `bereitstellung.md` (bestätigt über Offenen Punkt #3) | Schritt 7 (Abschnitt „Build und Veröffentlichungsartefakt", Zeilen 53–58 verifiziert) | Durchlauf-Abnahme | Abgedeckt |
| Gedanklicher/realer Durchlauf als Akzeptanzkriterium | Schritt 9 + Akzeptanzszenarien 1–3 | Explizit als „Nachweis" in den Szenarien geführt | Abgedeckt |
| Keine Änderung am Laufzeitverhalten / kein neuer Doc-Bereich / keine `/help`-Publikation / Linux+systemd als einziger voller Pfad / kein Kestrel-TLS-Pfad | Scope-Ausschlüsse #1, #4, #5, #6 — alle mit Begründung und Status `bestätigt (2026-11-21)` | Szenario 5 (Nicht-Erreichbarkeit über `/help`) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Installation gemäß Anleitung (beide Pfade) | — | Nicht erforderlich mit Begründung: Die Anforderung berührt keinen über UI oder Nutzeraktion erreichbaren Anwendungsablauf; es werden ausschließlich Markdown-Dateien plus ein statisches Ausschlusslisten-Literal geändert. Die Datei ist bewusst nicht über `/help` erreichbar — es existiert kein Browser-Fluss. Begründung im Plan-Abschnitt „E2E-Tests" ist vollständig und nachvollziehbar. |
| Katalog-Sichtbarkeit (Datei nicht publiziert) | — | Nicht erforderlich mit Begründung: Abgedeckt auf Unit-Ebene durch `HelpContentCatalogTests`-Verifikationslauf (Schritt 9, Szenario 5); kein UI-Fluss betroffen. |

## Hinweise

Optionale Punkte zur Nachplanung (keine Lücken, da nicht gefordert):

- `Jwt:MinimumRenewalWindowMinutes` (> 0, Default 5) wird im Basisteil nicht genannt; da der Default gültig ist, ist kein Setzen erforderlich — ein optionaler Hinweis in der Environment-Referenz wäre dennoch möglich.
- `Kestrel:Limits:MaxRequestBodySize` (20 MB) ist ein in der Bestandsaufnahme genannter Referenzpunkt, der in der Anleitung optional erwähnt werden kann.
- Bei der Caddy-Beispielkonfiguration ist das Präfix-Stripping präzise abzubilden (`handle_path /finance/*` vs. `handle`), damit `UsePathBase` und Proxy-Verhalten konsistent sind — Implementierungsdetail des Schreibvorgangs.
- Die Aufnahme in `TechnicalOnlyDocumentNames` folgt der expliziten Anforderung; `installation.md`/`demo-daten.md` stehen (bestehend) nicht in der Liste, obwohl sie nicht publiziert sind — die Konvention ist damit uneinheitlich, aber für diesen Plan verbindlich vorgegeben.
- Die 9 vorbestehenden E2E-Fehler (Bestandsaufnahme `tests.md`, überwiegend Playwright-Timeouts, mutmaßlich Folge des PathBase-Commits `44e9202c`/#557) sind im Risiko-Abschnitt korrekt als nicht zuzuordnend markiert; kein E2E-Gesamtlauf im Plan erforderlich, da kein Laufzeitverhalten geändert wird.
