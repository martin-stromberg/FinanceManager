using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Domain.Securities;
using FinanceManager.Shared.Dtos.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Guards the int-based enum mapping used by <c>TrendReversalService</c>: every domain enum member
/// must exist in the corresponding API DTO enum with the same numeric value and name.
/// </summary>
public sealed class TrendReversalDtoEnumMappingTests
{
    /// <summary>Domain and DTO trend status enums are kept in lockstep.</summary>
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    [Theory]
    [InlineData(TrendStatus.InsufficientData, TrendStatusDto.InsufficientData)]
    [InlineData(TrendStatus.Intact, TrendStatusDto.Intact)]
    [InlineData(TrendStatus.Weakened, TrendStatusDto.Weakened)]
    [InlineData(TrendStatus.ReversalLikely, TrendStatusDto.ReversalLikely)]
    public void TrendStatus_MapsToSameNamedDtoValue(TrendStatus domain, TrendStatusDto expected)
    {
        ((TrendStatusDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO signal type enums are kept in lockstep.</summary>
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    [Theory]
    [InlineData(TrendReversalSignalType.DeathCross, TrendReversalSignalTypeDto.DeathCross)]
    [InlineData(TrendReversalSignalType.LongTermLineBreak, TrendReversalSignalTypeDto.LongTermLineBreak)]
    [InlineData(TrendReversalSignalType.BearishMarketStructure, TrendReversalSignalTypeDto.BearishMarketStructure)]
    [InlineData(TrendReversalSignalType.MacdBearishCrossover, TrendReversalSignalTypeDto.MacdBearishCrossover)]
    [InlineData(TrendReversalSignalType.RsiBearishDivergence, TrendReversalSignalTypeDto.RsiBearishDivergence)]
    public void SignalType_MapsToSameNamedDtoValue(TrendReversalSignalType domain, TrendReversalSignalTypeDto expected)
    {
        ((TrendReversalSignalTypeDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO signal strength enums are kept in lockstep.</summary>
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    [Theory]
    [InlineData(TrendReversalSignalStrength.Weak, TrendReversalSignalStrengthDto.Weak)]
    [InlineData(TrendReversalSignalStrength.Moderate, TrendReversalSignalStrengthDto.Moderate)]
    [InlineData(TrendReversalSignalStrength.Strong, TrendReversalSignalStrengthDto.Strong)]
    public void SignalStrength_MapsToSameNamedDtoValue(TrendReversalSignalStrength domain, TrendReversalSignalStrengthDto expected)
    {
        ((TrendReversalSignalStrengthDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO line break classification enums are kept in lockstep.</summary>
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    [Theory]
    [InlineData(LineBreakClassification.None, LineBreakClassificationDto.None)]
    [InlineData(LineBreakClassification.ShortTermWeakness, LineBreakClassificationDto.ShortTermWeakness)]
    [InlineData(LineBreakClassification.StructuralReversal, LineBreakClassificationDto.StructuralReversal)]
    public void LineBreakClassification_MapsToSameNamedDtoValue(LineBreakClassification domain, LineBreakClassificationDto expected)
    {
        ((LineBreakClassificationDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Domain and DTO market structure classification enums are kept in lockstep.</summary>
    /// <param name="domain">Domain enum value.</param>
    /// <param name="expected">Expected DTO enum value.</param>
    [Theory]
    [InlineData(MarketStructureClassification.Unknown, MarketStructureClassificationDto.Unknown)]
    [InlineData(MarketStructureClassification.Intact, MarketStructureClassificationDto.Intact)]
    [InlineData(MarketStructureClassification.SidewaysBearish, MarketStructureClassificationDto.SidewaysBearish)]
    [InlineData(MarketStructureClassification.TrendLoss, MarketStructureClassificationDto.TrendLoss)]
    [InlineData(MarketStructureClassification.EmergingDowntrend, MarketStructureClassificationDto.EmergingDowntrend)]
    public void MarketStructureClassification_MapsToSameNamedDtoValue(MarketStructureClassification domain, MarketStructureClassificationDto expected)
    {
        ((MarketStructureClassificationDto)(int)domain).Should().Be(expected);
    }

    /// <summary>Each domain enum has exactly as many members as its DTO counterpart.</summary>
    [Fact]
    public void DomainAndDtoEnums_HaveMatchingMemberCounts()
    {
        Enum.GetValues<TrendStatus>().Should().HaveCount(Enum.GetValues<TrendStatusDto>().Length);
        Enum.GetValues<TrendReversalSignalType>().Should().HaveCount(Enum.GetValues<TrendReversalSignalTypeDto>().Length);
        Enum.GetValues<TrendReversalSignalStrength>().Should().HaveCount(Enum.GetValues<TrendReversalSignalStrengthDto>().Length);
        Enum.GetValues<LineBreakClassification>().Should().HaveCount(Enum.GetValues<LineBreakClassificationDto>().Length);
        Enum.GetValues<MarketStructureClassification>().Should().HaveCount(Enum.GetValues<MarketStructureClassificationDto>().Length);
    }
}
