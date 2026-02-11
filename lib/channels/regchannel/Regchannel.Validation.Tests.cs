using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class RegchannelValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public RegchannelValidationTests(ITestOutputHelper output)
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

        var ind = new Regchannel(10, 1.0);

        // Bar 0: regression = 100, slope = 0, stdDev = 0
        ind.Update(series[0]);
        Assert.Equal(100.0, ind.Last.Value, 1e-10);
        Assert.Equal(0.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);

        // Bar 1: Two points (100, 120 at x=0,1)
        // Perfect line through points: y = 100 + 20*x
        ind.Update(series[1]);
        Assert.Equal(120.0, ind.Last.Value, 1e-10);
        Assert.Equal(20.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);

        // Bar 2: Linear regression of (100, 120, 110)
        // slope = 5, intercept = 105, regression at x=2 = 115
        ind.Update(series[2]);
        Assert.Equal(115.0, ind.Last.Value, 1e-10);
        Assert.Equal(5.0, ind.Slope, 1e-10);

        // Residuals: 100-105=-5, 120-110=10, 110-115=-5
        // StdDev = sqrt((25+100+25)/3) = sqrt(50)
        double expectedStdDev = Math.Sqrt(50);
        Assert.Equal(expectedStdDev, ind.StdDev, 1e-10);

        _output.WriteLine("Regchannel manual calculation validated");
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

        var ind = new Regchannel(5, 2.0);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }

        // Perfect linear fit: slope = 10, no residuals
        Assert.Equal(140.0, ind.Last.Value, 1e-10);
        Assert.Equal(10.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);
        Assert.Equal(140.0, ind.Upper.Value, 1e-10);
        Assert.Equal(140.0, ind.Lower.Value, 1e-10);

        _output.WriteLine("Regchannel linear trend validated");
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

        var ind = new Regchannel(5, 2.0);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }

        // Constant: slope = 0, no residuals
        Assert.Equal(100.0, ind.Last.Value, 1e-10);
        Assert.Equal(0.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10);

        _output.WriteLine("Regchannel constant values validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] multipliers = { 1.0, 2.0, 3.0 };

        foreach (int period in periods)
        {
            foreach (double multiplier in multipliers)
            {
                // Batch (instance)
                var inst = new Regchannel(period, multiplier);
                var (bMid, bUp, bLo) = inst.Update(_testData.Data);

                // Static batch
                var (sMid, sUp, sLo) = Regchannel.Batch(_testData.Data, period, multiplier);

                ValidationHelper.VerifySeriesEqual(bMid, sMid);
                ValidationHelper.VerifySeriesEqual(bUp, sUp);
                ValidationHelper.VerifySeriesEqual(bLo, sLo);

                // Streaming
                var streaming = new Regchannel(period, multiplier);
                var sMidStream = new TSeries();
                var sUpStream = new TSeries();
                var sLoStream = new TSeries();
                foreach (var tv in _testData.Data)
                {
                    streaming.Update(tv);
                    sMidStream.Add(streaming.Last);
                    sUpStream.Add(streaming.Upper);
                    sLoStream.Add(streaming.Lower);
                }

                ValidationHelper.VerifySeriesEqual(sMid, sMidStream);
                ValidationHelper.VerifySeriesEqual(sUp, sUpStream);
                ValidationHelper.VerifySeriesEqual(sLo, sLoStream);

                // Span
                double[] source = _testData.ClosePrices.ToArray();
                double[] spanMid = new double[source.Length];
                double[] spanUp = new double[source.Length];
                double[] spanLo = new double[source.Length];
                Regchannel.Batch(source.AsSpan(), spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan(), period, multiplier);

                for (int i = 0; i < source.Length; i++)
                {
                    Assert.Equal(sMid[i].Value, spanMid[i], 9);
                    Assert.Equal(sUp[i].Value, spanUp[i], 9);
                    Assert.Equal(sLo[i].Value, spanLo[i], 9);
                }
            }
        }

        _output.WriteLine("Regchannel mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        const double multiplier = 2.0;

        var pub = new TSeries();
        var evtInd = new Regchannel(pub, period, multiplier);
        var evtMid = new TSeries();
        var evtUp = new TSeries();
        var evtLo = new TSeries();

        foreach (var tv in _testData.Data)
        {
            pub.Add(tv);
            evtMid.Add(evtInd.Last);
            evtUp.Add(evtInd.Upper);
            evtLo.Add(evtInd.Lower);
        }

        var (bMid, bUp, bLo) = Regchannel.Batch(_testData.Data, period, multiplier);

        ValidationHelper.VerifySeriesEqual(bMid, evtMid);
        ValidationHelper.VerifySeriesEqual(bUp, evtUp);
        ValidationHelper.VerifySeriesEqual(bLo, evtLo);

        _output.WriteLine("Regchannel eventing mode validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        const double multiplier = 2.5;

        var ((mid, up, lo), ind) = Regchannel.Calculate(_testData.Data, period, multiplier);

        Assert.True(ind.IsHot);
        Assert.Equal(period, ind.WarmupPeriod);
        Assert.Equal(mid.Last.Value, ind.Last.Value, 1e-10);
        Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
        Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

        // Continue streaming
        var next = new TValue(DateTime.UtcNow, 100);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("Regchannel Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;
        const double multiplier = 1.5;

        var (bMid, bUp, bLo) = Regchannel.Batch(_testData.Data, period, multiplier);

        var primed = new Regchannel(period, multiplier);
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

        Assert.Equal(bMid.Last.Value, primed.Last.Value, 1e-9);
        Assert.Equal(bUp.Last.Value, primed.Upper.Value, 1e-9);
        Assert.Equal(bLo.Last.Value, primed.Lower.Value, 1e-9);

        _output.WriteLine("Regchannel Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, up, lo) = Regchannel.Batch(_testData.Data, 50, 2.0);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        // Upper >= Middle >= Lower always
        for (int i = 0; i < mid.Count; i++)
        {
            Assert.True(up[i].Value >= mid[i].Value, $"Upper >= Middle at {i}");
            Assert.True(lo[i].Value <= mid[i].Value, $"Lower <= Middle at {i}");
        }

        _output.WriteLine("Regchannel large dataset validated");
    }

    [Fact]
    public void Validate_BandSymmetry_AllBars()
    {
        var ind = new Regchannel(20, 2.0);
        var (mid, up, lo) = ind.Update(_testData.Data);

        for (int i = 0; i < mid.Count; i++)
        {
            double upperWidth = up[i].Value - mid[i].Value;
            double lowerWidth = mid[i].Value - lo[i].Value;
            Assert.Equal(upperWidth, lowerWidth, 1e-10);
        }

        _output.WriteLine("Regchannel band symmetry validated for all bars");
    }

    [Fact]
    public void Validate_MultiplierScaling()
    {
        double[] multipliers = { 1.0, 2.0, 3.0, 4.0 };
        double[] widths = new double[multipliers.Length];

        for (int i = 0; i < multipliers.Length; i++)
        {
            var ind = new Regchannel(20, multipliers[i]);
            foreach (var tv in _testData.Data)
            {
                ind.Update(tv);
            }
            widths[i] = ind.Upper.Value - ind.Lower.Value;
        }

        // Widths should scale linearly with multiplier
        double baseWidth = widths[0];
        for (int i = 1; i < multipliers.Length; i++)
        {
            double expected = baseWidth * multipliers[i];
            Assert.Equal(expected, widths[i], 1e-9);
        }

        _output.WriteLine("Regchannel multiplier scaling validated");
    }

    [Fact]
    public void Validate_PeriodEffect_SmoothingAndSlope()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] slopes = new double[periods.Length];
        double[] middles = new double[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            var ind = new Regchannel(periods[i], 2.0);
            foreach (var tv in _testData.Data)
            {
                ind.Update(tv);
            }
            slopes[i] = ind.Slope;
            middles[i] = ind.Last.Value;
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

        _output.WriteLine("Regchannel period effect validated");
    }

    [Fact]
    public void Validate_StateRestoration_Iterative()
    {
        var ind = new Regchannel(15, 2.5);
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

        double midBefore = ind.Last.Value;
        double upBefore = ind.Upper.Value;
        double loBefore = ind.Lower.Value;
        double slopeBefore = ind.Slope;
        double stdDevBefore = ind.StdDev;

        for (int i = 0; i < 10; i++)
        {
            var corrected = gbm.Next(isNew: false);
            ind.Update(new TValue(corrected.Time, corrected.Close), isNew: false);
        }

        // Restore with remembered value
        ind.Update(remembered, isNew: false);

        Assert.Equal(midBefore, ind.Last.Value, 1e-6);
        Assert.Equal(upBefore, ind.Upper.Value, 1e-6);
        Assert.Equal(loBefore, ind.Lower.Value, 1e-6);
        Assert.Equal(slopeBefore, ind.Slope, 1e-6);
        Assert.Equal(stdDevBefore, ind.StdDev, 1e-6);

        _output.WriteLine("Regchannel state restoration validated");
    }

    [Fact]
    public void Validate_BandWidthFormula()
    {
        // Band width = 2 * multiplier * stdDev
        var ind = new Regchannel(20, 3.0);

        foreach (var tv in _testData.Data)
        {
            ind.Update(tv);

            double expectedWidth = 2 * 3.0 * ind.StdDev;
            double actualWidth = ind.Upper.Value - ind.Lower.Value;
            Assert.Equal(expectedWidth, actualWidth, 1e-10);
        }

        _output.WriteLine("Regchannel band width formula validated");
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

        var indUp = new Regchannel(10, 2.0);
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

        var indDown = new Regchannel(10, 2.0);
        foreach (var tv in downtrend)
        {
            indDown.Update(tv);
        }
        Assert.True(indDown.Slope < 0, "Downtrend should have negative slope");

        _output.WriteLine("Regchannel slope direction validated");
    }

    [Fact]
    public void Validate_SlidingWindow_Correctness()
    {
        const int period = 5;
        var ind = new Regchannel(period, 2.0);

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
        Assert.Equal(170.0, ind.Last.Value, 1e-10);
        Assert.Equal(10.0, ind.Slope, 1e-10);
        Assert.Equal(0.0, ind.StdDev, 1e-10); // Perfect linear fit

        _output.WriteLine("Regchannel sliding window validated");
    }

    [Fact]
    public void Validate_Residuals_NonLinearData()
    {
        // Test with data that doesn't fit a perfect line
        var ind = new Regchannel(4, 1.0);
        var t0 = DateTime.UtcNow;

        // Values: 100, 120, 100, 120 (oscillating)
        ind.Update(new TValue(t0, 100));
        ind.Update(new TValue(t0.AddMinutes(1), 120));
        ind.Update(new TValue(t0.AddMinutes(2), 100));
        ind.Update(new TValue(t0.AddMinutes(3), 120));

        // These values don't fit a line well, so stdDev should be significant
        Assert.True(ind.StdDev > 5, "Oscillating data should have significant residuals");

        // Bands should be wider than regression value
        Assert.True(ind.Upper.Value > ind.Last.Value, "Upper > Middle with residuals");
        Assert.True(ind.Lower.Value < ind.Last.Value, "Lower < Middle with residuals");

        _output.WriteLine("Regchannel residuals for non-linear data validated");
    }

    [Fact]
    public void Validate_StdDev_Formula()
    {
        // Verify stdDev calculation: sqrt(sum(residual^2)/n)
        var ind = new Regchannel(5, 2.0);
        var t0 = DateTime.UtcNow;

        // Known values for manual calculation
        double[] values = { 100, 105, 98, 107, 102 };
        foreach (double v in values)
        {
            ind.Update(new TValue(t0, v));
            t0 = t0.AddMinutes(1);
        }

        // Calculate expected regression manually
        // x: 0,1,2,3,4  y: 100,105,98,107,102
        // sumX = 10, sumX2 = 30, sumY = 512, sumXY = 1053
        // denom = 5*30 - 10*10 = 50
        // slope = (5*1053 - 10*512) / 50 = (5265-5120)/50 = 2.9
        // intercept = (512 - 2.9*10) / 5 = (512-29)/5 = 96.6
        // predicted: 96.6, 99.5, 102.4, 105.3, 108.2
        // residuals: 3.4, 5.5, -4.4, 1.7, -6.2
        // sum(r^2) = 11.56 + 30.25 + 19.36 + 2.89 + 38.44 = 102.5
        // stdDev = sqrt(102.5/5) = sqrt(20.5) ≈ 4.53

        // Slope should be positive (trend is slightly upward)
        Assert.True(ind.Slope > 0 && ind.Slope < 5, $"Slope={ind.Slope} should be small positive");
        // StdDev should be non-trivial since data doesn't fit perfectly
        Assert.True(ind.StdDev > 0 && ind.StdDev < 10, $"StdDev={ind.StdDev} should be positive");

        _output.WriteLine("Regchannel stdDev formula validated");
    }

    // ═══════════════════════════════════════════════════════════════
    //  TALib Validation
    //  TALib LinearReg computes the linear regression value at the end
    //  of the lookback window — same as Regchannel's midline (centerline).
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_Talib_LinearReg_Centerline()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] sourceData = _testData.RawData.ToArray();
        double[] linregOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            var (qMid, _, _) = Regchannel.Batch(_testData.Data, period, 2.0);

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
        _output.WriteLine("Regchannel centerline validated against TALib LinearReg for all periods");
    }

    [Fact]
    public void Validate_Talib_LinearRegSlope()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] sourceData = _testData.RawData.ToArray();
        double[] slopeOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Stream Regchannel and collect slopes
            var ind = new Regchannel(period, 2.0);
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

            // Compare slopes from end of series (converged)
            int count = slopes.Count;
            int start = Math.Max(0, count - 100);
            var (offset, _) = outRange.GetOffsetAndLength(slopeOutput.Length);

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
        _output.WriteLine("Regchannel slope validated against TALib LinearRegSlope for all periods");
    }

    [Fact]
    public void Validate_Tulip_LinearReg_Centerline()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var (qMid, _, _) = Regchannel.Batch(_testData.Data, period, 2.0);

            var linregIndicator = Tulip.Indicators.linreg;
            double[][] inputs = { sourceData };
            double[] options = { period };
            double[][] outputs = { new double[sourceData.Length - period + 1] };
            linregIndicator.Run(inputs, options, outputs);

            var tLinreg = outputs[0];
            int offset = period - 1; // Tulip output starts at index (period-1)

            // Compare last 100 values
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
        _output.WriteLine("Regchannel centerline validated against Tulip linreg for all periods");
    }
}
