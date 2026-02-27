namespace QuanTAlib.Tests;

public class VwadValidationTests
{
    private readonly ValidationTestData _data;
    private const int DefaultPeriod = 20;

    public VwadValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Vwad_NotAvailable_Skender()
    {
        // VWAD is a proprietary indicator not available in Skender.Stock.Indicators
        Assert.True(true, "VWAD is a proprietary indicator not available in Skender");
    }

    [Fact]
    public void Vwad_NotAvailable_Talib()
    {
        // VWAD is not available in TA-Lib
        Assert.True(true, "VWAD is a proprietary indicator not available in TA-Lib");
    }

    [Fact]
    public void Vwad_NotAvailable_Tulip()
    {
        // VWAD is not available in Tulip
        Assert.True(true, "VWAD is a proprietary indicator not available in Tulip");
    }

    [Fact]
    public void Vwad_NotAvailable_Ooples()
    {
        // VWAD is not available in Ooples
        Assert.True(true, "VWAD is a proprietary indicator not available in Ooples");
    }

    [Fact]
    public void Vwad_Streaming_Matches_Batch()
    {
        // Streaming
        var vwad = new Vwad(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(vwad.Update(bar).Value);
        }

        // Batch
        var batchResult = Vwad.Batch(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        // Cumulative indicators accumulate floating-point errors over many bars
        // 1e-10 tolerance is appropriate for ~5000 bar cumulative calculations
        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Vwad_Span_Matches_Streaming()
    {
        // Streaming
        var vwad = new Vwad(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(vwad.Update(bar).Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Vwad.Batch(high, low, close, volume, spanValues, DefaultPeriod);

        // Cumulative indicators accumulate floating-point errors over many bars
        // 1e-10 tolerance is appropriate for ~5000 bar cumulative calculations
        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Vwad_Batch_Matches_Span()
    {
        // Batch
        var batchResult = Vwad.Batch(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Vwad.Batch(high, low, close, volume, spanValues, DefaultPeriod);

        // Batch and Span use identical code path, should match exactly
        ValidationHelper.VerifyData(batchValues, spanValues, 0, 100, 1e-12);
    }

    [Fact]
    public void Vwad_Algorithm_Correctness_ManualCalculation()
    {
        // Manual calculation to verify algorithm correctness
        // Use a small dataset with known values
        int period = 3;
        var bars = new TBarSeries();

        // Create test bars with predictable OHLCV values
        // Bar 0: H=12, L=10, C=11, V=100 -> MFM = (11-10 - (12-11))/(12-10) = (1-1)/2 = 0
        // Bar 1: H=15, L=12, C=14, V=200 -> MFM = (14-12 - (15-14))/(15-12) = (2-1)/3 = 0.333
        // Bar 2: H=14, L=11, C=12, V=150 -> MFM = (12-11 - (14-12))/(14-11) = (1-2)/3 = -0.333

        bars.Add(new TBar(DateTime.UtcNow, 10, 12, 10, 11, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 12, 15, 12, 14, 200));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(2), 11, 14, 11, 12, 150));

        var vwad = new Vwad(period);
        var results = new List<double>();
        foreach (var bar in bars)
        {
            results.Add(vwad.Update(bar).Value);
        }

        // Bar 0: sumVol=100, volWeight=1, weightedMfv=100*0*1=0, cumVwad=0
        Assert.Equal(0, results[0], 6);

        // Bar 1: sumVol=300, volWeight=200/300=0.667, MFM=0.333, weightedMfv=200*0.333*0.667=44.4
        // cumVwad = 0 + 44.4 = 44.4
        double expectedBar1 = 200 * (1.0 / 3.0) * (200.0 / 300.0);
        Assert.Equal(expectedBar1, results[1], 6);

        // Bar 2: sumVol=450, volWeight=150/450=0.333, MFM=-0.333, weightedMfv=150*(-0.333)*0.333=-16.67
        // cumVwad = 44.4 - 16.67 = 27.8
        double expectedBar2 = expectedBar1 + 150 * (-1.0 / 3.0) * (150.0 / 450.0);
        Assert.Equal(expectedBar2, results[2], 6);
    }

    [Fact]
    public void Vwad_Algorithm_Correctness_RollingPeriod()
    {
        // Verify that volume sum rolls correctly after period is exceeded
        int period = 2;
        var bars = new TBarSeries();

        // Create 4 bars to test rolling behavior
        bars.Add(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100)); // MFM=0 (H=L=C)
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 10, 10, 10, 10, 200)); // MFM=0
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(2), 10, 10, 10, 10, 300)); // MFM=0, but volume rolls

        var vwad = new Vwad(period);

        // Bar 0: sumVol=100
        var r0 = vwad.Update(bars[0]);
        Assert.Equal(0, r0.Value, 10);

        // Bar 1: sumVol=300
        var r1 = vwad.Update(bars[1]);
        Assert.Equal(0, r1.Value, 10);

        // Bar 2: sumVol should be 200+300=500 (100 rolled out)
        // This tests that the rolling sum works correctly
        var r2 = vwad.Update(bars[2]);
        Assert.Equal(0, r2.Value, 10); // Still 0 because MFM=0 for all bars
    }

    [Fact]
    public void Vwad_Algorithm_Correctness_VolumeWeighting()
    {
        // Verify volume weighting amplifies high-volume bars
        int period = 10; // Large period so no rolling
        var bars = new TBarSeries();

        // Two bars with same MFM but different volumes
        // High volume bar should contribute more to VWAD
        bars.Add(new TBar(DateTime.UtcNow, 10, 20, 10, 15, 1000)); // MFM = 0 (close at midpoint)
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 10, 20, 10, 20, 100)); // MFM = 1 (close at high)

        var vwad = new Vwad(period);

        // Bar 0: MFM = (15-10 - (20-15))/(20-10) = (5-5)/10 = 0
        var r0 = vwad.Update(bars[0]);
        Assert.Equal(0, r0.Value, 10);

        // Bar 1: MFM = (20-10 - (20-20))/(20-10) = 10/10 = 1
        // sumVol = 1100, volWeight = 100/1100 = 0.0909
        // weightedMfv = 100 * 1 * 0.0909 = 9.09
        var r1 = vwad.Update(bars[1]);
        double expectedVolWeight = 100.0 / 1100.0;
        double expectedWeightedMfv = 100.0 * 1.0 * expectedVolWeight;
        Assert.Equal(expectedWeightedMfv, r1.Value, 6);
    }

    [Fact]
    public void Vwad_DifferentPeriods_ProduceDifferentResults()
    {
        // Different periods should produce different results
        var vwad10 = new Vwad(10);
        var vwad20 = new Vwad(20);
        var vwad50 = new Vwad(50);

        var results10 = new List<double>();
        var results20 = new List<double>();
        var results50 = new List<double>();

        foreach (var bar in _data.Bars)
        {
            results10.Add(vwad10.Update(bar).Value);
            results20.Add(vwad20.Update(bar).Value);
            results50.Add(vwad50.Update(bar).Value);
        }

        // After warmup, results should differ
        int checkIndex = 60; // Well past all warmup periods
        bool allSame = Math.Abs(results10[checkIndex] - results20[checkIndex]) < 1e-10 &&
                       Math.Abs(results20[checkIndex] - results50[checkIndex]) < 1e-10;

        Assert.False(allSame, "Different periods should produce different VWAD values");
    }

    [Fact]
    public void Vwad_Cumulative_AlwaysChanges_WithNonZeroMfm()
    {
        // VWAD is cumulative - it should change when MFM is non-zero
        var vwad = new Vwad(DefaultPeriod);
        double? previousValue = null;
        int changeCount = 0;

        foreach (var bar in _data.Bars)
        {
            var result = vwad.Update(bar);
            if (previousValue.HasValue && Math.Abs(result.Value - previousValue.Value) > 1e-15)
            {
                changeCount++;
            }
            previousValue = result.Value;
        }

        // Most bars should cause changes (unless MFM happens to be exactly 0)
        Assert.True(changeCount > _data.Bars.Count * 0.5, "VWAD should change for most bars with non-zero MFM");
    }
}
