using Xunit;

namespace QuanTAlib;

public class MedianTests
{
    [Fact]
    public void Median_OddPeriod_ReturnsMiddleValue()
    {
        // Arrange
        var median = new Median(3);

        // Act
        median.Update(new TValue(DateTime.MinValue, 10));
        median.Update(new TValue(DateTime.MinValue, 30));
        var result = median.Update(new TValue(DateTime.MinValue, 20));

        // Assert
        // Window: [10, 30, 20] -> Sorted: [10, 20, 30] -> Median: 20
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Median_EvenPeriod_ReturnsAverageOfMiddleValues()
    {
        // Arrange
        var median = new Median(4);

        // Act
        median.Update(new TValue(DateTime.MinValue, 10));
        median.Update(new TValue(DateTime.MinValue, 40));
        median.Update(new TValue(DateTime.MinValue, 20));
        var result = median.Update(new TValue(DateTime.MinValue, 30));

        // Assert
        // Window: [10, 40, 20, 30] -> Sorted: [10, 20, 30, 40] -> Median: (20 + 30) / 2 = 25
        Assert.Equal(25, result.Value);
    }

    [Fact]
    public void Median_UpdatesWithIsNewFalse_Correctly()
    {
        // Arrange
        var median = new Median(3);

        // Act
        median.Update(new TValue(DateTime.MinValue, 10));
        median.Update(new TValue(DateTime.MinValue, 20));

        // Update with 30 (isNew=true)
        var r1 = median.Update(new TValue(DateTime.MinValue, 30));
        // Window: [10, 20, 30] -> Median 20
        Assert.Equal(20, r1.Value);

        // Update with 40 (isNew=false) -> Replaces 30 with 40
        var r2 = median.Update(new TValue(DateTime.MinValue, 40), isNew: false);
        // Window: [10, 20, 40] -> Median 20
        Assert.Equal(20, r2.Value);

        // Update with 5 (isNew=false) -> Replaces 40 with 5
        var r3 = median.Update(new TValue(DateTime.MinValue, 5), isNew: false);
        // Window: [10, 20, 5] -> Sorted [5, 10, 20] -> Median 10
        Assert.Equal(10, r3.Value);
    }

    [Fact]
    public void Median_Batch_Matches_Streaming()
    {
        // Arrange
        int period = 5;
        var source = new TSeries();
        var r = new Random(123);
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.MinValue.AddSeconds(i), r.NextDouble() * 100));
        }

        // Act
        var medianBatch = Median.Batch(source, period);
        var medianStream = new Median(period);
        var streamResults = new List<double>();

        foreach (var val in source)
        {
            streamResults.Add(medianStream.Update(val).Value);
        }

        // Assert
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(medianBatch.Values[i], streamResults[i], 1e-9);
        }
    }

    [Fact]
    public void Median_StaticBatch_Matches_ClassBatch()
    {
        // Arrange
        int period = 5;
        double[] data = new double[20];
        for(int i=0; i<data.Length; i++) data[i] = i;

        // Act
        double[] output = new double[data.Length];
        Median.Batch(data, output, period);

        var series = new TSeries();
        for(int i=0; i<data.Length; i++) series.Add(new TValue(DateTime.MinValue, data[i]));
        var batchSeries = Median.Batch(series, period);

        // Assert
        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(batchSeries.Values[i], output[i], 1e-9);
        }
    }
}
