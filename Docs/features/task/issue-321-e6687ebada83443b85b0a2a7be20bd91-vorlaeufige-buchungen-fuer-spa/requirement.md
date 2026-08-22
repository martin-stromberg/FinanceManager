# Anforderung: Vorläufige Buchungen für Sparkonten

## Zusammenfassung

Für Sparkonten liegen echte Kontoauszüge oft erst mit erheblicher Verzögerung vor. Das System soll es ermöglichen, bereits bekannte Buchungen vorläufig zu erfassen, sie bis zum Eingang des realen Kontoauszugs in den relevanten Übersichten sichtbar zu machen und sie bei Buchung eines realen Kontoauszugs automatisch zu stornieren.

## Stakeholder-Ziel

Sparkontenbesitzer sollen bereits bekannte Zahlungen/Eingänge früher abbilden können, ohne den realen Kontoauszug schon zu besitzen. Bei Eingang des echten Auszugs werden die vorläufigen Posten neutralisiert und als storniert gekennzeichnet.

## Funktionale Anforderungen

### FA-1 Neuer Menüeintrag auf der Bankkonto-Detailseite

Auf der Detailseite eines Bankkontos muss im Ribbon-Menü eine neue Aktion **„Vorläufige Buchungen erfassen“** verfügbar sein.

### FA-2 Anlegen und Öffnen eines vorläufigen Kontoauszugs

Beim Ausführen der Aktion (FA-1) muss das System:

- Einen neuen Kontoauszug für das geöffnete Bankkonto anlegen.
- Den Beschreibungstext auf **„Vorl. Buchungen vom {Datum}“** setzen (übersetzbar).
- Das Merkmal **„Vorläufige Buchungen“** am Kontoauszug speichern.
- Den Kontoauszug automatisch im **Schnellbearbeitung**-Modus öffnen.
- Den Tastaturfokus in das Feld **Buchungsdatum** der Eingabezeile legen.

### FA-3 Vorläufig-Merkmal für Buchungen/Posten

Beim Buchen eines als **„Vorläufige Buchungen“** gekennzeichneten Kontoauszugs müssen alle daraus entstehenden Buchungen bzw. Posten das Merkmal **„Vorläufig“** erhalten.

### FA-4 Sichtbarkeit der Vorläufigkeit in Übersichten

Das Merkmal **„Vorläufig“** muss in folgenden Postenübersichten sichtbar dargestellt werden:

- Bankkonten
- Kontakte
- Sparpläne
- Wertpapiere

### FA-5 Automatische Stornierung vorläufiger Posten

Wird ein realer Kontoauszug ohne das Merkmal **„Vorläufige Buchungen“** gebucht, müssen für dasselbe Bankkonto:

- Alle bestehenden **vorläufigen Posten** dieses Bankkontos storniert werden.
- Die zugehörigen **Kontaktposten**, **Sparplanposten** und **Wertpapierposten** storniert werden.
- Die Beträge der stornierten vorläufigen Posten auf `0` (genullt) werden.
- Die Posten als **storniert** gekennzeichnet werden (darstellbar in den Übersichten).

### FA-6 Hinweis bei Buchung realer Kontoauszüge

Bei der Prüfung eines Kontoauszugs ohne Merkmal **„Vorläufige Buchungen“** muss das System einen Hinweis ausgeben, sofern für das Bankkonto vorläufige Buchungen existieren. Der Hinweis muss:

- Auf die bevorstehende Stornierung der vorläufigen Buchungen hinweisen.
- Einen Link zur Buchungsübersicht des Bankkontos enthalten.

## Rahmenbedingungen

- Die Funktion richtet sich insbesondere an Sparkonten mit verzögerten Kontoauszügen.
- Vorläufige Buchungen bleiben bis zur Buchung eines realen Auszugs sichtbar und nachvollziehbar.
- Die Stornierung erfolgt automatisch, ist aber transparent in den Übersichten erkennbar.

## Abgrenzung / Nicht im Scope

- Die Funktion ersetzt keine dauerhafte Buchung; sie dient der Zwischenabbildung.
- Eine automatische Übernahme der realen Buchungen aus dem neuen Kontoauszug in die vorläufigen Posten ist nicht vorgesehen.
