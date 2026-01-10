using QuanTAlib.Tests;
using MathNet.Numerics.Statistics;

namespace QuanTAlib.Validation;

public sealed class SkewValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void Skew_Matches_MathNet()
    {
        int period = 20;
        var skew = new Skew(period, isPopulation: false);
        var popSkew = new Skew(period, isPopulation: true);

        var quotes = _data.SkenderQuotes.ToList();
        double[] input = quotes.Select(q => (double)q.Close).ToArray();

        for (int i = 0; i < input.Length; i++)
        {
            var val = skew.Update(new TValue(quotes[i].Date, input[i]));
            var popVal = popSkew.Update(new TValue(quotes[i].Date, input[i]));

            // Validate last 100 bars
            if (i >= input.Length - 100)
            {
                var window = input[(i - period + 1)..(i + 1)];
                double expected = window.Skewness();
                double expectedPop = window.PopulationSkewness();

                Assert.Equal(expected, val.Value, 1e-6);
                Assert.Equal(expectedPop, popVal.Value, 1e-6);
            }
        }
    }
}
