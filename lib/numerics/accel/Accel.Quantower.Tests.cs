using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AccelIndicatorTests
{
    [Fact]
    public void AccelIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AccelIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ACCEL - Second Derivative (Acceleration)", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void AccelIndicator_MinHistoryDepths_IsThree()
    {
        var indicator = new AccelIndicator();
        Assert.Equal(3, indicator.MinHistoryDepths);
    }

    [Fact]
    public void AccelIndicator_ShortName_IsAccel()
    {
        var indicator = new AccelIndicator();
        Assert.Equal("ACCEL", indicator.ShortName);
    }

    [Fact]
    public void AccelIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Accel", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void AccelIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void AccelIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AccelIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AccelIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.Equal(0, indicator.LinesSeries[1].GetValue(i));
        }
    }

    [Fact]
    public void AccelIndicator_DifferentSourceTypes_Work()
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
            var indicator = new AccelIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void AccelIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new AccelIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AccelIndicator_LinearTrend_ProducesZeroAcceleration()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Linear trend: constant slope = zero acceleration
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 5; // constant +5 per bar
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastAccel = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastAccel, 6);
    }

    [Fact]
    public void AccelIndicator_AcceleratingTrend_ProducesPositiveAcceleration()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Quadratic trend: increasing slope = positive acceleration
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * i; // quadratic growth
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastAccel = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastAccel > 0);
    }

    [Fact]
    public void AccelIndicator_DeceleratingTrend_ProducesNegativeAcceleration()
    {
        var indicator = new AccelIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Decelerating trend: decreasing slope = negative acceleration
        for (int i = 0; i < 10; i++)
        {
            double price = 200 - i * i; // quadratic decay
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastAccel = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastAccel < 0);
    }
}
