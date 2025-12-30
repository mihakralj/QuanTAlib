using Xunit;

namespace QuanTAlib.Tests;

public class PseudoHuberTests
{
    private const double Epsilon = 1e-10;
    private const int DefaultPeriod = 14;

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new PseudoHuber(0));
        Assert.Throws<ArgumentException>(() => new PseudoHuber(-1));
        Assert.Throws<ArgumentException>(() => new PseudoHuber(10, 0));
        Assert.Throws<ArgumentException>(() => new PseudoHuber(10, -1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var pseudoHuber = new PseudoHuber(10);
        Assert.NotNull(pseudoHuber);
        Assert.Equal(10, pseudoHuber.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ValidDelta_Succeeds()
    {
        var pseudoHuber = new PseudoHuber(10, 0.5);
        Assert.NotNull(pseudoHuber);
        Assert.Equal(0.5, pseudoHuber.Delta);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_Accessible()
    {
        var pseudoHuber = new PseudoHuber(DefaultPeriod, 1.5);

        Assert.Equal(0, pseudoHuber.Last.Value);
        Assert.False(pseudoHuber.IsHot);
        Assert.Contains("PseudoHuber", pseudoHuber.Name, StringComparison.Ordinal);
        Assert.Equal(1.5, pseudoHuber.Delta);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var pseudoHuber = new PseudoHuber(5);

        Assert.False(pseudoHuber.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            pseudoHuber.Update(100 + i, 100.0);
            Assert.False(pseudoHuber.IsHot);
        }

        pseudoHuber.Update(105, 100.0);
        Assert.True(pseudoHuber.IsHot);
    }

    #endregion

    #region Calculation Tests

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var pseudoHuber = new PseudoHuber(5);

        for (int i = 0; i < 10; i++)
        {
            double value = 100 + i;
            pseudoHuber.Update(value, value);
        }

        Assert.Equal(0.0, pseudoHuber.Last.Value, Epsilon);
    }

    [Fact]
    public void Calculate_SmallErrors_ApproximatesL2()
    {
        // For small errors, Pseudo-Huber ≈ 0.5 * error²
        var pseudoHuber = new PseudoHuber(1, delta: 10.0);
        double error = 0.1; // Small relative to delta
        pseudoHuber.Update(100.0 + error, 100.0);

        // Pseudo-Huber = δ² * (√(1 + (x/δ)²) - 1)
        // For small x/δ: √(1 + ε) ≈ 1 + ε/2, so loss ≈ δ² * (x/δ)²/2 = x²/2
        double expectedApprox = error * error / 2.0;
        double ratio = pseudoHuber.Last.Value / expectedApprox;

        // Should be close to 1.0 for small errors
        Assert.InRange(ratio, 0.99, 1.01);
    }

    [Fact]
    public void Calculate_LargeErrors_ApproximatesL1()
    {
        // For large errors, Pseudo-Huber ≈ δ * |error| - δ²/2
        var pseudoHuber = new PseudoHuber(1, delta: 1.0);
        double error = 100.0; // Large relative to delta
        pseudoHuber.Update(100.0 + error, 100.0);

        // For large x: √(1 + (x/δ)²) ≈ |x/δ|
        // So loss ≈ δ² * (|x/δ| - 1) = δ|x| - δ²
        double expectedApprox = Math.Abs(error) - 1.0;
        double ratio = pseudoHuber.Last.Value / expectedApprox;

        // Should be close to 1.0 for large errors
        Assert.InRange(ratio, 0.99, 1.01);
    }

    [Fact]
    public void Calculate_SmoothTransition()
    {
        // Pseudo-Huber should be smooth across all error magnitudes
        var pseudoHuber = new PseudoHuber(1, delta: 1.0);
        double[] errors = { 0.01, 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 };
        double[] losses = new double[errors.Length];

        for (int i = 0; i < errors.Length; i++)
        {
            pseudoHuber.Reset();
            pseudoHuber.Update(100.0 + errors[i], 100.0);
            losses[i] = pseudoHuber.Last.Value;
        }

        // Losses should be monotonically increasing
        for (int i = 1; i < losses.Length; i++)
        {
            Assert.True(losses[i] > losses[i - 1],
                $"Loss should increase: {losses[i - 1]} -> {losses[i]}");
        }
    }

    [Fact]
    public void Calculate_Symmetry()
    {
        // Pseudo-Huber should be symmetric: loss(e) = loss(-e)
        var pseudoHuber1 = new PseudoHuber(1);
        var pseudoHuber2 = new PseudoHuber(1);

        double error = 5.0;
        pseudoHuber1.Update(100.0 + error, 100.0); // Positive error
        pseudoHuber2.Update(100.0 - error, 100.0); // Negative error

        Assert.Equal(pseudoHuber1.Last.Value, pseudoHuber2.Last.Value, Epsilon);
    }

    [Fact]
    public void Calculate_DeltaEffectOnTransition()
    {
        // Larger delta means smoother transition, smaller delta means sharper
        var smallDelta = new PseudoHuber(1, delta: 0.5);
        var largeDelta = new PseudoHuber(1, delta: 2.0);

        double error = 1.0; // Fixed error
        smallDelta.Update(100.0 + error, 100.0);
        largeDelta.Update(100.0 + error, 100.0);

        // With large delta, the loss is more quadratic (smaller)
        // With small delta, the loss is more linear (larger relative to quadratic)
        // The raw loss values depend on the formula
        Assert.True(double.IsFinite(smallDelta.Last.Value));
        Assert.True(double.IsFinite(largeDelta.Last.Value));
    }

    [Fact]
    public void Calculate_ComparedToHuber()
    {
        // Pseudo-Huber should produce similar (but not identical) results to Huber
        var huber = new Huber(1, delta: 1.0);
        var pseudoHuber = new PseudoHuber(1, delta: 1.0);

        // Test at various error magnitudes
        double[] errors = { 0.5, 1.0, 2.0 };

        foreach (var error in errors)
        {
            huber.Reset();
            pseudoHuber.Reset();

            huber.Update(100.0 + error, 100.0);
            pseudoHuber.Update(100.0 + error, 100.0);

            // They should be in the same ballpark
            double ratio = pseudoHuber.Last.Value / huber.Last.Value;
            Assert.InRange(ratio, 0.5, 2.0); // Within factor of 2
        }
    }

    [Fact]
    public void Calculate_AlwaysNonNegative()
    {
        var pseudoHuber = new PseudoHuber(DefaultPeriod);
        var gbm = new GBM();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next();
            pseudoHuber.Update(bar.Close, bar.Close + (i % 2 == 0 ? 1 : -1) * (i + 1));
            Assert.True(pseudoHuber.Last.Value >= 0, "Pseudo-Huber loss should always be non-negative");
        }
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var pseudoHuber = new PseudoHuber(5);

        pseudoHuber.Update(100.0, 99.0);
        pseudoHuber.Update(101.0, 99.0, isNew: true);
        double beforeUpdate = pseudoHuber.Last.Value;

        pseudoHuber.Update(105.0, 99.0, isNew: false);
        double afterUpdate = pseudoHuber.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var pseudoHuber = new PseudoHuber(5);
        var gbm = new GBM();

        // Feed 10 new values
        double tenthActual = 0, tenthPredicted = 0;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = bar.Close;
            tenthPredicted = bar.Close * 0.99;
            pseudoHuber.Update(tenthActual, tenthPredicted, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = pseudoHuber.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            pseudoHuber.Update(bar.Close, bar.Close * 1.01, isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        var finalResult = pseudoHuber.Update(tenthActual, tenthPredicted, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, Epsilon);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pseudoHuber = new PseudoHuber(DefaultPeriod);

        pseudoHuber.Update(100.0, 99.0);
        pseudoHuber.Update(101.0, 99.0);
        double valueBefore = pseudoHuber.Last.Value;

        pseudoHuber.Reset();

        Assert.Equal(0, pseudoHuber.Last.Value);
        Assert.False(pseudoHuber.IsHot);

        pseudoHuber.Update(50.0, 49.0);
        Assert.NotEqual(0, pseudoHuber.Last.Value);
        Assert.NotEqual(valueBefore, pseudoHuber.Last.Value);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var pseudoHuber = new PseudoHuber(5);

        pseudoHuber.Update(100.0, 99.0);
        pseudoHuber.Update(101.0, 99.5);

        var resultAfterNaN = pseudoHuber.Update(double.NaN, 100.0);
        Assert.True(double.IsFinite(resultAfterNaN.Value));

        var resultAfterNaN2 = pseudoHuber.Update(102.0, double.NaN);
        Assert.True(double.IsFinite(resultAfterNaN2.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var pseudoHuber = new PseudoHuber(5);

        pseudoHuber.Update(100.0, 99.0);
        pseudoHuber.Update(101.0, 99.5);

        var resultAfterPosInf = pseudoHuber.Update(double.PositiveInfinity, 100.0);
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = pseudoHuber.Update(102.0, double.NegativeInfinity);
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    #endregion

    #region Batch/Span Tests

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var gbm = new GBM();
        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        const int count = 100;
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1.0 + (i % 2 == 0 ? 0.01 : -0.01)));
        }

        // Calculate iteratively
        var iterative = new PseudoHuber(DefaultPeriod);
        var iterativeResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            iterativeResults.Add(iterative.Update(actualSeries[i].Value, predictedSeries[i].Value).Value);
        }

        // Calculate batch
        var batchResults = PseudoHuber.Calculate(actualSeries, predictedSeries, DefaultPeriod);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, iterativeResults[i], Epsilon);
        }
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] actual = [1, 2, 3, 4, 5];
        double[] predicted = [1.1, 2.1, 3.1, 4.1, 5.1];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() =>
            PseudoHuber.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            PseudoHuber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));

        Assert.Throws<ArgumentException>(() =>
            PseudoHuber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), DefaultPeriod, 0));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM();
        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();
        double[] actualData = new double[100];
        double[] predictedData = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next();
            actualData[i] = bar.Close;
            predictedData[i] = bar.Close * 0.99;
            actualSeries.Add(bar.Time, actualData[i]);
            predictedSeries.Add(bar.Time, predictedData[i]);
        }

        var tseriesResult = PseudoHuber.Calculate(actualSeries, predictedSeries, DefaultPeriod);
        PseudoHuber.Batch(actualData.AsSpan(), predictedData.AsSpan(), output.AsSpan(), DefaultPeriod);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], Epsilon);
        }
    }

    [Fact]
    public void SpanBatch_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 120, 130];
        double[] predicted = [99, 109, 115, double.NaN, 129];
        double[] output = new double[5];

        PseudoHuber.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var pseudoHuber = new PseudoHuber(DefaultPeriod);
        var input = new TValue(DateTime.UtcNow, 100.0);

        Assert.Throws<NotSupportedException>(() => pseudoHuber.Update(input));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var pseudoHuber = new PseudoHuber(DefaultPeriod);
        double[] data = [1, 2, 3, 4, 5];

        Assert.Throws<NotSupportedException>(() => pseudoHuber.Prime(data.AsSpan()));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow, 100);
        actual.Add(DateTime.UtcNow, 101);
        predicted.Add(DateTime.UtcNow, 99);

        Assert.Throws<ArgumentException>(() => PseudoHuber.Calculate(actual, predicted, 5));
    }

    #endregion

    #region Resync Tests

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        var pseudoHuber = new PseudoHuber(10);
        var gbm = new GBM();

        // Feed many values to trigger resync
        for (int i = 0; i < 2500; i++)
        {
            var bar = gbm.Next();
            pseudoHuber.Update(bar.Close, bar.Close * 0.99);
        }

        // Should still produce valid results after many iterations
        Assert.True(double.IsFinite(pseudoHuber.Last.Value));
        Assert.True(pseudoHuber.Last.Value >= 0);
    }

    #endregion
}
