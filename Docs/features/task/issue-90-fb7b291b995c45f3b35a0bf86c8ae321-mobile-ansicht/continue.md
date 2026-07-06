# Continue

- [x] in den Einstellungen muss die Tabelle der Anhänge, Sicherungen, IP-Sperrliste an mopbile Seiten angepasst werden. Wir sollten hier mit "Stacked Cards" arbeiten.
- [x] Die Filtermöglichkeiten in den Übersichten muss an mobile Ansichten angepasst werden. (bspw. Sparpläne) Das Sucheingabefeld und auch das Bis-Datum ragen über den rechten Rand hinaus.
- [x] Auf der Detailansicht eines Sparplans sorgt das Balkendiagramm für eine horizontale scrollmöglichkeit. können wir nur die balken des diagramms scrollbar machen, so dass das Panel drum herum an die seite engapsst ist?
- [x] Auf der Detailansicht eines Budgeteintrags sind die Überschriten der Eigenschaften unschön bis zum rechten Rand platziert. Die 100% Breite in den CSS_Eigenschaften im folgenden Block sorgen dafür.
	@media (max-width: 900px) {
    .card-view-responsive .card-table th {
        width: 100% !important;
        display: block;
        padding-bottom: .15rem;
    }
  }
- [x] Der Bedgetbericht ist nicht für mobile Ansichten optimiert. Beide Tabellen sorgen jeweils dafür, dass die ganze seite horizontal scrollbar ist. Bekommen wir das schöner hin?
- [x] Genauso sind die Berichte nicht mobil optimiert. Auch hier sind es die tabellarischen Ansichten und das Diagramm.