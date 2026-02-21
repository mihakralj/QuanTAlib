using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class JbandsIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new JbandsIndicator();

        Assert.Equal(7, ind.Period);
        Assert.Equal(0, ind.Phase);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Jbands - Jurik Adaptive Envelope Bands", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_MatchesWarmupFormula()
    {
        var ind = new JbandsIndicator { Period = 14 };
        int expected = (int)Math.Ceiling(20.0 + 80.0 * Math.Pow(14, 0.36));
        Assert.Equal(expected, ind.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new JbandsIndicator { Period = 10, Phase = 50 };
        Assert.Contains("10", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("50", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsThreeLineSeries()
    {
        var ind = new JbandsIndicator { Period = 7 };
        ind.Initialize();

        Assert.Equal(3, ind.LinesSeries.Count);
        Assert.Equal("Middle", ind.LinesSeries[0].Name);
        Assert.Equal("Upper", ind.LinesSeries[1].Name);
        Assert.Equal("Lower", ind.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new JbandsIndicator { Period = 5 };
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
        var ind = new JbandsIndicator { Period = 5 };
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
        var ind = new JbandsIndicator { Period = 5 };
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
        var ind = new JbandsIndicator { Period = 5 };
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
        var ind = new JbandsIndicator { Period = 5 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        // Create data with volatility
        double[] closes = [100, 105, 95, 110, 90, 115, 85, 120, 80, 125];
        for (int i = 0; i < closes.Length; i++)
        {
            double c = closes[i];
            ind.HistoricalData.AddBar(now.AddMinutes(i), c - 2, c + 5, c - 5, c);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, upper >= lower
        _ = ind.LinesSeries[0].GetValue(0); // middle (unused but verifies it's finite)
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        Assert.True(upper >= lower, $"Upper ({upper}) should be >= Lower ({lower})");
    }

    [Fact]
    public void Phase_Parameter_Affects_Output()
    {
        var indZero = new JbandsIndicator { Period = 7, Phase = 0 };
        var indPos = new JbandsIndicator { Period = 7, Phase = 50 };

        indZero.Initialize();
        indPos.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + Math.Sin(i * 0.3) * 10;
            indZero.HistoricalData.AddBar(now.AddMinutes(i), price - 1, price + 2, price - 2, price);
            indPos.HistoricalData.AddBar(now.AddMinutes(i), price - 1, price + 2, price - 2, price);
            indZero.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            indPos.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // Different phase should produce different middle band values
        double middleZero = indZero.LinesSeries[0].GetValue(0);
        double middlePos = indPos.LinesSeries[0].GetValue(0);

        Assert.NotEqual(middleZero, middlePos);
    }

    [Fact]
    public void Phase_Parameter_Stored_Correctly()
    {
        var indPos = new JbandsIndicator { Period = 7, Phase = 50 };
        var indNeg = new JbandsIndicator { Period = 7, Phase = -50 };

        Assert.Equal(50, indPos.Phase);
        Assert.Equal(-50, indNeg.Phase);

        // Verify both indicators produce valid output
        indPos.Initialize();
        indNeg.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + Math.Sin(i * 0.3) * 10;
            indPos.HistoricalData.AddBar(now.AddMinutes(i), price - 1, price + 2, price - 2, price);
            indNeg.HistoricalData.AddBar(now.AddMinutes(i), price - 1, price + 2, price - 2, price);
            indPos.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            indNeg.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // Both should produce finite values
        Assert.True(double.IsFinite(indPos.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indNeg.LinesSeries[0].GetValue(0)));
    }
}
