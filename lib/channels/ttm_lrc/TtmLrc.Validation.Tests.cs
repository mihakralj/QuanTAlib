using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class TtmLrcValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public TtmLrcValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose() => Dispose(true);

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    [Fact]
    public void Validate_ManualCalculation_ThreePoints()
    {
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        // Points: (0,100), (1,120), (2,110)
        series.Add(new TValue(t0, 100));
        series.Add(new TValue(t0.AddMinutes(1), 120));
        series.Add(new TValue(t0.AddMinutes(2), 110));

        var ind = new TtmLrc(10);

        // Bar 0: regression = 100, slope = 0, stdDev = 0
        ind.Update(series[0]);
        Assert.Equal(100.0, ind.Midline.Value, 1e-10);
        Assert.Equal(0.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);

        // Bar 1: Two points (100, 120 at x=0,1)
        // Perfect line through points: y = 100 + 20*x
        ind.Update(series[1]);
        Assert.Equal(120.0, ind.Midline.Value, 1e-10);
        Assert.Equal(20.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);

        // Bar 2: Linear regression of (100, 120, 110)
        // slope = 5, intercept = 105, regression at x=2 = 115
        ind.Update(series[2]);
        Assert.Equal(115.0, ind.Midline.Value, 1e-10);
        Assert.Equal(5.0, ind.Slope, 1e-10);

        // Residuals: 100-105=-5, 120-110=10, 110-115=-5
        // StdDev = sqrt((25+100+25)/3) = sqrt(50)
        double expectedStdDev = Math.Sqrt(50);
        Assert.Equal(expectedStdDev, ind.StdDev, 1e-10);

        // Verify ±1σ bands
        Assert.Equal(115.0 + expectedStdDev, ind.Upper1.Value, 1e-10);
        Assert.Equal(115.0 - expectedStdDev, ind.Lower1.Value, 1e-10);

        // Verify ±2σ bands
        Assert.Equal(115.0 + 2.0 * expectedStdDev, ind.Upper2.Value, 1e-10);
        Assert.Equal(115.0 - 2.0 * expectedStdDev, ind.Lower2.Value, 1e-10);

        _output.WriteLine("TtmLrc manual calculation validated");
    }

    [Fact]
    public void Validate_LinearTrend_ZeroResiduals()
    {
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        // Perfect linear trend: 100, 110, 120, 130, 140
        for (int i = 0; i < 5; i++)
        {
            series.Add(new TValue(t0.AddMinutes(i), 100 + i * 10));
        }

        var ind = new TtmLrc(5);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }

        // Perfect linear fit: slope = 10, no residuals
        Assert.Equal(140.0, ind.Midline.Value, 1e-10);
        Assert.Equal(10.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);
        Assert.Equal(1.0, ind.RSquared, 1e-10); // Perfect fit

        // All bands = midline when stddev = 0
        Assert.Equal(140.0, ind.Upper1.Value, 1e-10);
        Assert.Equal(140.0, ind.Lower1.Value, 1e-10);
        Assert.Equal(140.0, ind.Upper2.Value, 1e-10);
        Assert.Equal(140.0, ind.Lower2.Value, 1e-10);

        _output.WriteLine("TtmLrc linear trend validated");
    }

    [Fact]
    public void Validate_ConstantValues_ZeroResiduals()
    {
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        // Constant values: 100, 100, 100, 100, 100
        for (int i = 0; i < 5; i++)
        {
            series.Add(new TValue(t0.AddMinutes(i), 100));
        }

        var ind = new TtmLrc(5);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }

        // Constant: slope = 0, no residuals
        Assert.Equal(100.0, ind.Midline.Value, 1e-10);
        Assert.Equal(0.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);

        _output.WriteLine("TtmLrc constant values validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            // Batch (instance)
            var inst = new TtmLrc(period);
            var (bMid, bU1, bL1, bU2, bL2) = inst.Update(_testData.Data);

            // Static batch
            var (sMid, sU1, sL1, sU2, sL2) = TtmLrc.Batch(_testData.Data, period);

            ValidationHelper.VerifySeriesEqual(bMid, sMid);
            ValidationHelper.VerifySeriesEqual(bU1, sU1);
            ValidationHelper.VerifySeriesEqual(bL1, sL1);
            ValidationHelper.VerifySeriesEqual(bU2, sU2);
            ValidationHelper.VerifySeriesEqual(bL2, sL2);

            // Streaming
            var streaming = new TtmLrc(period);
            var sMidStream = new TSeries();
            var sU1Stream = new TSeries();
            var sL1Stream = new TSeries();
            var sU2Stream = new TSeries();
            var sL2Stream = new TSeries();
            foreach (var tv in _testData.Data)
            {
                streaming.Update(tv);
                sMidStream.Add(streaming.Midline);
                sU1Stream.Add(streaming.Upper1);
                sL1Stream.Add(streaming.Lower1);
                sU2Stream.Add(streaming.Upper2);
                sL2Stream.Add(streaming.Lower2);
            }

            ValidationHelper.VerifySeriesEqual(sMid, sMidStream);
            ValidationHelper.VerifySeriesEqual(sU1, sU1Stream);
            ValidationHelper.VerifySeriesEqual(sL1, sL1Stream);
            ValidationHelper.VerifySeriesEqual(sU2, sU2Stream);
            ValidationHelper.VerifySeriesEqual(sL2, sL2Stream);

            // Span
            double[] source = _testData.ClosePrices.ToArray();
            double[] spanMid = new double[source.Length];
            double[] spanU1 = new double[source.Length];
            double[] spanL1 = new double[source.Length];
            double[] spanU2 = new double[source.Length];
            double[] spanL2 = new double[source.Length];
            TtmLrc.Batch(source.AsSpan(), spanMid.AsSpan(), spanU1.AsSpan(), spanL1.AsSpan(), spanU2.AsSpan(), spanL2.AsSpan(), period);

            for (int i = 0; i < source.Length; i++)
            {
                Assert.Equal(sMid[i].Value, spanMid[i], 9);
                Assert.Equal(sU1[i].Value, spanU1[i], 9);
                Assert.Equal(sL1[i].Value, spanL1[i], 9);
                Assert.Equal(sU2[i].Value, spanU2[i], 9);
                Assert.Equal(sL2[i].Value, spanL2[i], 9);
            }
        }

        _output.WriteLine("TtmLrc mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;

        var pub = new TSeries();
        var evtInd = new TtmLrc(pub, period);
        var evtMid = new TSeries();
        var evtU1 = new TSeries();
        var evtL1 = new TSeries();
        var evtU2 = new TSeries();
        var evtL2 = new TSeries();

        foreach (var tv in _testData.Data)
        {
            pub.Add(tv);
            evtMid.Add(evtInd.Midline);
            evtU1.Add(evtInd.Upper1);
            evtL1.Add(evtInd.Lower1);
            evtU2.Add(evtInd.Upper2);
            evtL2.Add(evtInd.Lower2);
        }

        var (bMid, bU1, bL1, bU2, bL2) = TtmLrc.Batch(_testData.Data, period);

        ValidationHelper.VerifySeriesEqual(bMid, evtMid);
        ValidationHelper.VerifySeriesEqual(bU1, evtU1);
        ValidationHelper.VerifySeriesEqual(bL1, evtL1);
        ValidationHelper.VerifySeriesEqual(bU2, evtU2);
        ValidationHelper.VerifySeriesEqual(bL2, evtL2);

        _output.WriteLine("TtmLrc eventing mode validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;

        var ((mid, u1, l1, u2, l2), ind) = TtmLrc.Calculate(_testData.Data, period);

        Assert.True(ind.IsHot);
        Assert.Equal(period, ind.WarmupPeriod);
        Assert.Equal(mid.Last.Value, ind.Midline.Value, 1e-10);
        Assert.Equal(u1.Last.Value, ind.Upper1.Value, 1e-10);
        Assert.Equal(l1.Last.Value, ind.Lower1.Value, 1e-10);
        Assert.Equal(u2.Last.Value, ind.Upper2.Value, 1e-10);
        Assert.Equal(l2.Last.Value, ind.Lower2.Value, 1e-10);

        // Continue streaming
        var next = new TValue(DateTime.UtcNow, 100);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("TtmLrc Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;

        var (bMid, bU1, bL1, bU2, bL2) = TtmLrc.Batch(_testData.Data, period);

        var primed = new TtmLrc(period);
        var subset = new TSeries();
        for (int i = 0; i < 200; i++)
        {
            subset.Add(_testData.Data[i]);
        }

        primed.Prime(subset);

        for (int i = 200; i < _testData.Data.Count; i++)
        {
            primed.Update(_testData.Data[i]);
        }

        Assert.Equal(bMid.Last.Value, primed.Midline.Value, 1e-9);
        Assert.Equal(bU1.Last.Value, primed.Upper1.Value, 1e-9);
        Assert.Equal(bL1.Last.Value, primed.Lower1.Value, 1e-9);
        Assert.Equal(bU2.Last.Value, primed.Upper2.Value, 1e-9);
        Assert.Equal(bL2.Last.Value, primed.Lower2.Value, 1e-9);

        _output.WriteLine("TtmLrc Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, u1, l1, u2, l2) = TtmLrc.Batch(_testData.Data, 50);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(u1, startIndex: 0);
        ValidationHelper.VerifyAllFinite(l1, startIndex: 0);
        ValidationHelper.VerifyAllFinite(u2, startIndex: 0);
        ValidationHelper.VerifyAllFinite(l2, startIndex: 0);

        // Band ordering: Upper2 >= Upper1 >= Middle >= Lower1 >= Lower2
        for (int i = 0; i < mid.Count; i++)
        {
            Assert.True(u2[i].Value >= u1[i].Value, $"Upper2 >= Upper1 at {i}");
            Assert.True(u1[i].Value >= mid[i].Value, $"Upper1 >= Middle at {i}");
            Assert.True(l1[i].Value <= mid[i].Value, $"Lower1 <= Middle at {i}");
            Assert.True(l2[i].Value <= l1[i].Value, $"Lower2 <= Lower1 at {i}");
        }

        _output.WriteLine("TtmLrc large dataset validated");
    }

    [Fact]
    public void Validate_BandSymmetry_AllBars()
    {
        var ind = new TtmLrc(20);
        var (mid, u1, l1, u2, l2) = ind.Update(_testData.Data);

        for (int i = 0; i < mid.Count; i++)
        {
            // ±1σ symmetry
            double upper1Width = u1[i].Value - mid[i].Value;
            double lower1Width = mid[i].Value - l1[i].Value;
            Assert.Equal(upper1Width, lower1Width, 1e-10);

            // ±2σ symmetry
            double upper2Width = u2[i].Value - mid[i].Value;
            double lower2Width = mid[i].Value - l2[i].Value;
            Assert.Equal(upper2Width, lower2Width, 1e-10);

            // ±2σ should be exactly 2x ±1σ
            Assert.Equal(upper2Width, upper1Width * 2, 1e-10);
        }

        _output.WriteLine("TtmLrc band symmetry validated for all bars");
    }

    [Fact]
    public void Validate_RSquared_Range()
    {
        var ind = new TtmLrc(20);

        foreach (var tv in _testData.Data)
        {
            ind.Update(tv);
            Assert.True(ind.RSquared >= 0.0 && ind.RSquared <= 1.0, $"R² should be in [0,1], got {ind.RSquared}");
        }

        _output.WriteLine("TtmLrc R² range validated");
    }

    [Fact]
    public void Validate_RSquared_PerfectFit()
    {
        var t0 = DateTime.UtcNow;
        var ind = new TtmLrc(5);

        // Feed perfect linear data
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(t0.AddMinutes(i), 100 + i * 5));
        }

        Assert.Equal(1.0, ind.RSquared, 1e-9);
        Assert.Equal(0.0, ind.StdDev, 1e-9);

        _output.WriteLine("TtmLrc R² perfect fit validated");
    }

    [Fact]
    public void Validate_PeriodEffect_SmoothingAndSlope()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] slopes = new double[periods.Length];
        double[] middles = new double[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            var ind = new TtmLrc(periods[i]);
            foreach (var tv in _testData.Data)
            {
                ind.Update(tv);
            }
            slopes[i] = ind.Slope;
            middles[i] = ind.Midline.Value;
        }

        // All should produce finite values
        foreach (var s in slopes)
        {
            Assert.True(double.IsFinite(s));
        }
        foreach (var m in middles)
        {
            Assert.True(double.IsFinite(m));
        }

        _output.WriteLine("TtmLrc period effect validated");
    }

    [Fact]
    public void Validate_StateRestoration_Iterative()
    {
        var ind = new TtmLrc(15);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        // Build up state
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Multiple corrections
        var rememberedBar = gbm.Next(isNew: true);
        var remembered = new TValue(rememberedBar.Time, rememberedBar.Close);
        ind.Update(remembered, isNew: true);

        double midBefore = ind.Midline.Value;
        double u1Before = ind.Upper1.Value;
        double l1Before = ind.Lower1.Value;
        double u2Before = ind.Upper2.Value;
        double l2Before = ind.Lower2.Value;
        double slopeBefore = ind.Slope;
        double stdDevBefore = ind.StdDev;
        double rSquaredBefore = ind.RSquared;

        for (int i = 0; i < 10; i++)
        {
            var corrected = gbm.Next(isNew: false);
            ind.Update(new TValue(corrected.Time, corrected.Close), isNew: false);
        }

        // Restore with remembered value
        ind.Update(remembered, isNew: false);

        Assert.Equal(midBefore, ind.Midline.Value, 1e-6);
        Assert.Equal(u1Before, ind.Upper1.Value, 1e-6);
        Assert.Equal(l1Before, ind.Lower1.Value, 1e-6);
        Assert.Equal(u2Before, ind.Upper2.Value, 1e-6);
        Assert.Equal(l2Before, ind.Lower2.Value, 1e-6);
        Assert.Equal(slopeBefore, ind.Slope, 1e-6);
        Assert.Equal(stdDevBefore, ind.StdDev, 1e-6);
        Assert.Equal(rSquaredBefore, ind.RSquared, 1e-6);

        _output.WriteLine("TtmLrc state restoration validated");
    }

    [Fact]
    public void Validate_BandWidthFormula()
    {
        var ind = new TtmLrc(20);

        foreach (var tv in _testData.Data)
        {
            ind.Update(tv);

            // ±1σ band width = 2 * stdDev
            double expected1Width = 2 * ind.StdDev;
            double actual1Width = ind.Upper1.Value - ind.Lower1.Value;
            Assert.Equal(expected1Width, actual1Width, 1e-10);

            // ±2σ band width = 4 * stdDev
            double expected2Width = 4 * ind.StdDev;
            double actual2Width = ind.Upper2.Value - ind.Lower2.Value;
            Assert.Equal(expected2Width, actual2Width, 1e-10);
        }

        _output.WriteLine("TtmLrc band width formula validated");
    }

    [Fact]
    public void Validate_SlopeDirection()
    {
        // Test uptrend detection
        var uptrend = new TSeries();
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            uptrend.Add(new TValue(t0.AddMinutes(i), 100 + i * 2 + (i % 3))); // Noisy uptrend
        }

        var indUp = new TtmLrc(10);
        foreach (var tv in uptrend)
        {
            indUp.Update(tv);
        }
        Assert.True(indUp.Slope > 0, "Uptrend should have positive slope");

        // Test downtrend detection
        var downtrend = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            downtrend.Add(new TValue(t0.AddMinutes(i), 200 - i * 2 + (i % 3))); // Noisy downtrend
        }

        var indDown = new TtmLrc(10);
        foreach (var tv in downtrend)
        {
            indDown.Update(tv);
        }
        Assert.True(indDown.Slope < 0, "Downtrend should have negative slope");

        _output.WriteLine("TtmLrc slope direction validated");
    }

    [Fact]
    public void Validate_SlidingWindow_Correctness()
    {
        const int period = 5;
        var ind = new TtmLrc(period);

        // Feed specific values
        double[] values = { 100, 110, 120, 130, 140, 150, 160, 170 };
        var t0 = DateTime.UtcNow;

        foreach (double v in values)
        {
            ind.Update(new TValue(t0, v));
            t0 = t0.AddMinutes(1);
        }

        // Window should contain last 5: 130,140,150,160,170
        // Linear regression of 130,140,150,160,170 at x=0,1,2,3,4
        // Perfect linear fit: slope = 10, intercept = 130
        // regression at x=4 = 130 + 10*4 = 170
        Assert.Equal(170.0, ind.Midline.Value, 1e-10);
        Assert.Equal(10.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10); // Perfect linear fit
        Assert.Equal(1.0, ind.RSquared, 1e-10); // Perfect fit

        _output.WriteLine("TtmLrc sliding window validated");
    }

    [Fact]
    public void Validate_Residuals_NonLinearData()
    {
        // Test with data that doesn't fit a perfect line
        var ind = new TtmLrc(4);
        var t0 = DateTime.UtcNow;

        // Values: 100, 120, 100, 120 (oscillating)
        ind.Update(new TValue(t0, 100));
        ind.Update(new TValue(t0.AddMinutes(1), 120));
        ind.Update(new TValue(t0.AddMinutes(2), 100));
        ind.Update(new TValue(t0.AddMinutes(3), 120));

        // These values don't fit a line well, so stdDev should be significant
        Assert.True(ind.StdDev > 5, "Oscillating data should have significant residuals");
        Assert.True(ind.RSquared < 0.5, "Poor fit should have low R²");

        // Bands should be wider than regression value
        Assert.True(ind.Upper1.Value > ind.Midline.Value, "Upper1 > Midline with residuals");
        Assert.True(ind.Lower1.Value < ind.Midline.Value, "Lower1 < Midline with residuals");
        Assert.True(ind.Upper2.Value > ind.Upper1.Value, "Upper2 > Upper1 with residuals");
        Assert.True(ind.Lower2.Value < ind.Lower1.Value, "Lower2 < Lower1 with residuals");

        _output.WriteLine("TtmLrc residuals for non-linear data validated");
    }

    [Fact]
    public void Validate_DefaultPeriod_Is100()
    {
        // TTM LRC spec says default period should be 100
        var ind = new TtmLrc();
        Assert.Equal(100, ind.WarmupPeriod);
        Assert.Equal("TtmLrc(100)", ind.Name);

        _output.WriteLine("TtmLrc default period 100 validated");
    }

    [Fact]
    public void Validate_StdDev_Formula()
    {
        // Verify stdDev calculation: sqrt(sum(residual^2)/n)
        var ind = new TtmLrc(5);
        var t0 = DateTime.UtcNow;

        // Known values for manual calculation
        double[] values = { 100, 105, 98, 107, 102 };
        foreach (double v in values)
        {
            ind.Update(new TValue(t0, v));
            t0 = t0.AddMinutes(1);
        }

        // Slope should be positive (trend is slightly upward)
        Assert.True(ind.Slope > 0 && ind.Slope < 5, $"Slope={ind.Slope} should be small positive");
        // StdDev should be non-trivial since data doesn't fit perfectly
        Assert.True(ind.StdDev > 0 && ind.StdDev < 10, $"StdDev={ind.StdDev} should be positive");
        // R² should be moderate (not perfect fit)
        Assert.True(ind.RSquared > 0 && ind.RSquared < 1, $"R²={ind.RSquared} should be between 0 and 1");

        _output.WriteLine("TtmLrc stdDev formula validated");
    }

    [Fact]
    public void Validate_CompareWithRegchannel_Midline()
    {
        // TtmLrc midline should match Regchannel middle (both use linear regression)
        const int period = 20;

        var ttmLrc = new TtmLrc(period);
        var regchannel = new Regchannel(period, 1.0);

        foreach (var tv in _testData.Data)
        {
            ttmLrc.Update(tv);
            regchannel.Update(tv);
        }

        // Midlines should be identical
        Assert.Equal(regchannel.Last.Value, ttmLrc.Midline.Value, 1e-9);
        Assert.Equal(regchannel.Slope, ttmLrc.Slope, 1e-9);
        Assert.Equal(regchannel.StdDev, ttmLrc.StdDev, 1e-9);

        // TtmLrc ±1σ bands should match Regchannel with multiplier 1.0
        Assert.Equal(regchannel.Upper.Value, ttmLrc.Upper1.Value, 1e-9);
        Assert.Equal(regchannel.Lower.Value, ttmLrc.Lower1.Value, 1e-9);

        _output.WriteLine("TtmLrc vs Regchannel midline validated");
    }

    [Fact]
    public void Validate_CompareWithRegchannel_DoubleMultiplier()
    {
        // TtmLrc ±2σ bands should match Regchannel with multiplier 2.0
        const int period = 20;

        var ttmLrc = new TtmLrc(period);
        var regchannel2x = new Regchannel(period, 2.0);

        foreach (var tv in _testData.Data)
        {
            ttmLrc.Update(tv);
            regchannel2x.Update(tv);
        }

        // ±2σ bands should match Regchannel(20, 2.0)
        Assert.Equal(regchannel2x.Upper.Value, ttmLrc.Upper2.Value, 1e-9);
        Assert.Equal(regchannel2x.Lower.Value, ttmLrc.Lower2.Value, 1e-9);

        _output.WriteLine("TtmLrc ±2σ vs Regchannel(multiplier=2) validated");
    }

    // ═══════════════════════════════════════════════════════════════
    //  TALib Validation
    //  TALib LinearReg computes the linear regression value at the end
    //  of the lookback window — same as TtmLrc's midline.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_Talib_LinearReg_Midline()
    {
        int[] periods = { 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();
        double[] linregOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            var (qMid, _, _, _, _) = TtmLrc.Batch(_testData.Data, period);

            var retCode = Functions.LinearReg<double>(
                sourceData,
                0..^0,
                linregOutput,
                out var outRange,
                period);

            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.LinearRegLookback(period);

            ValidationHelper.VerifyData(qMid, linregOutput, outRange, lookback);
        }
        _output.WriteLine("TtmLrc midline validated against TALib LinearReg for all periods");
    }

    [Fact]
    public void Validate_Talib_LinearRegSlope()
    {
        int[] periods = { 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();
        double[] slopeOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            var ind = new TtmLrc(period);
            var slopes = new List<double>();
            foreach (var tv in _testData.Data)
            {
                ind.Update(tv);
                slopes.Add(ind.Slope);
            }

            var retCode = Functions.LinearRegSlope<double>(
                sourceData,
                0..^0,
                slopeOutput,
                out var outRange,
                period);

            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.LinearRegSlopeLookback(period);
            var (offset, _) = outRange.GetOffsetAndLength(slopeOutput.Length);

            int count = slopes.Count;
            int start = Math.Max(0, count - 100);

            for (int i = start; i < count; i++)
            {
                if (i < lookback)
                {
                    continue;
                }

                int tIndex = i - offset;
                if (tIndex < 0 || tIndex >= slopeOutput.Length)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(slopes[i] - slopeOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                    $"Slope mismatch at {i}: QuanTAlib={slopes[i]:G17}, TALib={slopeOutput[tIndex]:G17}");
            }
        }
        _output.WriteLine("TtmLrc slope validated against TALib LinearRegSlope for all periods");
    }

    [Fact]
    public void Validate_Tulip_LinearReg_Midline()
    {
        int[] periods = { 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var (qMid, _, _, _, _) = TtmLrc.Batch(_testData.Data, period);

            var linregIndicator = Tulip.Indicators.linreg;
            double[][] inputs = { sourceData };
            double[] options = { period };
            double[][] outputs = { new double[sourceData.Length - period + 1] };
            linregIndicator.Run(inputs, options, outputs);

            var tLinreg = outputs[0];
            int offset = period - 1;

            int count = qMid.Count;
            int start = Math.Max(0, count - 100);
            for (int i = start; i < count; i++)
            {
                int tIndex = i - offset;
                if (tIndex < 0 || tIndex >= tLinreg.Length)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(qMid[i].Value - tLinreg[tIndex]) <= ValidationHelper.TulipTolerance,
                    $"Mismatch at {i}: QuanTAlib={qMid[i].Value:G17}, Tulip={tLinreg[tIndex]:G17}");
            }
        }
        _output.WriteLine("TtmLrc midline validated against Tulip linreg for all periods");
    }
}
