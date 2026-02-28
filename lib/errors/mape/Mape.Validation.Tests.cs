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
}