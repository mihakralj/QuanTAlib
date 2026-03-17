using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MadhIndicatorTests
{
    [Fact]
    public void MadhIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MadhIndicator();

        Assert.Equal(8, indicator.ShortLength);
        Assert.Equal(27, indicator.DominantCycle);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MADH - Ehlers Moving Average Difference with Hann", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MadhIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MadhIndicator();

        Assert.Equal(0, MadhIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MadhIndicator_ShortName_IncludesParamsAndSource()
    {
        var indicator = new MadhIndicator { ShortLength = 10, DominantCycle = 30 };

        Assert.Contains("MADH", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void MadhIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MadhIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Madh.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MadhIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new MadhIndicator { ShortLength = 8, DominantCycle = 27 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MadhIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MadhIndicator { ShortLength = 3, DominantCycle = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MadhIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MadhIndicator { ShortLength = 3, DominantCycle = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MadhIndicator_InternalIndicator_HandlesBarCorrection()
    {
        var ma = new Madh(3, 4);
        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106];

        var now = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            ma.Update(new TValue(now.AddMinutes(i).Ticks, prices[i]), isNew: true);
        }

        double beforeCorrection = ma.Last.Value;

        ma.Update(new TValue(now.AddMinutes(9).Ticks, 100), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void MadhIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new MadhIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void MadhIndicator_MultipleHistoricalBars()
    {
        var indicator = new MadhIndicator { ShortLength = 3, DominantCycle = 6 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void MadhIndicator_ParamChange_UpdatesConfig()
    {
        var indicator = new MadhIndicator();
        indicator.ShortLength = 12;
        indicator.DominantCycle = 40;
        Assert.Equal(12, indicator.ShortLength);
        Assert.Equal(40, indicator.DominantCycle);
    }
}
