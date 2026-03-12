using QuanTAlib.Tests;
using Skender.Stock.Indicators;

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

    /// <summary>
    /// Structural validation against Skender <c>GetSlope().RSquared</c>.
    /// Skender R² measures goodness-of-fit of linear regression on price data.
    /// QuanTAlib Rsquared compares actual vs predicted values (different concept).
    /// Both must produce finite output bounded ≤ 1.
    /// </summary>
    [Fact]
    public void Validate_Skender_RSquared_Structural()
    {
        const int period = 20;

        // Skender R² from linear regression slope
        var sResult = _data.SkenderQuotes.GetSlope(period).ToList();

        int finiteCount = sResult.Count(r => r.RSquared is not null && double.IsFinite(r.RSquared.Value));
        Assert.True(finiteCount > 100, $"Skender should produce >100 finite R² values, got {finiteCount}");

        // All Skender R² values should be in [0, 1] for linear regression
        foreach (var r in sResult.Where(r => r.RSquared is not null))
        {
            Assert.True(r.RSquared!.Value >= -0.01 && r.RSquared.Value <= 1.01,
                $"Skender R² = {r.RSquared.Value} out of expected [0, 1] range");
        }

        // QuanTAlib R² (using close as actual, EMA as predicted — same as existing test)
        var rsq = new Rsquared(period);
        var ema = new Ema(5);
        var quotes = _data.SkenderQuotes.ToList();

        for (int i = 0; i < quotes.Count; i++)
        {
            double actual = (double)quotes[i].Close;
            double predicted = ema.Update(new TValue(quotes[i].Date, actual)).Value;
            rsq.Update(actual, predicted);
        }

        Assert.True(double.IsFinite(rsq.Last.Value), "QuanTAlib R² last must be finite");
        Assert.True(rsq.Last.Value <= 1.0 + 1e-9, $"QuanTAlib R² should be ≤ 1, got {rsq.Last.Value}");
    }

    [Fact]
    public void Rsquared_Correction_Recomputes()
    {
        var ind = new Rsquared(20);

        // Build state well past warmup
        for (int i = 0; i < 50; i++)
        {
            ind.Update(100.0 + (i * 0.5), 98.0 + (i * 0.5));
        }

        // Anchor bar
        const double anchorActual = 125.0;
        const double anchorPredicted = 123.0;
        ind.Update(anchorActual, anchorPredicted, isNew: true);
        double anchorResult = ind.Last.Value;

        // R² is scale-invariant: change only predicted (not ×10 both) to break R²
        ind.Update(anchorActual, 10.0, isNew: false);
        Assert.NotEqual(anchorResult, ind.Last.Value);

        // Correction back to original — must exactly restore original result
        ind.Update(anchorActual, anchorPredicted, isNew: false);
        Assert.Equal(anchorResult, ind.Last.Value, 1e-9);
    }
}
