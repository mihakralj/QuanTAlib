using Xunit;

namespace QuanTAlib.Tests;

public class MpeTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mpe(0));
        Assert.Throws<ArgumentException>(() => new Mpe(-1));
        var mpe = new Mpe(10);
        Assert.NotNull(mpe);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var mpe = new Mpe(10);
        var result = mpe.Update(100.0, 90.0);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, mpe.Last.Value);
    }

    [Fact]
    public void ZeroError_ReturnsZero()
    {
        var mpe = new Mpe(5);
        for (int i = 0; i < 5; i++)
        {
            mpe.Update(100.0, 100.0);
        }
        Assert.Equal(0.0, mpe.Last.Value, Precision);
    }

    [Fact]
    public void UnderPrediction_ReturnsPositive()
    {
        // MPE: 100 * (actual - predicted) / actual
        // When actual > predicted, result is positive
        var mpe = new Mpe(1);
        var result = mpe.Update(100.0, 80.0);
        // MPE = 100 * (100 - 80) / 100 = 20%
        Assert.Equal(20.0, result.Value, Precision);
    }

    [Fact]
    public void OverPrediction_ReturnsNegative()
    {
        // When actual < predicted, result is negative
        var mpe = new Mpe(1);
        var result = mpe.Update(100.0, 120.0);
        // MPE = 100 * (100 - 120) / 100 = -20%
        Assert.Equal(-20.0, result.Value, Precision);
    }

    [Fact]
    public void Period1_ReturnsCurrentError()
    {
        var mpe = new Mpe(1);
        // actual=100, predicted=90 -> MPE = 100 * (100-90)/100 = 10%
        var r1 = mpe.Update(100.0, 90.0);
        Assert.Equal(10.0, r1.Value, Precision);

        // actual=100, predicted=110 -> MPE = 100 * (100-110)/100 = -10%
        var r2 = mpe.Update(100.0, 110.0);
        Assert.Equal(-10.0, r2.Value, Precision);
    }

    [Fact]
    public void KnownValues_CalculatesCorrectly()
    {
        var mpe = new Mpe(3);
        // actual=100, predicted=90 -> MPE = 10%
        mpe.Update(100.0, 90.0);
        // actual=100, predicted=110 -> MPE = -10%
        mpe.Update(100.0, 110.0);
        // actual=100, predicted=100 -> MPE = 0%
        mpe.Update(100.0, 100.0);

        // Average: (10 + (-10) + 0) / 3 = 0%
        Assert.Equal(0.0, mpe.Last.Value, Precision);
    }

    [Fact]
    public void BiasDetection_PositiveBiasAverage()
    {
        var mpe = new Mpe(3);
        // Consistently under-predicting
        mpe.Update(100.0, 95.0);  // +5%
        mpe.Update(100.0, 90.0);  // +10%
        mpe.Update(100.0, 85.0);  // +15%

        // Average: (5 + 10 + 15) / 3 = 10%
        Assert.Equal(10.0, mpe.Last.Value, Precision);
        Assert.True(mpe.Last.Value > 0); // Positive bias
    }

    [Fact]
    public void BiasDetection_NegativeBiasAverage()
    {
        var mpe = new Mpe(3);
        // Consistently over-predicting
        mpe.Update(100.0, 105.0);  // -5%
        mpe.Update(100.0, 110.0);  // -10%
        mpe.Update(100.0, 115.0);  // -15%

        // Average: (-5 + -10 + -15) / 3 = -10%
        Assert.Equal(-10.0, mpe.Last.Value, Precision);
        Assert.True(mpe.Last.Value < 0); // Negative bias
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mpe = new Mpe(5);
        mpe.Update(100.0, 90.0);
        mpe.Update(100.0, 95.0);

        var resultAfterNaN = mpe.Update(double.NaN, 90.0);
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mpe = new Mpe(5);
        mpe.Update(100.0, 90.0);

        var resultAfterPosInf = mpe.Update(double.PositiveInfinity, 90.0);
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = mpe.Update(100.0, double.NegativeInfinity);
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void ZeroActual_HandledGracefully()
    {
        var mpe = new Mpe(5);
        mpe.Update(100.0, 90.0);
        var result = mpe.Update(0.0, 10.0);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var mpe = new Mpe(5);
        Assert.False(mpe.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            mpe.Update(100.0, 90.0 + i);
            Assert.False(mpe.IsHot);
        }

        mpe.Update(100.0, 95.0);
        Assert.True(mpe.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mpe = new Mpe(10);
        mpe.Update(100.0, 90.0);
        mpe.Update(100.0, 95.0);

        mpe.Reset();

        Assert.Equal(0, mpe.Last.Value);
        Assert.False(mpe.IsHot);
    }

    [Fact]
    public void IsNew_False_UpdatesCurrentBar()
    {
        var mpe = new Mpe(5);
        mpe.Update(100.0, 90.0);
        double valueBefore = mpe.Last.Value;

        mpe.Update(100.0, 95.0, isNew: false);
        double valueAfter = mpe.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mpe = new Mpe(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            mpe.Update(bar.Close, bar.Close * 0.95, isNew: true);
        }

        double stateAfterTen = mpe.Last.Value;

        var lastBar = gbm.Next(isNew: false);
        double lastActual = lastBar.Close;
        double lastPredicted = lastBar.Close * 0.95;

        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            mpe.Update(bar.Close, bar.Close * 0.9, isNew: false);
        }

        mpe.Update(lastActual, lastPredicted, isNew: false);

        Assert.Equal(stateAfterTen, mpe.Last.Value, 1e-6);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var mpeIterative = new Mpe(10);
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
            iterativeResults.Add(mpeIterative.Update(actualSeries[i], predictedSeries[i]).Value);
        }

        var batchResults = Mpe.Calculate(actualSeries, predictedSeries, 10);

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
            Mpe.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), 3));

        Assert.Throws<ArgumentException>(() =>
            Mpe.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Mpe.Calculate(actualSeries, predictedSeries, 10);
        Mpe.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), 10);

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

        Mpe.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

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

        Assert.Throws<ArgumentException>(() => Mpe.Calculate(actual, predicted, 5));
    }

    [Fact]
    public void Name_IsSetCorrectly()
    {
        var mpe = new Mpe(14);
        Assert.Equal("Mpe(14)", mpe.Name);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var mpe = new Mpe(20);
        Assert.Equal(20, mpe.WarmupPeriod);
    }

    [Fact]
    public void DifferenceFromMape_SignPreserved()
    {
        // MPE preserves sign, MAPE takes absolute value
        var mpe = new Mpe(2);
        var mape = new Mape(2);

        // Under-prediction: both should be positive
        mpe.Update(100.0, 90.0);  // +10%
        mape.Update(100.0, 90.0); // +10%

        // Over-prediction: MPE negative, MAPE positive
        mpe.Update(100.0, 110.0);  // -10%
        mape.Update(100.0, 110.0); // +10%

        // MPE average: (10 + (-10)) / 2 = 0
        // MAPE average: (10 + 10) / 2 = 10
        Assert.Equal(0.0, mpe.Last.Value, Precision);
        Assert.Equal(10.0, mape.Last.Value, Precision);
    }

    [Fact]
    public void SlidingWindow_Works()
    {
        var mpe = new Mpe(3);

        mpe.Update(100.0, 90.0);  // +10%
        mpe.Update(100.0, 95.0);  // +5%
        mpe.Update(100.0, 100.0); // 0%
        // Average: (10 + 5 + 0) / 3 = 5%
        Assert.Equal(5.0, mpe.Last.Value, Precision);

        mpe.Update(100.0, 105.0); // -5%
        // Window now: +5%, 0%, -5%
        // Average: (5 + 0 + (-5)) / 3 = 0%
        Assert.Equal(0.0, mpe.Last.Value, Precision);

        mpe.Update(100.0, 110.0); // -10%
        // Window now: 0%, -5%, -10%
        // Average: (0 + (-5) + (-10)) / 3 = -5%
        Assert.Equal(-5.0, mpe.Last.Value, Precision);
    }
}
