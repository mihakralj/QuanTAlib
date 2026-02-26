using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ReflexIndicatorTests
{
    [Fact]
    public void ReflexIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ReflexIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("REFLEX - Ehlers Reflex Indicator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ReflexIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ReflexIndicator();

        Assert.Equal(0, ReflexIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ReflexIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new ReflexIndicator { Period = 30 };

        Assert.Contains("REFLEX", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ReflexIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ReflexIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Reflex.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ReflexIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new ReflexIndicator { Period = 20 };

        indicator.Initialize();

        // After init, one line series should exist (Reflex is single output)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ReflexIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ReflexIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ReflexIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ReflexIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ReflexIndicator_InternalIndicator_HandlesBarCorrection()
    {
        // Test the underlying Reflex with isNew=false (bar correction)
        var ma = new Reflex(3);

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            ma.Update(new TValue(now.AddMinutes(i).Ticks, 100 + i), isNew: true);
        }

        double beforeCorrection = ma.Last.Value;

        // Correct last bar with a very different value
        ma.Update(new TValue(now.AddMinutes(9).Ticks, 200), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void ReflexIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new ReflexIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void ReflexIndicator_MultipleHistoricalBars()
    {
        var indicator = new ReflexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        // All values should be finite
        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void ReflexIndicator_PeriodChange_UpdatesConfig()
    {
        var indicator = new ReflexIndicator();
        indicator.Period = 25;
        Assert.Equal(25, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }
}
