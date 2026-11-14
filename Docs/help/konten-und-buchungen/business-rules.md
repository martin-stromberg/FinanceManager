← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — Business Rules

## Stornierung nur bei gültigem Zustand

**Beschreibung:** Eine Buchung darf nur storniert werden, wenn sie fachlich reversibel ist.

**Bedingungen:**
- Die Buchung existiert.
- Die Buchung gehört zum Benutzerkontext.
- Es bestehen keine fachlichen Sperrgründe.

**Verhalten:**
- Wenn alle Bedingungen erfüllt sind: Gegenbuchung wird erzeugt und verknüpft.
- Sonst: Es wird ein Validierungsfehler zurückgegeben.

**Umsetzung:** `PostingReversalService.CanReverseAsync` und `PostingReversalService.ReversePostingAsync`.

## Kontozugriff ist eigentümergebunden

**Beschreibung:** Konten- und Buchungszugriffe sind auf den jeweiligen Benutzer beschränkt.

**Bedingungen:**
- Authentifizierter Benutzerkontext muss vorhanden sein.

**Verhalten:**
- Wenn Eigentümer passt: Daten werden geliefert oder geändert.
- Sonst: Zugriff wird abgelehnt.

**Umsetzung:** `AccountsController` und `PostingsController` mit benutzerbezogenen Serviceabfragen.

## Sammelkonten verwalten mehrere IBANs

**Beschreibung:** Ein Sammelkonto kann mehrere verknüpfte IBANs für Unterkonten, Sparbriefe oder Sparpläne führen.

**Bedingungen:**
- Das Konto gehört dem angemeldeten Benutzer.
- Die verknüpfte IBAN ist für dieses Konto noch nicht vorhanden.

**Verhalten:**
- Wenn die IBAN neu ist: Sie wird dem Sammelkonto zugeordnet.
- Wenn die IBAN bereits existiert: Die Änderung wird abgelehnt.

**Umsetzung:** `AccountsController.GetLinkedIbansAsync`, `AccountsController.AddLinkedIbanAsync`, `AccountsController.RemoveLinkedIbanAsync` und `AccountService`.

## Buchungen benötigen mindestens eine Zielreferenz

**Beschreibung:** Eine Buchung (`Posting`) trägt selbst kein `OwnerUserId` — der Besitzerbezug ergibt sich indirekt über die referenzierten Aggregate (`Account`, `Contact`, `SavingsPlan`, `Security`, alle mit `OwnerUserId`). Damit kein verwaistes Posting ohne erreichbaren Besitzerpfad entstehen kann, muss mindestens eine der vier Zielreferenzen gesetzt sein.

**Bedingungen:**
- Bei der Erzeugung eines `Posting` über den Konstruktor.
- Bei jedem `INSERT`/`UPDATE` auf der `Postings`-Tabelle.

**Verhalten:**
- `new Posting(...)` mit vier `null`-Referenzen: `ArgumentException` (Fail-fast, alle Überladungen delegieren auf den Hauptkonstruktor).
- `INSERT`/`UPDATE` mit vier `NULL`-Referenzspalten: Ablehnung durch den Check-Constraint `CK_Postings_HasReference` — fängt auch den Guard-umgehenden `AssignBackupDto`-Pfad beim `SaveChanges` ab.
- Beim Backup-Restore werden Postings übersprungen, deren remappte Referenzen allesamt `null` sind (`LogWarning`), statt einen Waisendatensatz anzulegen.
- Mehrere gesetzte Referenzen sind zulässig — die Invariante lautet „mindestens eine", nicht „genau eine".

**Umsetzung:** `Posting`-Hauptkonstruktor (Guard), `AppDbContext` (`HasCheckConstraint("CK_Postings_HasReference", ...)`, EF-Migration `AddPostingReferenceCheckConstraint`), `SetupImportService.ImportVersion3` (Skip-Regel im Posting-Import).
