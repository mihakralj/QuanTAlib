using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class RegchannelIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new RegchannelIndicator();

        Assert.Equal(20, ind.Period);
        Assert.Equal(2.0, ind.Multiplier);
        Assert.Equal(PriceType.Close, ind.SourceType);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Regchannel - Linear Regression Channel", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_EqualsPeriod()
    {
        var ind = new RegchannelIndicator { Period = 30 };
        Assert.Equal(30, ind.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new RegchannelIndicator { Period = 20, Multiplier = 2.5 };
        Assert.Contains("20", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsThreeLineSeries()
    {
        var ind = new RegchannelIndicator { Period = 14, Multiplier = 2.0 };
        ind.Initialize();

        Assert.Equal(3, ind.LinesSeries.Count);
        Assert.Equal("Middle", ind.LinesSeries[0].Name);
        Assert.Equal("Upper", ind.LinesSeries[1].Name);
        Assert.Equal("Lower", ind.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new RegchannelIndicator { Period = 5, Multiplier = 2.0 };
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
        var ind = new RegchannelIndicator { Period = 5, Multiplier = 2.0 };
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
        var ind = new RegchannelIndicator { Period = 5, Multiplier = 2.0 };
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
        var ind = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(30, ind.LinesSeries[0].Count);
        Assert.Equal(30, ind.LinesSeries[1].Count);
        Assert.Equal(30, ind.LinesSeries[2].Count);

        for (int i = 0; i < 30; i++)
        {
            Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void Bands_Order_Correct()
    {
        var ind = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Add some volatility to ensure non-zero stddev
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + Math.Sin(i * 0.5) * 10;
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        Assert.True(upper >= middle, $"Upper ({upper}) should be >= Middle ({middle})");
        Assert.True(lower <= middle, $"Lower ({lower}) should be <= Middle ({middle})");
    }

    [Fact]
    public void FirstBar_BandsCollapsed()
    {
        var ind = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 100);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        // First bar: stddev = 0, so bands should be at middle
        Assert.Equal(100.0, middle, 1e-10);
        Assert.Equal(100.0, upper, 1e-10);
        Assert.Equal(100.0, lower, 1e-10);
    }

    [Fact]
    public void Multiplier_AffectsBandWidth()
    {
        var ind1 = new RegchannelIndicator { Period = 10, Multiplier = 1.0 };
        var ind2 = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind1.Initialize();
        ind2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i * 0.5;
            ind1.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price);
            ind2.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price);
            ind1.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            ind2.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double width1 = ind1.LinesSeries[1].GetValue(0) - ind1.LinesSeries[2].GetValue(0);
        double width2 = ind2.LinesSeries[1].GetValue(0) - ind2.LinesSeries[2].GetValue(0);

        Assert.Equal(width2, width1 * 2, 1e-9);
    }

    [Fact]
    public void Bands_Symmetric_AroundMiddle()
    {
        var ind = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i;
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        double upperDist = upper - middle;
        double lowerDist = middle - lower;

        Assert.Equal(upperDist, lowerDist, 1e-10);
    }

    [Fact]
    public void LinearData_ZeroStdDev()
    {
        var ind = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Perfect linear data: y = 100 + i
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i;
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price, price, price);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        // With perfect linear fit, stddev of residuals is 0
        Assert.Equal(middle, upper, 1e-9);
        Assert.Equal(middle, lower, 1e-9);
    }

    [Fact]
    public void DifferentPriceTypes_Work()
    {
        var indClose = new RegchannelIndicator { Period = 10, Multiplier = 2.0, SourceType = PriceType.Close };
        var indHigh = new RegchannelIndicator { Period = 10, Multiplier = 2.0, SourceType = PriceType.High };
        indClose.Initialize();
        indHigh.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indClose.HistoricalData.AddBar(now.AddMinutes(i), 100, 120, 80, 100);
            indHigh.HistoricalData.AddBar(now.AddMinutes(i), 100, 120, 80, 100);
            indClose.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            indHigh.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double closeMiddle = indClose.LinesSeries[0].GetValue(0);
        double highMiddle = indHigh.LinesSeries[0].GetValue(0);

        Assert.True(highMiddle > closeMiddle, "High price type should produce higher middle than Close");
    }

    [Fact]
    public void TrendingData_MiddleFollowsTrend()
    {
        var ind = new RegchannelIndicator { Period = 10, Multiplier = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + i * 2; // Strong uptrend
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, middle should be close to the current regression line value
        double middle = ind.LinesSeries[0].GetValue(0);
        double lastPrice = 100 + 29 * 2; // 158

        // Middle should be close to last price (within reasonable range for regression)
        Assert.True(Math.Abs(middle - lastPrice) < 10, $"Middle ({middle}) should be close to last price ({lastPrice})");
    }
}
