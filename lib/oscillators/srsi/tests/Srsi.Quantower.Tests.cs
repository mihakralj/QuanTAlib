using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SrsiIndicatorTests
{
    [Fact]
    public void SrsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SrsiIndicator();

        Assert.Equal(6, indicator.EmaLength);
        Assert.Equal(14, indicator.RsiLength);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SRSI - Apirine Slow RSI", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SrsiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SrsiIndicator();

        Assert.Equal(0, SrsiIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void SrsiIndicator_ShortName_IncludesParamsAndSource()
    {
        var indicator = new SrsiIndicator { EmaLength = 10, RsiLength = 20 };

        Assert.Contains("SRSI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SrsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SrsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Srsi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SrsiIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new SrsiIndicator { EmaLength = 6, RsiLength = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SrsiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SrsiIndicator { EmaLength = 3, RsiLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SrsiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SrsiIndicator { EmaLength = 3, RsiLength = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SrsiIndicator_InternalIndicator_HandlesBarCorrection()
    {
        var ma = new Srsi(3, 5);
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
    public void SrsiIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new SrsiIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void SrsiIndicator_MultipleHistoricalBars()
    {
        var indicator = new SrsiIndicator { EmaLength = 3, RsiLength = 5 };
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
    public void SrsiIndicator_ParamChange_UpdatesConfig()
    {
        var indicator = new SrsiIndicator();
        indicator.EmaLength = 10;
        Assert.Equal(10, indicator.EmaLength);

        indicator.RsiLength = 20;
        Assert.Equal(20, indicator.RsiLength);
    }
}
