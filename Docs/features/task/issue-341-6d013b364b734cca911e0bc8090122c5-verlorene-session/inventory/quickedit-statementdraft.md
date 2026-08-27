# QuickEdit und StatementDraft-ViewModels

## QuickEditTable

`FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor` rendert die editierbaren Felder fuer Buchungsdatum, Valuta, Betrag, Buchungsbeschreibung, Empfaenger und Verwendungszweck. Die Eingaben verwenden `oninput` fuer Text/Betrag und `onchange` fuer Datumsfelder. Tastaturaktionen wie F8 uebernehmen Werte aus der vorherigen Zeile.

Die Handler schreiben ausschliesslich ueber `SetEditValue` in das ViewModel. In der Komponente existieren keine `@onblur`-/`onblur`-Attribute, kein Ping und kein separater Keepalive-Serviceaufruf. Die lokale Eingabe bleibt damit unabhaengig von einem moeglichen Hintergrundrequest im DOM/ViewModel.

## ViewModel-Zustand

`FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs` haelt `_editValues` pro Entry-ID und Feld. `SetEditValue`, Reset-/Delete-Operationen und `CollectQuickEditSaveRequest` arbeiten auf diesem lokalen Zustand. Die Seite laedt die Draft-Eintraege ueber `StatementDrafts_GetAsync`; das Speichern erfolgt ueber den Batch-Update-/StatementDraft-API-Pfad.

Die vorhandene lokale Speicherung ist die Grundlage dafuer, dass ein Session-Ping keinen Re-Load der Tabelle und keine Ersetzung der Eingabewerte ausloesen darf. Bei einem API-Fehler waere zu pruefen, dass kein bestehender Reload- oder Redirectpfad die `_editValues` verwirft.

## GenericCardPage

`FinanceManager.Web/Components/Pages/GenericCardPage.razor` bindet editierbare Card-Felder an die jeweiligen ViewModel-/Provider-Operationen und verwendet `IApiClient`. Im untersuchten Code ist kein Blur-Ping oder allgemeiner Session-Keepalive-Hook vorhanden. Die Komponente ist daher ein moeglicher gemeinsamer Anschluss fuer aktive Interaktion, aber die Anforderung nennt fuer den konkreten Akzeptanzfall primaer `QuickEditTable`.

## API-Anschluss

Fuer einen Ping existiert im untersuchten QuickEdit-/StatementDraft-Code kein dedizierter API-Client-Endpunkt. Die Planung muss daher entweder einen vorhandenen sicheren, authentifizierten GET/POST-Endpunkt als Ping festlegen oder einen kleinen bestehenden Web/API-Anschluss ergaenzen. Der Ping muss Cookies mitsenden, darf keine Draft-Daten veraendern und muss die Response-Verarbeitung so gestalten, dass ein erneuertes Cookie automatisch im Browser landet.
