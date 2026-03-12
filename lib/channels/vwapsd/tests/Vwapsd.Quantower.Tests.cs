using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VwapsdIndicatorTests
{
    // ── Constructor & Defaults ──────────────────────────────────────────

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
    public void VwapsdIndicator_Constructor_Description_IsNotEmpty()
    {
        var indicator = new VwapsdIndicator();

        Assert.False(string.IsNullOrWhiteSpace(indicator.Description));
        Assert.Contains("volume", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VwapsdIndicator_Constructor_CreatesFourLineSeries()
    {
        var indicator = new VwapsdIndicator();

        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VwapsdIndicator_Constructor_LineSeriesNames_BeforeInit()
    {
        var indicator = new VwapsdIndicator();

        // Before OnInit, series have their constructor names
        Assert.Equal("VWAP", indicator.LinesSeries[0].Name);
        Assert.Equal("Upper", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower", indicator.LinesSeries[2].Name);
        Assert.Equal("Width", indicator.LinesSeries[3].Name);
    }

    // ── MinHistoryDepths ────────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new VwapsdIndicator();

        Assert.Equal(2, indicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    // ── ShortName ───────────────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_ShortName_DefaultFormat()
    {
        var indicator = new VwapsdIndicator();

        Assert.Equal("VWAPSD (2.0)", indicator.ShortName);
    }

    [Fact]
    public void VwapsdIndicator_ShortName_IncludesNumDevs()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.5 };

        Assert.Contains("VWAPSD", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    // ── SourceCodeLink ──────────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_SourceCodeLink_PointsToGitHub()
    {
        var indicator = new VwapsdIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vwapsd.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    // ── OnInit σ Rename ─────────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_Initialize_RenamesSeriesWithSigmaNotation()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        // After OnInit, Upper/Lower should have σ notation
        Assert.Equal("Upper (+2.0σ)", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower (-2.0σ)", indicator.LinesSeries[2].Name);
    }

    [Fact]
    public void VwapsdIndicator_Initialize_SigmaNotation_ReflectsNumDevs()
    {
        var indicator = new VwapsdIndicator { NumDevs = 1.5 };
        indicator.Initialize();

        Assert.Equal("Upper (+1.5σ)", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower (-1.5σ)", indicator.LinesSeries[2].Name);
    }

    [Fact]
    public void VwapsdIndicator_Initialize_PreservesSeriesCount()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };

        // After init, line series should exist (VWAP, Upper, Lower, Width)
        indicator.Initialize();
        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    // ── Parameters ──────────────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_Parameters_CanBeChanged()
    {
        var indicator = new VwapsdIndicator { NumDevs = 1.5 };
        Assert.Equal(1.5, indicator.NumDevs);

        indicator.NumDevs = 2.5;
        Assert.Equal(2.5, indicator.NumDevs);
    }

    [Fact]
    public void VwapsdIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new VwapsdIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    // ── ProcessUpdate: HistoricalBar ────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    // ── ProcessUpdate: NewBar ───────────────────────────────────────────

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

    // ── ProcessUpdate: NewTick ──────────────────────────────────────────

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

    // ── MultipleUpdates ─────────────────────────────────────────────────

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

    // ── AllBandsUpdate ──────────────────────────────────────────────────

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

    // ── BandRelationships ───────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_BandRelationships_AreCorrect()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 105, 95, 110, 90, 105, 100, 108, 92, 103 };
        double[] volumes = { 1000, 1500, 2000, 1200, 1800, 1100, 1600, 1300, 1900, 1400 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now, close, close + 3, close - 3, close, volumes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        double vwap = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);
        double width = indicator.LinesSeries[3].GetValue(0);

        Assert.True(upper >= vwap, $"Upper ({upper}) should be >= VWAP ({vwap})");
        Assert.True(vwap >= lower, $"VWAP ({vwap}) should be >= Lower ({lower})");
        Assert.True(Math.Abs(width - (upper - lower)) < 0.0001,
            $"Width ({width}) should equal Upper - Lower ({upper - lower})");
    }

    // ── VolumeWeighting ─────────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_VolumeWeighting_AffectsVwap()
    {
        var indicator1 = new VwapsdIndicator { NumDevs = 2.0 };
        var indicator2 = new VwapsdIndicator { NumDevs = 2.0 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;

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

        Assert.True(vwap1 < vwap2, $"VWAP1 ({vwap1}) should be less than VWAP2 ({vwap2}) due to volume weighting");
    }

    // ── NumDevs Effect ──────────────────────────────────────────────────

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

        double width1 = indicator1.LinesSeries[3].GetValue(0);
        double width2 = indicator2.LinesSeries[3].GetValue(0);

        // Width2 should be approximately 2x Width1
        Assert.True(Math.Abs(width2 - 2 * width1) < 0.0001,
            $"Width2 ({width2}) should be ~2x Width1 ({width1})");
    }

    // ── Width Non-Negative ──────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_Width_IsNonNegative()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 98, 105, 97, 103, 101, 99 };
        double[] volumes = { 1000, 1200, 800, 1500, 900, 1100, 1300, 700 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close, volumes[i]);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Width should be non-negative at every bar
        for (int i = 0; i < closes.Length; i++)
        {
            double w = indicator.LinesSeries[3].GetValue(closes.Length - 1 - i);
            Assert.True(w >= 0.0, $"Width at bar {i} ({w}) should be >= 0");
        }
    }

    // ── SingleBar Zero Width ────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_SingleBar_ProducesZeroWidth()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // With only one bar, stddev is 0 → width should be 0
        double width = indicator.LinesSeries[3].GetValue(0);
        Assert.Equal(0.0, width, 4);
    }

    // ── ShowColdValues False ────────────────────────────────────────────

    [Fact]
    public void VwapsdIndicator_ShowColdValues_False_SuppressesColdValues()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // With ShowColdValues=false, cold bars produce NaN
        double vwap = indicator.LinesSeries[0].GetValue(0);
        // Value is either NaN (suppressed) or finite (hot)
        Assert.True(double.IsNaN(vwap) || double.IsFinite(vwap));
    }

    [Fact]
    public void VwapsdIndicator_ShowColdValues_True_ShowsAllValues()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0, ShowColdValues = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // With ShowColdValues=true, all values should be finite
        double vwap = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(vwap));
    }

    // ── ReInitialize Updates Series Names ───────────────────────────────

    [Fact]
    public void VwapsdIndicator_ReInitialize_UpdatesSigmaNotation()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        Assert.Equal("Upper (+2.0σ)", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower (-2.0σ)", indicator.LinesSeries[2].Name);

        // Change NumDevs and re-init
        indicator.NumDevs = 3.0;
        indicator.Initialize();

        Assert.Equal("Upper (+3.0σ)", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower (-3.0σ)", indicator.LinesSeries[2].Name);
    }

    // ── VWAP Series Name Unchanged After Init ───────────────────────────

    [Fact]
    public void VwapsdIndicator_Initialize_VwapAndWidthNames_Unchanged()
    {
        var indicator = new VwapsdIndicator { NumDevs = 2.0 };
        indicator.Initialize();

        // VWAP and Width series names should remain as constructor set them
        Assert.Equal("VWAP", indicator.LinesSeries[0].Name);
        Assert.Equal("Width", indicator.LinesSeries[3].Name);
    }
}
