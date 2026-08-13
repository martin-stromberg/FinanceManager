# Ladeanimationen

## Ziel

Die Anwendung soll dem Benutzer bei langsamen Ladevorgängen unmittelbar anzeigen, dass eine Navigation oder das Absenden eines Formulars erkannt wurde und verarbeitet wird.

## Funktionale Anforderungen

### FA-01 Anzeige bei Navigation

Beim Verlassen einer Seite zur Navigation auf eine andere Seite wird eine Ladeanimation gestartet.

### FA-02 Anzeige beim Absenden von Formularen

Beim Absenden eines Formulars wird eine Ladeanimation gestartet, sofern dadurch ein Ladevorgang oder eine Navigation ausgelöst wird.

### FA-03 Form der Ladeanimation

Die Ladeanimation besteht aus genau einem schmalen, horizontalen Ladebalken.

### FA-04 Position auf Desktopgeräten

In der Desktopansicht wird der Ladebalken am oberen Rand der Seite platziert.

### FA-05 Position auf Mobilgeräten

In der mobilen Ansicht wird der Ladebalken am unteren Rand der Menüleiste platziert.

### FA-06 Animationsrichtung und Farbe

Der Ladebalken zeigt eine farblich animierte Bewegung von rechts nach links.

Bei jedem Navigationsklick wird die Farbe zufällig neu ausgewählt.

### FA-07 Sichtbarkeitsdauer

Der Ladebalken bleibt sichtbar, bis die Zielseite erreicht wurde beziehungsweise der ausgelöste Ladevorgang abgeschlossen ist.

### FA-08 Wiederholte Interaktionen

Wird derselbe oder ein anderer Link mehrfach hintereinander angeklickt, wird die bestehende Ladeanimation jeweils neu gestartet und erhält eine neue zufällig ausgewählte Farbe.

### FA-09 Einzelne Ladeleiste

Zu jedem Zeitpunkt wird höchstens ein Ladebalken angezeigt. Ein Neustart ersetzt beziehungsweise aktualisiert die bestehende Ladeanimation und erzeugt keine zusätzliche Ladeleiste.

## Nichtfunktionale Anforderungen

### NFA-01 Ressourcenverbrauch

Die Ladeanimation muss auch auf leistungsschwachen privaten Servern und Endgeräten ohne relevante Beeinträchtigung der Navigation funktionieren.

### NFA-02 Responsive Darstellung

Die Position des Ladebalkens muss abhängig von der Geräteansicht zwischen Desktop- und mobiler Darstellung wechseln.

## Abnahmekriterien

- Bei einer Navigation wird sofort ein schmaler horizontaler Ladebalken angezeigt.
- Beim Absenden eines Formulars mit anschließendem Ladevorgang oder einer Navigation wird sofort ein Ladebalken angezeigt.
- Der Ladebalken befindet sich auf Desktopgeräten am oberen Seitenrand.
- Der Ladebalken befindet sich auf Mobilgeräten am unteren Rand der Menüleiste.
- Die Animation bewegt sich sichtbar von rechts nach links.
- Jeder Navigationsklick startet die Animation mit einer zufällig gewählten Farbe neu.
- Der Ladebalken bleibt bis zum Erreichen der neuen Seite beziehungsweise bis zum Abschluss des Ladevorgangs sichtbar.
- Mehrere schnelle Klicks führen zu genau einem sichtbaren Ladebalken.
- Die Ladeanimation beeinträchtigt die Navigation auf leistungsschwachen Systemen nicht wesentlich.
