using System.Text.Json;
using FinanceManager.Domain.Reports;
using FinanceManager.Shared.Dtos.Reports;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Backup completeness for the free-text filter columns on <see cref="ReportFavorite"/>: the JSON
/// columns are included in <see cref="ReportFavorite.ToBackupDto"/>/<see cref="ReportFavorite.AssignBackupDto"/>
/// so a backup/restore round-trip preserves the filters, and backups written before the columns
/// existed still deserialize because the new DTO parameters have defaults.
/// </summary>
public sealed class ReportFavoriteBackupTests
{
    /// <summary>
    /// A favorite with configured text filters must expose them on the backup DTO as JSON arrays.
    /// </summary>
    [Fact]
    public void ToBackupDto_ShouldIncludeTextFilterJson()
    {
        var fav = new ReportFavorite(Guid.NewGuid(), "Fav", PostingKind.Bank, false, ReportInterval.Month, false, false, false, true, true);
        fav.SetFilters(null, null, null, null, null, null, null, null, null,
            bankTextFilters: new[] { "Tankstelle", "Miete" },
            securityTextFilters: new[] { "Dividende" });

        var dto = fav.ToBackupDto();

        Assert.Equal(new[] { "Tankstelle", "Miete" }, JsonSerializer.Deserialize<string[]>(dto.BankTextFiltersJson!));
        Assert.Equal(new[] { "Dividende" }, JsonSerializer.Deserialize<string[]>(dto.SecurityTextFiltersJson!));
        Assert.Null(dto.ContactTextFiltersJson);
        Assert.Null(dto.SavingsPlanTextFiltersJson);
    }

    /// <summary>
    /// Applying a backup DTO must restore the JSON columns so that <c>GetFilters</c> returns the
    /// original text filter lists (full backup/restore round-trip on entity level).
    /// </summary>
    [Fact]
    public void AssignBackupDto_ShouldRestoreTextFilters()
    {
        var owner = Guid.NewGuid();
        var source = new ReportFavorite(owner, "Fav", PostingKind.Bank, false, ReportInterval.Month, false, false, false, true, true);
        source.SetFilters(null, null, null, null, null, null, null, null, null,
            contactTextFilters: new[] { "Foo" },
            savingsPlanTextFilters: new[] { "Bar", "Baz" });
        var dto = source.ToBackupDto();

        var restored = new ReportFavorite(owner, "Other", PostingKind.Contact, true, ReportInterval.Year, true, true, true, false, false);
        restored.AssignBackupDto(dto);

        var filters = restored.GetFilters();
        Assert.Equal(new[] { "Foo" }, filters.ContactTextFilters);
        Assert.Equal(new[] { "Bar", "Baz" }, filters.SavingsPlanTextFilters);
        Assert.Null(filters.BankTextFilters);
        Assert.Null(filters.SecurityTextFilters);
        Assert.Equal(source.BankTextFiltersJson, restored.BankTextFiltersJson);
        Assert.Equal(source.SecurityTextFiltersJson, restored.SecurityTextFiltersJson);
    }

    /// <summary>
    /// A backup payload written before the text filter columns existed (no such JSON properties)
    /// must still deserialize into <see cref="ReportFavorite.ReportFavoriteBackupDto"/> and apply
    /// cleanly, leaving all text filters unset.
    /// </summary>
    [Fact]
    public void AssignBackupDto_ShouldAcceptOldBackup_WithoutTextFilterFields()
    {
        var json = """
        {
            "Id": "11111111-1111-1111-1111-111111111111",
            "OwnerUserId": "22222222-2222-2222-2222-222222222222",
            "Name": "Alt",
            "PostingKind": 0,
            "IncludeCategory": false,
            "Interval": 0,
            "Take": 24,
            "ComparePrevious": false,
            "CompareYear": false,
            "CompareProjection": false,
            "ShowChart": true,
            "Expandable": true,
            "PostingKindsCsv": null,
            "AccountIdsCsv": null,
            "ContactIdsCsv": null,
            "SavingsPlanIdsCsv": null,
            "SecurityIdsCsv": null,
            "ContactCategoryIdsCsv": null,
            "SavingsPlanCategoryIdsCsv": null,
            "SecurityCategoryIdsCsv": null,
            "SecuritySubTypesCsv": null,
            "IncludeDividendRelated": null,
            "UseValutaDate": false,
            "SavingsPlanPostingSource": 0
        }
        """;

        var dto = JsonSerializer.Deserialize<ReportFavorite.ReportFavoriteBackupDto>(json);
        Assert.NotNull(dto);
        Assert.Null(dto!.BankTextFiltersJson);
        Assert.Null(dto.ContactTextFiltersJson);
        Assert.Null(dto.SavingsPlanTextFiltersJson);
        Assert.Null(dto.SecurityTextFiltersJson);

        var restored = new ReportFavorite(dto.OwnerUserId, "x", PostingKind.Contact, false, ReportInterval.Month, false, false, false, true, true);
        restored.SetFilters(null, null, null, null, null, null, null, null, null, bankTextFilters: new[] { "stale" });
        restored.AssignBackupDto(dto);

        Assert.Null(restored.BankTextFiltersJson);
        Assert.Null(restored.GetFilters().BankTextFilters);
    }

    /// <summary>
    /// Malformed text filter JSON in a manipulated or corrupted backup must not break the
    /// favorite: <c>AssignBackupDto</c> accepts the payload and every subsequent read via
    /// <c>GetFilters</c>/<see cref="TextFilterList.FromJson"/> yields <c>null</c> instead of
    /// throwing a <see cref="JsonException"/>.
    /// </summary>
    [Fact]
    public void AssignBackupDto_ShouldTolerateMalformedTextFilterJson()
    {
        var owner = Guid.NewGuid();
        var restored = new ReportFavorite(owner, "x", PostingKind.Contact, false, ReportInterval.Month, false, false, false, true, true);
        var dto = new ReportFavorite.ReportFavoriteBackupDto(
            Guid.NewGuid(), owner, "Fav", PostingKind.Bank, false, ReportInterval.Month, 24,
            false, false, false, true, true, null, null, null, null, null, null, null, null, null,
            null, false, SavingsPlanPostingSource.PlanPostings,
            BankTextFiltersJson: "\"abc\"",
            ContactTextFiltersJson: "123",
            SavingsPlanTextFiltersJson: "[1,2]",
            SecurityTextFiltersJson: "[\"ok\"]");

        restored.AssignBackupDto(dto);

        var filters = restored.GetFilters();
        Assert.Null(filters.BankTextFilters);
        Assert.Null(filters.ContactTextFilters);
        Assert.Null(filters.SavingsPlanTextFilters);
        Assert.Equal(new[] { "ok" }, filters.SecurityTextFilters);
    }
}
