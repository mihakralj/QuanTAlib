using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PslIndicatorTests
{
    [Fact] public void PslIndicator_Constructor_SetsDefaults() { var i = new PslIndicator(); Assert.Equal(12, i.Period); Assert.Equal(SourceType.Close, i.Source); Assert.True(i.ShowColdValues); Assert.Equal("PSL - Psychological Line", i.Name); Assert.True(i.SeparateWindow); }
    [Fact] public void PslIndicator_MinHistoryDepths_EqualsZero() { Assert.Equal(0, PslIndicator.MinHistoryDepths); IWatchlistIndicator w = new PslIndicator(); Assert.Equal(0, w.MinHistoryDepths); }
    [Fact] public void PslIndicator_ShortName_IncludesParameters() { var i = new PslIndicator { Period = 20 }; i.Initialize(); Assert.Contains("PSL", i.ShortName, StringComparison.Ordinal); Assert.Contains("20", i.ShortName, StringComparison.Ordinal); }
    [Fact] public void PslIndicator_SourceCodeLink_IsValid() { var i = new PslIndicator(); Assert.Contains("github.com", i.SourceCodeLink, StringComparison.Ordinal); Assert.Contains("Psl.Quantower.cs", i.SourceCodeLink, StringComparison.Ordinal); }
    [Fact] public void PslIndicator_Initialize_CreatesInternalPsl() { var i = new PslIndicator { Period = 10 }; i.Initialize(); Assert.Single(i.LinesSeries); }
    [Fact]
    public void PslIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PslIndicator { Period = 5 }; indicator.Initialize();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++) { indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i); indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar)); }
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }
    [Fact]
    public void PslIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PslIndicator { Period = 5 }; indicator.Initialize();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        Assert.Single(indicator.LinesSeries);
    }
    [Fact] public void PslIndicator_Parameters_CanBeChanged() { var i = new PslIndicator { Period = 20 }; i.Initialize(); Assert.Equal(20, i.Period); }
}
