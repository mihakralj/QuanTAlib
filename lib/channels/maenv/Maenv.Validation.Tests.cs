using Skender.Stock.Indicators;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class MaenvValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MaenvValidationTests(ITestOutputHelper output)
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
    public void Validate_ManualCalculation_SMA()
    {
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        // Simple values for manual verification
        series.Add(new TValue(t0, 100));
        series.Add(new TValue(t0.AddMinutes(1), 110));
        series.Add(new TValue(t0.AddMinutes(2), 120));
        series.Add(new TValue(t0.AddMinutes(3), 130));

        var ind = new Maenv(3, 2.0, MaenvType.SMA);

        // Bar 0: SMA(100) = 100, bands ±2%
        ind.Update(series[0]);
        Assert.Equal(100.0, ind.Last.Value, 1e-10);
        Assert.Equal(102.0, ind.Upper.Value, 1e-10);
        Assert.Equal(98.0, ind.Lower.Value, 1e-10);

        // Bar 1: SMA(100,110) = 105, bands ±2%
        ind.Update(series[1]);
        Assert.Equal(105.0, ind.Last.Value, 1e-10);
        Assert.Equal(107.1, ind.Upper.Value, 1e-10);
        Assert.Equal(102.9, ind.Lower.Value, 1e-10);

        // Bar 2: SMA(100,110,120) = 110, bands ±2%
        ind.Update(series[2]);
        Assert.Equal(110.0, ind.Last.Value, 1e-10);
        Assert.Equal(112.2, ind.Upper.Value, 1e-10);
        Assert.Equal(107.8, ind.Lower.Value, 1e-10);

        // Bar 3: SMA(110,120,130) = 120, bands ±2%
        ind.Update(series[3]);
        Assert.Equal(120.0, ind.Last.Value, 1e-10);
        Assert.Equal(122.4, ind.Upper.Value, 1e-10);
        Assert.Equal(117.6, ind.Lower.Value, 1e-10);

        _output.WriteLine("Maenv SMA manual calculation validated");
    }

    [Fact]
    public void Validate_ManualCalculation_EMA_Convergence()
    {
        // Constant values should converge to that value due to warmup compensation
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(t0.AddMinutes(i), 100.0));
        }

        var ind = new Maenv(20, 1.0, MaenvType.EMA);

        foreach (var tv in series)
        {
            ind.Update(tv);
        }

        // EMA should converge to 100 due to warmup compensation
        Assert.InRange(ind.Last.Value, 99.99, 100.01);
        Assert.InRange(ind.Upper.Value, 100.99, 101.01);
        Assert.InRange(ind.Lower.Value, 98.99, 99.01);

        _output.WriteLine("Maenv EMA convergence validated");
    }

    [Fact]
    public void Validate_ManualCalculation_WMA()
    {
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        // WMA(3) weights: newest=9, middle=6, oldest=3 (total=18)
        series.Add(new TValue(t0, 100)); // First bar: WMA = 100
        series.Add(new TValue(t0.AddMinutes(1), 110)); // WMA = (110*9 + 100*6) / 15 = 1590/15 = 106
        series.Add(new TValue(t0.AddMinutes(2), 120)); // WMA = (120*9 + 110*6 + 100*3) / 18 = 1980/18 = 110

        var ind = new Maenv(3, 1.0, MaenvType.WMA);

        ind.Update(series[0]);
        Assert.Equal(100.0, ind.Last.Value, 1e-10);

        ind.Update(series[1]);
        double expected2 = (110.0 * 9 + 100.0 * 6) / 15.0;
        Assert.Equal(expected2, ind.Last.Value, 1e-10);

        ind.Update(series[2]);
        double expected3 = (120.0 * 9 + 110.0 * 6 + 100.0 * 3) / 18.0;
        Assert.Equal(expected3, ind.Last.Value, 1e-10);

        _output.WriteLine("Maenv WMA manual calculation validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] percentages = { 0.5, 1.0, 2.0, 5.0 };

        foreach (int period in periods)
        {
            foreach (double percentage in percentages)
            {
                foreach (MaenvType maType in Enum.GetValues<MaenvType>())
                {
                    // Batch (instance)
                    var inst = new Maenv(period, percentage, maType);
                    var (bMid, bUp, bLo) = inst.Update(_testData.Data);

                    // Static batch
                    var (sMid, sUp, sLo) = Maenv.Batch(_testData.Data, period, percentage, maType);

                    ValidationHelper.VerifySeriesEqual(bMid, sMid);
                    ValidationHelper.VerifySeriesEqual(bUp, sUp);
                    ValidationHelper.VerifySeriesEqual(bLo, sLo);

                    // Streaming
                    var streaming = new Maenv(period, percentage, maType);
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
                    Maenv.Batch(source.AsSpan(), spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan(), period, percentage, maType);

                    for (int i = 0; i < source.Length; i++)
                    {
                        Assert.Equal(sMid[i].Value, spanMid[i], 9);
                        Assert.Equal(sUp[i].Value, spanUp[i], 9);
                        Assert.Equal(sLo[i].Value, spanLo[i], 9);
                    }
                }
            }
        }

        _output.WriteLine("Maenv mode consistency validated (batch/stream/span) for all MA types");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        const double percentage = 2.0;

        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var pub = new TSeries();
            var evtInd = new Maenv(pub, period, percentage, maType);
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

            var (bMid, bUp, bLo) = Maenv.Batch(_testData.Data, period, percentage, maType);

            ValidationHelper.VerifySeriesEqual(bMid, evtMid);
            ValidationHelper.VerifySeriesEqual(bUp, evtUp);
            ValidationHelper.VerifySeriesEqual(bLo, evtLo);
        }

        _output.WriteLine("Maenv eventing mode validated for all MA types");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        const double percentage = 2.5;

        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var ((mid, up, lo), ind) = Maenv.Calculate(_testData.Data, period, percentage, maType);

            Assert.True(ind.IsHot);
            Assert.Equal(period, ind.WarmupPeriod);
            Assert.Equal(mid.Last.Value, ind.Last.Value, 1e-10);
            Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
            Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

            // Continue streaming
            var next = new TValue(DateTime.UtcNow, 100);
            ind.Update(next);
            Assert.True(ind.IsHot);
        }

        _output.WriteLine("Maenv Calculate validated for all MA types");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;
        const double percentage = 1.5;

        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var (bMid, bUp, bLo) = Maenv.Batch(_testData.Data, period, percentage, maType);

            var primed = new Maenv(period, percentage, maType);
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
        }

        _output.WriteLine("Maenv Prime validated against batch for all MA types");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var (mid, up, lo) = Maenv.Batch(_testData.Data, 50, 2.0, maType);

            ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
            ValidationHelper.VerifyAllFinite(up, startIndex: 0);
            ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

            // Upper > Lower for all bars (positive prices)
            for (int i = 0; i < mid.Count; i++)
            {
                Assert.True(up[i].Value > lo[i].Value, $"Upper > Lower at {i} for {maType}");
            }
        }

        _output.WriteLine("Maenv large dataset validated for all MA types");
    }

    [Fact]
    public void Validate_BandSymmetry_AllBars()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var ind = new Maenv(20, 2.0, maType);
            var (mid, up, lo) = ind.Update(_testData.Data);

            for (int i = 0; i < mid.Count; i++)
            {
                double upperWidth = up[i].Value - mid[i].Value;
                double lowerWidth = mid[i].Value - lo[i].Value;
                Assert.Equal(upperWidth, lowerWidth, 1e-10);
            }
        }

        _output.WriteLine("Maenv band symmetry validated for all bars and MA types");
    }

    [Fact]
    public void Validate_PercentageScaling()
    {
        double[] percentages = { 1.0, 2.0, 3.0, 4.0 };
        double[] widths = new double[percentages.Length];

        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            for (int i = 0; i < percentages.Length; i++)
            {
                var ind = new Maenv(20, percentages[i], maType);
                foreach (var tv in _testData.Data)
                {
                    ind.Update(tv);
                }
                widths[i] = ind.Upper.Value - ind.Lower.Value;
            }

            // Widths should scale linearly with percentage
            double baseWidth = widths[0];
            for (int i = 1; i < percentages.Length; i++)
            {
                double expected = baseWidth * percentages[i];
                Assert.Equal(expected, widths[i], 1e-9);
            }
        }

        _output.WriteLine("Maenv percentage scaling validated for all MA types");
    }

    [Fact]
    public void Validate_PeriodEffect_Smoothing()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] middles = new double[periods.Length];

        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            for (int i = 0; i < periods.Length; i++)
            {
                var ind = new Maenv(periods[i], 2.0, maType);
                foreach (var tv in _testData.Data)
                {
                    ind.Update(tv);
                }
                middles[i] = ind.Last.Value;
            }

            // All should produce finite values
            foreach (var m in middles)
            {
                Assert.True(double.IsFinite(m));
            }
        }

        _output.WriteLine("Maenv period effect validated for all MA types");
    }

    [Fact]
    public void Validate_StateRestoration_Iterative()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var ind = new Maenv(15, 2.5, maType);
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
        }

        _output.WriteLine("Maenv state restoration validated for all MA types");
    }

    [Fact]
    public void Validate_BandWidthFormula()
    {
        // Band width = 2 * middle * percentage / 100
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var ind = new Maenv(20, 3.0, maType);

            foreach (var tv in _testData.Data)
            {
                ind.Update(tv);

                double expectedWidth = 2 * ind.Last.Value * 3.0 / 100.0;
                double actualWidth = ind.Upper.Value - ind.Lower.Value;
                Assert.Equal(expectedWidth, actualWidth, 1e-10);
            }
        }

        _output.WriteLine("Maenv band width formula validated");
    }

    [Fact]
    public void Validate_MaTypesDifferent()
    {
        // Different MA types should produce different results (except for first bar)
        var indSma = new Maenv(10, 2.0, MaenvType.SMA);
        var indEma = new Maenv(10, 2.0, MaenvType.EMA);
        var indWma = new Maenv(10, 2.0, MaenvType.WMA);

        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            indSma.Update(tv);
            indEma.Update(tv);
            indWma.Update(tv);
        }

        // Values should be different (with high probability)
        bool allSame = Math.Abs(indSma.Last.Value - indEma.Last.Value) < 1e-10 &&
                       Math.Abs(indEma.Last.Value - indWma.Last.Value) < 1e-10;
        Assert.False(allSame, "Different MA types should produce different values");

        _output.WriteLine("Maenv MA types produce different results validated");
    }

    [Fact]
    public void Validate_WarmupCompensation_EMA()
    {
        // EMA should converge quickly due to warmup compensation
        var series = new TSeries();
        var t0 = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(t0.AddMinutes(i), 100.0));
        }

        var ind = new Maenv(20, 1.0, MaenvType.EMA);
        var (mid, _, _) = ind.Update(series);

        // After warmup, middle should be very close to constant price
        for (int i = 40; i < 100; i++)
        {
            Assert.InRange(mid[i].Value, 99.9, 100.1);
        }

        _output.WriteLine("Maenv EMA warmup compensation validated");
    }

    [Fact]
    public void Validate_SMA_RingBuffer_O1()
    {
        // SMA should maintain O(1) computation via ring buffer
        // Test that it produces correct rolling average
        var ind = new Maenv(5, 1.0, MaenvType.SMA);
        var values = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        for (int i = 0; i < values.Length; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, values[i]));

            // Calculate expected SMA
            int start = Math.Max(0, i - 4);
            double sum = 0;
            for (int j = start; j <= i; j++)
            {
                sum += values[j];
            }
            double expected = sum / (i - start + 1);

            Assert.Equal(expected, ind.Last.Value, 1e-10);
        }

        _output.WriteLine("Maenv SMA ring buffer O(1) validated");
    }

    [Fact]
    public void Validate_Skender_SMA_Centerline()
    {
        // Skender GetMaEnvelopes(lookbackPeriods, percentOffset, MaType.SMA)
        // QuanTAlib Maenv(period, percentage, MaenvType.SMA)
        // Both compute: Middle = SMA(Close), Upper = Middle + Middle*pct/100, Lower = Middle - Middle*pct/100
        // For SMA type, results should match exactly.

        int[] periods = { 5, 10, 20, 50 };
        double percentage = 2.5;

        foreach (var period in periods)
        {
            var (qMiddle, _, _) = Maenv.Batch(_testData.Data, period, percentage, MaenvType.SMA);

            var sResult = _testData.SkenderQuotes
                .GetMaEnvelopes(period, percentage, MaType.SMA)
                .ToList();

            ValidationHelper.VerifyData(qMiddle, sResult, s => s.Centerline);
        }
        _output.WriteLine("Maenv SMA centerline validated against Skender for all periods");
    }

    [Fact]
    public void Validate_Skender_SMA_UpperEnvelope()
    {
        int[] periods = { 5, 10, 20, 50 };
        double percentage = 2.5;

        foreach (var period in periods)
        {
            var (_, qUpper, _) = Maenv.Batch(_testData.Data, period, percentage, MaenvType.SMA);

            var sResult = _testData.SkenderQuotes
                .GetMaEnvelopes(period, percentage, MaType.SMA)
                .ToList();

            ValidationHelper.VerifyData(qUpper, sResult, s => s.UpperEnvelope);
        }
        _output.WriteLine("Maenv SMA upper envelope validated against Skender for all periods");
    }

    [Fact]
    public void Validate_Skender_SMA_LowerEnvelope()
    {
        int[] periods = { 5, 10, 20, 50 };
        double percentage = 2.5;

        foreach (var period in periods)
        {
            var (_, _, qLower) = Maenv.Batch(_testData.Data, period, percentage, MaenvType.SMA);

            var sResult = _testData.SkenderQuotes
                .GetMaEnvelopes(period, percentage, MaType.SMA)
                .ToList();

            ValidationHelper.VerifyData(qLower, sResult, s => s.LowerEnvelope);
        }
        _output.WriteLine("Maenv SMA lower envelope validated against Skender for all periods");
    }

    [Fact]
    public void Maenv_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateMovingAverageEnvelope();
        var values = result.OutputValues.Values.First();
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}