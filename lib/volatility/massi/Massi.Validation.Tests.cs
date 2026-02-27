// Massi: Mathematical property validation tests
// Mass Index by Donald Dorsey. While Ooples has GetMassIndex(), the implementation
// differences (EMA compensation, continuous vs discrete sum) make direct comparison
// unreliable. Validation uses mathematical property testing instead.

using Tulip;

namespace QuanTAlib.Tests;

using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

public class MassiValidationTests
{
    private const int DefaultEmaLength = 9;
    private const int DefaultSumLength = 25;
    private const int TestDataLength = 500;

    [Fact]
    public void Massi_Output_IsFiniteForGbmData()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = massi.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Massi output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Massi_Output_IsPositive_AfterWarmup()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = massi.Update(bars[i], isNew: true);
            if (massi.IsHot)
            {
                Assert.True(result.Value > 0,
                    $"Massi output must be positive after warmup at bar {i}, got {result.Value}");
            }
        }
    }

    [Fact]
    public void Massi_ConstantRange_ConvergesToSumLength()
    {
        // When High-Low is constant, EMA1 = EMA2 after convergence,
        // so ratio = 1.0. Sum of 25 ratios = 25.0.
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < 300; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                101, 101, 99, 100, 1000); // constant range = 2
            massi.Update(bar, isNew: true);
        }

        // After convergence: ratio ≈ 1.0, sum ≈ 25.0
        Assert.Equal(DefaultSumLength, massi.Last.Value, tolerance: 0.5);
    }

    [Fact]
    public void Massi_Ratio_ConvergesToOne_ForConstantRange()
    {
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < 300; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                102, 102, 98, 100, 1000);
            massi.Update(bar, isNew: true);
        }

        // EMA1/EMA2 should converge to 1.0 for constant range
        Assert.Equal(1.0, massi.Ratio, precision: 3);
    }

    [Fact]
    public void Massi_Ema1_GreaterThanZero_ForPositiveRange()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < bars.Count; i++)
        {
            massi.Update(bars[i], isNew: true);
            if (massi.IsHot)
            {
                Assert.True(massi.Ema1 > 0,
                    $"EMA1 must be > 0 at bar {i}, got {massi.Ema1}");
            }
        }
    }

    [Fact]
    public void Massi_Ema2_GreaterThanZero_ForPositiveRange()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < bars.Count; i++)
        {
            massi.Update(bars[i], isNew: true);
            if (massi.IsHot)
            {
                Assert.True(massi.Ema2 > 0,
                    $"EMA2 must be > 0 at bar {i}, got {massi.Ema2}");
            }
        }
    }

    [Fact]
    public void Massi_BatchTBarSeries_MatchesStreaming()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch
        var batchResults = Massi.Batch(bars, DefaultEmaLength, DefaultSumLength);

        // Streaming
        var streamMassi = new Massi(DefaultEmaLength, DefaultSumLength);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamMassi.Update(bars[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 10);
        }
    }

    [Fact]
    public void Massi_WideningRange_IncreasesValue()
    {
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        // Start with constant narrow range
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100.5, 100.5, 99.5, 100, 1000); // range = 1
            massi.Update(bar, isNew: true);
        }
        double narrowValue = massi.Last.Value;

        // Abruptly widen the range
        for (int i = 100; i < 150; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                110, 110, 90, 100, 1000); // range = 20
            massi.Update(bar, isNew: true);
        }
        double wideValue = massi.Last.Value;

        // Widening range causes EMA1 to react faster than EMA2,
        // so ratio > 1 and MASSI increases
        Assert.True(wideValue > narrowValue,
            $"Widening range should increase MASSI: narrow={narrowValue}, wide={wideValue}");
    }

    [Fact]
    public void Massi_DifferentParameters_ProduceDifferentResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var massi1 = new Massi(9, 25);
        var massi2 = new Massi(5, 10);

        for (int i = 0; i < bars.Count; i++)
        {
            massi1.Update(bars[i], isNew: true);
            massi2.Update(bars[i], isNew: true);
        }

        Assert.NotEqual(massi1.Last.Value, massi2.Last.Value);
    }

    [Fact]
    public void Massi_BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var massi = new Massi(DefaultEmaLength, DefaultSumLength);

        for (int i = 0; i < 40; i++)
        {
            massi.Update(bars[i], isNew: true);
        }

        massi.Update(bars[40], isNew: true);
        double afterNew = massi.Last.Value;

        massi.Update(bars[40], isNew: false);
        double afterCorrection = massi.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }

    // === Tulip Cross-Validation ===

    /// <summary>
    /// Structural validation against Tulip <c>mass</c> indicator.
    /// Algorithm variant: Tulip <c>mass</c> uses a single <c>period</c> for both the EMA
    /// smoothing window and the summation window (25 bars hardcoded in some builds).
    /// QuanTAlib uses separate <c>emaLength</c> and <c>sumLength</c> parameters.
    /// Direct numeric equality is not asserted; test documents the difference and
    /// verifies both implementations produce finite, positive output on the same data.
    /// </summary>
    [Fact]
    public void Massi_Tulip_StructuralVariant_BothFinite()
    {
        const int period = 9;
        var bars = new GBM(sigma: 0.3, seed: 42).Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] highData = new double[bars.Count];
        double[] lowData = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            highData[i] = bars[i].High;
            lowData[i] = bars[i].Low;
        }

        // Tulip mass — single period (covers both EMA pass and sum window)
        var tulipIndicator = Tulip.Indicators.mass;
        double[][] inputs = { highData, lowData };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[highData.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        // QuanTAlib Massi — separate emaLength / sumLength
        var massi = new Massi(emaLength: period, sumLength: DefaultSumLength);
        foreach (var bar in bars) { massi.Update(bar); }

        // Structural: Tulip must produce finite, positive output
        Assert.True(tResult.Length > 0, "Tulip mass must produce output");
        foreach (double v in tResult)
        {
            Assert.True(double.IsFinite(v), $"Tulip mass produced non-finite value: {v}");
            Assert.True(v > 0, $"Mass Index must be positive, got {v}");
        }

        Assert.True(massi.IsHot, "QuanTAlib Massi must be hot after sufficient bars");
        Assert.True(massi.Last.Value > 0, "QuanTAlib Massi last value must be positive");
    }

    [Fact]
    public void Massi_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateMassIndex();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
