using QuanTAlib.Tests;
using MathNet.Numerics.Statistics;

namespace QuanTAlib.Validation;

public sealed class MedianValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();

    public void Dispose()
    {
        _data.Dispose();
    }

    // Note: Standard TA libraries (Skender, TA-Lib, Tulip, Ooples) do not provide a
    // "Rolling Median" indicator. They typically provide "Median Price" which is (High+Low)/2.
    // Therefore, we validate against a robust LINQ-based reference implementation and MathNet.

    [Fact]
    public void Median_Matches_LinqImplementation()
    {
        // Arrange
        const int period = 10;
        var quotes = _data.SkenderQuotes.ToList();
        double[] data = quotes.Select(q => (double)q.Close).ToArray();
        int count = data.Length;

        // Act
        var tSeries = new TSeries();
        for (int i = 0; i < count; i++)
        {
            tSeries.Add(new TValue(quotes[i].Date, data[i]));
        }
        var medianSeries = Median.Batch(tSeries, period);

        // Assert
        for (int i = 0; i < count; i++)
        {
            double expected;
            if (i < period - 1)
            {
                // For the first period-1 values, our implementation accumulates.
                var window = data.Take(i + 1).OrderBy(x => x).ToList();
                expected = CalculateMedian(window);
            }
            else
            {
                // Full window
                var window = data.Skip(i - period + 1).Take(period).OrderBy(x => x).ToList();
                expected = CalculateMedian(window);
            }

            // Validate last 100 bars
            if (i >= count - 100)
            {
                Assert.Equal(expected, medianSeries.Values[i], ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Median_Matches_MathNet()
    {
        // Arrange
        int period = 10;
        var quotes = _data.SkenderQuotes.ToList();
        double[] data = quotes.Select(q => (double)q.Close).ToArray();
        int count = data.Length;

        var median = new Median(period);

        // Act & Assert
        for (int i = 0; i < count; i++)
        {
            var tValue = median.Update(new TValue(quotes[i].Date, data[i]));

            if (i >= count - 100)
            {
                var window = data[(i - period + 1)..(i + 1)];
                double expected = window.Median();
                Assert.Equal(expected, tValue.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    private static double CalculateMedian(List<double> sortedWindow)
    {
        int count = sortedWindow.Count;
        if (count == 0) return 0; // Or NaN

        int mid = count / 2;
        if (count % 2 != 0)
        {
            return sortedWindow[mid];
        }

        return (sortedWindow[mid - 1] + sortedWindow[mid]) * 0.5;
    }
}
