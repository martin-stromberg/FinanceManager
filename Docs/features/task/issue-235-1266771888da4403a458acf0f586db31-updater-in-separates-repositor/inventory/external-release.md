# Externer Release

## Quelle

| Feld | Wert |
|------|------|
| Git-Repository | `https://github.com/martin-stromberg/msTools.Updater.git` |
| GitHub-Repository | `martin-stromberg/msTools.Updater` |
| Abfrage | `gh release view --repo martin-stromberg/msTools.Updater --json tagName,name,isPrerelease,isDraft,publishedAt,assets` |
| Abfragedatum | 2026-07-30 |

## Aktueller Release

| Feld | Wert |
|------|------|
| Tag | `v0.2.0` |
| Name | `v0.2.0` |
| Draft | `false` |
| Prerelease | `false` |
| PublishedAt | `2026-07-30T17:00:32Z` |

## Asset

| Feld | Wert |
|------|------|
| Name | `release.zip` |
| Content-Type | `application/zip` |
| Größe | `70778` Bytes |
| CreatedAt | `2026-07-30T17:00:31Z` |
| UpdatedAt | `2026-07-30T17:00:31Z` |
| SHA256 | `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c` |
| URL | `https://github.com/martin-stromberg/msTools.Updater/releases/download/v0.2.0/release.zip` |

## Relevanz für die Umsetzung

Das Release-Asset ist ein ZIP und keine direkt referenzierbare NuGet-Datei. Die Planungs-/Implementierungsphase muss daher:

1. `release.zip` herunterladen.
2. SHA256 gegen den Release-Digest prüfen.
3. ZIP-Inhalt inventarisieren.
4. Passende Assembly für das Target Framework der Anwendung (`net10.0`) auswählen.
5. Entscheiden, ob das ZIP unverändert, die entpackte DLL oder beides versioniert wird.

## Ablageoptionen

Im Repository ist keine bestehende Konvention für externe Binary-Artefakte erkennbar. Relevante Beobachtung:

- `Directory.Build.props` enthält `DefaultItemExcludes` für `**\artifacts\**`.
- `artifacts/` ist dadurch als Arbeits-/Build-Ausgabeverzeichnis behandelt und für dauerhaft referenzierte Dateien nur mit expliziten MSBuild-Includes geeignet.

Pragmatische Ablage für versionierte externe Bibliotheken:

```text
external/
└── msTools.Updater/
    └── v0.2.0/
        ├── release.zip
        ├── SHA256SUMS.txt
        ├── README.md
        └── lib/
            └── ...
```

Die konkrete DLL-Struktur hängt vom ZIP-Inhalt ab und muss nach dem Download bestätigt werden.

## Noch nicht geprüft

Das Asset wurde im Inventory nicht heruntergeladen oder entpackt. Nicht bekannt sind deshalb:

- Assembly-Name.
- Root-Namespace.
- Ziel-Frameworks im ZIP.
- Public API im Vergleich zu `SoftwareSchmiede.AutoUpdate`.
- Ob XML-Dokumentation oder zusätzliche Abhängigkeiten im ZIP enthalten sind.
