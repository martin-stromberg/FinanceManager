# Testplan: Posting-Stornierung (Reversal)

> **Basis:** `docs/tests/reversal-coverage-gaps.md`  
> **Stand:** 2025-06  
> **Branch:** `140-buchung-rückgängig-machen`  
> **Framework:** xUnit · FluentAssertions · Moq · NullLogger · WebApplicationFactory  
> **Naming:** `<MethodName>_Should<Erwartung>_When<Umstand>()`  
> **Muster:** Arrange / Act / Assert (je Abschnitt Leerzeile)  
> **Prioritäten:** Prio 1 = API-Verhalten & Kern-Datenfluss · Prio 2 = Validierungslogik & Domain-Guards · Prio 3 = ViewModel & UI-Spalte · Prio 4 = Niedrig-Hängende-Früchte (NICE-TO-HAVE)

---

## Übersicht: Testklassen und Dateien

| # | Testklasse | Datei | Prio | ~Tests | Lücken-IDs |
|---|---|---|---|---|---|
| 1 | `ApiClientPostingsReversalTests` (**neu**) | `FinanceManager.Tests.Integration/ApiClient/ApiClientPostingsReversalTests.cs` | **1** | 6 | L01–L06 |
| 2 | `PostingReversalServiceTests` (**erweitern**) | `FinanceManager.Tests/Infrastructure/Postings/PostingReversalServiceTests.cs` | **1–4** | +16 (→25) | L07–L21 |
| 3 | `PostingReversalDomainTests` (**neu**) | `FinanceManager.Tests/Domain/PostingReversalDomainTests.cs` | **2** | 6 | L22–L27 |
| 4 | `PostingsCardViewModelReversalTests` (**neu**) | `FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs` | **2–3** | 5 | L28–L32 |
| 5 | `PostingsListReversalColumnTests` (**neu**) | `FinanceManager.Tests/ViewModels/PostingsListReversalColumnTests.cs` | **3** | 3 | L33–L35 |
| 6 | `PostingBackupDtoReversalTests` (**neu**) | `FinanceManager.Tests/Domain/PostingBackupDtoReversalTests.cs` | **3** | 1 | L36 |
| **Gesamt** | | | | **~37** | |

---

## ⚠️ Bekannte Diskrepanz: 403 vs. 400 für fremde Buchungen (L03)

> **Wichtiger Befund vor der Implementierung prüfen!**

Der `PostingsController` fängt `UnauthorizedAccessException` → HTTP 403 Forbidden.  
`PostingReversalService.ReversePostingAsync` wirft jedoch `InvalidOperationException` mit der Meldung *„User {userId} is not authorized to reverse posting {postingId}"* (via `CanReverseAsync`, Zeile ~143).  
Da `InvalidOperationException` **nicht** als `UnauthorizedAccessException` behandelt wird, trifft der generische Catch-Block ein → **HTTP 400** (nicht 403).

**Der 403-Zweig im Controller ist mit der aktuellen Service-Implementierung nicht erreichbar.**

Empfehlung:
- L03-Integrationstest zuerst schreiben (wird anfangs rot/400 statt 403 zeigen)
- Service anpassen: `throw new UnauthorizedAccessException(...)` statt `InvalidOperationException` bei Eigentümerverletzung **oder** Controller-Catch-Block auf die `InvalidOperationException`-Meldung erweitern
- Nach Fix: Test grün + Commit

Der Plan dokumentiert L03 als **Soll: 403** mit dem Hinweis, das aktuell beobachtbare Verhalten zu prüfen.

---

## Was NICHT implementiert wird

| Was | Warum |
|-----|-------|
| Blazor-Komponenten-Rendering-Tests (`PostingsCard.razor`, `PostingsList.razor`) | Eigenes Blazor-Test-Framework nötig (bspw. bUnit), unverhältnismäßiger Aufwand |
| Transaction-Rollback-Tests (L_extra) | `Microsoft.EntityFrameworkCore.InMemory` ignoriert Transaktionen; kein aussagekräftiger Test möglich |
| Backup-Roundtrip über echte API (`/api/backup` → Restore-Endpoint) | Zu umfangreich für diesen Scope; `PostingBackupDto`-Serialisierung reicht als Unit-Test |
| Vollständige End-to-End-Roundtrip-Integrationstests | Zu umfangreich für diesen Branch; separater Issue empfohlen |
| ReversePostingCommand-Handler (L_extra) | In dieser Codebase kein CQRS-Command-Handler vorhanden; Controller ruft Service direkt auf |

---

## Implementierungsreihenfolge

```
Prio 1  → Datei 1 (API Integration: L01, L02, L03*, L04)
Prio 1  → Datei 2 Erweiterung (Service: L12, L13, L17, L21)
Prio 2  → Datei 2 Erweiterung (Service: L07–L11)
Prio 2  → Datei 3 (Domain: L22–L25)
Prio 2  → Datei 4 (ViewModel Ribbon: L28, L29, L30)
Prio 3  → Datei 1 Ergänzung (API: L05, L06)
Prio 3  → Datei 2 Erweiterung (Service: L14, L15, L19)
Prio 3  → Datei 4 (ViewModel Error: L31, L32)
Prio 3  → Datei 5 (List-Spalte: L33, L34)
Prio 3  → Datei 6 (Backup: L36)
Prio 4  → Datei 2 Erweiterung (Service: L16, L18, L20)
Prio 4  → Datei 3 (Domain: L26, L27)
Prio 4  → Datei 5 (List-Spalte: L35)
```

---

## Datei 1: `ApiClientPostingsReversalTests.cs` (NEU) — Prio 1 & 3

**Klasse:** `ApiClientPostingsReversalTests : IClassFixture<TestWebApplicationFactory>`  
**Namespace:** `FinanceManager.Tests.Integration.ApiClient`  

**Gemeinsame Hilfsmethoden (analog zu `ApiClientPostingsTests`):**

```csharp
private FinanceManager.Shared.ApiClient CreateClient(); // factory.CreateClient(AllowAutoRedirect=false)
private async Task<(ApiClient api, Guid accountId)> SetupAuthenticatedWithAccountAsync();
    // → Register, CreateAccount("Test-Konto", Giro, "DE89370400440532013000")
private async Task<Guid> BookPostingViaStatementAsync(ApiClient api, Guid accountId, decimal amount, string subject);
    // → Upload minimal ING-CSV, set contact, book → return PostingId
```

---

### L01 — `ReversePosting_ShouldReturn200WithReversalResult_WhenOwnerReversesValidPosting` (Prio 1)

**Lücke:** L01  
**Was geprüft wird:** Happy Path – 200 OK + vollständiges `ReversalResultDto`

```
Arrange:
  - Eingeloggter User besitzt ein Konto
  - Buchung wurde per Statement-Draft eingebucht (→ echte Posting-ID)
  
Act:
  - api.Postings_ReverseAsync(postingId)
  
Assert:
  - result != null
  - result.ReversedPostingIds enthält postingId
  - result.CreatedReversalIds.Count == 1
  - result.StatementImportId != Guid.Empty
```

---

### L02 — `ReversePosting_ShouldReturn409Conflict_WhenPostingAlreadyReversed` (Prio 1)

**Lücke:** L02

```
Arrange:
  - Buchung existiert und gehört dem User
  - Erste Stornierung via api.Postings_ReverseAsync(id) war erfolgreich
  
Act:
  - Zweite api.Postings_ReverseAsync(id) → sollte scheitern
  
Assert:
  - Wirft HttpRequestException ODER gibt null zurück
  - HTTP-Statuscode ist 409 Conflict
  - (Direkt via _factory.CreateClient + HttpClient.PostAsync für Statuscode-Inspektion)
```

> **Tipp:** Für HTTP-Status-Code direkte `HttpClient.PostAsync(...)` verwenden und `response.StatusCode` prüfen; der typisierte `ApiClient` gibt bei Non-2xx `null` zurück.

---

### L03 — `ReversePosting_ShouldReturn4xxForbiddenOrBadRequest_WhenPostingBelongsToAnotherUser` (Prio 1) ⚠️

**Lücke:** L03  
**Hinweis:** Aktuell ist der erwartete Status **400 Bad Request** (nicht 403) – siehe [Diskrepanz-Abschnitt](#⚠️-bekannte-diskrepanz-403-vs-400-für-fremde-buchungen-l03) oben.

```
Arrange:
  - User A meldet sich an, legt Konto an, bucht Posting (→ postingIdVonA)
  - User B meldet sich mit separatem ApiClient an (eigener CreateClient()-Aufruf)
  
Act:
  - api_von_B.HttpClient.PostAsync($"/api/postings/{postingIdVonA}/reverse", null)
  
Assert:
  - response.StatusCode == HttpStatusCode.Forbidden (403) ← Soll-Verhalten
  - ODER response.StatusCode == HttpStatusCode.BadRequest (400) ← Ist-Verhalten prüfen
  - response.IsSuccessStatusCode == false (mindestens das)
```

---

### L04 — `ReversePosting_ShouldReturn400BadRequest_WhenPostingIdDoesNotExist` (Prio 1)

**Lücke:** L04

```
Arrange:
  - Eingeloggter User
  - nonExistentId = Guid.NewGuid()
  
Act:
  - HTTP POST /api/postings/{nonExistentId}/reverse
  
Assert:
  - response.StatusCode == HttpStatusCode.BadRequest (400)
  - response.Content enthält ProblemDetails mit title "Bad Request"
```

---

### L05 — `ValidateReversal_ShouldReturn200WithIsValidTrue_WhenOwnerValidatesReversiblePosting` (Prio 3)

**Lücke:** L05

```
Arrange:
  - Eingeloggter User mit eigener, stornierbarer Buchung
  
Act:
  - api.Postings_ValidateReversalAsync(postingId)
  
Assert:
  - result != null
  - result.IsValid == true
  - result.Errors.Count == 0
```

---

### L06 — `ReversePosting_ShouldReturn401Unauthorized_WhenNotAuthenticated` (Prio 3)

**Lücke:** L06

```
Arrange:
  - HttpClient ohne Login (kein Auth-Cookie)
  
Act:
  - HTTP POST /api/postings/{beliebige-guid}/reverse
  
Assert:
  - response.StatusCode == HttpStatusCode.Unauthorized (401)
```

---

## Datei 2: `PostingReversalServiceTests.cs` (ERWEITERN) — Prio 1–4

**Bestehende Klasse:** `PostingReversalServiceTests` in `FinanceManager.Tests/Infrastructure/Postings/`  
**Hinzufügen:** 16 neue Tests (bestehende 9 Tests bleiben unverändert)

**Bestehende Hilfsmethoden weiternutzen:**
- `CreateContext()` → frische InMemory-DB
- `CreateService(context, aggregateService?)` → PostingReversalService
- `CreateAccount(ownerId)` → Account
- `CreatePosting(accountId, amount, subject)` → Posting mit SetGroup(NewGuid)

---

### CanReverseAsync – Fehlerpfade (Prio 2)

#### L07 — `CanReverseAsync_ShouldReturnInvalid_WhenPostingNotFound`

```
Arrange:
  - Leere DB (kein Posting)
  - nonExistentId = Guid.NewGuid()

Act:
  - var result = await service.CanReverseAsync(nonExistentId, OwnerId);

Assert:
  - result.IsValid.Should().BeFalse()
  - result.Errors.Should().ContainMatch($"*{nonExistentId}*")
```

---

#### L08 — `CanReverseAsync_ShouldReturnInvalid_WhenUserIsNotOwner`

```
Arrange:
  - Account mit OwnerId, Posting darauf
  
Act:
  - var result = await service.CanReverseAsync(posting.Id, OtherId);

Assert:
  - result.IsValid.Should().BeFalse()
  - result.Errors.Should().ContainMatch($"*{OtherId}*")
```

---

#### L09 — `CanReverseAsync_ShouldReturnInvalid_WhenPostingAlreadyReversed`

```
Arrange:
  - Posting mit ReversedByPostingId gesetzt
    (via SetReversedBy(dummyReversal, OwnerId))

Act:
  - var result = await service.CanReverseAsync(posting.Id, OwnerId);

Assert:
  - result.IsValid.Should().BeFalse()
  - result.Errors.Should().ContainMatch("*already been reversed*")
```

---

#### L10 — `CanReverseAsync_ShouldReturnInvalid_WhenPostingIsAReversal`

```
Arrange:
  - original Posting + reversal Posting (reversal.SetReversalFor(original))

Act:
  - var result = await service.CanReverseAsync(reversal.Id, OwnerId);

Assert:
  - result.IsValid.Should().BeFalse()
  - result.Errors.Should().ContainMatch("*reversal*")
```

---

#### L11 — `CanReverseAsync_ShouldReturnInvalid_WhenGroupIsPartiallyReversed`

```
Arrange:
  - 2 Postings in gleicher Gruppe (groupId)
  - posting2 ist bereits storniert (SetReversedBy(...))

Act:
  - var result = await service.CanReverseAsync(posting1.Id, OwnerId);

Assert:
  - result.IsValid.Should().BeFalse()
  - result.Errors.Should().ContainMatch("*partially reversed*")
```

---

### ReversePostingAsync – Service-Detailverhalten (Prio 1)

#### L12 — `ReversePostingAsync_ShouldCreateStatementImportWithReversalFormat` (Prio 1)

```
Arrange:
  - Account + Posting (amount=100m, subject="Gehalt")
  - context.Accounts.Add, context.Postings.Add, SaveChanges

Act:
  - var result = await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - result.StatementImportId.Should().NotBe(Guid.Empty)
  - var import = await context.StatementImports.FindAsync(result.StatementImportId)
  - import.Should().NotBeNull()
  - import!.Format.Should().Be(ImportFormat.Reversal)
  - import.OriginalFileName.Should().Contain(posting.Id.ToString())
  - import.OriginalFileName.Should().StartWith("REVERSAL_")
```

---

#### L13 — `ReversePostingAsync_ShouldCreateStatementEntryWithNegatedAmount` (Prio 1)

```
Arrange:
  - Account + Posting (amount=250m, subject="Miete")

Act:
  - await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - var entry = await context.StatementEntries
        .FirstOrDefaultAsync(e => e.StatementImportId == result.StatementImportId)
  - entry.Should().NotBeNull()
  - entry!.Amount.Should().Be(-250m)
  - entry.Subject.Should().StartWith("REVERSAL:")
```

---

#### L17 — `ReversePostingAsync_ShouldThrow_WhenPostingHasNoAccountId` (Prio 1)

> Posting ohne AccountId → `GetPostingOwnerUserIdAsync` wirft `InvalidOperationException`.

```
Arrange:
  - Posting direkt mit accountId: null angelegt (bypasse CreatePosting-Helper):
    var posting = new Posting(sourceId: Guid.NewGuid(), kind: PostingKind.Bank,
        accountId: null, ..., amount: 50m, subject: "Kein Konto");
    posting.SetGroup(Guid.NewGuid());
    context.Postings.Add(posting); SaveChanges();

Act:
  - var act = async () => await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*account*")
  
  // ODER: CanReverseAsync schlägt fehl (je nach Implementierung)
  // → prüfen ob Fehler in CanReverseAsync oder ReversePostingAsync aufschlägt
```

---

#### L21 — `GetRelatedPostingsAsync_ShouldReturnEmptyList_WhenPostingGroupIdIsEmpty` (Prio 1)

> **Potenzieller Bug:** `GroupId == Guid.Empty` → Query holt alle anderen Postings ohne Gruppe.

```
Arrange:
  - Posting p1 OHNE SetGroup() → p1.GroupId == Guid.Empty
  - Posting p2 OHNE SetGroup() → p2.GroupId == Guid.Empty (anderes Posting)
  - Beide in DB

Act:
  - var result = await service.GetRelatedPostingsAsync(p1.Id);

Assert:
  - result.Should().BeEmpty()
  // Wenn result.Count > 0: BUG dokumentieren!
  // Erwartetes Defensiv-Verhalten: leere Liste, da Empty GroupId ignoriert werden soll
```

---

### ReversePostingAsync – Prio 3

#### L14 — `ReversePostingAsync_ShouldCallAggregateService_OncePerCreatedReversalPosting` (Prio 3)

```
Arrange:
  - var aggMock = new Mock<IPostingAggregateService>()
  - Account + Posting
  - var service = CreateService(context, aggMock.Object)

Act:
  - await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - aggMock.Verify(a => a.UpsertForPostingAsync(
        It.IsAny<Posting>(), It.IsAny<CancellationToken>()), Times.Once)
```

---

#### L15 — `ReversePostingAsync_ShouldAssignSharedNewGroupId_ToAllGroupReversals` (Prio 3)

```
Arrange:
  - groupId = Guid.NewGuid()
  - posting1.SetGroup(groupId), posting2.SetGroup(groupId)

Act:
  - var result = await service.ReversePostingAsync(posting1.Id, OwnerId);

Assert:
  - result.CreatedReversalIds.Should().HaveCount(2)
  - var r1 = await context.Postings.FindAsync(result.CreatedReversalIds[0]);
  - var r2 = await context.Postings.FindAsync(result.CreatedReversalIds[1]);
  - r1!.GroupId.Should().Be(r2!.GroupId)
  - r1.GroupId.Should().NotBe(groupId)   // neue Gruppe, nicht die originale
```

---

#### L19 — `ReversePostingAsync_ShouldNegateQuantity_ForSecurityPostings` (Prio 3)

```
Arrange:
  - Security-Posting (kind: PostingKind.Security, quantity: 5.5m, amount: 1000m)
  - Quantity gesetzt via Reflection oder Constructor-Parameter (je nach API)

Act:
  - await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - var reversal = context.Postings.Single(p => p.ReversalForPostingId == posting.Id)
  - reversal.Quantity.Should().Be(-5.5m)
```

---

### Niedrig-Hängende Früchte (Prio 4)

#### L16 — `ReversePostingAsync_ShouldSetReversedAtUtc_AfterReversal`

```
Arrange:
  - Account + Posting
  - var before = DateTime.UtcNow

Act:
  - await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - var updated = await context.Postings.FindAsync(posting.Id);
  - updated!.ReversedAtUtc.Should().NotBeNull()
  - updated.ReversedAtUtc!.Value.Should().BeAfter(before)
  - updated.ReversedAtUtc.Value.Kind.Should().Be(DateTimeKind.Utc)
```

---

#### L18 — `ReversePostingAsync_ShouldSetSubjectToReversal_WhenOriginalSubjectIsNull`

```
Arrange:
  - Posting mit subject: null

Act:
  - await service.ReversePostingAsync(posting.Id, OwnerId);

Assert:
  - var reversal = context.Postings.Single(p => p.ReversalForPostingId == posting.Id);
  - reversal.Subject.Should().Be("REVERSAL")   // kein Doppelpunkt, kein Leerzeichen
```

---

#### L20 — `GetRelatedPostingsAsync_ShouldReturnEmptyList_WhenPostingDoesNotExist`

```
Arrange:
  - nonExistentId = Guid.NewGuid()

Act:
  - var result = await service.GetRelatedPostingsAsync(nonExistentId);

Assert:
  - result.Should().BeEmpty()
  - // Kein Exception erwartet
```

---

## Datei 3: `PostingReversalDomainTests.cs` (NEU) — Prio 2 & 4

**Klasse:** `PostingReversalDomainTests`  
**Namespace:** `FinanceManager.Tests.Domain`  
**Keine Abhängigkeiten:** Pure Domain-Objekte, kein Moq nötig.

**Hilfsmethode:**
```csharp
private static Posting CreatePosting() =>
    new Posting(Guid.NewGuid(), PostingKind.Bank, accountId: Guid.NewGuid(),
        contactId: null, savingsPlanId: null, securityId: null,
        bookingDate: new DateTime(2025, 1, 1), amount: 100m,
        subject: "Test", recipientName: "R", description: null, securitySubType: null);
```

---

### L22 — `SetReversedBy_ShouldThrowArgumentNullException_WhenReversalPostingIsNull` (Prio 2)

```
Arrange:
  - var posting = CreatePosting();

Act:
  - Action act = () => posting.SetReversedBy(null!, OwnerId);

Assert:
  - act.Should().Throw<ArgumentNullException>()
       .WithParameterName("reversalPosting")
```

---

### L23 — `SetReversedBy_ShouldThrowInvalidOperationException_WhenAlreadyReversed` (Prio 2)

```
Arrange:
  - var posting = CreatePosting();
  - var reversal1 = CreatePosting();
  - posting.SetReversedBy(reversal1, OwnerId);  // erste Stornierung

Act:
  - var reversal2 = CreatePosting();
  - Action act = () => posting.SetReversedBy(reversal2, OwnerId);

Assert:
  - act.Should().Throw<InvalidOperationException>()
       .WithMessage($"*{posting.Id}*")
       .And.Message.Should().Contain(reversal1.Id.ToString())
```

---

### L24 — `SetReversalFor_ShouldThrowArgumentNullException_WhenOriginalPostingIsNull` (Prio 2)

```
Arrange:
  - var posting = CreatePosting();

Act:
  - Action act = () => posting.SetReversalFor(null!);

Assert:
  - act.Should().Throw<ArgumentNullException>()
       .WithParameterName("originalPosting")
```

---

### L25 — `SetReversalFor_ShouldThrowInvalidOperationException_WhenAlreadyMarkedAsReversal` (Prio 2)

```
Arrange:
  - var posting = CreatePosting();
  - var original1 = CreatePosting();
  - posting.SetReversalFor(original1);  // erste Markierung

Act:
  - var original2 = CreatePosting();
  - Action act = () => posting.SetReversalFor(original2);

Assert:
  - act.Should().Throw<InvalidOperationException>()
       .WithMessage($"*{posting.Id}*")
       .And.Message.Should().Contain(original1.Id.ToString())
```

---

### L26 — `IsReversed_ShouldBeFalseInitially_AndTrueAfterSetReversedBy` (Prio 4)

```
Arrange:
  - var posting = CreatePosting();
  - var reversal = CreatePosting();

Act + Assert (2 Schritte):
  1. posting.IsReversed.Should().BeFalse()
  2. posting.SetReversedBy(reversal, OwnerId);
     posting.IsReversed.Should().BeTrue()
     posting.ReversedByPostingId.Should().Be(reversal.Id)
```

---

### L27 — `IsReversal_ShouldBeFalseInitially_AndTrueAfterSetReversalFor` (Prio 4)

```
Arrange:
  - var posting = CreatePosting();
  - var original = CreatePosting();

Act + Assert (2 Schritte):
  1. posting.IsReversal.Should().BeFalse()
  2. posting.SetReversalFor(original);
     posting.IsReversal.Should().BeTrue()
     posting.ReversalForPostingId.Should().Be(original.Id)
```

---

## Datei 4: `PostingsCardViewModelReversalTests.cs` (NEU) — Prio 2 & 3

**Klasse:** `PostingsCardViewModelReversalTests`  
**Namespace:** `FinanceManager.Tests.ViewModels`  
**Abhängigkeiten:** Moq, IApiClient, ICurrentUserService, NullStringLocalizer

**Hilfsmethoden:**
```csharp
// Analog zu PostingDetailViewModelTests
private static (PostingsCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
{
    var services = new ServiceCollection();
    services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
    var apiMock = new Mock<IApiClient>();
    services.AddSingleton(apiMock.Object);
    return (new PostingsCardViewModel(services.BuildServiceProvider()), apiMock);
}

private static PostingServiceDto MakePosting(Guid id,
    bool isReversed = false, bool isReversal = false) =>
    new PostingServiceDto(Id: id, ..., IsReversed: isReversed, IsReversal: isReversal,
        ReversedByPostingId: isReversed ? Guid.NewGuid() : null,
        ReversalForPostingId: isReversal ? Guid.NewGuid() : null);

private static IStringLocalizer CreateDummyLocalizer() => new DummyLocalizer();
// DummyLocalizer: IStringLocalizer mit LocalizedString(key, key) für alle keys
```

---

### L28 — `RibbonReverseAction_ShouldBeDisabled_WhenPostingIsReversed` (Prio 2)

```
Arrange:
  - var (vm, api) = CreateVm();
  - var id = Guid.NewGuid();
  - api.Setup(a => a.Postings_GetByIdAsync(id, ...))
       .ReturnsAsync(MakePosting(id, isReversed: true));

Act:
  - await vm.InitializeAsync(id);
  - var regs = vm.GetRibbonRegisters(CreateDummyLocalizer());
  - var reverseAction = regs
        .SelectMany(r => r.Tabs)
        .SelectMany(t => t.Items)
        .Single(a => a.Id == "Reverse");

Assert:
  - reverseAction.Disabled.Should().BeTrue()
```

---

### L29 — `RibbonReverseAction_ShouldBeDisabled_WhenPostingIsReversal` (Prio 2)

```
Arrange + Act: analog L28, aber MakePosting(id, isReversal: true)

Assert:
  - reverseAction.Disabled.Should().BeTrue()
```

---

### L30 — `ReverseAsync_ShouldRaiseNavigateToPosting_WhenApiReturnsReversalId` (Prio 2)

```
Arrange:
  - var (vm, api) = CreateVm();
  - var postingId = Guid.NewGuid();
  - var reversalId = Guid.NewGuid();
  - api.Setup(a => a.Postings_GetByIdAsync(postingId, ...))
       .ReturnsAsync(MakePosting(postingId));
  - api.Setup(a => a.Postings_ReverseAsync(postingId, ...))
       .ReturnsAsync(new ReversalResultDto(
           new[] { postingId }, new[] { reversalId }, Guid.NewGuid()));
  
  - await vm.InitializeAsync(postingId);
  
  - string? capturedAction = null; string? capturedParam = null;
  - vm.UiActionRequested += (action, param) => { capturedAction = action; capturedParam = param; };

Act:
  - // Ribbon-Callback aufrufen (triggert ReverseAsync)
  - var regs = vm.GetRibbonRegisters(CreateDummyLocalizer());
  - var reverseCallback = regs.SelectMany(r => r.Tabs).SelectMany(t => t.Items)
        .Single(a => a.Id == "Reverse").Callback!;
  - await reverseCallback.Invoke();

Assert:
  - capturedAction.Should().Be("NavigateToPosting")
  - capturedParam.Should().Be(reversalId.ToString())
```

---

### L31 — `ReverseAsync_ShouldSetError_WhenApiReturnsNull` (Prio 3)

```
Arrange:
  - api.Postings_ReverseAsync(...) → Returns(Task.FromResult<ReversalResultDto?>(null))
  - api.LastErrorCode = "CONFLICT"; api.LastError = "Already reversed"
  
Act:
  - await reverseCallback.Invoke()

Assert:
  - vm.Error.Should().NotBeNullOrEmpty()
  - vm.Loading.Should().BeFalse()
  - capturedAction.Should().BeNull()  // keine Navigation
```

---

### L32 — `ReverseAsync_ShouldSetErrorAndClearLoading_WhenApiThrowsException` (Prio 3)

```
Arrange:
  - api.Postings_ReverseAsync(...).Throws(new HttpRequestException("Netzwerkfehler"))
  
Act:
  - await reverseCallback.Invoke()

Assert:
  - vm.Error.Should().Contain("Netzwerkfehler")
  - vm.Loading.Should().BeFalse()
```

---

## Datei 5: `PostingsListReversalColumnTests.cs` (NEU) — Prio 3 & 4

**Klasse:** `PostingsListReversalColumnTests`  
**Namespace:** `FinanceManager.Tests.ViewModels`  

**Strategie:** `AccountPostingsListViewModel` extends `BasePostingsListViewModel` und ruft `BuildRecords()` intern auf.  
Die Storno-Spalte ist die letzte Spalte (Index 7, Key `"storno"`).

**Hilfsmethoden:**
```csharp
private static AccountPostingsListViewModel CreateVm(IApiClient api)
{
    var services = new ServiceCollection();
    services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
    services.AddSingleton(api);
    return new AccountPostingsListViewModel(services.BuildServiceProvider(), Guid.NewGuid());
}

private static PostingServiceDto MakePosting(bool isReversal, bool isReversed) =>
    new PostingServiceDto(Id: Guid.NewGuid(), ...,
        IsReversal: isReversal, IsReversed: isReversed,
        ReversedByPostingId: isReversed ? Guid.NewGuid() : null,
        ReversalForPostingId: isReversal ? Guid.NewGuid() : null);

// Hilfsmethode zum Laden und Abrufen der Storno-Zelle:
private static string GetStornoCell(ListRecord record)
    => record.Cells.Last().Text ?? string.Empty;
```

---

### L33 — `StornoColumn_ShouldShowCheckmark_WhenPostingIsReversal` (Prio 3)

```
Arrange:
  - api gibt [MakePosting(isReversal: true, isReversed: false)] zurück

Act:
  - await vm.InitializeAsync();
  - var record = vm.Items.Single();

Assert:
  - GetStornoCell(record).Should().Be("✓")
```

---

### L34 — `StornoColumn_ShouldShowDash_WhenPostingIsReversed` (Prio 3)

```
Arrange:
  - api gibt [MakePosting(isReversal: false, isReversed: true)] zurück

Act + Assert:
  - GetStornoCell(record).Should().Be("—")
```

---

### L35 — `StornoColumn_ShouldBeEmpty_WhenPostingIsNeitherReversedNorReversal` (Prio 4)

```
Arrange:
  - api gibt [MakePosting(isReversal: false, isReversed: false)] zurück

Act + Assert:
  - GetStornoCell(record).Should().BeEmpty()
```

---

## Datei 6: `PostingBackupDtoReversalTests.cs` (NEU) — Prio 3

**Klasse:** `PostingBackupDtoReversalTests`  
**Namespace:** `FinanceManager.Tests.Domain`  
**Abhängigkeiten:** Nur Domain – keine Mocks nötig.

---

### L36 — `ToBackupDto_ShouldIncludeAllReversalFields_WhenPostingIsReversed` (Prio 3)

```
Arrange:
  - var posting = new Posting(...)  // ohne SetGroup nötig
  - var reversal = new Posting(...)
  - posting.SetReversedBy(reversal, userId: someUserId);
  
  // Für ReversedAtUtc: direkt nach SetReversedBy prüfen

Act:
  - var dto = posting.ToBackupDto();

Assert:
  - dto.ReversedByPostingId.Should().Be(reversal.Id)
  - dto.ReversalForPostingId.Should().BeNull()
  - dto.ReversedByUserId.Should().Be(someUserId)
  - dto.ReversedAtUtc.Should().NotBeNull()
  - dto.ReversedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc)

// Bonus: Roundtrip via AssignBackupDto
  - var restored = new Posting(...)
  - restored.AssignBackupDto(dto)
  - restored.ReversedByPostingId.Should().Be(reversal.Id)
  - restored.ReversedByUserId.Should().Be(someUserId)
  - restored.ReversedAtUtc.Should().Be(dto.ReversedAtUtc)
```

---

## Namespaces und Usings im Überblick

### `ApiClientPostingsReversalTests.cs`
```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;
using FinanceManager.Shared.Dtos.Postings;
// GlobalUsings liefert: Shared.Dtos.Statements, Shared.Dtos.Accounts, etc.
```

### `PostingReversalServiceTests.cs` (Ergänzung)
```csharp
// bestehende Usings + ggf.:
using FinanceManager.Domain.Statements;  // StatementImport, ImportFormat
using FinanceManager.Infrastructure;     // AppDbContext
using Microsoft.EntityFrameworkCore;     // FindAsync, FirstOrDefaultAsync
```

### `PostingReversalDomainTests.cs`
```csharp
using FinanceManager.Domain.Postings;
using FluentAssertions;
using Xunit;
```

### `PostingsCardViewModelReversalTests.cs`
```csharp
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
// GlobalUsings liefert PostingServiceDto, ReversalResultDto
```

### `PostingsListReversalColumnTests.cs`
```csharp
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings;  // AccountPostingsListViewModel
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
```

---

## Implementierungshinweise für spezifische Testaufgaben

### StatementImport in L12/L13 abfragen
Der InMemory-Kontext kennt `context.StatementImports` (DbSet). Stelle sicher, dass `AppDbContext.StatementImports` und `AppDbContext.StatementEntries` im Test-DbSet registriert sind (analog zu `context.Postings`). Da dieselbe `context`-Instanz verwendet wird, sind nach `SaveChangesAsync` alle neu angelegten Einträge direkt sichtbar.

### IStringLocalizer-Dummy für Ribbon-Tests
```csharp
private sealed class DummyLocalizer : IStringLocalizer
{
    public LocalizedString this[string name] => new(name, name);
    public LocalizedString this[string name, params object[] arguments] => new(name, name);
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => Enumerable.Empty<LocalizedString>();
}
```

### Ribbon-Zugriff
Nach `await vm.InitializeAsync(postingId)` (das `LoadAsync` aufruft und `GetRibbonRegisterDefinition` triggert):
```csharp
var regs = vm.GetRibbonRegisters(new DummyLocalizer());
var action = regs
    .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
    .SelectMany(t => t.Items ?? Enumerable.Empty<UiRibbonAction>())
    .FirstOrDefault(a => a.Id == "Reverse" || a.Action == "Reverse");
```

### UiActionRequested-Event abonnieren
Das Event ist auf `BaseViewModel` deklariert. Signatur: `event Action<string, string?> UiActionRequested`:
```csharp
string? capturedAction = null;
string? capturedParam = null;
vm.UiActionRequested += (action, param) => { capturedAction = action; capturedParam = param; };
```

---

## Gesamtübersicht: Lücken → Tests

| Lücken-ID | Testmethode | Datei | Prio |
|-----------|-------------|-------|------|
| L01 | `ReversePosting_ShouldReturn200WithReversalResult_...` | ApiClientPostingsReversalTests | **1** |
| L02 | `ReversePosting_ShouldReturn409Conflict_...` | ApiClientPostingsReversalTests | **1** |
| L03 | `ReversePosting_ShouldReturn4xxForbiddenOrBadRequest_...` | ApiClientPostingsReversalTests | **1** ⚠️ |
| L04 | `ReversePosting_ShouldReturn400BadRequest_...` | ApiClientPostingsReversalTests | **1** |
| L05 | `ValidateReversal_ShouldReturn200WithIsValidTrue_...` | ApiClientPostingsReversalTests | 3 |
| L06 | `ReversePosting_ShouldReturn401Unauthorized_...` | ApiClientPostingsReversalTests | 3 |
| L07 | `CanReverseAsync_ShouldReturnInvalid_WhenPostingNotFound` | PostingReversalServiceTests | 2 |
| L08 | `CanReverseAsync_ShouldReturnInvalid_WhenUserIsNotOwner` | PostingReversalServiceTests | 2 |
| L09 | `CanReverseAsync_ShouldReturnInvalid_WhenPostingAlreadyReversed` | PostingReversalServiceTests | 2 |
| L10 | `CanReverseAsync_ShouldReturnInvalid_WhenPostingIsAReversal` | PostingReversalServiceTests | 2 |
| L11 | `CanReverseAsync_ShouldReturnInvalid_WhenGroupIsPartiallyReversed` | PostingReversalServiceTests | 2 |
| L12 | `ReversePostingAsync_ShouldCreateStatementImportWithReversalFormat` | PostingReversalServiceTests | **1** |
| L13 | `ReversePostingAsync_ShouldCreateStatementEntryWithNegatedAmount` | PostingReversalServiceTests | **1** |
| L14 | `ReversePostingAsync_ShouldCallAggregateService_OncePerReversalPosting` | PostingReversalServiceTests | 3 |
| L15 | `ReversePostingAsync_ShouldAssignSharedNewGroupId_ToAllGroupReversals` | PostingReversalServiceTests | 3 |
| L16 | `ReversePostingAsync_ShouldSetReversedAtUtc_AfterReversal` | PostingReversalServiceTests | 4 |
| L17 | `ReversePostingAsync_ShouldThrow_WhenPostingHasNoAccountId` | PostingReversalServiceTests | **1** |
| L18 | `ReversePostingAsync_ShouldSetSubjectToReversal_WhenOriginalSubjectIsNull` | PostingReversalServiceTests | 4 |
| L19 | `ReversePostingAsync_ShouldNegateQuantity_ForSecurityPostings` | PostingReversalServiceTests | 3 |
| L20 | `GetRelatedPostingsAsync_ShouldReturnEmptyList_WhenPostingDoesNotExist` | PostingReversalServiceTests | 4 |
| L21 | `GetRelatedPostingsAsync_ShouldReturnEmptyList_WhenPostingGroupIdIsEmpty` | PostingReversalServiceTests | **1** |
| L22 | `SetReversedBy_ShouldThrowArgumentNullException_WhenReversalPostingIsNull` | PostingReversalDomainTests | 2 |
| L23 | `SetReversedBy_ShouldThrowInvalidOperationException_WhenAlreadyReversed` | PostingReversalDomainTests | 2 |
| L24 | `SetReversalFor_ShouldThrowArgumentNullException_WhenOriginalPostingIsNull` | PostingReversalDomainTests | 2 |
| L25 | `SetReversalFor_ShouldThrowInvalidOperationException_WhenAlreadyMarkedAsReversal` | PostingReversalDomainTests | 2 |
| L26 | `IsReversed_ShouldBeFalseInitially_AndTrueAfterSetReversedBy` | PostingReversalDomainTests | 4 |
| L27 | `IsReversal_ShouldBeFalseInitially_AndTrueAfterSetReversalFor` | PostingReversalDomainTests | 4 |
| L28 | `RibbonReverseAction_ShouldBeDisabled_WhenPostingIsReversed` | PostingsCardViewModelReversalTests | 2 |
| L29 | `RibbonReverseAction_ShouldBeDisabled_WhenPostingIsReversal` | PostingsCardViewModelReversalTests | 2 |
| L30 | `ReverseAsync_ShouldRaiseNavigateToPosting_WhenApiReturnsReversalId` | PostingsCardViewModelReversalTests | 2 |
| L31 | `ReverseAsync_ShouldSetError_WhenApiReturnsNull` | PostingsCardViewModelReversalTests | 3 |
| L32 | `ReverseAsync_ShouldSetErrorAndClearLoading_WhenApiThrowsException` | PostingsCardViewModelReversalTests | 3 |
| L33 | `StornoColumn_ShouldShowCheckmark_WhenPostingIsReversal` | PostingsListReversalColumnTests | 3 |
| L34 | `StornoColumn_ShouldShowDash_WhenPostingIsReversed` | PostingsListReversalColumnTests | 3 |
| L35 | `StornoColumn_ShouldBeEmpty_WhenPostingIsNeitherReversedNorReversal` | PostingsListReversalColumnTests | 4 |
| L36 | `ToBackupDto_ShouldIncludeAllReversalFields_WhenPostingIsReversed` | PostingBackupDtoReversalTests | 3 |
