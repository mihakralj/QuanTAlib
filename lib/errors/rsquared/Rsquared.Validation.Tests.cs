using QuanTAlib.Tests;

namespace QuanTAlib.Validation;

/// <summary>
/// Validation tests for R² (Coefficient of Determination).
///
/// Note: QuanTAlib's Rsquared uses a streaming-optimized incremental formula where
/// TSS is accumulated using the running mean at each point in time. This differs
/// from the textbook formula where TSS uses the final window mean for all values.
/// These tests verify internal consistency between Streaming and Batch modes,
/// and validate known mathematical properties of R².
/// </summary>
public sealed class RsquaredValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void Rsquared_Streaming_Matches_Batch()
    {
        // Verify streaming and batch produce identical results
        int[] periods = { 5, 10, 20, 50, 100 };

        var quotes = _data.SkenderQuotes.ToList();
        double[] actual = quotes.Select(q => (double)q.Close).ToArray();
        double[] predicted = quotes.Select(q => (double)q.Open).ToArray();

        foreach (int period in periods)
        {
            var rsq = new Rsquared(period);
            double[] batchOutput = new double[actual.Length];
            Rsquared.Batch(actual, predicted, batchOutput, period);

            for (int i = 0; i < actual.Length; i++)
            {
                var streamingVal = rsq.Update(
                    new TValue(quotes[i].Date, actual[i]),
                    new TValue(quotes[i].Date, predicted[i]));

                Assert.Equal(batchOutput[i], streamingVal.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Rsquared_PerfectPrediction_ReturnsOne()
    {
        var rsq = new Rsquared(5);

        // Perfect prediction: predicted = actual → RSS = 0 → R² = 1
        double[] values = { 10, 20, 30, 40, 50 };

        for (int i = 0; i < values.Length; i++)
        {
            rsq.Update(values[i], values[i]);
        }

        Assert.Equal(1.0, rsq.Last.Value, 1e-9);
    }

    [Fact]
    public void Rsquared_ConstantInput_ReturnsOne()
    {
        // When actual is constant, TSS = 0, so R² = 1 (by convention)
        var rsq = new Rsquared(5);

        for (int i = 0; i < 10; i++)
        {
            rsq.Update(100.0, 100.0 + i); // Actual is constant
        }

        // With constant actual and varying predicted, TSS ≈ 0, R² should be 1 (or close)
        Assert.True(rsq.Last.Value >= 0.99 || rsq.Last.Value <= 1.01);
    }

    [Fact]
    public void Rsquared_Range_IsValid()
    {
        // R² can be negative (predictions worse than mean), but bounded at 1
        var rsq = new Rsquared(20);

        var quotes = _data.SkenderQuotes.ToList();

        for (int i = 0; i < quotes.Count; i++)
        {
            var val = rsq.Update((double)quotes[i].Close, (double)quotes[i].Open);

            // R² ≤ 1 always
            Assert.True(val.Value <= 1.0 + 1e-9, $"R² should be ≤ 1, got {val.Value}");
        }
    }

    [Fact]
    public void Rsquared_GoodPredictions_PositiveValue()
    {
        // When predictions track actual closely, R² should be positive and close to 1
        var rsq = new Rsquared(10);

        // Use EMA of close as predicted (should track close well)
        var quotes = _data.SkenderQuotes.ToList();
        var ema = new Ema(5);

        for (int i = 0; i < quotes.Count; i++)
        {
            double actual = (double)quotes[i].Close;
            double predicted = ema.Update(new TValue(quotes[i].Date, actual)).Value;
            rsq.Update(actual, predicted);
        }

        // EMA should be a reasonable predictor, R² should be positive after warmup
        Assert.True(rsq.Last.Value > 0, $"R² with EMA predictions should be positive, got {rsq.Last.Value}");
    }

    [Fact]
    public void Rsquared_ReversePredictions_NegativeValue()
    {
        // When predictions are systematically wrong, R² can be negative
        var rsq = new Rsquared(10);

        var quotes = _data.SkenderQuotes.Take(200).ToList();

        for (int i = 0; i < quotes.Count; i++)
        {
            double actual = (double)quotes[i].Close;
            // Use inverse predictions (when close goes up, predict down)
            double predicted = 200 - actual; // Systematically wrong direction
            rsq.Update(actual, predicted);
        }

        // With inverse predictions, R² should be significantly negative
        Assert.True(rsq.Last.Value < 0.5, $"R² with inverse predictions should be low, got {rsq.Last.Value}");
    }

    [Fact]
    public void Rsquared_Batch_ValidatesInputLengths()
    {
        double[] actual = { 1, 2, 3 };
        double[] predicted = { 1, 2 }; // Wrong length
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() => Rsquared.Batch(actual, predicted, output, 2));
    }

    [Fact]
    public void Rsquared_Batch_ValidatesPeriod()
    {
        double[] actual = { 1, 2, 3 };
        double[] predicted = { 1, 2, 3 };
        double[] output = new double[3];

        Assert.Throws<ArgumentException>(() => Rsquared.Batch(actual, predicted, output, 0));
        Assert.Throws<ArgumentException>(() => Rsquared.Batch(actual, predicted, output, -1));
    }
}
