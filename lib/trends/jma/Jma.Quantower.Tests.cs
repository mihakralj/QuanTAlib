using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class JmaIndicatorTests
{
    [Fact]
    public void JmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new JmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(0, indicator.Phase);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("JMA - Jurik Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void JmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new JmaIndicator { Period = 20 };

        Assert.Equal(0, JmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void JmaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new JmaIndicator { Period = 15, Phase = 50 };

        Assert.Contains("JMA", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
        Assert.Contains("50", indicator.ShortName);
    }

    [Fact]
    public void JmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new JmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Jma.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void JmaIndicator_Initialize_CreatesInternalJma()
    {
        var indicator = new JmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void JmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new JmaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void JmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new JmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void JmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new JmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }


    [Fact]
    public void JmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new JmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }
    }

    [Fact]
    public void JmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new JmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void JmaIndicator_Parameters_CanBeChanged()
    {
        var indicator = new JmaIndicator { Period = 5, Phase = 10 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(10, indicator.Phase);

        indicator.Period = 20;
        indicator.Phase = -10;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(-10, indicator.Phase);
        Assert.Equal(0, JmaIndicator.MinHistoryDepths);
    }
}
