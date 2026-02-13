// Massi: Mathematical property validation tests
// Mass Index by Donald Dorsey. While Ooples has GetMassIndex(), the implementation
// differences (EMA compensation, continuous vs discrete sum) make direct comparison
// unreliable. Validation uses mathematical property testing instead.

namespace QuanTAlib.Tests;

using Xunit;

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
}
