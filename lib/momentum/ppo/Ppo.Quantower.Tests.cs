using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PpoIndicatorTests
{
    [Fact]
    public void PpoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PpoIndicator();

        Assert.Equal("PPO - Percentage Price Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(12, indicator.FastPeriod);
        Assert.Equal(26, indicator.SlowPeriod);
        Assert.Equal(9, indicator.SignalPeriod);
    }

    [Fact]
    public void PpoIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new PpoIndicator();

        Assert.Equal(0, PpoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PpoIndicator_ShortName_IncludesPeriods()
    {
        var indicator = new PpoIndicator();
        indicator.Initialize();

        Assert.Equal("PPO(12,26,9):Close", indicator.ShortName);
    }

    [Fact]
    public void PpoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PpoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ppo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PpoIndicator_Initialize_CreatesThreeLineSeries()
    {
        var indicator = new PpoIndicator();
        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count);
        Assert.Equal("PPO", indicator.LinesSeries[0].Name);
        Assert.Equal("Signal", indicator.LinesSeries[1].Name);
        Assert.Equal("Histogram", indicator.LinesSeries[2].Name);
    }

    [Fact]
    public void PpoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PpoIndicator
        {
            FastPeriod = 2,
            SlowPeriod = 5,
            SignalPeriod = 2,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100 + i);
        }

        var args = new UpdateArgs(UpdateReason.HistoricalBar);

        for (int i = 0; i < 10; i++)
        {
            indicator.ProcessUpdate(args);
        }

        double ppo = indicator.LinesSeries[0].GetValue(0);
        double signal = indicator.LinesSeries[1].GetValue(0);
        double hist = indicator.LinesSeries[2].GetValue(0);

        Assert.False(double.IsNaN(ppo));
        Assert.False(double.IsNaN(signal));
        Assert.False(double.IsNaN(hist));
    }

    [Fact]
    public void PpoIndicator_MultipleUpdates_ProducesFiniteSequence()
    {
        var indicator = new PpoIndicator
        {
            FastPeriod = 3,
            SlowPeriod = 7,
            SignalPeriod = 3,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(30, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 30; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void PpoIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };

        foreach (var source in sources)
        {
            var indicator = new PpoIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void PpoIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new PpoIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void PpoIndicator_HistogramEqualsLineDifference()
    {
        var indicator = new PpoIndicator
        {
            FastPeriod = 3,
            SlowPeriod = 7,
            SignalPeriod = 3,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Histogram should equal PPO line - Signal line
        double ppo = indicator.LinesSeries[0].GetValue(0);
        double signal = indicator.LinesSeries[1].GetValue(0);
        double hist = indicator.LinesSeries[2].GetValue(0);

        Assert.Equal(ppo - signal, hist, 10);
    }
}
