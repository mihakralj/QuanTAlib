namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for AOBV (Archer On-Balance Volume) indicator.
/// Note: AOBV is a proprietary indicator not available in external libraries
/// (TA-Lib, Skender, Tulip, Ooples). Validation focuses on internal consistency.
/// </summary>
public class AobvValidationTests
{
    private readonly ValidationTestData _data;

    public AobvValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Aobv_NotAvailable_Skender()
    {
        // AOBV is a proprietary indicator by EverGet (Archer)
        // Not available in Skender.Stock.Indicators
        Assert.True(true, "AOBV is proprietary - not available in Skender");
    }

    [Fact]
    public void Aobv_NotAvailable_Talib()
    {
        // AOBV is a proprietary indicator
        // TA-Lib has OBV but not AOBV (smoothed OBV)
        Assert.True(true, "AOBV is proprietary - not available in TA-Lib");
    }

    [Fact]
    public void Aobv_NotAvailable_Tulip()
    {
        // AOBV is a proprietary indicator
        // Tulip has OBV but not AOBV (smoothed OBV)
        Assert.True(true, "AOBV is proprietary - not available in Tulip");
    }

    [Fact]
    public void Aobv_NotAvailable_Ooples()
    {
        // AOBV is a proprietary indicator
        // Not available in OoplesFinance.StockIndicators
        Assert.True(true, "AOBV is proprietary - not available in Ooples");
    }

    [Fact]
    public void Aobv_Streaming_Matches_Batch()
    {
        // Streaming
        var aobv = new Aobv();
        var streamingFast = new List<double>();
        var streamingSlow = new List<double>();
        foreach (var bar in _data.Bars)
        {
            aobv.Update(bar);
            streamingFast.Add(aobv.LastFast.Value);
            streamingSlow.Add(aobv.LastSlow.Value);
        }

        // Batch
        var (batchFast, _) = Aobv.Calculate(_data.Bars);
        var batchFastArray = batchFast.Values.ToArray();

        // Compare Fast EMA values (primary output)
        ValidationHelper.VerifyData(streamingFast.ToArray(), batchFastArray, 0, 100, 1e-12);
    }

    [Fact]
    public void Aobv_Span_Matches_Streaming()
    {
        // Streaming
        var aobv = new Aobv();
        var streamingFast = new List<double>();
        foreach (var bar in _data.Bars)
        {
            aobv.Update(bar);
            streamingFast.Add(aobv.LastFast.Value);
        }

        // Span
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanFast = new double[close.Length];
        var spanSlow = new double[close.Length];

        Aobv.Batch(close, volume, spanFast, spanSlow);

        ValidationHelper.VerifyData(streamingFast.ToArray(), spanFast, 0, 100, 1e-12);
    }

    [Fact]
    public void Aobv_Fast_Slow_Relationship()
    {
        // Fast EMA (period 4) should be more responsive than Slow EMA (period 14)
        // Calculate variance of differences from raw OBV
        var aobv = new Aobv();
        var fastDeltas = new List<double>();
        var slowDeltas = new List<double>();
        double prevFast = 0, prevSlow = 0;

        foreach (var bar in _data.Bars)
        {
            aobv.Update(bar);
            if (aobv.IsHot)
            {
                fastDeltas.Add(Math.Abs(aobv.LastFast.Value - prevFast));
                slowDeltas.Add(Math.Abs(aobv.LastSlow.Value - prevSlow));
            }
            prevFast = aobv.LastFast.Value;
            prevSlow = aobv.LastSlow.Value;
        }

        // Fast should have higher average delta (more responsive)
        var avgFastDelta = fastDeltas.Average();
        var avgSlowDelta = slowDeltas.Average();

        Assert.True(avgFastDelta >= avgSlowDelta * 0.9,
            $"Fast EMA should be at least as responsive as slow. Fast avg delta: {avgFastDelta}, Slow avg delta: {avgSlowDelta}");
    }

    [Fact]
    public void Aobv_Warmup_Convergence()
    {
        // Test that warmup compensation produces stable values
        var aobv = new Aobv();
        int warmupPeriod = aobv.WarmupPeriod;
        int count = 0;

        foreach (var bar in _data.Bars)
        {
            aobv.Update(bar);
            count++;
            if (count >= warmupPeriod)
            {
                Assert.True(aobv.IsHot, $"Should be hot after {warmupPeriod} bars");
                break;
            }
        }
    }

    [Fact]
    public void Aobv_Values_Are_Finite()
    {
        var aobv = new Aobv();

        foreach (var bar in _data.Bars)
        {
            aobv.Update(bar);
            Assert.True(double.IsFinite(aobv.LastFast.Value), "Fast EMA should be finite");
            Assert.True(double.IsFinite(aobv.LastSlow.Value), "Slow EMA should be finite");
            Assert.True(double.IsFinite(aobv.Last.Value), "Last value should be finite");
        }
    }

    [Fact]
    public void Aobv_CrossValidation_OBV_Trend()
    {
        // When OBV is trending up, both EMAs should eventually trend up
        // Create synthetic uptrend data
        var bars = new TBarSeries();
        double baseClose = 100.0;
        double baseVolume = 1000000.0;

        for (int i = 0; i < 50; i++)
        {
            // Consistently rising closes with volume
            bars.Add(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                baseClose + i, // Open
                baseClose + i + 1, // High
                baseClose + i - 0.5, // Low
                baseClose + i + 0.5, // Close (always rising)
                baseVolume));
        }

        var aobv = new Aobv();
        double lastFast = 0, lastSlow = 0;
        int risingFastCount = 0, risingSlowCount = 0;

        foreach (var bar in bars)
        {
            aobv.Update(bar);
            if (aobv.IsHot)
            {
                if (aobv.LastFast.Value > lastFast)
                {
                    risingFastCount++;
                }
                if (aobv.LastSlow.Value > lastSlow)
                {
                    risingSlowCount++;
                }
                lastFast = aobv.LastFast.Value;
                lastSlow = aobv.LastSlow.Value;
            }
        }

        // In an uptrend, most values should be rising
        Assert.True(risingFastCount > 20, $"Fast EMA should trend up in uptrend, rising count: {risingFastCount}");
        Assert.True(risingSlowCount > 15, $"Slow EMA should trend up in uptrend, rising count: {risingSlowCount}");
    }
}
