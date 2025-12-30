using Xunit;

namespace QuanTAlib.Tests;

public class RmsleTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Rmsle(0));
        Assert.Throws<ArgumentException>(() => new Rmsle(-1));
        var rmsle = new Rmsle(10);
        Assert.NotNull(rmsle);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var rmsle = new Rmsle(10);
        var result = rmsle.Update(100.0, 90.0);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, rmsle.Last.Value);
    }

    [Fact]
    public void ZeroError_ReturnsZero()
    {
        var rmsle = new Rmsle(5);
        for (int i = 0; i < 5; i++)
        {
            rmsle.Update(100.0, 100.0);
        }
        Assert.Equal(0.0, rmsle.Last.Value, Precision);
    }

    [Fact]
    public void KnownValues_CalculatesCorrectly()
    {
        var rmsle = new Rmsle(1);
        // RMSLE = sqrt((log(1 + actual) - log(1 + predicted))²)
        // actual=99, predicted=49 -> log(100) - log(50) = ln(2)
        // RMSLE = |ln(2)| ≈ 0.693
        var result = rmsle.Update(99.0, 49.0);
        double expected = Math.Abs(Math.Log(100.0) - Math.Log(50.0));
        Assert.Equal(expected, result.Value, Precision);
    }

    [Fact]
    public void IsSqrtOfMsle()
    {
        var rmsle = new Rmsle(5);
        var msle = new Msle(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            rmsle.Update(bar.Close, bar.Close * 0.95);
            msle.Update(bar.Close, bar.Close * 0.95);
        }

        Assert.Equal(Math.Sqrt(msle.Last.Value), rmsle.Last.Value, Precision);
    }

    [Fact]
    public void Period1_ReturnsCurrentError()
    {
        var rmsle = new Rmsle(1);
        // actual=9, predicted=4 -> log(10) - log(5) = ln(2)
        var r1 = rmsle.Update(9.0, 4.0);
        double expected1 = Math.Abs(Math.Log(10.0) - Math.Log(5.0));
        Assert.Equal(expected1, r1.Value, Precision);

        // Perfect prediction
        var r2 = rmsle.Update(100.0, 100.0);
        Assert.Equal(0.0, r2.Value, Precision);
    }

    [Fact]
    public void ZeroValues_HandledCorrectly()
    {
        var rmsle = new Rmsle(1);
        // actual=0, predicted=0 -> log(1) - log(1) = 0
        var bothZero = rmsle.Update(0.0, 0.0);
        Assert.Equal(0.0, bothZero.Value, Precision);

        // actual=0, predicted=9 -> |log(1) - log(10)| = ln(10)
        var actualZero = rmsle.Update(0.0, 9.0);
        double expectedActualZero = Math.Abs(Math.Log(1.0) - Math.Log(10.0));
        Assert.Equal(expectedActualZero, actualZero.Value, Precision);

        // actual=9, predicted=0 -> |log(10) - log(1)| = ln(10)
        var predZero = rmsle.Update(9.0, 0.0);
        double expectedPredZero = Math.Abs(Math.Log(10.0) - Math.Log(1.0));
        Assert.Equal(expectedPredZero, predZero.Value, Precision);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var rmsle = new Rmsle(5);
        rmsle.Update(100.0, 90.0);
        rmsle.Update(100.0, 95.0);

        var resultAfterNaN = rmsle.Update(double.NaN, 90.0);
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var rmsle = new Rmsle(5);
        rmsle.Update(100.0, 90.0);

        var resultAfterPosInf = rmsle.Update(double.PositiveInfinity, 90.0);
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = rmsle.Update(100.0, double.NegativeInfinity);
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void NegativeValues_TreatedAsInvalid()
    {
        var rmsle = new Rmsle(5);
        rmsle.Update(100.0, 90.0);

        var resultAfterNeg = rmsle.Update(-50.0, 90.0);
        Assert.True(double.IsFinite(resultAfterNeg.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var rmsle = new Rmsle(5);
        Assert.False(rmsle.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            rmsle.Update(100.0, 90.0 + i);
            Assert.False(rmsle.IsHot);
        }

        rmsle.Update(100.0, 95.0);
        Assert.True(rmsle.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rmsle = new Rmsle(10);
        rmsle.Update(100.0, 90.0);
        rmsle.Update(100.0, 95.0);

        rmsle.Reset();

        Assert.Equal(0, rmsle.Last.Value);
        Assert.False(rmsle.IsHot);
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var rmsle = new Rmsle(5);
        rmsle.Update(100.0, 90.0);
        double valueBefore = rmsle.Last.Value;

        rmsle.Update(100.0, 95.0, isNew: false);
        double valueAfter = rmsle.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var rmsle = new Rmsle(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            rmsle.Update(bar.Close, bar.Close * 0.95, isNew: true);
        }

        double stateAfterTen = rmsle.Last.Value;

        var lastBar = gbm.Next(isNew: false);
        double lastActual = lastBar.Close;
        double lastPredicted = lastBar.Close * 0.95;

        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            rmsle.Update(bar.Close, bar.Close * 0.9, isNew: false);
        }

        rmsle.Update(lastActual, lastPredicted, isNew: false);

        Assert.Equal(stateAfterTen, rmsle.Last.Value, 1e-6);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var rmsleIterative = new Rmsle(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * 0.95);
        }

        var iterativeResults = new List<double>();
        for (int i = 0; i < actualSeries.Count; i++)
        {
            iterativeResults.Add(rmsleIterative.Update(actualSeries[i], predictedSeries[i]).Value);
        }

        var batchResults = Rmsle.Calculate(actualSeries, predictedSeries, 10);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i].Value, Precision);
        }
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] actual = [100, 100, 100];
        double[] predicted = [90, 95, 100];
        double[] output = new double[3];
        double[] wrongSizeOutput = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Rmsle.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        Assert.Throws<ArgumentException>(() =>
            Rmsle.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();
        double[] actualArr = new double[100];
        double[] predictedArr = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualArr[i] = bar.Close;
            predictedArr[i] = bar.Close * 0.95;
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * 0.95);
        }

        var tseriesResult = Rmsle.Calculate(actualSeries, predictedSeries, 10);
        Rmsle.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], Precision);
        }
    }

    [Fact]
    public void SpanBatch_HandlesNaN()
    {
        double[] actual = [100, 100, double.NaN, 100, 100];
        double[] predicted = [90, 95, 92, double.NaN, 95];
        double[] output = new double[5];

        Rmsle.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Calculate_MismatchedLengths_ThrowsException()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 100);
        predicted.Add(DateTime.UtcNow.Ticks, 90);

        Assert.Throws<ArgumentException>(() => Rmsle.Calculate(actual, predicted, 5));
    }

    [Fact]
    public void Name_IsSetCorrectly()
    {
        var rmsle = new Rmsle(14);
        Assert.Equal("Rmsle(14)", rmsle.Name);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var rmsle = new Rmsle(20);
        Assert.Equal(20, rmsle.WarmupPeriod);
    }

    [Fact]
    public void CompareWithRmse_DifferentScaling()
    {
        var rmsle = new Rmsle(1);
        var rmse = new Rmse(1);

        // Large values: actual=1000000, predicted=500000
        var rmsleResult = rmsle.Update(1000000.0, 500000.0);
        var rmseResult = rmse.Update(1000000.0, 500000.0);

        // RMSE = 500000
        // RMSLE = |log(1000001) - log(500001)| ≈ 0.69
        Assert.True(rmsleResult.Value < 1.0);
        Assert.True(rmseResult.Value > 100000);
    }

    [Fact]
    public void SlidingWindow_Works()
    {
        var rmsle = new Rmsle(3);

        // actual=0, predicted=0 -> RMSLE = 0
        rmsle.Update(0.0, 0.0);
        Assert.Equal(0.0, rmsle.Last.Value, Precision);

        // actual=e-1≈1.718, predicted=0 -> |log(e) - log(1)| = 1
        rmsle.Update(Math.E - 1, 0.0);
        // MSLE average: (0 + 1) / 2 = 0.5, RMSLE = sqrt(0.5)
        Assert.Equal(Math.Sqrt(0.5), rmsle.Last.Value, Precision);

        // actual=0, predicted=0 -> RMSLE = 0
        rmsle.Update(0.0, 0.0);
        // MSLE average: (0 + 1 + 0) / 3 = 1/3, RMSLE = sqrt(1/3)
        Assert.Equal(Math.Sqrt(1.0 / 3.0), rmsle.Last.Value, Precision);
    }

    [Fact]
    public void AlwaysNonNegative()
    {
        var rmsle = new Rmsle(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = rmsle.Update(bar.Close, bar.Close * (0.8 + 0.4 * (i % 2)));
            Assert.True(result.Value >= 0, $"RMSLE should always be non-negative, got {result.Value}");
        }
    }
}
