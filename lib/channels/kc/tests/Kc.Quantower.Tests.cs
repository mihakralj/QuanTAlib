using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class KcIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new KcIndicator();

        Assert.Equal(20, ind.Period);
        Assert.Equal(2.0, ind.Multiplier);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Kc - Keltner Channel", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_EqualsPeriodTimesTwo()
    {
        var ind = new KcIndicator { Period = 15 };
        Assert.Equal(30, ind.MinHistoryDepths); // Period * 2
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new KcIndicator { Period = 12, Multiplier = 1.5 };
        Assert.Contains("12", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("1.5", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsThreeLineSeries()
    {
        var ind = new KcIndicator { Period = 14, Multiplier = 2.0 };
        ind.Initialize();

        Assert.Equal(3, ind.LinesSeries.Count);
        Assert.Equal("Middle", ind.LinesSeries[0].Name);
        Assert.Equal("Upper", ind.LinesSeries[1].Name);
        Assert.Equal("Lower", ind.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new KcIndicator { Period = 3, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, ind.LinesSeries[0].Count);
        Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_Appends()
    {
        var ind = new KcIndicator { Period = 3, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);
        ind.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 92, 104);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_DoesNotThrow()
    {
        var ind = new KcIndicator { Period = 5, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 105, 95, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void MultipleUpdates_ProducesFiniteSeries()
    {
        var ind = new KcIndicator { Period = 5, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, ind.LinesSeries[0].Count);
        Assert.Equal(20, ind.LinesSeries[1].Count);
        Assert.Equal(20, ind.LinesSeries[2].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void Bands_Order_Correct()
    {
        var ind = new KcIndicator { Period = 5, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Create bars with some volatility
        for (int i = 0; i < 10; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100, 1000);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        // After warmup with volatility, upper > middle > lower
        Assert.True(upper >= middle, $"Upper ({upper}) should be >= Middle ({middle})");
        Assert.True(lower <= middle, $"Lower ({lower}) should be <= Middle ({middle})");
    }

    [Fact]
    public void Bands_Expand_WithVolatility()
    {
        var ind = new KcIndicator { Period = 5, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;

        // First few bars: low volatility
        for (int i = 0; i < 5; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double lowVolWidth = ind.LinesSeries[1].GetValue(0) - ind.LinesSeries[2].GetValue(0);

        // Next bars: high volatility
        for (int i = 5; i < 15; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100, 120, 80, 100);
            ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        }

        double highVolWidth = ind.LinesSeries[1].GetValue(0) - ind.LinesSeries[2].GetValue(0);

        Assert.True(highVolWidth > lowVolWidth, "Higher volatility should produce wider bands");
    }

    [Fact]
    public void FirstBar_AllBandsEqualClose()
    {
        var ind = new KcIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 105);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        // First bar: all equal close (no ATR yet)
        Assert.Equal(105.0, middle, 1e-10);
        Assert.Equal(105.0, upper, 1e-10);
        Assert.Equal(105.0, lower, 1e-10);
    }

    [Fact]
    public void Multiplier_AffectsBandWidth()
    {
        var ind1 = new KcIndicator { Period = 10, Multiplier = 1.0 };
        var ind2 = new KcIndicator { Period = 10, Multiplier = 2.0 };
        ind1.Initialize();
        ind2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind1.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100);
            ind2.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100);
            ind1.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            ind2.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double width1 = ind1.LinesSeries[1].GetValue(0) - ind1.LinesSeries[2].GetValue(0);
        double width2 = ind2.LinesSeries[1].GetValue(0) - ind2.LinesSeries[2].GetValue(0);

        Assert.Equal(width2, width1 * 2, 1e-9);
    }
}
