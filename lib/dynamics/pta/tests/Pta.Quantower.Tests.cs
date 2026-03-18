using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PtaIndicatorTests
{
    [Fact]
    public void PtaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PtaIndicator();

        Assert.Equal(250, indicator.LongPeriod);
        Assert.Equal(40, indicator.ShortPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("PTA - Ehlers Precision Trend Analysis", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PtaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PtaIndicator();

        Assert.Equal(0, PtaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PtaIndicator_ShortName_IncludesPeriodsAndSource()
    {
        var indicator = new PtaIndicator { LongPeriod = 100, ShortPeriod = 20 };

        Assert.Contains("PTA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("100", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PtaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PtaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pta.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PtaIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PtaIndicator { LongPeriod = 50, ShortPeriod = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PtaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PtaIndicator { LongPeriod = 50, ShortPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void PtaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PtaIndicator { LongPeriod = 50, ShortPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PtaIndicator_InternalIndicator_HandlesBarCorrection()
    {
        var ma = new Pta(50, 10);
        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106,
                           101, 103, 98, 104, 96, 105, 99, 107, 98, 108];

        var now = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            ma.Update(new TValue(now.AddMinutes(i).Ticks, prices[i]), isNew: true);
        }

        double beforeCorrection = ma.Last.Value;

        // Correct last bar with significantly different value
        ma.Update(new TValue(now.AddMinutes(19).Ticks, 200), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void PtaIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new PtaIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void PtaIndicator_MultipleHistoricalBars()
    {
        var indicator = new PtaIndicator { LongPeriod = 50, ShortPeriod = 10 };
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
    public void PtaIndicator_PeriodChange_UpdatesConfig()
    {
        var indicator = new PtaIndicator();
        indicator.LongPeriod = 100;
        Assert.Equal(100, indicator.LongPeriod);

        indicator.ShortPeriod = 20;
        Assert.Equal(20, indicator.ShortPeriod);
    }
}
