using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ReverseEmaIndicatorTests
{
    [Fact]
    public void ReverseEmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ReverseEmaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("REVERSEEMA - Ehlers Reverse EMA", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ReverseEmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ReverseEmaIndicator();

        Assert.Equal(0, ReverseEmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ReverseEmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new ReverseEmaIndicator { Period = 20 };

        Assert.Contains("REVERSEEMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseEmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ReverseEmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("ReverseEma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseEmaIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new ReverseEmaIndicator { Period = 14 };

        indicator.Initialize();

        // After init, one line series should exist (ReverseEma is single output)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ReverseEmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ReverseEmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ReverseEmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ReverseEmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ReverseEmaIndicator_InternalIndicator_HandlesBarCorrection()
    {
        // Test the underlying ReverseEma with isNew=false (bar correction)
        var ma = new ReverseEma(3);

        var now = DateTime.UtcNow;
        ma.Update(new TValue(now.Ticks, 100), isNew: true);
        ma.Update(new TValue(now.AddMinutes(1).Ticks, 105), isNew: true);

        double beforeCorrection = ma.Last.Value;

        // Correct last bar
        ma.Update(new TValue(now.AddMinutes(1).Ticks, 110), isNew: false);
        double afterCorrection = ma.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
        Assert.True(double.IsFinite(afterCorrection));
    }

    [Fact]
    public void ReverseEmaIndicator_DifferentSourceTypes()
    {
        foreach (SourceType sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new ReverseEmaIndicator();
            indicator.Source = sourceType;
            Assert.Equal(sourceType, indicator.Source);
        }
    }

    [Fact]
    public void ReverseEmaIndicator_MultipleHistoricalBars()
    {
        var indicator = new ReverseEmaIndicator { Period = 5 };
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
    public void ReverseEmaIndicator_PeriodChange_UpdatesConfig()
    {
        var indicator = new ReverseEmaIndicator();
        indicator.Period = 25;
        Assert.Equal(25, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }
}
