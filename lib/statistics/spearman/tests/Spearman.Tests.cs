namespace QuanTAlib.Tests;

public class SpearmanTests
{
    [Fact]
    public void Constructor_PeriodOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Spearman(1));
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Spearman(0));
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var s = new Spearman(10);
        Assert.Equal("Spearman(10)", s.Name);
    }

    [Fact]
    public void SingleInput_Update_ThrowsNotSupported()
    {
        var s = new Spearman(5);
        Assert.Throws<NotSupportedException>(() => s.Update(new TValue(DateTime.UtcNow, 1.0)));
    }

    [Fact]
    public void SingleInput_UpdateTSeries_ThrowsNotSupported()
    {
        var s = new Spearman(5);
        var ts = new TSeries();
        Assert.Throws<NotSupportedException>(() => s.Update(ts));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var s = new Spearman(5);
        Assert.Throws<NotSupportedException>(() => s.Prime(stackalloc double[] { 1, 2, 3 }));
    }

    [Fact]
    public void PerfectConcordance_ReturnsOne()
    {
        var s = new Spearman(5);
        for (int i = 1; i <= 5; i++)
        {
            s.Update((double)i, (double)i, isNew: true);
        }
        Assert.Equal(1.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void PerfectDiscordance_ReturnsMinusOne()
    {
        var s = new Spearman(5);
        for (int i = 1; i <= 5; i++)
        {
            s.Update((double)i, 6.0 - i, isNew: true);
        }
        Assert.Equal(-1.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void KnownSequence_MatchesExpected()
    {
        // X = [1,2,3,4,5], Y = [1,3,2,5,4]
        // Ranks X = [1,2,3,4,5], Ranks Y = [1,3,2,5,4]
        // d = [0,-1,1,-1,1], d² = [0,1,1,1,1], Σd² = 4
        // ρ = 1 - 6*4/(5*24) = 1 - 24/120 = 1 - 0.2 = 0.8
        var s = new Spearman(5);
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [1, 3, 2, 5, 4];

        for (int i = 0; i < 5; i++)
        {
            s.Update(x[i], y[i], isNew: true);
        }

        Assert.Equal(0.8, s.Last.Value, 1e-10);
    }

    [Fact]
    public void Symmetry_RhoXY_EqualsRhoYX()
    {
        var s1 = new Spearman(5);
        var s2 = new Spearman(5);
        double[] x = [10, 20, 15, 30, 25];
        double[] y = [5, 15, 10, 25, 20];

        for (int i = 0; i < 5; i++)
        {
            s1.Update(x[i], y[i], isNew: true);
            s2.Update(y[i], x[i], isNew: true);
        }

        Assert.Equal(s1.Last.Value, s2.Last.Value, 1e-10);
    }

    [Fact]
    public void Antisymmetry_RhoXNegY_EqualsNegRhoXY()
    {
        var s1 = new Spearman(5);
        var s2 = new Spearman(5);
        double[] x = [10, 20, 15, 30, 25];
        double[] y = [5, 15, 10, 25, 20];

        for (int i = 0; i < 5; i++)
        {
            s1.Update(x[i], y[i], isNew: true);
            s2.Update(x[i], -y[i], isNew: true);
        }

        Assert.Equal(-s1.Last.Value, s2.Last.Value, 1e-10);
    }

    [Fact]
    public void ConstantSeries_ReturnsZero()
    {
        var s = new Spearman(5);
        for (int i = 0; i < 5; i++)
        {
            s.Update(42.0, (double)(i + 1), isNew: true);
        }
        Assert.Equal(0.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void BothConstant_ReturnsZero()
    {
        var s = new Spearman(5);
        for (int i = 0; i < 5; i++)
        {
            s.Update(42.0, 42.0, isNew: true);
        }
        Assert.Equal(0.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void TiedValues_HandledCorrectly()
    {
        // X = [1, 2, 2, 4, 5], Y = [5, 4, 3, 2, 1]
        // Ranks X = [1, 2.5, 2.5, 4, 5] (ties → average rank)
        // Ranks Y = [5, 4, 3, 2, 1]
        // Pearson on these ranks → negative correlation
        var s = new Spearman(5);
        double[] x = [1, 2, 2, 4, 5];
        double[] y = [5, 4, 3, 2, 1];

        for (int i = 0; i < 5; i++)
        {
            s.Update(x[i], y[i], isNew: true);
        }

        // Should be close to -1 (strong negative monotonic relationship)
        Assert.True(s.Last.Value < -0.9);
    }

    [Fact]
    public void IsHot_RequiresAtLeastTwo()
    {
        var s = new Spearman(5);
        Assert.False(s.IsHot);

        s.Update(1.0, 2.0, isNew: true);
        Assert.False(s.IsHot);

        s.Update(2.0, 3.0, isNew: true);
        Assert.True(s.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsNaN()
    {
        var s = new Spearman(5);
        s.Update(1.0, 2.0, isNew: true);
        Assert.True(double.IsNaN(s.Last.Value));
    }

    [Fact]
    public void IsNewFalse_CorrectsBars()
    {
        var s = new Spearman(5);
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [2, 4, 6, 8, 10];

        for (int i = 0; i < 5; i++)
        {
            s.Update(x[i], y[i], isNew: true);
        }

        double original = s.Last.Value;

        // Correct with different value — break rank correlation
        s.Update(100.0, 1.0, isNew: false);
        double corrected = s.Last.Value;
        Assert.NotEqual(original, corrected);

        // Correct back to original
        s.Update(x[4], y[4], isNew: false);
        double restored = s.Last.Value;
        Assert.Equal(original, restored, 1e-10);
    }

    [Fact]
    public void NaN_SubstitutesLastValid()
    {
        var s = new Spearman(5);
        for (int i = 1; i <= 4; i++)
        {
            s.Update((double)i, (double)i, isNew: true);
        }

        // Feed NaN — should use last valid value
        s.Update(double.NaN, double.NaN, isNew: true);
        Assert.True(double.IsFinite(s.Last.Value));
    }

    [Fact]
    public void Infinity_SubstitutesLastValid()
    {
        var s = new Spearman(5);
        for (int i = 1; i <= 4; i++)
        {
            s.Update((double)i, (double)i, isNew: true);
        }

        s.Update(double.PositiveInfinity, double.NegativeInfinity, isNew: true);
        Assert.True(double.IsFinite(s.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var s = new Spearman(5);
        for (int i = 1; i <= 5; i++)
        {
            s.Update((double)i, (double)i, isNew: true);
        }

        Assert.True(s.IsHot);

        s.Reset();
        Assert.False(s.IsHot);
        Assert.Equal(default, s.Last);
    }

    [Fact]
    public void SlidingWindow_DropOldValues()
    {
        var s = new Spearman(3);

        // Fill window: X=[1,2,3], Y=[1,2,3] → ρ = 1.0
        s.Update(1.0, 1.0, isNew: true);
        s.Update(2.0, 2.0, isNew: true);
        s.Update(3.0, 3.0, isNew: true);
        Assert.Equal(1.0, s.Last.Value, 1e-10);

        // Push to window: X=[2,3,100], Y=[2,3,-100] → mixed correlation
        s.Update(100.0, -100.0, isNew: true);
        // Window now [2,3,100] vs [2,3,-100]: ranks X=[1,2,3], Y=[2,3,1] → not perfect
        Assert.True(s.Last.Value < 1.0);
    }

    [Fact]
    public void BatchTSeries_MatchesStreaming()
    {
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var gbmY = new GBM(startPrice: 100, mu: 0.03, sigma: 0.15, seed: 99);

        var seriesX = new TSeries();
        var seriesY = new TSeries();

        for (int i = 0; i < 50; i++)
        {
            var barX = gbmX.Next();
            var barY = gbmY.Next();
            seriesX.Add(new TValue(barX.Time, barX.Close));
            seriesY.Add(new TValue(barY.Time, barY.Close));
        }

        TSeries batch = Spearman.Batch(seriesX, seriesY, 10);

        var streaming = new Spearman(10);
        for (int i = 0; i < 50; i++)
        {
            streaming.Update(seriesX[i], seriesY[i], isNew: true);
            Assert.Equal(streaming.Last.Value, batch[i].Value, 1e-10);
        }
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var gbmY = new GBM(startPrice: 100, mu: 0.03, sigma: 0.15, seed: 99);

        double[] xValues = new double[50];
        double[] yValues = new double[50];

        for (int i = 0; i < 50; i++)
        {
            xValues[i] = gbmX.Next().Close;
            yValues[i] = gbmY.Next().Close;
        }

        double[] output = new double[50];
        Spearman.Batch(xValues.AsSpan(), yValues.AsSpan(), output.AsSpan(), 10);

        var streaming = new Spearman(10);
        for (int i = 0; i < 50; i++)
        {
            streaming.Update(xValues[i], yValues[i], isNew: true);
            Assert.Equal(streaming.Last.Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void BatchSpan_MismatchedLengths_Throws()
    {
        double[] x = new double[10];
        double[] y = new double[5];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Spearman.Batch(x.AsSpan(), y.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void BatchSpan_MismatchedOutput_Throws()
    {
        double[] x = new double[10];
        double[] y = new double[10];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() =>
            Spearman.Batch(x.AsSpan(), y.AsSpan(), output.AsSpan(), 3));
    }

    [Fact]
    public void BatchSpan_InvalidPeriod_Throws()
    {
        double[] x = new double[10];
        double[] y = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Spearman.Batch(x.AsSpan(), y.AsSpan(), output.AsSpan(), 1));
    }

    [Fact]
    public void BatchTSeries_MismatchedLengths_Throws()
    {
        var sx = new TSeries();
        var sy = new TSeries();
        sx.Add(new TValue(DateTime.UtcNow, 1.0));

        Assert.Throws<ArgumentException>(() => Spearman.Batch(sx, sy, 3));
    }

    [Fact]
    public void Calculate_ReturnsTupleWithResults()
    {
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var gbmY = new GBM(startPrice: 100, mu: 0.03, sigma: 0.15, seed: 99);

        var seriesX = new TSeries();
        var seriesY = new TSeries();

        for (int i = 0; i < 30; i++)
        {
            var barX = gbmX.Next();
            var barY = gbmY.Next();
            seriesX.Add(new TValue(barX.Time, barX.Close));
            seriesY.Add(new TValue(barY.Time, barY.Close));
        }

        var (results, indicator) = Spearman.Calculate(seriesX, seriesY, 10);
        Assert.Equal(30, results.Count);
        Assert.NotNull(indicator);
    }

    [Fact]
    public void OutputBounded_BetweenMinusOneAndPlusOne()
    {
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var gbmY = new GBM(startPrice: 100, mu: 0.03, sigma: 0.15, seed: 99);

        var s = new Spearman(10);
        for (int i = 0; i < 100; i++)
        {
            var barX = gbmX.Next();
            var barY = gbmY.Next();
            s.Update(barX.Close, barY.Close, isNew: true);

            if (double.IsFinite(s.Last.Value))
            {
                Assert.InRange(s.Last.Value, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void MonotonicTransform_PreservesCorrelation()
    {
        // Spearman measures monotonic association — applying a strictly increasing
        // transform to either series should not change ρ
        var s1 = new Spearman(5);
        var s2 = new Spearman(5);
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [10, 20, 15, 30, 25];

        for (int i = 0; i < 5; i++)
        {
            s1.Update(x[i], y[i], isNew: true);
            // Apply f(x) = x³ (strictly increasing)
            s2.Update(x[i] * x[i] * x[i], y[i], isNew: true);
        }

        Assert.Equal(s1.Last.Value, s2.Last.Value, 1e-10);
    }

    [Fact]
    public void EventChaining_Fires()
    {
        var s = new Spearman(3);
        int eventCount = 0;
        s.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 1; i <= 5; i++)
        {
            s.Update((double)i, (double)i, isNew: true);
        }

        Assert.Equal(5, eventCount);
    }

    [Fact]
    public void BatchSpan_NaN_HandledSafely()
    {
        double[] x = [1, 2, double.NaN, 4, 5];
        double[] y = [5, 4, 3, 2, 1];
        double[] output = new double[5];

        Spearman.Batch(x.AsSpan(), y.AsSpan(), output.AsSpan(), 3);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(output[i]) || double.IsNaN(output[i]));
        }
    }

    [Fact]
    public void LargePeriod_NoStackOverflow()
    {
        // Test with period > StackallocThreshold (256)
        var s = new Spearman(300);
        for (int i = 1; i <= 300; i++)
        {
            s.Update((double)i, (double)i, isNew: true);
        }
        Assert.Equal(1.0, s.Last.Value, 1e-10);
    }
}
