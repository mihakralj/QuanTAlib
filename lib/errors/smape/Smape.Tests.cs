using Xunit;

namespace QuanTAlib.Tests;

public class SmapeTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Smape(0));
        Assert.Throws<ArgumentException>(() => new Smape(-1));
        var smape = new Smape(10);
        Assert.NotNull(smape);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var smape = new Smape(10);
        var result = smape.Update(100.0, 90.0);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, smape.Last.Value);
    }

    [Fact]
    public void ZeroError_ReturnsZero()
    {
        var smape = new Smape(5);
        for (int i = 0; i < 5; i++)
        {
            smape.Update(100.0, 100.0);
        }
        Assert.Equal(0.0, smape.Last.Value, Precision);
    }

    [Fact]
    public void KnownValues_CalculatesCorrectly()
    {
        var smape = new Smape(1);
        // SMAPE = 200 * |actual - predicted| / (|actual| + |predicted|)
        // actual=100, predicted=80 -> 200 * |20| / (100 + 80) = 4000 / 180 = 22.222...%
        var result = smape.Update(100.0, 80.0);
        Assert.Equal(200.0 * 20.0 / 180.0, result.Value, Precision);
    }

    [Fact]
    public void Symmetric_SamePenaltyForOverUnder()
    {
        // SMAPE should give same value for over and under prediction
        var smape1 = new Smape(1);
        var smape2 = new Smape(1);

        // Under-prediction: actual=100, predicted=80
        var result1 = smape1.Update(100.0, 80.0);

        // Over-prediction: actual=80, predicted=100
        var result2 = smape2.Update(80.0, 100.0);

        // Both should give same SMAPE
        Assert.Equal(result1.Value, result2.Value, Precision);
    }

    [Fact]
    public void BoundedBetween0And200()
    {
        var smape = new Smape(1);

        // Perfect prediction -> 0%
        var perfect = smape.Update(100.0, 100.0);
        Assert.Equal(0.0, perfect.Value, Precision);

        // Maximum error: one is 0, other is non-zero -> 200%
        var maxError = smape.Update(100.0, 0.0);
        Assert.Equal(200.0, maxError.Value, Precision);

        // Another max error case
        var maxError2 = smape.Update(0.0, 100.0);
        Assert.Equal(200.0, maxError2.Value, Precision);
    }

    [Fact]
    public void Period1_ReturnsCurrentError()
    {
        var smape = new Smape(1);
        // actual=100, predicted=50 -> 200 * 50 / 150 = 66.67%
        var r1 = smape.Update(100.0, 50.0);
        Assert.Equal(200.0 * 50.0 / 150.0, r1.Value, Precision);

        // actual=100, predicted=100 -> 0%
        var r2 = smape.Update(100.0, 100.0);
        Assert.Equal(0.0, r2.Value, Precision);
    }

    [Fact]
    public void BothZero_ReturnsZero()
    {
        var smape = new Smape(1);
        // Both zero should be treated as perfect prediction
        var result = smape.Update(0.0, 0.0);
        Assert.Equal(0.0, result.Value, Precision);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var smape = new Smape(5);
        smape.Update(100.0, 90.0);
        smape.Update(100.0, 95.0);

        var resultAfterNaN = smape.Update(double.NaN, 90.0);
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var smape = new Smape(5);
        smape.Update(100.0, 90.0);

        var resultAfterPosInf = smape.Update(double.PositiveInfinity, 90.0);
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = smape.Update(100.0, double.NegativeInfinity);
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var smape = new Smape(5);
        Assert.False(smape.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            smape.Update(100.0, 90.0 + i);
            Assert.False(smape.IsHot);
        }

        smape.Update(100.0, 95.0);
        Assert.True(smape.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var smape = new Smape(10);
        smape.Update(100.0, 90.0);
        smape.Update(100.0, 95.0);

        smape.Reset();

        Assert.Equal(0, smape.Last.Value);
        Assert.False(smape.IsHot);
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var smape = new Smape(5);
        smape.Update(100.0, 90.0);
        double valueBefore = smape.Last.Value;

        smape.Update(100.0, 95.0, isNew: false);
        double valueAfter = smape.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var smape = new Smape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            smape.Update(bar.Close, bar.Close * 0.95, isNew: true);
        }

        double stateAfterTen = smape.Last.Value;

        var lastBar = gbm.Next(isNew: false);
        double lastActual = lastBar.Close;
        double lastPredicted = lastBar.Close * 0.95;

        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            smape.Update(bar.Close, bar.Close * 0.9, isNew: false);
        }

        smape.Update(lastActual, lastPredicted, isNew: false);

        Assert.Equal(stateAfterTen, smape.Last.Value, 1e-6);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var smapeIterative = new Smape(10);
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
            iterativeResults.Add(smapeIterative.Update(actualSeries[i], predictedSeries[i]).Value);
        }

        var batchResults = Smape.Calculate(actualSeries, predictedSeries, 10);

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
            Smape.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        Assert.Throws<ArgumentException>(() =>
            Smape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Smape.Calculate(actualSeries, predictedSeries, 10);
        Smape.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), 10);

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

        Smape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

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

        Assert.Throws<ArgumentException>(() => Smape.Calculate(actual, predicted, 5));
    }

    [Fact]
    public void Name_IsSetCorrectly()
    {
        var smape = new Smape(14);
        Assert.Equal("Smape(14)", smape.Name);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var smape = new Smape(20);
        Assert.Equal(20, smape.WarmupPeriod);
    }

    [Fact]
    public void CompareWithMape_DifferentForAsymmetricCases()
    {
        // For same absolute difference, MAPE depends on actual value
        // SMAPE treats both directions symmetrically
        var mape1 = new Mape(1);
        var mape2 = new Mape(1);
        var smape1 = new Smape(1);
        var smape2 = new Smape(1);

        // Case 1: actual > predicted (100 vs 80)
        var mapeResult1 = mape1.Update(100.0, 80.0);
        var smapeResult1 = smape1.Update(100.0, 80.0);

        // Case 2: actual < predicted (80 vs 100)
        var mapeResult2 = mape2.Update(80.0, 100.0);
        var smapeResult2 = smape2.Update(80.0, 100.0);

        // MAPE differs (20% vs 25%)
        // actual=100, pred=80: MAPE = 100*20/100 = 20%
        // actual=80, pred=100: MAPE = 100*20/80 = 25%
        Assert.Equal(20.0, mapeResult1.Value, Precision);
        Assert.Equal(25.0, mapeResult2.Value, Precision);
        Assert.NotEqual(mapeResult1.Value, mapeResult2.Value);

        // SMAPE is symmetric
        Assert.Equal(smapeResult1.Value, smapeResult2.Value, Precision);
    }

    [Fact]
    public void SlidingWindow_Works()
    {
        var smape = new Smape(3);

        // Use simpler values for easier verification
        // actual=100, predicted=100 -> SMAPE = 0%
        smape.Update(100.0, 100.0);
        Assert.Equal(0.0, smape.Last.Value, Precision);

        // actual=100, predicted=0 -> SMAPE = 200%
        smape.Update(100.0, 0.0);
        // Average: (0 + 200) / 2 = 100%
        Assert.Equal(100.0, smape.Last.Value, Precision);

        // actual=100, predicted=100 -> SMAPE = 0%
        smape.Update(100.0, 100.0);
        // Average: (0 + 200 + 0) / 3 = 66.67%
        Assert.Equal(200.0 / 3.0, smape.Last.Value, Precision);

        // Add another perfect prediction
        smape.Update(100.0, 100.0);
        // Window now: [200, 0, 0]
        // Average: (200 + 0 + 0) / 3 = 66.67%
        Assert.Equal(200.0 / 3.0, smape.Last.Value, Precision);
    }

    [Fact]
    public void NegativeValues_HandledCorrectly()
    {
        var smape = new Smape(1);
        // actual=-100, predicted=-80 -> |diff|=20, sum_abs=180
        // SMAPE = 200 * 20 / 180 = 22.22%
        var result = smape.Update(-100.0, -80.0);
        Assert.Equal(200.0 * 20.0 / 180.0, result.Value, Precision);
    }
}
