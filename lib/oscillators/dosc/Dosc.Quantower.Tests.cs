using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class DoscIndicatorTests
{
    [Fact]
    public void DoscIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DoscIndicator();

        Assert.Equal(14, indicator.RsiPeriod);
        Assert.Equal(5, indicator.Ema1Period);
        Assert.Equal(3, indicator.Ema2Period);
        Assert.Equal(9, indicator.SigPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DOSC - Derivative Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DoscIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DoscIndicator();

        Assert.Equal(0, DoscIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void DoscIndicator_ShortName_IncludesAllParams()
    {
        var indicator = new DoscIndicator { RsiPeriod = 10, Ema1Period = 4, Ema2Period = 2, SigPeriod = 7 };

        Assert.Contains("DOSC", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("4", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("7", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DoscIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DoscIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dosc.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DoscIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new DoscIndicator { RsiPeriod = 14, Ema1Period = 5, Ema2Period = 3, SigPeriod = 9 };

        indicator.Initialize();

        // Single output line series
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DoscIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DoscIndicator { RsiPeriod = 3, Ema1Period = 2, Ema2Period = 2, SigPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void DoscIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DoscIndicator { RsiPeriod = 3, Ema1Period = 2, Ema2Period = 2, SigPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DoscIndicator_InternalIndicator_HandlesBarCorrection()
    {
        // Use alternating zigzag data so RSI is not a degenerate 100/0, ensuring DOSC != 0
        // and a large-drop correction produces a measurably different result.
        var ma = new Dosc(3, 2, 2, 3);
        var now = DateTime.UtcNow;
        double[] prices = [100, 102, 99, 103, 97, 104, 98, 105, 97, 106];

        for (int i = 0; i < prices.Length; i++)
        {
            ma.Update(new TValue(now.AddMinutes(i).Ticks, prices[i]), isNew: true);
        }

        double beforeCorrection = ma.Last.Value;

        // Correct last bar with a steep drop — RSI collapses, DOSC must change
        ma.Update(new TValue(now.AddMinutes(9).Ticks, 50), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void DoscIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new DoscIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void DoscIndicator_MultipleHistoricalBars()
    {
        var indicator = new DoscIndicator { RsiPeriod = 5, Ema1Period = 3, Ema2Period = 2, SigPeriod = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(30, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 30; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void DoscIndicator_PeriodChange_UpdatesConfig()
    {
        var indicator = new DoscIndicator();
        indicator.RsiPeriod = 7;
        Assert.Equal(7, indicator.RsiPeriod);

        indicator.SigPeriod = 5;
        Assert.Equal(5, indicator.SigPeriod);
    }
}
