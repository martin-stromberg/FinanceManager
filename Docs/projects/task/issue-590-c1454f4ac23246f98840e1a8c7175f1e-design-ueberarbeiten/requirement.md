# Anforderung – Umsetzung beider Design-Varianten (Issue #590)

## Ziel

Beide genehmigten Design-Varianten der überarbeiteten Benutzeroberfläche werden als
**zwei unabhängige, alternative Implementierungen** umgesetzt. Für jede Variante
entsteht ein eigener Branch mit einem Pull Request gegen `staging`. Der Anwender
prüft beide Varianten mit einem produktiven Backup und genehmigt einen der PRs.

Ursprungsauftrag (Issue-Kommentar):

> „Mich interessieren beide Varianten. Erstelle bitte zwei Branches dafür. Setze
> beide Varianten um. … Hauptsache, die E2E-Tests sind jeweils vollständig und
> auch die Dokumentation jeweils an das neue Design angepasst. Ich werde mit Hilfe
> eines produktiven Backups die Funktionalität prüfen. Als Ergebnis soll es für
> jeden Branch einen PR gegen staging geben. Einer von denen wird dann genehmigt
> (falls die Umsetzung gelungen ist)."

## Verbindliche Design-Vorgaben

Die überarbeiteten Entwürfe nach Abnahme-Feedback sind verbindlich:

- `Docs/design/proposals/variantA/` – Variante A „Modernisierte Weiterentwicklung":
  breite Sidebar mit Labels, klassische Ribbon-Aktionsleiste mit Gruppen und
  Captions, Datentabellen, Dark-UI (`#0b0e13`/`#10131a`), Indigo-Akzent `#6d8dff`,
  Inter-Typografie.
- `Docs/design/proposals/variantB/` – Variante B „Vollständig neu gedacht":
  schlanke Icon-Rail statt Text-Sidebar, kontextuelle Top-Action-Bar (gleiche
  Aktionen), kartenbasierte Inhalte, Glas-Flächen, Teal/Violett-Akzente
  (`#2dd4bf`/`#8b5cf6`).
- `Docs/design/proposals/README.md` dokumentiert die Nachbearbeitung:
  kanonische 13er-Navigation, einheitliches FM-Logo, echte Ribbon-Aktionen je
  Ansicht, entfernte erfundene Features, Startseite mit Mitteilungen +
  konfigurierbaren KPI-Kacheln + Berichts-Favoriten.
- `Docs/design/inventory/` – verbindliche Ist-Struktur: alle 70 Ansichten als
  HTML/Screenshots inkl. echter Ribbon-Gruppen und -Aktionen je Ansicht.
- `Docs/features/task-issue-590-.../plan.md` – Umsetzungsplanung aus der
  Entwurfsphase (analysiert Bereiche, Komponenten, Risiken).

## Fachliche Anforderungen

1. **Keine neuen Features.** Nur bestehende Funktionalität wird neu gestaltet —
   keine erfundenen Menüpunkte, Statistiken, Sync-Features oder Aktiv-Flags.
2. **Funktionale Gleichwertigkeit.** Alle vorhandenen Ansichten und Aktionen
   bleiben erreichbar und funktionieren; der Anwender prüft mit einem
   produktiven Backup.
3. **Beide Varianten vollständig:** Shell (Navigation, Logo, Topbar),
   Aktionsleiste, alle Listen-, Karten-, Report- und Overlay-Ansichten im
   jeweiligen visuellen Stil.
4. **E2E-Tests vollständig** je Branch (`FinanceManager.Tests.E2E`, inkl.
   Abnahme-Screenshots).
5. **Dokumentation** je Branch an das neue Design angepasst (`Docs/help/`,
   ggf. Screenshots/Texte; README/Release Notes nach Lifecycle).
6. **PRs gegen `staging`** – einer pro Variante, mit Closing-Bezug zu Issue #590.

## Abhängigkeiten zwischen den Varianten

Die Varianten sind **wechselseitig exklusive Alternativen**: Sie ändern dieselben
UI-Flächen und werden niemals gemergt ineinander oder sequenziell aufeinander
angewendet. Jeder Umsetzungs-Branch basiert auf dem gemeinsamen Design-Branch
(= Projekt-Basisbranch, enthält Inventar + Entwürfe als Spezifikation).

## Akzeptanzkriterien

- Zwei Branches, je ein PR gegen `staging`.
- Build + Unit-/Integrations-Tests grün, E2E-Tests laufen; Abnahme-Screenshots
  erzeugt und sichtgeprüft.
- Bedienbarkeit mit produktivem Backup gegeben (kein Datenmodell-, kein
  Migrationseingriff nötig; UI-only).
- Dokumentation beschreibt das jeweilige neue Design.

## Explizite Nicht-Ziele

- Keine fachlichen Änderungen an Domain/Application/Infrastructure (kein
  Banking-Sync, keine neuen Felder, keine Migrations).
- Kein Merge der Varianten untereinander; kein Merge nach `staging` durch den
  Agenten — die Genehmigung entscheidet der Anwender.

## Offene Fragen

Keine — alle Klärungen erfolgten im Abnahme-Feedback (siehe Issue #590).
