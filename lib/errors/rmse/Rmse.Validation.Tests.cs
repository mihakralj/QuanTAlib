using MathNet.Numerics;
using QuanTAlib.Tests;

namespace QuanTAlib.Validation;

public sealed class RmseValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void Rmse_Matches_MathNet()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        var quotes = _data.SkenderQuotes.ToList();
        double[] actual = quotes.Select(q => (double)q.Close).ToArray();
        double[] predicted = quotes.Select(q => (double)q.Open).ToArray();

        foreach (int period in periods)
        {
            var rmse = new Rmse(period);

            for (int i = 0; i < actual.Length; i++)
            {
                var val = rmse.Update(
                    new TValue(quotes[i].Date, actual[i]),
                    new TValue(quotes[i].Date, predicted[i]));

                // Validate last 100 bars
                if (i >= actual.Length - 100 && i >= period - 1)
                {
                    var windowActual = actual[(i - period + 1)..(i + 1)];
                    var windowPredicted = predicted[(i - period + 1)..(i + 1)];

                    // RMSE = sqrt(MSE)
                    double expected = Math.Sqrt(Distance.MSE(windowActual, windowPredicted));

                    Assert.Equal(expected, val.Value, 1e-9);
                }
            }
        }
    }

    [Fact]
    public void Rmse_Batch_Matches_MathNet()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        var quotes = _data.SkenderQuotes.ToList();
        double[] actual = quotes.Select(q => (double)q.Close).ToArray();
        double[] predicted = quotes.Select(q => (double)q.Open).ToArray();

        foreach (int period in periods)
        {
            double[] output = new double[actual.Length];
            Rmse.Batch(actual, predicted, output, period);

            // Validate last 100 bars
            for (int i = actual.Length - 100; i < actual.Length; i++)
            {
                if (i >= period - 1)
                {
                    var windowActual = actual[(i - period + 1)..(i + 1)];
                    var windowPredicted = predicted[(i - period + 1)..(i + 1)];

                    // RMSE = sqrt(MSE)
                    double expected = Math.Sqrt(Distance.MSE(windowActual, windowPredicted));

                    Assert.Equal(expected, output[i], 1e-9);
                }
            }
        }
    }
}
