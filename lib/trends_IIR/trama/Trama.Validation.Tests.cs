using Xunit;

namespace QuanTAlib.Tests;

public class TramaValidationTests
{
    private const int DefaultPeriod = 14;
    private const long Seed = 54321;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private static TSeries GetTestSeries(int count = 500)
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(count, Seed, Step);
        return bars.Close;
    }

    // ── Self-consistency: no external library implements TRAMA ─────

    [Fact]
    public void Streaming_Matches_SpanBatch()
    {
        var series = GetTestSeries(500);

        // Streaming
        var trama = new Trama(DefaultPeriod);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama.Update(series[i]).Value);
        }

        // Span batch
        var output = new double[series.Count];
        Trama.Batch(series.Values, output, DefaultPeriod);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.Equal(streamResults[i], output[i], 1e-9);
        }
    }

    [Fact]
    public void Streaming_Matches_TSeries()
    {
        var series = GetTestSeries(500);

        // Streaming
        var trama1 = new Trama(DefaultPeriod);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama1.Update(series[i]).Value);
        }

        // TSeries
        var trama2 = new Trama(DefaultPeriod);
        var batchResults = trama2.Update(series);

        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Matches_Streaming()
    {
        var series = GetTestSeries(500);

        // Streaming
        var trama = new Trama(DefaultPeriod);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama.Update(series[i]).Value);
        }

        // Static Calculate
        var (calcResults, _) = Trama.Calculate(series, DefaultPeriod);

        for (int i = 0; i < calcResults.Count; i++)
        {
            Assert.Equal(streamResults[i], calcResults.Values[i], 1e-9);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(50)]
    public void AllModes_Match_AcrossPeriods(int period)
    {
        var series = GetTestSeries(300);

        // Streaming
        var trama = new Trama(period);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama.Update(series[i]).Value);
        }

        // Span batch
        var output = new double[series.Count];
        Trama.Batch(series.Values, output, period);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.Equal(streamResults[i], output[i], 1e-9);
        }
    }

    [Fact]
    public void Prime_Matches_Streaming()
    {
        var series = GetTestSeries(500);

        // Streaming
        var trama1 = new Trama(DefaultPeriod);
        for (int i = 0; i < series.Count; i++)
        {
            trama1.Update(series[i]);
        }

        // Prime
        var trama2 = new Trama(DefaultPeriod);
        trama2.Prime(series.Values);

        Assert.Equal(trama1.Last.Value, trama2.Last.Value, 1e-9);
    }

    [Fact]
    public void DirectionalCorrectness_UpTrend()
    {
        // Strong uptrend should produce TRAMA values between start and current price
        var trama = new Trama(DefaultPeriod);
        double startPrice = 100.0;

        for (int i = 0; i < 100; i++)
        {
            trama.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, startPrice + i));
        }

        double lastPrice = startPrice + 99;
        // TRAMA should lag behind price but be above start
        Assert.True(trama.Last.Value > startPrice, "TRAMA should be above start price in uptrend");
        Assert.True(trama.Last.Value <= lastPrice, "TRAMA should not exceed current price in uptrend");
    }

    [Fact]
    public void DirectionalCorrectness_DownTrend()
    {
        var trama = new Trama(DefaultPeriod);
        double startPrice = 200.0;

        for (int i = 0; i < 100; i++)
        {
            trama.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, startPrice - i));
        }

        double lastPrice = startPrice - 99;
        Assert.True(trama.Last.Value < startPrice, "TRAMA should be below start price in downtrend");
        Assert.True(trama.Last.Value >= lastPrice, "TRAMA should not go below current price in downtrend");
    }
}
