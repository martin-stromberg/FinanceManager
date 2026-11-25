# Design-Entwürfe Issue #590 — Stitch-Varianten

Zwei alternative Design-Varianten für die komplette FinanceManager-Oberfläche, generiert mit
Google Stitch auf Basis des Inventars unter `Docs/design/inventory/`.

## Varianten

| Variante | Ordner | Ansatz |
|----------|--------|--------|
| **A — Modernisierte Weiterentwicklung** | `variantA/` | Bewahrt die bestehende Struktur (breite Sidebar mit Labels, klassisches Ribbon mit Aktionsgruppen, Datentabellen). Verfeinert zu sauberem, zeitgemässem Dark-UI: neutrale Flächen (`#0b0e13`/`#10131a`), feine Borders, Indigo-Akzent (`#6d8dff`), Inter-Typografie, 8–10 px Radien, ruhige Tabellen. |
| **B — Vollständig neu gedacht** | `variantB/` | Gleiche Funktionalität, neue visuelle Sprache: schlanke Icon-Rail statt Text-Sidebar, kontextuelle Top-Action-Bar statt Ribbon (gleiche Aktionen!), kartenbasierte Inhalte auf geschichtetem Dark-Background, Glas-Flächen, Teal/Violett-Akzente (`#2dd4bf`/`#8b5cf6`), expressive KPI-Typografie. |

## Harte Randbedingungen (beide Varianten)

- Nur bestehende Funktionalität — keine neuen Features, Menüpunkte oder Statistiken.
- Dark-only, UI-Sprache Deutsch.
- Informationsarchitektur fix: Sidebar-Navigation (Start, Konten, Kontakte, Sparpläne,
  Wertpapiere, Kontoauszüge, Budget, Berichte, Benutzerverwaltung, KI-Zugänge,
  Einstellungen, Hilfe, Abmelden) + Aktionsleiste + scrollbarer Content.
- Desktop als Primär-Viewport; Mobile-Varianten für repräsentative Ansichten
  (`home`, `login`, `list-accounts`, `card-account`, `card-setup`, `reports-dashboard`).

## Struktur

```
proposals/
  manifest.json            Generierungsstatus je Ansicht/Variante/Viewport
  variantA/
    desktop/<view>.png|.html
    mobile/<view>.png|.html
  variantB/
    desktop/<view>.png|.html
    mobile/<view>.png|.html
```

Die `.html`-Dateien sind die von Stitch erzeugten Single-File-Referenz-Implementierungen
(Tailwind) und dienen der Detailbetrachtung — nicht als direkter Umsetzungs-Code.

## Vergleich

Jede Ansicht ist als PNG in beiden Varianten vorhanden und kann direkt nebeneinander
gelegt werden. Zusätzlich stehen die Ist-Zustände unter
`Docs/design/inventory/screenshots/{desktop,mobile}/` zur Verfügung
(Dateinamen der Inventar-Screenshots entsprechen den View-Ids, z. B. `list-accounts.png`).

## Generierung

```
cd scripts/stitch
node generate-proposals.mjs            # beide Varianten, Desktop
node generate-proposals.mjs --mobile   # + Mobile-Subset
node generate-proposals.mjs --variant b --only home,login
```

Der Lauf ist fortsetzbar: vorhandene PNGs werden übersprungen (`manifest.json`).
Der API-Key wird aus der Windows-Benutzervariable `GOOGLE_STITCH` gelesen und nie
protokolliert.

## Abnahme

Die Varianten werden im GitHub-Issue #590 vorgelegt; eine Umsetzung erfolgt erst nach
schriftlicher Freigabe einer Variante (siehe Tasks-Liste, Abnahme-Gate).
