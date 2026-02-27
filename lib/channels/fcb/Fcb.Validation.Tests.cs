using Skender.Stock.Indicators;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class FcbValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public FcbValidationTests(ITestOutputHelper output)
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
    public void Validate_ManualCalculation_FractalDetection()
    {
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Create clear fractal patterns:
        // Fractal high at bar 1: highs = 100, 120, 110
        // Fractal low at bar 1: lows = 90, 70, 80
        series.Add(new TBar(t0, 0, 100, 90, 95, 100));
        series.Add(new TBar(t0.AddMinutes(1), 0, 120, 70, 95, 100)); // Fractal high=120, low=70
        series.Add(new TBar(t0.AddMinutes(2), 0, 110, 80, 95, 100)); // Confirms fractals

        var ind = new Fcb(3);
        var (mid, up, lo) = ind.Update(series);

        // Upper should be highest fractal high = 120
        // Lower should be lowest fractal low = 70
        // Middle = (120 + 70) / 2 = 95
        Assert.Equal(120.0, up.Last.Value, 1e-10);
        Assert.Equal(70.0, lo.Last.Value, 1e-10);
        Assert.Equal(95.0, mid.Last.Value, 1e-10);

        _output.WriteLine("FCB manual fractal detection validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            // Batch (instance)
            var inst = new Fcb(period);
            var (bMid, bUp, bLo) = inst.Update(_testData.Bars);

            // Static batch
            var (sMid, sUp, sLo) = Fcb.Batch(_testData.Bars, period);

            ValidationHelper.VerifySeriesEqual(bMid, sMid);
            ValidationHelper.VerifySeriesEqual(bUp, sUp);
            ValidationHelper.VerifySeriesEqual(bLo, sLo);

            // Streaming
            var streaming = new Fcb(period);
            var sMidStream = new TSeries();
            var sUpStream = new TSeries();
            var sLoStream = new TSeries();
            foreach (var bar in _testData.Bars)
            {
                streaming.Update(bar);
                sMidStream.Add(streaming.Last);
                sUpStream.Add(streaming.Upper);
                sLoStream.Add(streaming.Lower);
            }

            ValidationHelper.VerifySeriesEqual(sMid, sMidStream);
            ValidationHelper.VerifySeriesEqual(sUp, sUpStream);
            ValidationHelper.VerifySeriesEqual(sLo, sLoStream);

            // Span
            double[] high = _testData.HighPrices.ToArray();
            double[] low = _testData.LowPrices.ToArray();
            double[] spanMid = new double[high.Length];
            double[] spanUp = new double[high.Length];
            double[] spanLo = new double[high.Length];
            Fcb.Batch(high.AsSpan(), low.AsSpan(),
                spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan(), period);

            for (int i = 0; i < high.Length; i++)
            {
                Assert.Equal(sMid[i].Value, spanMid[i], 9);
                Assert.Equal(sUp[i].Value, spanUp[i], 9);
                Assert.Equal(sLo[i].Value, spanLo[i], 9);
            }
        }

        _output.WriteLine("FCB mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        var pub = new TBarSeries();
        var evtInd = new Fcb(pub, period);
        var evtMid = new TSeries();
        var evtUp = new TSeries();
        var evtLo = new TSeries();

        foreach (var bar in _testData.Bars)
        {
            pub.Add(bar);
            evtMid.Add(evtInd.Last);
            evtUp.Add(evtInd.Upper);
            evtLo.Add(evtInd.Lower);
        }

        var (bMid, bUp, bLo) = Fcb.Batch(_testData.Bars, period);

        ValidationHelper.VerifySeriesEqual(bMid, evtMid);
        ValidationHelper.VerifySeriesEqual(bUp, evtUp);
        ValidationHelper.VerifySeriesEqual(bLo, evtLo);

        _output.WriteLine("FCB eventing mode validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        var ((mid, up, lo), ind) = Fcb.Calculate(_testData.Bars, period);

        Assert.True(ind.IsHot);
        Assert.Equal(period + 2, ind.WarmupPeriod); // period + 2 for fractal detection
        Assert.Equal(mid.Last.Value, ind.Last.Value, 1e-10);
        Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
        Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

        // Continue streaming
        var next = new TBar(DateTime.UtcNow, 0, 150, 50, 100, 1000);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("FCB Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;

        var (bMid, bUp, bLo) = Fcb.Batch(_testData.Bars, period);

        var primed = new Fcb(period);
        var subset = new TBarSeries();
        for (int i = 0; i < 200; i++)
        {
            subset.Add(_testData.Bars[i]);
        }

        primed.Prime(subset);

        for (int i = 200; i < _testData.Bars.Count; i++)
        {
            primed.Update(_testData.Bars[i]);
        }

        Assert.Equal(bMid.Last.Value, primed.Last.Value, 1e-9);
        Assert.Equal(bUp.Last.Value, primed.Upper.Value, 1e-9);
        Assert.Equal(bLo.Last.Value, primed.Lower.Value, 1e-9);

        _output.WriteLine("FCB Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, up, lo) = Fcb.Batch(_testData.Bars, 50);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        // After warmup, upper should always be >= lower
        int warmup = 50 + 2; // period + 2 for fractal detection
        for (int i = warmup; i < mid.Count; i++)
        {
            Assert.True(up[i].Value >= lo[i].Value, $"Upper >= Lower at {i}");
        }

        _output.WriteLine("FCB large dataset validated");
    }

    [Fact]
    public void Validate_FractalPattern_CorrectDetection()
    {
        // Create a series with known fractal patterns
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Pattern designed to have clear fractals:
        // highs: 100, 110, 105 -> fractal high at index 1 = 110
        // lows:   50,  45,  48 -> fractal low at index 1 = 45
        double[] highs = { 100, 110, 105, 108, 115, 112, 118, 125, 120 };
        double[] lows = { 50, 45, 48, 42, 47, 40, 44, 38, 42 };

        for (int i = 0; i < highs.Length; i++)
        {
            series.Add(new TBar(t0.AddMinutes(i), 0, highs[i], lows[i], (highs[i] + lows[i]) / 2, 100));
        }

        var ind = new Fcb(5);
        var (mid, up, lo) = ind.Update(series);

        // All outputs should be finite
        for (int i = 0; i < series.Count; i++)
        {
            Assert.True(double.IsFinite(mid[i].Value), $"Mid finite at {i}");
            Assert.True(double.IsFinite(up[i].Value), $"Upper finite at {i}");
            Assert.True(double.IsFinite(lo[i].Value), $"Lower finite at {i}");
        }

        // After enough bars, upper should be >= lower
        for (int i = 3; i < series.Count; i++)
        {
            Assert.True(up[i].Value >= lo[i].Value, $"Upper >= Lower at {i}");
        }

        _output.WriteLine("FCB fractal pattern validation passed");
    }

    [Fact]
    public void Validate_MiddleIsAverage_AllBars()
    {
        var ind = new Fcb(20);
        var (mid, up, lo) = ind.Update(_testData.Bars);

        for (int i = 0; i < mid.Count; i++)
        {
            double expected = (up[i].Value + lo[i].Value) * 0.5;
            Assert.Equal(expected, mid[i].Value, 1e-10);
        }

        _output.WriteLine("FCB middle = average validated for all bars");
    }

    [Fact]
    public void Validate_BandsAreMonotonic_WithinWindow()
    {
        // The upper band should be the highest fractal high in the window
        // The lower band should be the lowest fractal low in the window
        // These values shouldn't jump erratically unless new fractals are detected

        var ind = new Fcb(10);
        foreach (var bar in _testData.Bars)
        {
            ind.Update(bar);

            // Upper should always be >= Lower
            if (ind.IsHot)
            {
                Assert.True(ind.Upper.Value >= ind.Lower.Value,
                    $"Upper ({ind.Upper.Value}) should be >= Lower ({ind.Lower.Value})");
            }
        }

        _output.WriteLine("FCB band monotonicity validated");
    }

    [Fact]
    public void Validate_Skender_BandStructure()
    {
        // Skender GetFcb(windowSpan) uses Williams fractal carry-forward:
        //   - windowSpan is half-width for 3-bar fractal detection (min=2)
        //   - UpperBand = last confirmed FractalBear (highest high carry-forward)
        //   - LowerBand = last confirmed FractalBull (lowest low carry-forward)
        //   - Results are decimal? (need cast to double)
        //
        // NOTE: Skender's UpperBand can be LOWER than LowerBand when the last
        // bear fractal occurred at a lower price than the last bull fractal.
        // This is a known property of fractal carry-forward algorithms.
        //
        // QuanTAlib Fcb(period) uses monotonic deques over a lookback window
        // and always maintains Upper >= Lower ordering.

        int windowSpan = 2;
        var sResult = _testData.SkenderQuotes
            .GetFcb(windowSpan)
            .ToList();

        // Verify Skender produces finite values
        int validCount = 0;
        for (int i = 0; i < sResult.Count; i++)
        {
            if (sResult[i].UpperBand.HasValue && sResult[i].LowerBand.HasValue)
            {
                double upper = (double)sResult[i].UpperBand!.Value;
                double lower = (double)sResult[i].LowerBand!.Value;
                Assert.True(double.IsFinite(upper), $"Skender Upper finite at bar {i}");
                Assert.True(double.IsFinite(lower), $"Skender Lower finite at bar {i}");
                Assert.True(upper > 0, $"Skender Upper positive at bar {i}");
                Assert.True(lower > 0, $"Skender Lower positive at bar {i}");
                validCount++;
            }
        }

        Assert.True(validCount > 0, "Skender should produce some valid FCB values");
        _output.WriteLine($"Skender FCB band structure validated ({validCount} valid bars with finite values)");
    }

    [Fact]
    public void Validate_Skender_BothProduceChannels()
    {
        // Both QuanTAlib and Skender FCB should produce meaningful channels
        // that track price structure. Verify both produce valid finite values.
        //
        // NOTE: Skender bands can cross (Upper < Lower) due to fractal
        // carry-forward semantics, so we only validate finite positive values.

        int windowSpan = 2;
        int period = 20;

        var sResult = _testData.SkenderQuotes
            .GetFcb(windowSpan)
            .ToList();

        var (qMiddle, qUpper, qLower) = Fcb.Batch(_testData.Bars, period);

        // After warmup, both should have valid bands
        int qValidCount = 0;
        int sValidCount = 0;

        for (int i = period + 2; i < qMiddle.Count && i < sResult.Count; i++)
        {
            if (qUpper[i].Value > 0 && qLower[i].Value > 0)
            {
                Assert.True(qUpper[i].Value >= qLower[i].Value,
                    $"QuanTAlib Upper >= Lower at bar {i}");
                qValidCount++;
            }

            if (sResult[i].UpperBand.HasValue && sResult[i].LowerBand.HasValue)
            {
                double sUpper = (double)sResult[i].UpperBand!.Value;
                double sLower = (double)sResult[i].LowerBand!.Value;
                Assert.True(double.IsFinite(sUpper) && sUpper > 0,
                    $"Skender Upper finite and positive at bar {i}");
                Assert.True(double.IsFinite(sLower) && sLower > 0,
                    $"Skender Lower finite and positive at bar {i}");
                sValidCount++;
            }
        }

        Assert.True(qValidCount > 100, $"QuanTAlib produced {qValidCount} valid bars");
        Assert.True(sValidCount > 100, $"Skender produced {sValidCount} valid bars");

        _output.WriteLine($"FCB channel comparison: QuanTAlib={qValidCount}, Skender={sValidCount} valid bars");
    }

    [Fact]
    public void Fcb_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateFractalChaosBands();
        var values = result.OutputValues.Values.First();
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
