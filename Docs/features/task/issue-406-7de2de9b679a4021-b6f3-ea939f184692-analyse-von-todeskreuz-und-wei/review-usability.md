# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TrendReversalWidget.razor (Umkehrsignale-Widget auf der Wertpapierkarte)
- **Erreichbarkeit** — Die Anforderung verlangt eine „explizite, kurze Ausgabe, wenn kein Umkehrsignal vorliegt" und einen klar benannten aktuellen Trendstatus (z. B. „Trend intakt"). Das Widget rendert jedoch gar nichts, sobald `Signals` leer ist (`TrendReversalWidget.razor`, Zeile 7: `@if (_loading || _data == null || _data.Signals is not { Count: > 0 })`). Damit ist für die Status `Intact` und `InsufficientData` auf der Wertpapierkarte nichts sichtbar: Die Anwenderin kann „Trend intakt – keine Umkehrsignale" nicht von „Funktion fehlt/Ladefehler" unterscheiden und auch nicht erkennen, dass zu wenig Kursdaten vorliegen. Die dafür bereits vorhandenen lokalisierten Texte (`Status_Intact`, `Status_Intact_Text`, `Status_InsufficientData`, `Status_InsufficientData_Text` in `TrendReversalWidget.de.resx`/`.en.resx`) sind in der UI unerreichbar.
  Empfehlung: Im Fall `Signals.Count == 0` eine kompakte einzeilige Box desselben Widget-Stils anzeigen, z. B. „Umkehrsignale — Trend intakt (keine Umkehrsignale erkannt)" bzw. „Zu wenig Daten", inklusive Info-Schaltfläche, damit Status und Bedeutung abrufbar bleiben.
- **Erreichbarkeit** — Die in der Anforderung geforderten Klassifikationen „kurzfristige Schwäche" vs. „strukturelle Trendwende" (Linienbruch) sowie „beginnender Abwärtstrend"/„Trendverlust"/„Seitwärtsphase mit Abwärtstendenz" (Marktstruktur) werden dem Anwender nirgends gezeigt. Die API liefert sie bereits (`TrendReversalDto.LineBreak.Classification`, `TrendReversalDto.MarketStructure.Classification`), das Widget zeigt aber nur die generische Stärke „schwach/mittel/stark" plus Signalname und Datum — weder in der Box noch im Erklär-Panel. Eine Anwenderin kann damit nicht erkennen, ob ein Linienbruch als kurzfristige Schwäche oder als strukturelle Trendwende eingestuft wurde.
  Empfehlung: Im Erklär-Panel je Signal eine lokalisierte Klassifikationszeile ergänzen (z. B. `LineBreak_Classification_*`, `MarketStructure_Classification_*` als neue resx-Schlüssel), da die Daten im DTO schon vorhanden sind.

## Geprüfte Interaktionen

- Wertpapier-Karteikarte öffnen und Umkehrsignale mit kurzen Erklärungen sehen (Widget unterhalb „Goldenes Kreuz", `CardPage.razor` Zeile 57) → unauffällig: Signalnamen, Stärke und Datum in Klartext, lokalisiert de/en; Datum kulturabhängig formatiert.
- Aktuellen Trendstatus bzw. den Fall „kein Umkehrsignal" bzw. „zu wenig Daten" erkennen → Befund vorhanden (Widget bei `Intact`/`InsufficientData` komplett ausgeblendet, siehe oben).
- Konkrete Einstufung eines Linienbruchs/einer Marktstruktur ablesen → Befund vorhanden (Klassifikationen nicht dargestellt, siehe oben).
- Bedeutungserklärungen und Berechnungsgrundlage abrufen → unauffällig: Info-Schaltfläche ⓘ mit `title`/`aria-label` öffnet Seitenpanel mit Status-, Signal- und Score-Erklärung, Formeltext und Disclaimer; etabliertes `ReturnSummaryWidget`-/`GoldenCrossWidget`-Muster (`rsw-*`-Klassen, Backdrop, `role="dialog"`).
- Trendwende-Hinweise im Setup ein-/ausschalten (`SetupNotificationsTab.razor` Zeile 45–51) → unauffällig: Checkbox mit verständlichem Titel („Trendwende-Hinweise aktivieren") und Hilfetext, `label for`-Zuordnung, identisches Muster wie die Golden-Cross-Checkbox; Dirty-Tracking und Speichern im ViewModel korrekt eingebunden.
- Hinweis auf der Startseite bei erkannten Umkehrsignalen erhalten → unauffällig: `SecurityPriceWorker` ruft `EvaluateAndNotifyAsync`; Ausgabe über `HomeNotifications` auf `Home.razor`; Texte lokalisiert mit Wertpapiername/Symbol statt technischer Kennungen (`TrendReversalLocalizer.*.resx`).
- Eingabe interner Kennungen (IDs, GUIDs, Codes) → unauffällig: nirgends erforderlich; `SecurityId` wird intern aus der Routen-`Id` übergeben, Benachrichtigungen nennen Wertpapiername und Symbol.
- Suche/Auswahl statt Kennungseingabe → unauffällig: keine Identifikationsaufgabe in der Anforderung, nichts zu suchen/auszuwählen.

## Geprüfte Dateien

- `FinanceManager.Web/Components/Shared/TrendReversalWidget.razor`
- `FinanceManager.Web/Components/Pages/CardPage.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupNotificationsTab.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupNotificationsViewModel.cs`
- `FinanceManager.Web/Resources/Components/Shared/TrendReversalWidget.de.resx`
- `FinanceManager.Web/Resources/Components/Shared/TrendReversalWidget.en.resx`
- `FinanceManager.Web/Resources/Services/TrendReversalLocalizer.de.resx`
- `FinanceManager.Web/Resources/Services/TrendReversalLocalizer.en.resx`
- `FinanceManager.Web/Services/TrendReversalLocalizer.cs`
- `FinanceManager.Web/Resources/Pages.resx` / `Pages.de.resx` / `Pages.en.resx` (neue Schlüssel `Notifications_TrendReversal*`)
- `FinanceManager.Web/wwwroot/css/app.ReturnSummaryWidget.css`
- `FinanceManager.Shared/Dtos/Securities/TrendReversalDto.cs` (UI-Vertrag)
- `FinanceManager.Web/Services/SecurityPriceWorker.cs` (Benachrichtigungs-Auslöser)
- `Docs/help/wertpapiermanagement/umkehrsignale.md` und `index.md`
