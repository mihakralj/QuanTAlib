using Xunit;

namespace QuanTAlib.Tests;

public class SgfTests
{
    private readonly GBM _gbm;

    public SgfTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Sgf(10, polyOrder: 10)); // Order >= size
        Assert.Throws<ArgumentException>(() => new Sgf(10, polyOrder: 15));

        var sgf = new Sgf(10, polyOrder: 2);
        Assert.NotNull(sgf);
        Assert.Equal("Sgf(9,2)", sgf.Name); // Period adjusted to odd
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        const int period = 21;
        int polyOrder = 2;

        // 1. Batch Mode
        var batchResult = new Sgf(period, polyOrder).Update(series);

        // 2. Streaming Mode
        var streaming = new Sgf(period, polyOrder);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // 3. Span Mode
        double[] spanInput = series.Values.ToArray();
        double[] spanOutput = new double[spanInput.Length];
        Sgf.Calculate(spanInput, spanOutput, period, polyOrder);

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            double batchVal = batchResult[i].Value;
            double streamVal = streamingResults[i].Value;
            double spanVal = spanOutput[i];

            if (double.IsNaN(batchVal))
            {
                Assert.True(double.IsNaN(streamVal));
                Assert.True(double.IsNaN(spanVal));
            }
            else
            {
                Assert.Equal(batchVal, streamVal, 1e-9);
                Assert.Equal(batchVal, spanVal, 1e-9);
            }
        }
    }

    [Fact]
    public void HandlesNaN()
    {
        var sgf = new Sgf(5, 2);

        sgf.Update(new TValue(DateTime.UtcNow, 100));
        sgf.Update(new TValue(DateTime.UtcNow, double.NaN));
        sgf.Update(new TValue(DateTime.UtcNow, 102));

        // Should produce valid result if sufficient valid data exists within window
        Assert.True(double.IsFinite(sgf.Last.Value));
    }

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        var sgf = new Sgf(21, 2);
        Assert.Equal(21, sgf.WarmupPeriod);
        Assert.False(sgf.IsHot);

        for (int i = 0; i < 21; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.True(sgf.IsHot);
    }
}