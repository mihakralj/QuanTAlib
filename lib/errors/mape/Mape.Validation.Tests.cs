using QuanTAlib.Tests;

namespace QuanTAlib.Validation;

public sealed class MapeValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void Mape_Matches_MathNetStyle_Computation()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        var quotes = _data.SkenderQuotes.ToList();
        double[] actual = quotes.Select(q => (double)q.Close).ToArray();
        double[] predicted = quotes.Select(q => (double)q.Open).ToArray();

        foreach (int period in periods)
        {
            var mape = new Mape(period);

            for (int i = 0; i < actual.Length; i++)
            {
                var val = mape.Update(
                    new TValue(quotes[i].Date, actual[i]),
                    new TValue(quotes[i].Date, predicted[i]));

                // Validate last 100 bars
                if (i >= actual.Length - 100 && i >= period - 1)
                {
                    var windowActual = actual[(i - period + 1)..(i + 1)];
                    var windowPredicted = predicted[(i - period + 1)..(i + 1)];

                    double expected = ComputeMape(windowActual, windowPredicted);

                    Assert.Equal(expected, val.Value, 1e-9);
                }
            }
        }
    }

    [Fact]
    public void Mape_Batch_Matches_MathNetStyle_Computation()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        var quotes = _data.SkenderQuotes.ToList();
        double[] actual = quotes.Select(q => (double)q.Close).ToArray();
        double[] predicted = quotes.Select(q => (double)q.Open).ToArray();

        foreach (int period in periods)
        {
            double[] output = new double[actual.Length];
            Mape.Batch(actual, predicted, output, period);

            // Validate last 100 bars
            for (int i = actual.Length - 100; i < actual.Length; i++)
            {
                if (i >= period - 1)
                {
                    var windowActual = actual[(i - period + 1)..(i + 1)];
                    var windowPredicted = predicted[(i - period + 1)..(i + 1)];

                    double expected = ComputeMape(windowActual, windowPredicted);

                    Assert.Equal(expected, output[i], 1e-9);
                }
            }
        }
    }

    private static double ComputeMape(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted)
    {
        const double epsilon = 1e-10;
        double sum = 0.0;

        for (int i = 0; i < actual.Length; i++)
        {
            double divisor = Math.Abs(actual[i]) < epsilon ? epsilon : actual[i];
            sum += 100.0 * Math.Abs((actual[i] - predicted[i]) / divisor);
        }

        return sum / actual.Length;
    }

    [Fact]
    public void Mape_Correction_Recomputes()
    {
        var ind = new Mape(20);

        // Build state well past warmup (actual always > 0 so MAPE denominator is valid)
        for (int i = 0; i < 50; i++)
        {
            ind.Update(100.0 + i * 0.5, 98.0 + i * 0.5);
        }

        // Anchor bar
        const double anchorActual = 125.0;
        const double anchorPredicted = 123.0;
        ind.Update(anchorActual, anchorPredicted, isNew: true);
        double anchorResult = ind.Last.Value;

        // MAPE is scale-invariant: ×10 on both actual and predicted leaves ratio unchanged.
        // Change only predicted to dramatically alter the error percentage.
        ind.Update(anchorActual, 10.0, isNew: false);
        Assert.NotEqual(anchorResult, ind.Last.Value);

        // Correction back to original — must exactly restore original result
        ind.Update(anchorActual, anchorPredicted, isNew: false);
        Assert.Equal(anchorResult, ind.Last.Value, 1e-9);
    }
}