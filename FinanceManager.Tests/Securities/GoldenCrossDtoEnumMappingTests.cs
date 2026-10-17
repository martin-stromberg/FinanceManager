using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Domain.Securities;
using FinanceManager.Shared.Dtos.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Guards the int-based enum mapping used by <c>GoldenCrossService</c>: every domain enum member
/// must exist in the corresponding API DTO enum with the same numeric value and name.
/// </summary>
public sealed class GoldenCrossDtoEnumMappingTests
{
    /// <summary>Domain and DTO entry scenario enums are kept in lockstep.</summary>
    [Theory]
    [InlineData(GoldenCrossEntryScenario.Early, GoldenCrossEntryScenarioDto.Early)]
    [InlineData(GoldenCrossEntryScenario.Average, GoldenCrossEntryScenarioDto.Average)]
    [InlineData(GoldenCrossEntryScenario.Late, GoldenCrossEntryScenarioDto.Late)]
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    public void EntryScenario_MapsToSameNamedDtoValue(GoldenCrossEntryScenario domain, GoldenCrossEntryScenarioDto expected)
    {
        ((GoldenCrossEntryScenarioDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO window status enums are kept in lockstep.</summary>
    [Theory]
    [InlineData(GoldenCrossWindowStatus.Upcoming, GoldenCrossWindowStatusDto.Upcoming)]
    [InlineData(GoldenCrossWindowStatus.Open, GoldenCrossWindowStatusDto.Open)]
    [InlineData(GoldenCrossWindowStatus.Closed, GoldenCrossWindowStatusDto.Closed)]
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    public void WindowStatus_MapsToSameNamedDtoValue(GoldenCrossWindowStatus domain, GoldenCrossWindowStatusDto expected)
    {
        ((GoldenCrossWindowStatusDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO retest status enums are kept in lockstep.</summary>
    [Theory]
    [InlineData(GoldenCrossRetestStatus.None, GoldenCrossRetestStatusDto.None)]
    [InlineData(GoldenCrossRetestStatus.InProgress, GoldenCrossRetestStatusDto.InProgress)]
    [InlineData(GoldenCrossRetestStatus.Successful, GoldenCrossRetestStatusDto.Successful)]
    [InlineData(GoldenCrossRetestStatus.Failed, GoldenCrossRetestStatusDto.Failed)]
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    public void RetestStatus_MapsToSameNamedDtoValue(GoldenCrossRetestStatus domain, GoldenCrossRetestStatusDto expected)
    {
        ((GoldenCrossRetestStatusDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO line enums are kept in lockstep.</summary>
    [Theory]
    [InlineData(GoldenCrossLine.Short, GoldenCrossLineDto.Short)]
    [InlineData(GoldenCrossLine.Long, GoldenCrossLineDto.Long)]
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    public void Line_MapsToSameNamedDtoValue(GoldenCrossLine domain, GoldenCrossLineDto expected)
    {
        ((GoldenCrossLineDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO trend structure enums are kept in lockstep.</summary>
    [Theory]
    [InlineData(GoldenCrossTrendStructure.Unknown, GoldenCrossTrendStructureDto.Unknown)]
    [InlineData(GoldenCrossTrendStructure.StableUptrend, GoldenCrossTrendStructureDto.StableUptrend)]
    [InlineData(GoldenCrossTrendStructure.WeakUptrend, GoldenCrossTrendStructureDto.WeakUptrend)]
    [InlineData(GoldenCrossTrendStructure.Sideways, GoldenCrossTrendStructureDto.Sideways)]
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    public void TrendStructure_MapsToSameNamedDtoValue(GoldenCrossTrendStructure domain, GoldenCrossTrendStructureDto expected)
    {
        ((GoldenCrossTrendStructureDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO phase enums are kept in lockstep.</summary>
    [Theory]
    [InlineData(GoldenCrossPhase.InsufficientData, GoldenCrossPhaseDto.InsufficientData)]
    [InlineData(GoldenCrossPhase.Far, GoldenCrossPhaseDto.Far)]
    [InlineData(GoldenCrossPhase.Approaching, GoldenCrossPhaseDto.Approaching)]
    [InlineData(GoldenCrossPhase.Crossed, GoldenCrossPhaseDto.Crossed)]
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    public void Phase_MapsToSameNamedDtoValue(GoldenCrossPhase domain, GoldenCrossPhaseDto expected)
    {
        ((GoldenCrossPhaseDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Each domain enum has exactly as many members as its DTO counterpart.</summary>
    [Fact]
    public void DomainAndDtoEnums_HaveMatchingMemberCounts()
    {
        Enum.GetValues<GoldenCrossEntryScenario>().Should().HaveCount(Enum.GetValues<GoldenCrossEntryScenarioDto>().Length);
        Enum.GetValues<GoldenCrossWindowStatus>().Should().HaveCount(Enum.GetValues<GoldenCrossWindowStatusDto>().Length);
        Enum.GetValues<GoldenCrossRetestStatus>().Should().HaveCount(Enum.GetValues<GoldenCrossRetestStatusDto>().Length);
        Enum.GetValues<GoldenCrossLine>().Should().HaveCount(Enum.GetValues<GoldenCrossLineDto>().Length);
        Enum.GetValues<GoldenCrossTrendStructure>().Should().HaveCount(Enum.GetValues<GoldenCrossTrendStructureDto>().Length);
        Enum.GetValues<GoldenCrossPhase>().Should().HaveCount(Enum.GetValues<GoldenCrossPhaseDto>().Length);
    }
}
