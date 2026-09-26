# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Bestätigungsdialog liegt oberhalb geöffneter Overlays (Löschen von Anhängen wieder möglich) | Schritt 1+2: Klasse `confirm-dialog-layer` am `.split-center`-Container des `ConfirmDialog` (Zeile 7) + `.split-center.confirm-dialog-layer { z-index: 1100; }` in `app.css` | bUnit `ConfirmDialog_RendersDedicatedLayerClass` (CSS-Hook) + E2E `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` (Hit-Target-Nachweis) | Lücke — E2E-Test verwendet den Ribbon-Selektor `#Attachments`, der auf der Kontenkarte nicht existiert (`BankAccountCardViewModel` vergibt Id `"OpenAttachments"`, Zeile 277); der Test schlägt wie beschrieben am Ribbon-Klick fehl |
| Abbruchpfad über Overlay bleibt funktionsfähig (Abbrechen/Schließen) | Programmablauf „Abbruchpfade“ — unverändertes `ConfirmationService.Cancel()` | E2E `AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` (`.confirm-dialog-actions button.secondary`, Selektor existiert: `ConfirmDialog.razor` Zeile 16) | Abgedeckt |
| Backdrop-Klick bricht nur die Bestätigung ab, Overlay bleibt offen (Requirement Zeile 28, Offene Frage 3) | Programmablauf „Abbruchpfade“ Punkt 2 + Seiteneffekt „Backdrop-Interaktion“ — Verhalten explizit beibehalten | E2E `AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` (`page.Mouse.ClickAsync` auf Viewport-Ecke außerhalb des Dialogs) | Abgedeckt |
| Lösung gilt für alle Seiten/Overlay-Varianten, nicht nur `AttachmentsPanel` (Offene Frage 2) | `z-index: 1100` > alle inventarisierten Schichten (`.split-center`, `.modal-overlay`, `.modal-backdrop` = 1000; `.rsw-backdrop` = 900/901); DOM-Reihenfolge irrelevant | Repräsentative Abdeckung über die drei `AttachmentsPanel`-Szenarien, begründet im Plan | Abgedeckt |
| `theme.Dark.css` auf Konsistenz prüfen | Seiteneffekt „`theme.Dark.css`“: kein eigener `z-index` vorhanden, keine Änderung nötig — inventarisiert und im Plan vermerkt | Nicht erforderlich (kein z-index in der Theme-Datei) | Abgedeckt |
| Keine Änderungen an `ConfirmationService`, `AttachmentsPanel`-Logik, Attachment-API (Nicht-Anforderung) | Plan beschränkt sich auf `ConfirmDialog.razor`-Markup und `app.css`; bestätigt in Übersicht und Änderungen | Bestehende Service-/API-Tests bleiben unverändert gültig | Abgedeckt |
| Offene Frage 1: generisches z-index-System vs. gezielte Anhebung | Designentscheidung: gezielte Anhebung, kein gestaffeltes Ebenen-System (YAGNI) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] E2E-Happy-Path `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` ist wie beschrieben nicht ausführbar: Der Ribbon-Selektor `#Attachments` existiert auf `/card/accounts/{id}` nicht. `BankAccountCardViewModel` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`, Zeile 277) vergibt die Ribbon-Id `"OpenAttachments"`; `"Attachments"` vergeben nur `SecurityCardViewModel` (Zeile 383) und `ContactCardViewModel` (Zeile 300). Korrektur: Selektor `#OpenAttachments` verwenden oder den Test auf eine Kontakt-/Wertpapierkarte (`#Attachments`) umstellen — `AttachmentEntityKind` beim Seeding entsprechend anpassen (`Contact`/`Security` statt `Account = 5`).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Anhang aus geöffnetem Anhangs-Overlay löschen (CardPage-Pfad) | `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` | Lücke — falscher Ribbon-Selektor `#Attachments` (siehe oben) |
| Abbruch per Button über Overlay | `AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` | Abgedeckt |
| Abbruch per Backdrop-Klick über Overlay | `AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` | Abgedeckt |
| Anhangs-Löschen aus dem Listen-Overlay in `ListPage.razor` (Requirement nennt beide Hosts) | Kein separater Test geplant | Nicht erforderlich mit Begründung — identischer `ConfirmAsync`-Pfad, identischer `.split-center`-Container und identische Selektoren (`button.icon-btn.danger` in `AttachmentsPanel.razor` Zeile 118); die zentrale Schichtkorrektur am `ConfirmDialog` ist seitenunabhängig, die CardPage-Szenarien sind repräsentativ |
| Übrige `ConfirmAsync`-Aufrufer (`ContactMergePanel`, `AssignStatementOverlay`, `SecurityPriceImportPanel`, `HomeKpiGrid`, `.modal-overlay`-Dialoge) | Kein separater Test je Overlay-Typ geplant | Nicht erforderlich mit Begründung — im Plan dokumentiert und vom Anwender bestätigt; alle teilen denselben Confirm-Pfad und `z-index: 1000`-Schichten |

## Fehlende oder unvollständige Planbestandteile

- [ ] Testdetail in Schritt 4 / Tests-Tabelle korrigieren: Ribbon-Button-Id auf der Kontenkarte ist `OpenAttachments`, nicht `Attachments` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` Zeile 277; gerendert als `id="@item.Id"` in `Ribbon.razor` Zeile 70). Ohne Korrektur schlägt der geplante E2E-Test bereits beim Auslösen des Benutzerflusses fehl.

## Hinweise

- Verifiziert: `AttachmentEntityKind.Account = 5` existiert (`FinanceManager.Domain/Attachments/AttachmentEntityKind.cs` Zeile 41); `BrowserApiHelper.PostMultipartAsync` existiert (`FinanceManager.Tests.E2E/Helpers/BrowserApiHelper.cs` Zeile 200); `.confirm-dialog-actions` existiert (`ConfirmDialog.razor` Zeile 16); der bestehende E2E-Test `AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm` nutzt bereits `page.Locator("#Delete")` und `.confirm-dialog-actions button.secondary` — derselbe Id-basierte Ansatz funktioniert mit `#OpenAttachments`.
- Der Backdrop-Test per `page.Mouse.ClickAsync` auf eine Viewport-Ecke ist plausibel: `.split-center` hat `padding: 1.5rem`, die Ecke trifft den Confirm-Backdrop; durch `z-index: 1100` kann der Klick das darunterliegende Overlay nicht erreichen.
- Die seitenübergreifende Gültigkeit des Fixes ist korrekt begründet: `ConfirmationDialogHost` steht auf allen Seiten vor den Overlay-Containern (inventory/logic.md), ein eigener `z-index: 1100` macht die DOM-Reihenfolge irrelevant.
