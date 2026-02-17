using QuanTAlib.Tests;
using MathNet.Numerics.Statistics;

namespace QuanTAlib.Validation;

public sealed class KurtosisValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void Kurtosis_Matches_MathNet()
    {
        const int period = 20;
        var kurtosis = new Kurtosis(period, isPopulation: false);
        var popKurtosis = new Kurtosis(period, isPopulation: true);

        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();

        for (int i = 0; i < input.Length; i++)
        {
            var val = kurtosis.Update(new TValue(quotes[i].Date, input[i]));
            var popVal = popKurtosis.Update(new TValue(quotes[i].Date, input[i]));

            // Validate last 100 bars
            if (i >= input.Length - 100)
            {
                var window = input[(i - period + 1)..(i + 1)];
                double expected = window.Kurtosis();
                double expectedPop = window.PopulationKurtosis();

                Assert.Equal(expected, val.Value, 1e-4);
                Assert.Equal(expectedPop, popVal.Value, 1e-4);
            }
        }
    }
}
