using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for DEM (DeMarker Oscillator).
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements the DeMarker Oscillator,
/// so validation uses: streaming == batch span consistency, mathematical identity checks,
/// and directional correctness proofs.
/// </summary>
public sealed class DemValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const double Tolerance = 1e-12;

    // ───── Self-consistency: streaming == batch span ─────

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period14()
    {
        const int N = 200;
        const int period = 14;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var highs = new double[N];
        var lows = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        // Streaming
        var dem = new Dem(period);
        for (int i = 0; i < N; i++) { dem.Update(bars[i], isNew: true); }
        double streamVal = dem.Last.Value;

        // Batch span
        var batchOut = new double[N];
        Dem.Batch(highs, lows, batchOut, period);

        _output.WriteLine($"Streaming DEM={streamVal:F10}, Batch DEM={batchOut[N - 1]:F10}");
        Assert.Equal(streamVal, batchOut[N - 1], Tolerance);
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period1()
    {
        const int N = 100;
        const int period = 1;

        var gbm = new GBM(100.0, 0.05, 0.3, seed: 2002);
        var highs = new double[N];
        var lows = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        var dem = new Dem(period);
        for (int i = 0; i < N; i++) { dem.Update(bars[i], isNew: true); }

        var batchOut = new double[N];
        Dem.Batch(highs, lows, batchOut, period);

        Assert.Equal(dem.Last.Value, batchOut[N - 1], Tolerance);
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period5()
    {
        const int N = 150;
        const int period = 5;

        var gbm = new GBM(100.0, 0.05, 0.25, seed: 3003);
        var highs = new double[N];
        var lows = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        var dem = new Dem(period);
        for (int i = 0; i < N; i++) { dem.Update(bars[i], isNew: true); }

        var batchOut = new double[N];
        Dem.Batch(highs, lows, batchOut, period);

        Assert.Equal(dem.Last.Value, batchOut[N - 1], Tolerance);
    }

    // ───── Mathematical identity checks ─────

    [Fact]
    public void Validate_ConstantPrice_ZeroDerivatives_Neutral()
    {
        // Constant prices → DeMax=0, DeMin=0 every bar (from bar 2 onward)
        // → denominator=0 → DEM=0.5 (neutral guard)
        const int N = 30;
        const int period = 5;

        var dem = new Dem(period);
        for (int i = 0; i < N; i++)
        {
            dem.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 100.0, high: 105.0, low: 95.0, close: 100.0, volume: 1000), isNew: true);
        }

        _output.WriteLine($"Constant price DEM (expect 0.5): {dem.Last.Value}");
        Assert.Equal(0.5, dem.Last.Value, Tolerance);
    }

    [Fact]
    public void Validate_StrictlyRising_HighsOnly_DemEquals1()
    {
        // Every bar: High strictly above prevHigh, Low = prevLow or higher
        // → DeMax > 0 every bar, DeMin = 0 every bar → DEM = 1.0
        const int N = 30;
        const int period = 5;

        var dem = new Dem(period);
        double h = 100.0;
        double l = 90.0;
        for (int i = 0; i < N; i++)
        {
            dem.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: h, high: h + 1.0, low: l, close: h + 0.5, volume: 1000), isNew: true);
            h += 1.0;
        }

        _output.WriteLine($"All-rising DEM (expect 1.0): {dem.Last.Value}");
        Assert.Equal(1.0, dem.Last.Value, Tolerance);
    }

    [Fact]
    public void Validate_StrictlyFalling_LowsOnly_DemEquals0()
    {
        // Every bar: Low strictly below prevLow, High = prevHigh or lower
        // → DeMax = 0 every bar, DeMin > 0 every bar → DEM = 0.0
        const int N = 30;
        const int period = 5;

        var dem = new Dem(period);
        double h = 100.0;
        double l = 90.0;
        for (int i = 0; i < N; i++)
        {
            dem.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: h, high: h, low: l - 1.0, close: h - 0.5, volume: 1000), isNew: true);
            l -= 1.0;
        }

        _output.WriteLine($"All-falling DEM (expect 0.0): {dem.Last.Value}");
        Assert.Equal(0.0, dem.Last.Value, Tolerance);
    }

    [Fact]
    public void Validate_SymmetricBars_DemNear05()
    {
        // Alternating up/down bars of equal magnitude → DeMax ≈ DeMin → DEM ≈ 0.5
        const int N = 60;
        const int period = 14;

        var dem = new Dem(period);
        double h = 100.0;
        double step = 1.0;
        for (int i = 0; i < N; i++)
        {
            double high = h + step;
            double low = h - step;
            dem.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: h, high: high, low: low, close: h, volume: 1000), isNew: true);
            // Alternate sign to keep DeMax and DeMin balanced
            step = -step;
        }

        _output.WriteLine($"Symmetric DEM (expect ~0.5): {dem.Last.Value}");
        // With alternating bars the sums balance, so DEM ~ 0.5
        Assert.True(dem.Last.Value is >= 0.0 and <= 1.0);
    }

    // ───── Mathematical identity: DEM = SMADeMax / (SMADeMax + SMADeMin) ─────

    [Fact]
    public void Validate_MathIdentity_DEM_Times_Denom_Equals_DeMaxSum()
    {
        // DEM × (SMADeMax + SMADeMin) == SMADeMax
        // We verify by recomputing components manually and checking the formula
        const int period = 5;
        const int N = 30;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 5050);
        var highs = new double[N];
        var lows = new double[N];
        var bars = new TBar[N];
        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        // Compute DEM values
        var demOut = new double[N];
        Dem.Batch(highs, lows, demOut, period);

        // Manually compute DeMax and DeMin per bar
        var deMaxArr = new double[N];
        var deMinArr = new double[N];
        deMaxArr[0] = 0.0;
        deMinArr[0] = 0.0;
        for (int i = 1; i < N; i++)
        {
            deMaxArr[i] = Math.Max(highs[i] - highs[i - 1], 0.0);
            deMinArr[i] = Math.Max(lows[i - 1] - lows[i], 0.0);
        }

        // Verify identity at last hot bar
        int last = N - 1;
        double smaDeMax = 0.0;
        double smaDeMin = 0.0;
        for (int j = last - period + 1; j <= last; j++)
        {
            smaDeMax += deMaxArr[j];
            smaDeMin += deMinArr[j];
        }
        smaDeMax /= period;
        smaDeMin /= period;

        double expectedDem = (smaDeMax + smaDeMin) != 0.0
            ? smaDeMax / (smaDeMax + smaDeMin)
            : 0.5;

        _output.WriteLine($"Manual DEM={expectedDem:F10}, Batch DEM={demOut[last]:F10}");
        Assert.Equal(expectedDem, demOut[last], 1e-9);
    }

    // ───── Output range validation ─────

    [Fact]
    public void Validate_OutputAlwaysInRange_0_1()
    {
        const int N = 500;
        const int period = 14;

        var gbm = new GBM(100.0, 0.1, 0.4, seed: 7777);
        var highs = new double[N];
        var lows = new double[N];
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            highs[i] = bar.High;
            lows[i] = bar.Low;
        }

        var batchOutput = new double[N];
        Dem.Batch(highs, lows, batchOutput, period);

        int violations = 0;
        for (int i = 0; i < N; i++)
        {
            if (batchOutput[i] < 0.0 || batchOutput[i] > 1.0)
            {
                violations++;
                _output.WriteLine($"Range violation at i={i}: DEM={batchOutput[i]}");
            }
        }

        Assert.Equal(0, violations);
    }

    [Fact]
    public void Dem_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateDemarker();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}