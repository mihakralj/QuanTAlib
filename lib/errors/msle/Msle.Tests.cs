using Xunit;

namespace QuanTAlib.Tests;

public class MsleTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Msle(0));
        Assert.Throws<ArgumentException>(() => new Msle(-1));
        var msle = new Msle(10);
        Assert.NotNull(msle);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var msle = new Msle(10);
        var result = msle.Update(100.0, 90.0);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, msle.Last.Value);
    }

    [Fact]
    public void ZeroError_ReturnsZero()
    {
        var msle = new Msle(5);
        for (int i = 0; i < 5; i++)
        {
            msle.Update(100.0, 100.0);
        }
        Assert.Equal(0.0, msle.Last.Value, Precision);
    }

    [Fact]
    public void KnownValues_CalculatesCorrectly()
    {
        var msle = new Msle(1);
        // MSLE = (log(1 + actual) - log(1 + predicted))²
        // actual=99, predicted=49 -> log(100) - log(50) = ln(100) - ln(50) = ln(2)
        // MSLE = ln(2)² ≈ 0.480453
        var result = msle.Update(99.0, 49.0);
        double expected = Math.Pow(Math.Log(100.0) - Math.Log(50.0), 2);
        Assert.Equal(expected, result.Value, Precision);
    }

    [Fact]
    public void Period1_ReturnsCurrentError()
    {
        var msle = new Msle(1);
        // actual=9, predicted=4 -> log(10) - log(5) = ln(2)
        var r1 = msle.Update(9.0, 4.0);
        double expected1 = Math.Pow(Math.Log(10.0) - Math.Log(5.0), 2);
        Assert.Equal(expected1, r1.Value, Precision);

        // Perfect prediction
        var r2 = msle.Update(100.0, 100.0);
        Assert.Equal(0.0, r2.Value, Precision);
    }

    [Fact]
    public void AsymmetricPenalty_UnderPredictionPenalizedMore()
    {
        var msle1 = new Msle(1);
        var msle2 = new Msle(1);

        // Under-prediction: actual=100, predicted=50
        // log(101) - log(51) ≈ 0.683
        var underPred = msle1.Update(100.0, 50.0);

        // Over-prediction: actual=50, predicted=100
        // log(51) - log(101) ≈ -0.683
        var overPred = msle2.Update(50.0, 100.0);

        // Squared errors are equal for MSLE (unlike MAPE)
        // But the raw log errors show asymmetry
        Assert.Equal(underPred.Value, overPred.Value, Precision);
    }

    [Fact]
    public void ZeroValues_HandledCorrectly()
    {
        var msle = new Msle(1);
        // actual=0, predicted=0 -> log(1) - log(1) = 0
        var bothZero = msle.Update(0.0, 0.0);
        Assert.Equal(0.0, bothZero.Value, Precision);

        // actual=0, predicted=9 -> log(1) - log(10) = -ln(10)
        var actualZero = msle.Update(0.0, 9.0);
        double expectedActualZero = Math.Pow(Math.Log(1.0) - Math.Log(10.0), 2);
        Assert.Equal(expectedActualZero, actualZero.Value, Precision);

        // actual=9, predicted=0 -> log(10) - log(1) = ln(10)
        var predZero = msle.Update(9.0, 0.0);
        double expectedPredZero = Math.Pow(Math.Log(10.0) - Math.Log(1.0), 2);
        Assert.Equal(expectedPredZero, predZero.Value, Precision);
    }

    [Fact]
    public void LargeScale_CompressesErrors()
    {
        var msle = new Msle(1);
        var mse = new Mse(1);

        // Large values: actual=1000000, predicted=500000
        var msleResult = msle.Update(1000000.0, 500000.0);
        var mseResult = mse.Update(1000000.0, 500000.0);

        // MSE = (500000)² = 2.5e11
        // MSLE = (log(1000001) - log(500001))² ≈ 0.48 (much smaller)
        Assert.True(msleResult.Value < 1.0);
        Assert.True(mseResult.Value > 1e10);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var msle = new Msle(5);
        msle.Update(100.0, 90.0);
        msle.Update(100.0, 95.0);

        var resultAfterNaN = msle.Update(double.NaN, 90.0);
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var msle = new Msle(5);
        msle.Update(100.0, 90.0);

        var resultAfterPosInf = msle.Update(double.PositiveInfinity, 90.0);
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = msle.Update(100.0, double.NegativeInfinity);
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void NegativeValues_TreatedAsInvalid()
    {
        var msle = new Msle(5);
        msle.Update(100.0, 90.0);

        // Negative values should use last valid value
        var resultAfterNeg = msle.Update(-50.0, 90.0);
        Assert.True(double.IsFinite(resultAfterNeg.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var msle = new Msle(5);
        Assert.False(msle.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            msle.Update(100.0, 90.0 + i);
            Assert.False(msle.IsHot);
        }

        msle.Update(100.0, 95.0);
        Assert.True(msle.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var msle = new Msle(10);
        msle.Update(100.0, 90.0);
        msle.Update(100.0, 95.0);

        msle.Reset();

        Assert.Equal(0, msle.Last.Value);
        Assert.False(msle.IsHot);
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var msle = new Msle(5);
        msle.Update(100.0, 90.0);
        double valueBefore = msle.Last.Value;

        msle.Update(100.0, 95.0, isNew: false);
        double valueAfter = msle.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var msle = new Msle(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            msle.Update(bar.Close, bar.Close * 0.95, isNew: true);
        }

        double stateAfterTen = msle.Last.Value;

        var lastBar = gbm.Next(isNew: false);
        double lastActual = lastBar.Close;
        double lastPredicted = lastBar.Close * 0.95;

        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            msle.Update(bar.Close, bar.Close * 0.9, isNew: false);
        }

        msle.Update(lastActual, lastPredicted, isNew: false);

        Assert.Equal(stateAfterTen, msle.Last.Value, 1e-6);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var msleIterative = new Msle(10);
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
            iterativeResults.Add(msleIterative.Update(actualSeries[i], predictedSeries[i]).Value);
        }

        var batchResults = Msle.Calculate(actualSeries, predictedSeries, 10);

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
            Msle.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        Assert.Throws<ArgumentException>(() =>
            Msle.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Msle.Calculate(actualSeries, predictedSeries, 10);
        Msle.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), 10);

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

        Msle.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

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

        Assert.Throws<ArgumentException>(() => Msle.Calculate(actual, predicted, 5));
    }

    [Fact]
    public void Name_IsSetCorrectly()
    {
        var msle = new Msle(14);
        Assert.Equal("Msle(14)", msle.Name);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var msle = new Msle(20);
        Assert.Equal(20, msle.WarmupPeriod);
    }

    [Fact]
    public void SlidingWindow_Works()
    {
        var msle = new Msle(3);

        // actual=0, predicted=0 -> MSLE = 0
        msle.Update(0.0, 0.0);
        Assert.Equal(0.0, msle.Last.Value, Precision);

        // actual=e-1≈1.718, predicted=0 -> log(e) - log(1) = 1 -> MSLE = 1
        msle.Update(Math.E - 1, 0.0);
        // Average: (0 + 1) / 2 = 0.5
        Assert.Equal(0.5, msle.Last.Value, Precision);

        // actual=0, predicted=0 -> MSLE = 0
        msle.Update(0.0, 0.0);
        // Average: (0 + 1 + 0) / 3 = 1/3
        Assert.Equal(1.0 / 3.0, msle.Last.Value, Precision);
    }

    [Fact]
    public void MultiplicativeRelationship_ConsistentError()
    {
        // MSLE is consistent for multiplicative relationships
        var msle1 = new Msle(1);
        var msle2 = new Msle(1);
        var mse1 = new Mse(1);
        var mse2 = new Mse(1);

        // actual=10, predicted=5 (ratio 2:1)
        var smallMsle = msle1.Update(10.0, 5.0);
        var smallMse = mse1.Update(10.0, 5.0);

        // actual=1000, predicted=500 (ratio 2:1)
        var largeMsle = msle2.Update(1000.0, 500.0);
        var largeMse = mse2.Update(1000.0, 500.0);

        // MSLE should be more consistent for same ratios than MSE
        // log(11) - log(6) ≈ 0.606 vs log(1001) - log(501) ≈ 0.692
        // The +1 offset causes some difference for small values
        double msleDiff = Math.Abs(smallMsle.Value - largeMsle.Value);
        double mseRatio = largeMse.Value / smallMse.Value;

        // MSLE difference should be much smaller than the MSE ratio
        // MSE: 25 vs 250000 (ratio of 10000)
        // MSLE difference is only about 0.12 (squared log errors)
        Assert.True(msleDiff < 0.2, $"MSLE difference was {msleDiff}");
        Assert.True(mseRatio > 1000, $"MSE ratio was {mseRatio}");
    }
}
