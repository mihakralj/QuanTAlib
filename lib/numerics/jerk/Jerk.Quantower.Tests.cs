using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class JerkIndicatorTests
{
    [Fact]
    public void JerkIndicator_Constructor_SetsDefaults()
    {
        var indicator = new JerkIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("JERK - Third Derivative", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void JerkIndicator_MinHistoryDepths_IsFour()
    {
        var indicator = new JerkIndicator();
        Assert.Equal(4, indicator.MinHistoryDepths);
    }

    [Fact]
    public void JerkIndicator_ShortName_IsJerk()
    {
        var indicator = new JerkIndicator();
        Assert.Equal("JERK", indicator.ShortName);
    }

    [Fact]
    public void JerkIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Jerk", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void JerkIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void JerkIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void JerkIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void JerkIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new JerkIndicator();
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
    public void JerkIndicator_DifferentSourceTypes_Work()
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
            var indicator = new JerkIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void JerkIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new JerkIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void JerkIndicator_QuadraticTrend_ProducesZeroJerk()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Quadratic trend: constant acceleration = zero jerk
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * i; // constant accel = 2
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastJerk = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastJerk, 6);
    }

    [Fact]
    public void JerkIndicator_CubicTrend_ProducesConstantJerk()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Cubic trend: f(x) = x³ has third derivative = 6
        // Using f(i) = i³, the discrete third differences converge to 6
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * i * i; // cubic growth
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastJerk = indicator.LinesSeries[0].GetValue(0);
        // For f(x) = x³, discrete third difference = 6
        Assert.Equal(6.0, lastJerk, 6);
    }

    [Fact]
    public void JerkIndicator_LinearTrend_ProducesZeroJerk()
    {
        var indicator = new JerkIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Linear trend: zero accel = zero jerk
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 5; // constant slope
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastJerk = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastJerk, 6);
    }
}
