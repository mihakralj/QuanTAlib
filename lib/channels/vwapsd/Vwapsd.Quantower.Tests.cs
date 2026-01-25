using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VwapsdIndicatorTests
{
    [Fact]
    public void VwapsdIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VwapsdIndicator();

        Assert.Equal(2.0, indicator.NumDevs);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VWAPSD - Volume Weighted Average Price with Configurable Standard Deviation Bands", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VwapsdIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new VwapsdIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VwapsdIndicator_ShortName_IncludesNumDevs()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.5 };

        Assert.Contains("VWAPSD", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VwapsdIndicator_Initialize_CreatesFourLineSeries()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (VWAP, Upper, Lower, Width)
        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VwapsdIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
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
    public void VwapsdIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106, 1500);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VwapsdIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
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
    public void VwapsdIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
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
    public void VwapsdIndicator_Parameters_CanBeChanged()
    {
        var indicator = new VwapsdIndicator { NumDevs = 1.5 };
        Assert.Equal(1.5, indicator.NumDevs);

        indicator.NumDevs = 2.5;
        Assert.Equal(2.5, indicator.NumDevs);
    }

    [Fact]
    public void VwapsdIndicator_AllBandsUpdate_Correctly()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000 + i * 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Verify all 4 line series have values (VWAP, Upper, Lower, Width)
        Assert.Equal(4, indicator.LinesSeries.Count);
        foreach (var series in indicator.LinesSeries)
        {
            Assert.Equal(5, series.Count);
            Assert.True(double.IsFinite(series.GetValue(0)));
        }
    }

    [Fact]
    public void VwapsdIndicator_BandRelationships_AreCorrect()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
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

        // Get last values: VWAP=0, Upper=1, Lower=2, Width=3
        double vwap = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);
        double width = indicator.LinesSeries[3].GetValue(0);

        // Band relationships: Upper > VWAP > Lower
        Assert.True(upper >= vwap, $"Upper ({upper}) should be >= VWAP ({vwap})");
        Assert.True(vwap >= lower, $"VWAP ({vwap}) should be >= Lower ({lower})");

        // Width = Upper - Lower (2 × numDevs × StdDev)
        Assert.True(Math.Abs(width - (upper - lower)) < 0.0001,
            $"Width ({width}) should equal Upper - Lower ({upper - lower})");
    }

    [Fact]
    public void VwapsdIndicator_VolumeWeighting_AffectsVwap()
    {
        var indicator1 = new VwapsdIndicator { NumDevs = 2.0 };
        var indicator2 = new VwapsdIndicator { NumDevs = 2.0 };
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

    [Fact]
    public void VwapsdIndicator_NumDevs_AffectsBandWidth()
    {
        var indicator1 = new VwapsdIndicator { NumDevs = 1.0 };
        var indicator2 = new VwapsdIndicator { NumDevs = 2.0 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 105, 95, 110, 90 };
        double[] volumes = { 1000, 1500, 2000, 1200, 1800 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator1.HistoricalData.AddBar(now, close, close + 3, close - 3, close, volumes[i]);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.HistoricalData.AddBar(now, close, close + 3, close - 3, close, volumes[i]);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Width should be proportional to numDevs
        double width1 = indicator1.LinesSeries[3].GetValue(0);
        double width2 = indicator2.LinesSeries[3].GetValue(0);

        // Width2 should be approximately 2x Width1
        Assert.True(Math.Abs(width2 - 2 * width1) < 0.0001,
            $"Width2 ({width2}) should be ~2x Width1 ({width1})");
    }
}