using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class HighestIndicatorTests
{
    [Fact]
    public void HighestIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HighestIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.High, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HIGHEST - Rolling Maximum", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HighestIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new HighestIndicator { Period = 20 };
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void HighestIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new HighestIndicator { Period = 14 };
        Assert.Equal("HIGHEST(14)", indicator.ShortName);
    }

    [Fact]
    public void HighestIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new HighestIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Highest", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void HighestIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HighestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HighestIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HighestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HighestIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new HighestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void HighestIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new HighestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                110 + i * 2,   // High increases
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void HighestIndicator_DifferentSourceTypes_Work()
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
            var indicator = new HighestIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void HighestIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new HighestIndicator { Period = 10, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void HighestIndicator_TracksMaximum_Correctly()
    {
        var indicator = new HighestIndicator { Period = 5, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with increasing highs
        double[] highs = { 100, 105, 110, 108, 112 };
        for (int i = 0; i < highs.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 95, highs[i], 90, 98);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // The highest should be 112 (most recent bar's high)
        double lastHighest = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(112, lastHighest);
    }

    [Fact]
    public void HighestIndicator_WindowSlides_Correctly()
    {
        var indicator = new HighestIndicator { Period = 3, Source = SourceType.High };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Highs: 100, 120, 110, 105, 115
        double[] highs = { 100, 120, 110, 105, 115 };
        for (int i = 0; i < highs.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 95, highs[i], 90, 98);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // After all bars, window contains [110, 105, 115], highest should be 115
        double lastHighest = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(115, lastHighest);
    }

    [Fact]
    public void HighestIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            var indicator = new HighestIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < period + 10; i++)
            {
                indicator.HistoricalData.AddBar(
                    now.AddMinutes(i),
                    100 + i,
                    105 + i,
                    95 + i,
                    102 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(period + 10, indicator.LinesSeries[0].Count);
        }
    }
}
