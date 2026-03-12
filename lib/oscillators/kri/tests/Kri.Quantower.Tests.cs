using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class KriIndicatorTests
{
    [Fact] public void KriIndicator_Constructor_SetsDefaults() { var i = new KriIndicator(); Assert.Equal(14, i.Period); Assert.Equal(SourceType.Close, i.Source); Assert.True(i.ShowColdValues); Assert.Equal("KRI - Kairi Relative Index", i.Name); Assert.True(i.SeparateWindow); }
    [Fact] public void KriIndicator_MinHistoryDepths_EqualsZero() { Assert.Equal(0, KriIndicator.MinHistoryDepths); IWatchlistIndicator w = new KriIndicator(); Assert.Equal(0, w.MinHistoryDepths); }
    [Fact] public void KriIndicator_ShortName_IncludesParameters() { var i = new KriIndicator { Period = 20 }; i.Initialize(); Assert.Contains("KRI", i.ShortName, StringComparison.Ordinal); Assert.Contains("20", i.ShortName, StringComparison.Ordinal); }
    [Fact] public void KriIndicator_SourceCodeLink_IsValid() { var i = new KriIndicator(); Assert.Contains("github.com", i.SourceCodeLink, StringComparison.Ordinal); Assert.Contains("Kri.Quantower.cs", i.SourceCodeLink, StringComparison.Ordinal); }
    [Fact] public void KriIndicator_Initialize_CreatesInternalKri() { var i = new KriIndicator { Period = 10 }; i.Initialize(); Assert.Single(i.LinesSeries); }
    [Fact]
    public void KriIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KriIndicator { Period = 5 }; indicator.Initialize();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++) { indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i); indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar)); }
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }
    [Fact]
    public void KriIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new KriIndicator { Period = 5 }; indicator.Initialize();
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
    [Fact] public void KriIndicator_Parameters_CanBeChanged() { var i = new KriIndicator { Period = 20 }; i.Initialize(); Assert.Equal(20, i.Period); }
}
