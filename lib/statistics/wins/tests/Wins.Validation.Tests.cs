namespace QuanTAlib.Tests;

/// <summary>
/// Wins self-consistency validation.
/// Validates internal consistency: batch == streaming == span.
/// </summary>
public class WinsValidationTests
{
    [Fact]
    public void Wins_Streaming_Equals_SpanBatch()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 8008);
        int n = 200;
        int period = 20;
        double winPct = 10.0;

        var prices = new double[n];
        var times = new long[n];
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            prices[i] = bar.Close;
            times[i] = t0.AddMinutes(i).Ticks;
        }

        var streaming = new Wins(period, winPct);
        var streamValues = new double[n];
        for (int i = 0; i < n; i++)
        {
            streamValues[i] = streaming.Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), prices[i])).Value;
        }

        var spanValues = new double[n];
        Wins.Batch(prices, spanValues, period, winPct);

        for (int i = period - 1; i < n; i++)
        {
            Assert.Equal(streamValues[i], spanValues[i], 9);
        }
    }

    [Fact]
    public void Wins_WinPctZero_EqualsSMA_LongSeries()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 9009);
        int n = 200;
        int period = 14;

        var prices = new double[n];
        for (int i = 0; i < n; i++)
        {
            prices[i] = rng.Next().Close;
        }

        var wins0 = new double[n];
        Wins.Batch(prices, wins0, period, 0.0);

        // Manual SMA reference
        for (int i = period - 1; i < n; i++)
        {
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += prices[j];
            }

            double sma = sum / period;
            Assert.Equal(sma, wins0[i], 9);
        }
    }

    [Fact]
    public void Wins_BatchTSeries_EqualsStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 1010);
        int n = 50;
        int period = 10;
        double winPct = 15.0;

        var series = new TSeries();
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            series.Add(new TValue(t0.AddMinutes(i), bar.Close));
        }

        var batchResult = Wins.Batch(series, period, winPct);

        var streaming = new Wins(period, winPct);
        TValue lastStream = default;
        for (int i = 0; i < n; i++)
        {
            lastStream = streaming.Update(series[i]);
        }

        Assert.Equal(lastStream.Value, batchResult[n - 1].Value, 9);
    }

    [Fact]
    public void Wins_MoreRobust_ThanSMA_WithOutlier()
    {
        // With extreme outlier, WINS result should be closer to the "true" mean
        // than raw SMA, because outlier is clamped to boundary
        var wins = new Wins(10, 10.0);
        double[] data = [100, 101, 99, 100, 102, 98, 100, 101, 99, 1000]; // outlier at end

        double smaSum = 0;
        for (int i = 0; i < 10; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, data[i]));
            smaSum += data[i];
        }

        double sma = smaSum / 10; // ~189 with outlier
        double winsResult = wins.Last.Value;

        // WINS should be less than SMA (because 1000 is clamped to boundary ~101)
        Assert.True(winsResult < sma);
        Assert.True(winsResult > 95); // should be near 100
    }
}
