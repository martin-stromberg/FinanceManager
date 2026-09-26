# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Bestätigungsdialog liegt oberhalb geöffneter Overlays; Löschen von Anhängen wieder möglich | Schritt 1+2: Klasse `confirm-dialog-layer` am `.split-center`-Container des `ConfirmDialog` (Zeile 7) + `.split-center.confirm-dialog-layer { z-index: 1100; }` in `app.css` — verifiziert: `ConfirmDialog.razor` Zeile 7 trägt aktuell nur `split-center`; `.split-center` hat `z-index: 1000` (`app.css` Zeilen 409–417) | bUnit `ConfirmDialog_RendersDedicatedLayerClass` (CSS-Hook) + E2E `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` (Playwright-Hit-Target-Nachweis) | Abgedeckt |
| Korrekter Auslösepfad im E2E-Test (Lücke aus Lauf 1 behoben) | Kontaktkarte `/card/contacts/{id}` mit Ribbon-Selektor `#Attachments` — verifiziert: `[CardRoute("contacts")]` auf `ContactCardViewModel` (Zeile 15), Ribbon-Action `Attachments` Zeile 300 ruft `RequestOpenAttachments(AttachmentEntityKind.Contact, Id)`; `Ribbon.razor` Zeile 70 rendert `id="@item.Id"`; Upload-Endpunkt `POST /api/attachments/{entityKind}/{entityId:guid}` existiert (`AttachmentsController` Zeile 127), `AttachmentEntityKind.Contact = 2` (`AttachmentEntityKind.cs` Zeile 21) | E2E-Szenarien seeden Kontakt via `AccountsApiSeedHelper.CreateBankContactAsync` (`AccountsApiSeedHelper.cs` Zeile 128; Konstruktor `(page, databasePath, ownerUserId)` wie im Plan beschrieben) und Anhänge via `BrowserApiHelper.PostMultipartAsync` (`BrowserApiHelper.cs` Zeile 200) | Abgedeckt |
| Abbruchpfad über Overlay bleibt funktionsfähig (Abbrechen/Schließen) | Programmablauf „Abbruchpfade" — unverändertes `ConfirmationService.Cancel()` | E2E `AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` (`button.secondary` in `.confirm-dialog-actions` existiert: `ConfirmDialog.razor` Zeile 17) | Abgedeckt |
| Backdrop-Klick bricht nur die Bestätigung ab, Overlay bleibt offen (Requirement Zeile 28, Offene Frage 3) | Programmablauf „Abbruchpfade" Punkt 2 + Seiteneffekt „Backdrop-Interaktion" — Verhalten explizit beibehalten; `confirm-dialog-layer` deckt den Viewport ab und schirmt den Overlay-Backdrop gegen versehentliche Klicks ab | E2E `AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` (`page.Mouse.ClickAsync` auf Viewport-Ecke; `.split-center` hat `padding: 1.5rem`, die Ecke trifft den Confirm-Backdrop) | Abgedeckt |
| Lösung gilt für alle Seiten/Overlay-Varianten, nicht nur `AttachmentsPanel` (Offene Frage 2) | `z-index: 1100` liegt über allen inventarisierten Schichten (`.split-center`, `.modal-overlay`, `.modal-backdrop` = 1000; `.rsw-backdrop` = 900/901; `#blazor-error-ui` = 1000); DOM-Reihenfolge irrelevant, gilt seitenübergreifend | Repräsentative Abdeckung über die drei `AttachmentsPanel`-Szenarien; identischer `ConfirmAsync`-Pfad für alle Aufrufer, im Plan begründet | Abgedeckt |
| `theme.Dark.css` auf Konsistenz prüfen | Seiteneffekt „`theme.Dark.css`": kein eigener `z-index` vorhanden (nur Backdrop-Farbe, Zeilen 131–133), keine Änderung nötig — inventarisiert und im Plan vermerkt | Nicht erforderlich (kein `z-index` in der Theme-Datei) | Abgedeckt |
| Keine Änderungen an `ConfirmationService`, `AttachmentsPanel`-Logik, Attachment-API (Nicht-Anforderung) | Plan beschränkt sich auf `ConfirmDialog.razor`-Markup und `app.css`; bestätigt in Übersicht, Änderungen und Programmabläufen | Bestehende Service-/API-Tests (`ConfirmationServiceTests`, `AttachmentsControllerTests`, `AttachmentServiceTests`) bleiben unverändert gültig | Abgedeckt |
| Offene Frage 1: generisches z-index-System vs. gezielte Anhebung | Designentscheidung: gezielte Anhebung, kein gestaffeltes Ebenen-System (YAGNI) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Anhang aus geöffnetem Anhangs-Overlay der Kontaktkarte löschen: Dialog sichtbar/bedienbar, Bestätigung löscht | `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` (`/card/contacts/{id}` → `#Attachments` → `button.icon-btn.danger` in `AttachmentsPanel.razor` Zeile ~118 → Bestätigen in `.confirm-dialog-actions`) | Abgedeckt |
| Abbrechen im Dialog über Overlay: Dialog schließt, Overlay + Anhang bleiben | `AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` | Abgedeckt |
| Backdrop-Klick bricht nur die Bestätigung ab, Overlay bleibt offen | `AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` | Abgedeckt |
| Anhangs-Löschen aus dem Listen-Overlay in `ListPage.razor` (Requirement nennt beide Hosts) | Kein separater Test geplant | Nicht erforderlich mit Begründung — identischer `ConfirmAsync`-Pfad, identischer `.split-center`-Container und identische Selektoren; die zentrale Schichtkorrektur am `ConfirmDialog` ist seitenunabhängig |
| Übrige `ConfirmAsync`-Aufrufer (`ContactMergePanel`, `AssignStatementOverlay`, `SecurityPriceImportPanel`, `HomeKpiGrid`, `.modal-overlay`-Dialoge) | Kein separater Test je Overlay-Typ geplant | Nicht erforderlich mit Begründung — im Plan dokumentiert und vom Anwender bestätigt; alle teilen denselben Confirm-Pfad und `z-index: 1000`-Schichten |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- Die im ersten Lauf beanstandete Lücke ist behoben: Der Plan nutzt jetzt `/card/contacts/{id}` mit `#Attachments` (existiert nur auf Kontakt- und Wertpapierkarte) statt `#Attachments` auf der Kontenkarte — dort heißt die Aktion weiterhin `OpenAttachments` (`BankAccountCardViewModel` Zeile 277).
- Verifiziert, dass `#Attachments` im E2E direkt klickbar ist: `Ribbon.razor` (`BuildTabsToRender`, Zeilen 166–254) führt alle `UiRibbonTab`s zu **einem** Tab mit Gruppen zusammen (`singleTab`), alle Gruppen inkl. der `Attachments`-Aktion werden ohne Tab-Wechsel gerendert.
- `ShowConfirmations` ist Default `true` (`UserProfileSettingsDto` Zeile 21; `ConfirmationService` fällt bei API-Fehler ebenfalls auf `true`) — die geplanten E2E-Tests benötigen kein explizites Setzen der Benutzereinstellung; der bestehende E2E-Test `AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm` lief im Ausgangslauf bereits erfolgreich mit diesem Default.
- Klickbarkeits-Nachweis: Da bUnit kein CSS auswertet, ist der Playwright-Hit-Target-Check beim Klick auf den Bestätigen-Button (`button.btn.btn-primary`, `ConfirmDialog.razor` Zeile 18) der korrekte Nachweis für die Schichtreihenfolge — der Plan benennt das explizit.
