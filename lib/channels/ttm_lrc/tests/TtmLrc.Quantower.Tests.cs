using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class TtmLrcIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new TtmLrcIndicator();

        Assert.Equal(100, ind.Period);
        Assert.Equal(PriceType.Close, ind.SourceType);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("TTM LRC - Linear Regression Channel", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_EqualsPeriod()
    {
        var ind = new TtmLrcIndicator { Period = 50 };
        Assert.Equal(50, ind.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new TtmLrcIndicator { Period = 75 };
        Assert.Contains("75", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsFiveLineSeries()
    {
        var ind = new TtmLrcIndicator { Period = 20 };
        ind.Initialize();

        Assert.Equal(5, ind.LinesSeries.Count);
        Assert.Equal("Midline", ind.LinesSeries[0].Name);
        Assert.Equal("Upper1", ind.LinesSeries[1].Name);
        Assert.Equal("Lower1", ind.LinesSeries[2].Name);
        Assert.Equal("Upper2", ind.LinesSeries[3].Name);
        Assert.Equal("Lower2", ind.LinesSeries[4].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new TtmLrcIndicator { Period = 5 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, ind.LinesSeries[0].Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(ind.LinesSeries[i].GetValue(0)), $"LinesSeries[{i}] should be finite");
        }
    }

    [Fact]
    public void ProcessUpdate_NewBar_Appends()
    {
        var ind = new TtmLrcIndicator { Period = 5 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);
        ind.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 92, 104);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, ind.LinesSeries[0].Count);
        Assert.Equal(2, ind.LinesSeries[1].Count);
        Assert.Equal(2, ind.LinesSeries[2].Count);
        Assert.Equal(2, ind.LinesSeries[3].Count);
        Assert.Equal(2, ind.LinesSeries[4].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_DoesNotThrow()
    {
        var ind = new TtmLrcIndicator { Period = 5 };
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
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        for (int lineIdx = 0; lineIdx < 5; lineIdx++)
        {
            Assert.Equal(30, ind.LinesSeries[lineIdx].Count);
            for (int i = 0; i < 30; i++)
            {
                Assert.True(double.IsFinite(ind.LinesSeries[lineIdx].GetValue(i)), $"LinesSeries[{lineIdx}][{i}] should be finite");
            }
        }
    }

    [Fact]
    public void Bands_Order_Correct()
    {
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Add some volatility to ensure non-zero stddev
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + Math.Sin(i * 0.5) * 10;
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double midline = ind.LinesSeries[0].GetValue(0);
        double upper1 = ind.LinesSeries[1].GetValue(0);
        double lower1 = ind.LinesSeries[2].GetValue(0);
        double upper2 = ind.LinesSeries[3].GetValue(0);
        double lower2 = ind.LinesSeries[4].GetValue(0);

        // Upper2 >= Upper1 >= Midline >= Lower1 >= Lower2
        Assert.True(upper2 >= upper1, $"Upper2 ({upper2}) should be >= Upper1 ({upper1})");
        Assert.True(upper1 >= midline, $"Upper1 ({upper1}) should be >= Midline ({midline})");
        Assert.True(midline >= lower1, $"Midline ({midline}) should be >= Lower1 ({lower1})");
        Assert.True(lower1 >= lower2, $"Lower1 ({lower1}) should be >= Lower2 ({lower2})");
    }

    [Fact]
    public void FirstBar_BandsCollapsed()
    {
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 100);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double midline = ind.LinesSeries[0].GetValue(0);
        double upper1 = ind.LinesSeries[1].GetValue(0);
        double lower1 = ind.LinesSeries[2].GetValue(0);
        double upper2 = ind.LinesSeries[3].GetValue(0);
        double lower2 = ind.LinesSeries[4].GetValue(0);

        // First bar: stddev = 0, so bands should be at midline
        Assert.Equal(100.0, midline, 1e-10);
        Assert.Equal(100.0, upper1, 1e-10);
        Assert.Equal(100.0, lower1, 1e-10);
        Assert.Equal(100.0, upper2, 1e-10);
        Assert.Equal(100.0, lower2, 1e-10);
    }

    [Fact]
    public void Bands_Symmetric_AroundMiddle()
    {
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var bars = new GBM(seed: 42).Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 20; i++)
        {
            var bar = bars[i];
            ind.HistoricalData.AddBar(bar.AsDateTime, bar.Open, bar.High, bar.Low, bar.Close);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double midline = ind.LinesSeries[0].GetValue(0);
        double upper1 = ind.LinesSeries[1].GetValue(0);
        double lower1 = ind.LinesSeries[2].GetValue(0);
        double upper2 = ind.LinesSeries[3].GetValue(0);
        double lower2 = ind.LinesSeries[4].GetValue(0);

        double upper1Dist = upper1 - midline;
        double lower1Dist = midline - lower1;
        double upper2Dist = upper2 - midline;
        double lower2Dist = midline - lower2;

        Assert.Equal(upper1Dist, lower1Dist, 1e-10);
        Assert.Equal(upper2Dist, lower2Dist, 1e-10);
        Assert.Equal(upper2Dist, upper1Dist * 2, 1e-10);
    }

    [Fact]
    public void LinearData_ZeroStdDev()
    {
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Perfect linear data: y = 100 + 2*i
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i * 2;
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price, price, price);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double midline = ind.LinesSeries[0].GetValue(0);
        double upper1 = ind.LinesSeries[1].GetValue(0);
        double lower1 = ind.LinesSeries[2].GetValue(0);
        double upper2 = ind.LinesSeries[3].GetValue(0);
        double lower2 = ind.LinesSeries[4].GetValue(0);

        // With perfect linear fit, stddev of residuals is 0
        Assert.Equal(midline, upper1, 1e-9);
        Assert.Equal(midline, lower1, 1e-9);
        Assert.Equal(midline, upper2, 1e-9);
        Assert.Equal(midline, lower2, 1e-9);
    }

    [Fact]
    public void DifferentPriceTypes_Work()
    {
        var indClose = new TtmLrcIndicator { Period = 10, SourceType = PriceType.Close };
        var indHigh = new TtmLrcIndicator { Period = 10, SourceType = PriceType.High };
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

        double closeMidline = indClose.LinesSeries[0].GetValue(0);
        double highMidline = indHigh.LinesSeries[0].GetValue(0);

        Assert.True(highMidline > closeMidline, "High price type should produce higher midline than Close");
    }

    [Fact]
    public void TrendingData_MiddleFollowsTrend()
    {
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + i * 2; // Strong uptrend
            ind.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, midline should be close to the current regression line value
        double midline = ind.LinesSeries[0].GetValue(0);
        double lastPrice = 100 + 29 * 2; // 158

        // Midline should be close to last price (within reasonable range for regression)
        Assert.True(Math.Abs(midline - lastPrice) < 10, $"Midline ({midline}) should be close to last price ({lastPrice})");
    }

    [Fact]
    public void Outer_Bands_Width_Double_Of_Inner()
    {
        var ind = new TtmLrcIndicator { Period = 10 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        var bars = new GBM(seed: 42).Fetch(20, now.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 20; i++)
        {
            var bar = bars[i];
            ind.HistoricalData.AddBar(bar.AsDateTime, bar.Open, bar.High, bar.Low, bar.Close);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double midline = ind.LinesSeries[0].GetValue(0);
        double upper1 = ind.LinesSeries[1].GetValue(0);
        double upper2 = ind.LinesSeries[3].GetValue(0);

        double inner1Sigma = upper1 - midline;
        double outer2Sigma = upper2 - midline;

        // ±2σ bands should be exactly twice as wide as ±1σ bands
        Assert.Equal(inner1Sigma * 2, outer2Sigma, 1e-10);
    }

    [Fact]
    public void DefaultPeriod100_HigherWarmup()
    {
        var ind = new TtmLrcIndicator(); // Default period = 100
        ind.Initialize();

        Assert.Equal(100, ind.MinHistoryDepths);
    }
}
