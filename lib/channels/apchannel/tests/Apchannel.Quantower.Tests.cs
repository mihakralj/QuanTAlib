using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class ApchannelIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var indicator = new ApchannelIndicator();

        Assert.Equal(0.2, indicator.Alpha);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Apchannel - Adaptive Price Channel", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_IsCorrect()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.1 };
        Assert.Equal(30, indicator.MinHistoryDepths); // ceil(3.0 / 0.1) = 30

        indicator = new ApchannelIndicator { Alpha = 0.2 };
        Assert.Equal(15, indicator.MinHistoryDepths); // ceil(3.0 / 0.2) = 15

        indicator = new ApchannelIndicator { Alpha = 0.5 };
        Assert.Equal(6, indicator.MinHistoryDepths); // ceil(3.0 / 0.5) = 6
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.15 };
        Assert.Contains("0.15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_CreatesThreeLineSeries()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.2 };
        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count);
        Assert.Equal("Middle", indicator.LinesSeries[0].Name);
        Assert.Equal("Upper", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower", indicator.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[2].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);
        Assert.Equal(20, indicator.LinesSeries[1].Count);
        Assert.Equal(20, indicator.LinesSeries[2].Count);

        // All values should be finite
        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void BandRelationship_UpperAboveLowerBelowMiddle()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 1000);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, upper > middle > lower
        double middle = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);

        Assert.True(upper > middle, $"Upper ({upper}) should be > Middle ({middle})");
        Assert.True(lower < middle, $"Lower ({lower}) should be < Middle ({middle})");
    }

    [Fact]
    public void Alpha_AffectsResponsiveness()
    {
        var now = DateTime.UtcNow;

        // Slow response with low alpha
        var slowIndicator = new ApchannelIndicator { Alpha = 0.1 };
        slowIndicator.Initialize();

        // Fast response with high alpha
        var fastIndicator = new ApchannelIndicator { Alpha = 0.5 };
        fastIndicator.Initialize();

        // Initialize with stable prices
        for (int i = 0; i < 10; i++)
        {
            slowIndicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            slowIndicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));

            fastIndicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            fastIndicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // Then add a price spike
        for (int i = 10; i < 15; i++)
        {
            slowIndicator.HistoricalData.AddBar(now.AddMinutes(i), 120, 130, 115, 125, 1000);
            slowIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

            fastIndicator.HistoricalData.AddBar(now.AddMinutes(i), 120, 130, 115, 125, 1000);
            fastIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        }

        // Fast alpha should adapt more quickly to the new price level
        double slowUpper = slowIndicator.LinesSeries[1].GetValue(0);
        double fastUpper = fastIndicator.LinesSeries[1].GetValue(0);

        // Fast response should be closer to 130 (recent high)
        Assert.True(fastUpper > slowUpper, $"Fast upper ({fastUpper}) should be > Slow upper ({slowUpper})");
    }

    [Fact]
    public void Alpha_CanBeChanged()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.2 };
        Assert.Equal(0.2, indicator.Alpha);

        indicator.Alpha = 0.35;
        Assert.Equal(0.35, indicator.Alpha);
    }

    [Fact]
    public void MiddleLine_IsMidpointOfBands()
    {
        var indicator = new ApchannelIndicator { Alpha = 0.2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);

        double expectedMiddle = (upper + lower) / 2.0;
        Assert.Equal(expectedMiddle, middle, 6); // 6 decimal precision
    }
}
