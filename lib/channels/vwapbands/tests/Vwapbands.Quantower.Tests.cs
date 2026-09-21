using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VwapbandsIndicatorTests
{
    [Fact]
    public void VwapbandsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VwapbandsIndicator();

        Assert.Equal(1.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VWAPBANDS - Volume Weighted Average Price with Standard Deviation Bands", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VwapbandsIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new VwapbandsIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VwapbandsIndicator_ShortName_IncludesMultiplier()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 2.5 };

        Assert.Contains("VWAPBANDS", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VwapbandsIndicator_Initialize_CreatesSixLineSeries()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (VWAP, Upper1, Lower1, Upper2, Lower2, Width)
        Assert.Equal(6, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VwapbandsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator.Initialize();

        // Add historical data with volume
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have values
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void VwapbandsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106, 1500);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VwapbandsIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void VwapbandsIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };
        double[] volumes = { 1000, 1500, 2000, 1200, 1800 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close, volumes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        // VWAP should be within price range
        double lastVwap = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastVwap >= 95 && lastVwap <= 110);
    }

    [Fact]
    public void VwapbandsIndicator_Parameters_CanBeChanged()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.5 };
        Assert.Equal(1.5, indicator.Multiplier);

        indicator.Multiplier = 2.5;
        Assert.Equal(2.5, indicator.Multiplier);
    }

    [Fact]
    public void VwapbandsIndicator_AllBandsUpdate_Correctly()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000 + (i * 100));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Verify all 6 line series have values
        Assert.Equal(6, indicator.LinesSeries.Count);
        foreach (var series in indicator.LinesSeries)
        {
            Assert.Equal(5, series.Count);
            Assert.True(double.IsFinite(series.GetValue(0)));
        }
    }

    [Fact]
    public void VwapbandsIndicator_BandRelationships_AreCorrect()
    {
        var indicator = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add varied data to generate band width
        double[] closes = { 100, 105, 95, 110, 90, 105, 100, 108, 92, 103 };
        double[] volumes = { 1000, 1500, 2000, 1200, 1800, 1100, 1600, 1300, 1900, 1400 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now, close, close + 3, close - 3, close, volumes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Get last values: VWAP=0, Upper1=1, Lower1=2, Upper2=3, Lower2=4, Width=5
        double vwap = indicator.LinesSeries[0].GetValue(0);
        double upper1 = indicator.LinesSeries[1].GetValue(0);
        double lower1 = indicator.LinesSeries[2].GetValue(0);
        double upper2 = indicator.LinesSeries[3].GetValue(0);
        double lower2 = indicator.LinesSeries[4].GetValue(0);
        double width = indicator.LinesSeries[5].GetValue(0);

        // Band relationships: Upper2 > Upper1 > VWAP > Lower1 > Lower2
        Assert.True(upper2 >= upper1, $"Upper2 ({upper2}) should be >= Upper1 ({upper1})");
        Assert.True(upper1 >= vwap, $"Upper1 ({upper1}) should be >= VWAP ({vwap})");
        Assert.True(vwap >= lower1, $"VWAP ({vwap}) should be >= Lower1 ({lower1})");
        Assert.True(lower1 >= lower2, $"Lower1 ({lower1}) should be >= Lower2 ({lower2})");

        // Width = Upper1 - Lower1 (2 × multiplier × StdDev)
        Assert.True(Math.Abs(width - (upper1 - lower1)) < 0.0001,
            $"Width ({width}) should equal Upper1 - Lower1 ({upper1 - lower1})");
    }

    [Fact]
    public void VwapbandsIndicator_VolumeWeighting_AffectsVwap()
    {
        var indicator1 = new VwapbandsIndicator { Multiplier = 1.0 };
        var indicator2 = new VwapbandsIndicator { Multiplier = 1.0 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same prices but different volume distributions
        // Process both bars for each indicator

        // Indicator1: high volume on low price, low volume on high price
        indicator1.HistoricalData.AddBar(now, 100, 102, 98, 100, 10000);
        indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator1.HistoricalData.AddBar(now.AddMinutes(1), 110, 112, 108, 110, 100);
        indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Indicator2: low volume on low price, high volume on high price
        indicator2.HistoricalData.AddBar(now, 100, 102, 98, 100, 100);
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator2.HistoricalData.AddBar(now.AddMinutes(1), 110, 112, 108, 110, 10000);
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double vwap1 = indicator1.LinesSeries[0].GetValue(0);
        double vwap2 = indicator2.LinesSeries[0].GetValue(0);

        // VWAP1 should be lower (weighted toward 100 due to high volume at low price)
        // VWAP2 should be higher (weighted toward 110 due to high volume at high price)
        Assert.True(vwap1 < vwap2, $"VWAP1 ({vwap1}) should be less than VWAP2 ({vwap2}) due to volume weighting");
    }
}
