using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MidpointIndicatorTests
{
    [Fact]
    public void MidpointIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MidpointIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MIDPOINT - Rolling Range Midpoint", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MidpointIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new MidpointIndicator { Period = 20 };
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void MidpointIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new MidpointIndicator { Period = 14 };
        Assert.Equal("MIDPOINT(14)", indicator.ShortName);
    }

    [Fact]
    public void MidpointIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new MidpointIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Midpoint", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void MidpointIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MidpointIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MidpointIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MidpointIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 92, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MidpointIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new MidpointIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MidpointIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new MidpointIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + (i * 2),
                105 + (i * 2),
                95 + (i * 2),
                102 + (i * 2));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void MidpointIndicator_DifferentSourceTypes_Work()
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
            var indicator = new MidpointIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void MidpointIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new MidpointIndicator { Period = 10, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MidpointIndicator_ComputesMidpoint_Correctly()
    {
        var indicator = new MidpointIndicator { Period = 5, Source = SourceType.Close };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Close prices: 100, 110, 90, 105, 95
        // Highest = 110, Lowest = 90, Midpoint = (110 + 90) / 2 = 100
        double[] closes = { 100, 110, 90, 105, 95 };
        for (int i = 0; i < closes.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closes[i], closes[i] + 5, closes[i] - 5, closes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastMidpoint = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(100, lastMidpoint);
    }

    [Fact]
    public void MidpointIndicator_WindowSlides_Correctly()
    {
        var indicator = new MidpointIndicator { Period = 3, Source = SourceType.Close };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Closes: 100, 120, 80, 90, 110
        // After 5 bars, window = [80, 90, 110]
        // Highest = 110, Lowest = 80, Midpoint = 95
        double[] closes = { 100, 120, 80, 90, 110 };
        for (int i = 0; i < closes.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closes[i], closes[i] + 5, closes[i] - 5, closes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastMidpoint = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(95, lastMidpoint);
    }

    [Fact]
    public void MidpointIndicator_SymmetricRange_MidpointEqualsCenter()
    {
        var indicator = new MidpointIndicator { Period = 3, Source = SourceType.Close };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Symmetric: 50, 100, 150 -> midpoint = (150 + 50) / 2 = 100
        double[] closes = { 50, 100, 150 };
        for (int i = 0; i < closes.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), closes[i], closes[i] + 5, closes[i] - 5, closes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double midpoint = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(100, midpoint);
    }

    [Fact]
    public void MidpointIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            var indicator = new MidpointIndicator { Period = period };
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
