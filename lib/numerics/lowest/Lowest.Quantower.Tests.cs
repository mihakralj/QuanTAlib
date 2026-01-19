using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class LowestIndicatorTests
{
    [Fact]
    public void LowestIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LowestIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Low, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LOWEST - Rolling Minimum", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LowestIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new LowestIndicator { Period = 20 };
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void LowestIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new LowestIndicator { Period = 14 };
        Assert.Equal("LOWEST(14)", indicator.ShortName);
    }

    [Fact]
    public void LowestIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new LowestIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Lowest", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LowestIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LowestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LowestIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LowestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 92, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LowestIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LowestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LowestIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new LowestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 - i * 2,
                105 - i * 2,
                90 - i * 2,   // Low decreases
                102 - i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void LowestIndicator_DifferentSourceTypes_Work()
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
            var indicator = new LowestIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void LowestIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new LowestIndicator { Period = 10, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void LowestIndicator_TracksMinimum_Correctly()
    {
        var indicator = new LowestIndicator { Period = 5, Source = SourceType.Low };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with decreasing lows
        double[] lows = { 100, 95, 90, 92, 88 };
        for (int i = 0; i < lows.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 102, 110, lows[i], 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // The lowest should be 88 (most recent bar's low)
        double lastLowest = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(88, lastLowest);
    }

    [Fact]
    public void LowestIndicator_WindowSlides_Correctly()
    {
        var indicator = new LowestIndicator { Period = 3, Source = SourceType.Low };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Lows: 100, 80, 90, 95, 85
        double[] lows = { 100, 80, 90, 95, 85 };
        for (int i = 0; i < lows.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 102, 110, lows[i], 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After all bars, window contains [90, 95, 85], lowest should be 85
        double lastLowest = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(85, lastLowest);
    }

    [Fact]
    public void LowestIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            var indicator = new LowestIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < period + 10; i++)
            {
                indicator.HistoricalData.AddBar(
                    now.AddMinutes(i),
                    100 - i,
                    105 - i,
                    95 - i,
                    102 - i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(period + 10, indicator.LinesSeries[0].Count);
        }
    }
}
