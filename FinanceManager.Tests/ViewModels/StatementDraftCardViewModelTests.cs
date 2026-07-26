using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.StatementDrafts;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
using System.Linq;

namespace FinanceManager.Tests.ViewModels;

public sealed class StatementDraftCardViewModelTests
{
    private sealed class DummyGenericLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class DummyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static (StatementDraftCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyGenericLocalizer<>));
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new StatementDraftCardViewModel(sp);
        return (vm, apiMock);
    }

    private static StatementDraftEntryDto Entry(
        Guid id,
        int entryNumber = 1,
        bool isAnnounced = false,
        StatementDraftEntryStatus status = StatementDraftEntryStatus.Open)
        => new(
            id,
            entryNumber,
            new DateTime(2026, 7, 20),
            null,
            12.34m,
            "EUR",
            $"Subject {entryNumber}",
            $"Recipient {entryNumber}",
            $"Description {entryNumber}",
            isAnnounced,
            false,
            status,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            null);

    private static StatementDraftDetailDto Draft(Guid draftId, IReadOnlyList<StatementDraftEntryDto> entries)
        => new(
            draftId,
            "statement.csv",
            null,
            null,
            StatementDraftStatus.Draft,
            entries.Sum(e => e.Amount),
            false,
            null,
            null,
            null,
            null,
            entries,
            null,
            null);

    private static StatementDraftEntriesListViewModel CreateEntriesVm(IReadOnlyList<StatementDraftEntryDto> entries, out Mock<IApiClient> apiMock)
    {
        var draftId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyGenericLocalizer<>));
        apiMock = new Mock<IApiClient>();
        apiMock
            .Setup(x => x.StatementDrafts_GetAsync(draftId, false, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Draft(draftId, entries));
        services.AddSingleton(apiMock.Object);
        return new StatementDraftEntriesListViewModel(services.BuildServiceProvider(), draftId);
    }

    private static UiRibbonAction SaveQuickEditAction(StatementDraftCardViewModel vm)
    {
        var localizer = new DummyLocalizer();
        return ((FinanceManager.Web.ViewModels.Common.BaseViewModel)vm).GetRibbonRegisters(localizer)!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .SelectMany(t => t.Items ?? Enumerable.Empty<UiRibbonAction>())
            .Single(a => a.Action == "SaveQuickEdit");
    }

    private static StatementDraftEntriesListViewModel EmbeddedEntries(StatementDraftCardViewModel vm)
        => Assert.IsType<StatementDraftEntriesListViewModel>(vm.EmbeddedList);

    private static async Task<StatementDraftCardViewModel> CreateLoadedCardAsync(IReadOnlyList<StatementDraftEntryDto> entries)
    {
        var draftId = Guid.NewGuid();
        var (vm, apiMock) = CreateVm();
        apiMock
            .Setup(x => x.StatementDrafts_GetAsync(draftId, false, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Draft(draftId, entries));

        await vm.LoadAsync(draftId);
        return vm;
    }

    [Fact]
    public async Task NewDraft_SelectingBankAccount_enablesSaveInRibbon()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        // Act: initialize in create/new mode
        await vm.LoadAsync(System.Guid.Empty);

        // It's possible the CardRecord isn't populated in this unit test environment.
        // Construct a minimal CardField for the assigned account and simulate selection.
        var acctField = new CardField(
            "Card_Caption_StatementDrafts_AssignedAccount",
            CardFieldKind.Text,
            text: string.Empty,
            symbolId: null,
            amount: null,
            boolValue: null,
            editable: true,
            lookupType: "bankaccount",
            lookupField: "Name",
            valueId: null,
            lookupFilter: null,
            hint: null,
            allowAdd: true);

        // simulate user selecting a bank account lookup item
        var lookup = new BaseViewModel.LookupItem(System.Guid.NewGuid(), "Test Account");
        vm.ValidateLookupField(acctField, lookup);

        // Selecting a lookup should create a pending change
        Assert.True(vm.HasPendingChanges, "ViewModel must report pending changes after selecting a lookup item.");

        // Request ribbon registers (use GetRibbonRegisters to ensure aggregated registers are returned)
        var localizer = new DummyLocalizer();
        var regs = ((FinanceManager.Web.ViewModels.Common.BaseViewModel)vm).GetRibbonRegisters(localizer);

        // In this test environment we expect ribbon registers to be present and include the Save action.
        Assert.NotNull(regs);
        var saveAction = regs.SelectMany(r => r.Tabs ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonTab>())
            .SelectMany(t => t.Items ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonAction>())
            .FirstOrDefault(a => a.Action == "Save");

        Assert.NotNull(saveAction);
        Assert.False(saveAction.Disabled, "Save action must be enabled after selecting an account in create mode");
    }

    [Fact]
    public async Task BeginQuickEdit_AddsPlaceholderRow()
    {
        var vm = CreateEntriesVm(new[] { Entry(Guid.NewGuid()) }, out _);
        await vm.InitializeAsync();

        await vm.BeginQuickEditAsync();

        Assert.Single(vm.Items.Where(i => i.IsPlaceholder));
        Assert.Equal(2, vm.VisibleQuickEditItems.Count);
    }

    [Fact]
    public async Task MarkRowForDeletion_OnlyMarksLocalDeleteUntilSave()
    {
        var entryId = Guid.NewGuid();
        var vm = CreateEntriesVm(new[] { Entry(entryId) }, out var apiMock);
        await vm.InitializeAsync();
        await vm.BeginQuickEditAsync();

        vm.MarkRowForDeletion(entryId);

        var request = vm.CollectQuickEditSaveRequest();
        Assert.Contains(entryId, request.Deletes);
        Assert.Empty(request.Updates);
        Assert.Empty(request.Creates);
        apiMock.Verify(x => x.StatementDrafts_BatchUpdateDetailedAsync(
            It.IsAny<Guid>(),
            It.IsAny<BatchUpdateRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyBatchValidationErrors_ShowsPendingDeletedRowWithHint()
    {
        var entryId = Guid.NewGuid();
        var vm = CreateEntriesVm(new[] { Entry(entryId) }, out _);
        await vm.InitializeAsync();
        await vm.BeginQuickEditAsync();

        vm.MarkRowForDeletion(entryId);
        Assert.DoesNotContain(vm.VisibleQuickEditItems, i => i.Id == entryId);

        vm.ApplyBatchValidationErrors(new BatchUpdateErrorResponseDto
        {
            Errors =
            {
                new EntryErrorDto
                {
                    EntryId = entryId,
                    FieldErrors = { new FieldErrorDto { Field = string.Empty, Message = "Entry cannot be deleted in quick edit" } }
                }
            }
        });
        vm.RequestFocusFirstInvalid();

        var item = Assert.Single(vm.VisibleQuickEditItems.Where(i => i.Id == entryId));
        var record = Assert.Single(vm.Records.Where(r => ((StatementDraftEntryItem)r.Item).Id == entryId));
        Assert.Contains("Entry cannot be deleted in quick edit", record.Hint);
        Assert.Equal(entryId, vm.CollectPendingDeleteIds().Single());
        Assert.Equal(entryId, vm.ConsumeFocusFirstInvalid());
        Assert.Equal(new DateTime(2026, 7, 20), vm.GetEditValue(item.Id, "BookingDate"));
    }

    [Fact]
    public async Task EndQuickEdit_RestoresDeletedRowsAndRemovesNewLocalRows()
    {
        var entryId = Guid.NewGuid();
        var vm = CreateEntriesVm(new[] { Entry(entryId) }, out _);
        await vm.InitializeAsync();
        await vm.BeginQuickEditAsync();

        vm.MarkRowForDeletion(entryId);
        var placeholder = vm.Items.Single(i => i.IsPlaceholder);
        vm.SetEditValue(placeholder.Id, "BookingDate", new DateTime(2026, 7, 21));
        vm.SetEditValue(placeholder.Id, "Amount", 9.99m);
        vm.SetEditValue(placeholder.Id, "Subject", "New row");
        Assert.DoesNotContain(vm.VisibleQuickEditItems, i => i.Id == entryId);
        Assert.Contains(vm.Items, i => i.IsNew);

        await vm.EndQuickEditAsync();

        Assert.Single(vm.Items);
        Assert.Equal(entryId, vm.Items[0].Id);
        Assert.DoesNotContain(vm.Items, i => i.IsNew || i.IsPlaceholder);
        Assert.False(vm.HasPendingQuickEditChanges());
    }

    [Fact]
    public async Task RibbonSaveQuickEdit_IsEnabledForPureDelete()
    {
        var entryId = Guid.NewGuid();
        var cardVm = await CreateLoadedCardAsync(new[] { Entry(entryId) });
        var entriesVm = EmbeddedEntries(cardVm);
        await entriesVm.BeginQuickEditAsync();

        entriesVm.MarkRowForDeletion(entryId);

        var saveAction = SaveQuickEditAction(cardVm);
        Assert.False(saveAction.Disabled);
    }

    [Fact]
    public async Task RibbonSaveQuickEdit_IsEnabledForValidPureCreate()
    {
        var cardVm = await CreateLoadedCardAsync(Array.Empty<StatementDraftEntryDto>());
        var entriesVm = EmbeddedEntries(cardVm);
        await entriesVm.BeginQuickEditAsync();
        var placeholder = entriesVm.Items.Single(i => i.IsPlaceholder);

        entriesVm.SetEditValue(placeholder.Id, "BookingDate", new DateTime(2026, 7, 21));
        entriesVm.SetEditValue(placeholder.Id, "Amount", 9.99m);
        entriesVm.SetEditValue(placeholder.Id, "Subject", "New row");

        var saveAction = SaveQuickEditAction(cardVm);
        Assert.False(saveAction.Disabled);
    }

    [Fact]
    public async Task RibbonSaveQuickEdit_IsDisabledForInvalidNewRow()
    {
        var cardVm = await CreateLoadedCardAsync(Array.Empty<StatementDraftEntryDto>());
        var entriesVm = EmbeddedEntries(cardVm);
        await entriesVm.BeginQuickEditAsync();
        var placeholder = entriesVm.Items.Single(i => i.IsPlaceholder);

        entriesVm.SetEditValue(placeholder.Id, "Subject", "Incomplete row");

        var saveAction = SaveQuickEditAction(cardVm);
        Assert.True(entriesVm.HasPendingQuickEditChanges());
        Assert.True(saveAction.Disabled);
    }

    [Fact]
    public async Task IsAnnouncedOpenRow_IsNotEditableOrDeletableInQuickEdit()
    {
        var entryId = Guid.NewGuid();
        var vm = CreateEntriesVm(new[] { Entry(entryId, isAnnounced: true, status: StatementDraftEntryStatus.Open) }, out _);
        await vm.InitializeAsync();
        await vm.BeginQuickEditAsync();
        var entry = vm.Items.Single(i => i.Id == entryId);

        Assert.True(entry.IsAnnounced);
        Assert.False(entry.CanDelete);
        Assert.False(vm.CanDeleteRow(entry));
        Assert.False(vm.IsRowEditable(entry));

        vm.MarkRowForDeletion(entryId);

        Assert.Empty(vm.CollectQuickEditSaveRequest().Deletes);
        Assert.Contains(vm.VisibleQuickEditItems, i => i.Id == entryId);
    }

    [Fact]
    public async Task ResetDuplicateQuickEdit_CollectsStatusResetWithFieldUpdates()
    {
        var entryId = Guid.NewGuid();
        var vm = CreateEntriesVm(new[] { Entry(entryId, status: StatementDraftEntryStatus.AlreadyBooked) }, out _);
        await vm.InitializeAsync();
        await vm.BeginQuickEditAsync();
        var entry = vm.Items.Single(i => i.Id == entryId);

        Assert.False(vm.IsRowEditable(entry));

        vm.SetEditValue(entryId, "Status", StatementDraftEntryStatus.Open);
        vm.SetEditValue(entryId, "Subject", "Corrected duplicate");

        Assert.True(vm.IsRowEditable(entry));
        var request = vm.CollectQuickEditSaveRequest();
        var update = Assert.Single(request.Updates);
        Assert.Equal(entryId, update.EntryId);
        Assert.Equal(StatementDraftEntryStatus.Open, update.Fields["Status"]);
        Assert.Equal("Corrected duplicate", update.Fields["Subject"]);
    }
}
