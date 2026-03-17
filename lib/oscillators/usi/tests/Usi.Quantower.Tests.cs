using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class UsiIndicatorTests
{
    [Fact]
    public void UsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UsiIndicator();

        Assert.Equal(28, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("USI - Ehlers Ultimate Strength Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void UsiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new UsiIndicator();

        Assert.Equal(0, UsiIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void UsiIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new UsiIndicator { Period = 14 };

        Assert.Contains("USI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void UsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new UsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Usi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void UsiIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new UsiIndicator { Period = 28 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void UsiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new UsiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void UsiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new UsiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void UsiIndicator_InternalIndicator_HandlesBarCorrection()
    {
        var ma = new Usi(5);
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
    public void UsiIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new UsiIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void UsiIndicator_MultipleHistoricalBars()
    {
        var indicator = new UsiIndicator { Period = 5 };
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
    public void UsiIndicator_PeriodChange_UpdatesConfig()
    {
        var indicator = new UsiIndicator();
        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 56;
        Assert.Equal(56, indicator.Period);
    }
}
