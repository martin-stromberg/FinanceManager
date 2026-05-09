# Flow-Dokumentation

Technische Ablaufdokumente mit Mermaid-Diagrammen und Verweisen auf konkreten Code.

- [Import & Classification](./import-classification.md) – Import, Parserwahl, Klassifikation und Persistenz von Drafts.
- [Statement Draft Booking](./statement-draft-booking.md) – Validierung und Buchungsablauf von Draft-Entries.
- [Split / UploadGroup](./split-uploadgroup.md) – Parent/Child-Verknüpfung bei Split-Drafts und Gruppierung.
- [Posting Aggregates](./posting-aggregates.md) – Aktualisierung und Nutzung der Reporting-Aggregate.
- [Security-Preisabruf (Worker + Backfill)](./security-price-worker.md) – Konsistente Fehlerklassifikation und Benachrichtigung für `SecurityPriceWorker` und `SecurityPricesBackfillExecutor` inkl. `SecurityPriceProviderErrorUserMessageBuilder` (RateLimit/Transient/Providerfehler).
