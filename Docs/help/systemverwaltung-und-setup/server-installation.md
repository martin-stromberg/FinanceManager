← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Server-Installation

Diese Anleitung beschreibt die vollständige Installation von FinanceManager auf
einem Linux-Server — vom leeren System bis zum laufenden Dienst. Sie richtet
sich an Betreiber ohne Projektvorwissen und deckt zwei Betriebsarten ab:

- **Pfad A — ohne Reverse-Proxy:** Die Anwendung ist direkt per HTTP auf einem
  eigenen Port erreichbar (z. B. im LAN oder Intranet).
- **Pfad B — mit Reverse-Proxy:** Ein vorgeschalteter Proxy terminiert TLS
  (HTTPS) und leitet Anfragen unter einem Unterpfad an die intern laufende
  Anwendung weiter.

Der Basisteil gilt für beide Pfade identisch; danach wird genau einer der
beiden Varianten-Abschnitte durchgearbeitet.

## Basisteil

### 1. Voraussetzungen

- Linux-Server mit systemd (alle gängigen Distributionen).
- Root- oder sudo-Zugriff für die Einrichtung.
- Die Werkzeuge `unzip`, `curl` und `openssl` müssen installiert sein; auf
  Minimalinstallationen gegebenenfalls nachinstallieren:

  ```bash
  # Debian/Ubuntu
  sudo apt install unzip curl openssl

  # RHEL/Fedora
  sudo dnf install unzip curl openssl
  ```
- Keine .NET-Runtime erforderlich — das Veröffentlichungspaket ist
  self-contained (Ziel-Framework `net10.0`) und enthält alle Laufzeitdateien.

> **Hinweis:** Für Windows-Server existiert ebenfalls ein
> Veröffentlichungspaket (`FinanceManager-vX.Y.Z-win-x64.zip`); diese
> Anleitung beschreibt ausschließlich den Linux-Pfad mit systemd.

### 2. Artefakt beschaffen

Entweder das fertige Release-Paket `FinanceManager-vX.Y.Z-linux-x64.zip` aus
den GitHub-Releases des Projekts laden (Aufbau der Pakete siehe
[Release und Bereitstellung](bereitstellung.md)) oder das Paket selbst bauen
und auf den Server übertragen:

```bash
dotnet publish FinanceManager.Web/FinanceManager.Web.csproj \
  -c Release -r linux-x64 --self-contained true \
  --output publish/linux-x64
```

Das Publish-Verzeichnis enthält die ausführbare Datei `FinanceManager.Web`.

### 3. Service-Account anlegen

Ein dedizierter, unprivilegierter System-Account ohne Login-Shell:

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin financemanager
```

- `--system` legt einen System-Account mit UID im Systembereich an.
- `--no-create-home` verzichtet auf ein Home-Verzeichnis; alle Daten liegen im
  Installationsverzeichnis.
- `--shell /usr/sbin/nologin` verhindert die interaktive Anmeldung. Der Pfad
  ist distributionsabhängig (teils `/sbin/nologin`); `which nologin` zeigt
  den korrekten Ort an.

### 4. Installationsverzeichnis einrichten

```bash
sudo mkdir -p /opt/financemanager
sudo unzip FinanceManager-vX.Y.Z-linux-x64.zip -d /opt/financemanager
sudo chown -R financemanager:financemanager /opt/financemanager
sudo chmod +x /opt/financemanager/FinanceManager.Web
```

Das Verzeichnis `/opt/financemanager` ist zugleich Arbeitsverzeichnis
(ContentRoot) des Dienstes und Ort der ausführbaren Datei.

### 5. Schreibpfade und Rechte

Die Anwendung schreibt zur Laufzeit in folgende Pfade:

| Pfad | Inhalt | Auflösung |
|------|--------|-----------|
| `financemanager.db` | SQLite-Datenbank; Schema wird beim ersten Start per Migration angelegt | relativ zum ContentRoot |
| `backups/` | Benutzer-Backups (`backups/<userId>/`) | unter dem ContentRoot |
| `updates/` | Arbeitsverzeichnis des Self-Updates (Pending-Paket, Staging, Lock) | unter dem ContentRoot |
| `logs/` | Datei-Logging `logs/app-{date}.log`, in Production aktiviert | relativ zum Publish-Verzeichnis |

Der ContentRoot entspricht dem `WorkingDirectory` der systemd-Unit
(`/opt/financemanager`); das Publish-Verzeichnis ist derselbe Ort, weil die
ausführbare Datei dort liegt. Alle Schreibpfade liegen damit unter
`/opt/financemanager` — da das Verzeichnis dem Service-Account gehört
(Schritt 4), sind die Dateirechte bereits korrekt.

Zusätzlich wertet die Anwendung beim Start ein optionales Verzeichnis
`init/` unter dem ContentRoot aus — ein vom Betreiber bereitgestelltes
Initialisierungsverzeichnis (Auto-Initialisierung, z. B. Setup-Importe).
Es wird ausschließlich gelesen; ist es nicht vorhanden, wird die
Initialisierung übersprungen. Als reines Lese-Verzeichnis kann es
gegebenenfalls auch read-only eingebunden werden.

Optional kann ein persistenter Pfad für den Data-Protection-Key-Ring gesetzt
werden (`DataProtection__KeysPath`, siehe Environment-Datei). Dieser Pfad
sollte ebenfalls unter `/opt/financemanager` liegen, damit die
Hardening-Regeln der systemd-Unit ihn abdecken.

### 6. Environment-Datei mit Pflichtwerten

Secrets und Umgebungsvariablen werden nicht inline in die systemd-Unit
geschrieben, sondern in eine separate Environment-Datei — so bleiben sie aus
`systemctl show`-Ausgaben und aus der Unit-Datei heraus, die gegebenenfalls
weiter verteilt wird:

```bash
sudo mkdir -p /etc/financemanager
sudo touch /etc/financemanager/financemanager.env
sudo chown root:root /etc/financemanager/financemanager.env
sudo chmod 600 /etc/financemanager/financemanager.env
```

Die Datei gehört `root` und ist nur für `root` lesbar (`0600`). systemd liest
sie beim Starten des Dienstes ein, bevor auf den Service-Account gewechselt
wird — der Service-Account selbst benötigt also keinen Lesezugriff.

Inhalt von `/etc/financemanager/financemanager.env`:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
Jwt__Key=<hier-generierten-schluessel-eintragen>
Jwt__Issuer=financemanager
Jwt__Audience=financemanager
Jwt__LifetimeMinutes=30
Updates__ServiceName=financemanager
# Optional: persistenter Pfad für den Data-Protection-Key-Ring
# DataProtection__KeysPath=/opt/financemanager/dataprotection-keys
```

- `ASPNETCORE_ENVIRONMENT=Production` aktiviert die Produktionskonfiguration
  (`appsettings.Production.json`), die Start-Validierung der
  JWT-Konfiguration sowie HSTS und produktive Fehlerseiten.
- `Jwt__Key` ist der HMAC-Signaturschlüssel für Anmeldetoken und muss
  mindestens 32 UTF-8-Bytes Entropie enthalten. Erzeugung auf der
  Kommandozeile:

  ```bash
  openssl rand -base64 48
  ```

  Der Schlüssel darf nicht in `appsettings`-Dateien oder im Repository
  abgelegt werden; bekannte Platzhalterwerte werden beim Start abgelehnt.
- `Jwt__Issuer` / `Jwt__Audience` müssen zu den ausgestellten Tokens passen;
  der Standard `financemanager` ist für eine eigenständige Instanz
  ausreichend.
- `Jwt__LifetimeMinutes` steuert die Token-Lebensdauer (produktionsnah
  maximal `1440`, Betriebsstandard `30`).
- `Updates__ServiceName=financemanager` teilt dem Self-Update den Namen des
  systemd-Dienstes mit, damit geplante Installationen den Dienst kontrolliert
  stoppen und starten können.

Die JWT-Werte werden beim Start validiert (`ValidateOnStart`): Fehlt
`Jwt__Key`, entspricht er einem bekannten Platzhalter oder enthält er
weniger als 32 UTF-8-Bytes, bricht der Anwendungsstart mit einem
Validierungsfehler ab — sichtbar in `journalctl -u financemanager`.

### 7. systemd-Unit anlegen

Datei `/etc/systemd/system/financemanager.service`:

```ini
[Unit]
Description=FinanceManager Web-Anwendung
After=network.target

[Service]
Type=simple
User=financemanager
Group=financemanager
WorkingDirectory=/opt/financemanager
ExecStart=/opt/financemanager/FinanceManager.Web
EnvironmentFile=/etc/financemanager/financemanager.env
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/opt/financemanager

[Install]
WantedBy=multi-user.target
```

Bedeutung der Direktiven:

- `User=` / `Group=` starten den Prozess als unprivilegierter
  Service-Account.
- `WorkingDirectory=/opt/financemanager` bestimmt den ContentRoot — dort
  liegen Datenbank, Backups, das Update-Arbeitsverzeichnis sowie optional
  das nur gelesene `init/`-Verzeichnis.
- `ExecStart=` verweist auf die ausführbare Datei aus dem Publish-Paket.
- `EnvironmentFile=` lädt die in Schritt 6 angelegte Datei mit Secrets und
  Konfiguration.
- `Restart=on-failure` (mit `RestartSec=5`) startet den Dienst nach einem
  Abbruch automatisch neu.
- `NoNewPrivileges=true` verhindert, dass der Prozess über setuid/setgid
  zusätzliche Rechte erlangt.
- `PrivateTmp=true` gibt dem Dienst ein eigenes, isoliertes `/tmp`.
- `ProtectSystem=strict` mountet das gesamte Dateisystem read-only für den
  Dienst.
- `ReadWritePaths=/opt/financemanager` nimmt das Installationsverzeichnis
  explizit von der Read-only-Regel aus — darunter liegen sämtliche
  Schreibpfade (Datenbank, `backups/`, `updates/`, `logs/`). Das nur
  gelesene `init/`-Verzeichnis benötigt keine Schreibrechte, ist bei
  Bedarf aber ebenfalls erreichbar.
- `After=network.target` (Unit-Sektion) startet den Dienst nach dem
  Netzwerk-Subsystem; `WantedBy=multi-user.target` (Install-Sektion)
  aktiviert ihn für den normalen Systemstart.

### 8. Dienst aktivieren und prüfen

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now financemanager
sudo systemctl status financemanager
journalctl -u financemanager -f
```

- `daemon-reload` liest die neue Unit ein.
- `enable --now` aktiviert den Autostart und startet den Dienst sofort.
- `journalctl -u financemanager` zeigt das Dienst-Log. Beim ersten Start
  legt die Anwendung das SQLite-Schema in `financemanager.db` automatisch
  per Migration an — der Erststart kann daher einige Sekunden dauern; die
  Datei ist anschließend im Installationsverzeichnis sichtbar. Die
  Migrationsmeldungen selbst erscheinen in der Produktivkonfiguration nicht
  im Log (`Microsoft.*`-Kategorien protokollieren dort erst ab `Warning`);
  als Erfolgsnachweis dient der Smoke-Test weiter unten.

Smoke-Test auf dem Server:

```bash
curl http://localhost:5003/health
```

Erwartete Antwort: `{"status":"ok"}`. Der Endpunkt `/health` (alternativ
`/api/health`) ist anonym erreichbar.

Damit ist der Basisteil abgeschlossen. Je nach Betriebsart folgt jetzt
entweder Pfad A oder Pfad B.

## Pfad A — Betrieb ohne Reverse-Proxy

Geeignet für LAN-/Intranet-Installationen, bei denen die Anwendung direkt per
HTTP erreichbar sein soll.

### A1. Listener konfigurieren

In `Production` lauscht die Anwendung per Voreinstellung auf allen
Netzwerkschnittstellen an Port **5003** (`Kestrel:Endpoints:Http:Url =
http://*:5003` aus `appsettings.Production.json`). Ein anderer Port wird über
die Environment-Datei gesetzt — Beispiel Port 8080:

```dotenv
Kestrel__Endpoints__Http__Url=http://*:8080
```

Die klassische Variable `ASPNETCORE_URLS` greift in diesem Setup **nicht**:
Die ausgelieferte `appsettings.Production.json` definiert bereits
`Kestrel:Endpoints`, und konfigurierte Endpunkte haben grundsätzlich Vorrang
vor der generischen `urls`-Konfiguration. Eine Port-Änderung erfolgt daher
immer über `Kestrel__Endpoints__Http__Url` wie oben gezeigt.

Nach jeder Änderung der Environment-Datei den Dienst neu starten:
`sudo systemctl restart financemanager`.

### A2. Firewall öffnen

Je nach Distribution — Beispiele für den gewählten Port (hier 5003):

```bash
# ufw (Debian/Ubuntu)
sudo ufw allow 5003/tcp

# firewalld (RHEL/Fedora)
sudo firewall-cmd --permanent --add-port=5003/tcp
sudo firewall-cmd --reload
```

### A3. Erreichbarkeit prüfen

Von einem anderen Rechner im Netz:

```bash
curl http://<server-adresse>:5003/health
```

Erwartete Antwort: `{"status":"ok"}`. Danach ist die Anwendung im Browser
unter `http://<server-adresse>:5003/` erreichbar.

`AllowedHosts` steht im Standard auf `*`. Deployment-spezifisch kann in der
Environment-Datei vorsorglich eine semikolongetrennte Liste der tatsächlich
verwendeten Namen/IPs hinterlegt werden:

```dotenv
AllowedHosts=meinserver;meinserver.local;192.168.1.10
```

> **Hinweis:** `AllowedHosts` wird in der aktuellen Version nicht
> ausgewertet — die zugehörige Host-Filter-Middleware ist in der Anwendung
> nicht registriert. Die Einstellung hat daher derzeit keine Wirkung und
> kann vorsorglich für künftige Versionen gesetzt werden.

### A4. Verhalten ohne TLS

- Die HTTPS-Umleitung ist in Production aktiv. Da kein HTTPS-Endpunkt
  konfiguriert ist, erscheint beim ersten eingehenden Request lediglich eine
  einmalige Warnung im Log („Failed to determine the https port for
  redirect."); HTTP-Anfragen laufen normal durch.
- HSTS-Header werden nur auf HTTPS-Anfragen gesetzt und bleiben auf reinem
  HTTP ohne Wirkung.

Für eine öffentliche TLS-Terminierung (HTTPS aus dem Internet) ist Pfad B
mit einem Reverse-Proxy vorgesehen; eine Zertifikatsverwaltung direkt in der
Anwendung ist nicht Teil dieser Anleitung.

### A5. Einordnung

Für die Nutzung im eigenen LAN oder Intranet ist Pfad A vollständig — mit
dem Smoke-Test aus A3 ist die Installation abgeschlossen. Soll die Anwendung
aus dem Internet erreichbar sein, wird Pfad B empfohlen.

## Pfad B — Betrieb hinter einem Reverse-Proxy

Geeignet für öffentlich erreichbare Installationen: Ein Reverse-Proxy
terminiert TLS (HTTPS) und leitet Anfragen an die intern auf Loopback
laufende Anwendung weiter. Als Referenzbeispiel dient Caddy mit eingebauter,
automatischer Zertifikatsverwaltung; ein kompakter nginx-Block steht als
Alternative.

### B1. Interne Bindung auf Loopback

Die Anwendung soll nur lokal erreichbar sein; der Proxy übernimmt den
externen Verkehr. In der Environment-Datei:

```dotenv
Kestrel__Endpoints__Http__Url=http://127.0.0.1:5003
```

Der Port 5003 wird in der Firewall **nicht** geöffnet — extern sichtbar sind
ausschließlich die Proxy-Ports 80 und 443. Nach der Änderung den Dienst neu
starten: `sudo systemctl restart financemanager`.

### B2. Reverse-Proxy einrichten

Referenzkonfiguration mit Caddy (Datei `/etc/caddy/Caddyfile`), die
Anwendung unter dem Unterpfad `/finance` der Domain `finance.example.com`:

```caddyfile
finance.example.com {
    @finance path /finance /finance/*
    reverse_proxy @finance 127.0.0.1:5003
}
```

- Caddy bezieht und erneuert das TLS-Zertifikat für `finance.example.com`
  automatisch (Voraussetzung: DNS-Eintrag zeigt auf die Server-IP, Ports 80
  und 443 sind von außen erreichbar).
- `reverse_proxy` leitet Anfragen unter `/finance` unverändert (inklusive
  Pfad-Präfix) an `127.0.0.1:5003` weiter und setzt dabei die Header
  `X-Forwarded-For` und `X-Forwarded-Proto` selbstständig.

Alternative mit nginx (Site-Konfiguration; Zertifikate z. B. über
certbot/Let's Encrypt):

```nginx
server {
    listen 443 ssl;
    server_name finance.example.com;

    ssl_certificate     /etc/letsencrypt/live/finance.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/finance.example.com/privkey.pem;

    location = /finance { return 301 /finance/; }

    location /finance/ {
        proxy_pass http://127.0.0.1:5003;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Bei nginx müssen die `X-Forwarded-*`-Header explizit gesetzt werden
(`proxy_set_header`); `proxy_pass` ohne Pfad-Anteil reicht die Original-URI
`/finance/...` an die Anwendung durch.

### B3. Vertrauenswürdige Proxies konfigurieren

Die Anwendung honoriert `X-Forwarded-For`/`X-Forwarded-Proto` nur von
eingetragenen Proxies — im sicheren Standard werden die Header vollständig
ignoriert. In der Environment-Datei die Adresse des Proxies eintragen:

```dotenv
ReverseProxy__KnownProxies=127.0.0.1
```

- Läuft der Proxy auf demselben Server (wie im Referenzbeispiel), ist
  `127.0.0.1` die richtige Adresse; bei einem Proxy auf einem anderen Host
  ist dessen IP (`ReverseProxy__KnownProxies`) bzw. dessen Subnetz
  (`ReverseProxy__KnownNetworks`, CIDR-Schreibweise) einzutragen. Mehrere
  Werte werden semikolongetrennt angegeben (z. B. `10.0.0.5;10.0.0.6` oder
  `10.0.0.0/24;fd00::/8`).
- **Wichtig:** Ohne Eintrag werden die Forwarded-Header ignoriert — die
  Anwendung sieht dann die Proxy-Adresse als Client-IP (Rate-Limiting und
  IP-Sperren greifen auf die falsche Adresse) und `http` als Schema
  (HSTS-Header und HTTPS-Verhalten sind falsch).

### B4. PathBase für den Unterpfad setzen

Damit Routen, Links und Weiterleitungen den Unterpfad korrekt abbilden, wird
`PathBase` als erste Middleware der Pipeline ausgewertet. In der
Environment-Datei:

```dotenv
PathBase=/finance
```

Der Wert muss zum Proxy-Mapping passen: Der Proxy reicht `/finance/...`
unverändert an die Anwendung, `PathBase=/finance` entfernt das Präfix für
das interne Routing. Nach der Änderung den Dienst neu starten.

> **Hinweis:** Soll die Anwendung direkt unter der Domain-Wurzel laufen
> (z. B. `https://finance.example.com/` statt `https://example.com/finance/`),
> entfällt `PathBase` — der Proxy mappt dann `/` auf den internen Port
> (`reverse_proxy 127.0.0.1:5003` ohne Pfad-Matcher).

### B5. AllowedHosts setzen (optional)

Hinter dem Proxy kann vorsorglich der öffentliche Hostname hinterlegt
werden:

```dotenv
AllowedHosts=finance.example.com
```

> **Hinweis:** `AllowedHosts` wird in der aktuellen Version nicht
> ausgewertet, da die Host-Filter-Middleware in der Anwendung nicht
> registriert ist — die Einstellung hat derzeit keine Wirkung und ist als
> Vorbereitung für künftige Versionen zu verstehen. Der lokale Smoke-Test
> (`curl http://localhost:5003/health`) bleibt davon unberührt und liefert
> weiterhin `{"status":"ok"}`.

### B6. End-to-End-Prüfung

```bash
curl https://finance.example.com/finance/health
```

Erwartete Antwort: `{"status":"ok"}` über TLS. Danach im Browser
`https://finance.example.com/finance/` aufrufen und die Anwendung bedienen.

Prüfpunkte für den Unterpfad-Betrieb:

- Die Adresszeile bleibt bei jeder internen Navigation unter `/finance/` —
  Klicks in der seitlichen Icon-Leiste (z. B. „Konten") führen zu
  `…/finance/list/accounts`, nicht zu `…/list/accounts`.
- Nach dem Anmelden ist die Oberfläche interaktiv (Ribbon-Aktionen,
  Suche, Detailansichten reagieren sofort) — die Live-Verbindung läuft
  ebenfalls unter dem Präfix.
- Ein direkter Aufruf einer geschützten Seite ohne Anmeldung leitet auf
  `…/finance/login?returnUrl=…` weiter — nicht auf `/login` der
  Domain-Wurzel.

Kontrolle der Header-Übernahme: Client-IP und `https`-Schema müssen korrekt
bei der Anwendung ankommen — sonst greifen Rate-Limits auf die
Proxy-Adresse oder das Schema wird als `http` erkannt. Das typische
Fehlerbild und die Behebung beschreibt der Abschnitt „Hinter einem
Reverse-Proxy stimmen Client-IP oder Protokoll nicht" in der
[Fehlerbehebung](troubleshooting.md).

## Nächste Schritte

Nach erfolgreichem Server-Setup geht es mit der Einrichtung innerhalb der
Anwendung weiter:

- [Installation und Konfiguration](installation.md) — Erstbenutzer-/
  Administrator-Registrierung, Setup-Seite und optionale Konfiguration
  (Self-Update, AlphaVantage-Schlüssel u. a.).
- [Fehlerbehebung](troubleshooting.md) — bei Problemen mit
  Reverse-Proxy-Headern, Rate-Limits oder `security.txt`.
