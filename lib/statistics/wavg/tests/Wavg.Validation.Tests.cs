namespace QuanTAlib.Tests;

/// <summary>
/// Wavg self-consistency validation.
/// Validates against manual WMA computation and cross-mode consistency.
/// </summary>
public class WavgValidationTests
{
    [Fact]
    public void Wavg_Streaming_Equals_SpanBatch()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 5005);
        int n = 200;
        int period = 14;

        var prices = new double[n];
        var times = new long[n];
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            prices[i] = bar.Close;
            times[i] = t0.AddMinutes(i).Ticks;
        }

        // Streaming
        var streaming = new Wavg(period);
        var streamValues = new double[n];
        for (int i = 0; i < n; i++)
        {
            streamValues[i] = streaming.Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), prices[i])).Value;
        }

        // Span batch
        var spanValues = new double[n];
        Wavg.Batch(prices, spanValues, period);

        for (int i = period - 1; i < n; i++)
        {
            Assert.Equal(streamValues[i], spanValues[i], 6);
        }
    }

    [Fact]
    public void Wavg_ManualWMA_Matches_KnownPeriod()
    {
        // Verify against hand-computed WMA
        // Values [10, 20, 30], period=3
        // weights [1,2,3], denom=6
        // WMA = (1*10 + 2*20 + 3*30)/6 = (10+40+90)/6 = 140/6 ≈ 23.333
        var wavg = new Wavg(3);
        wavg.Update(new TValue(DateTime.UtcNow, 10.0));
        wavg.Update(new TValue(DateTime.UtcNow, 20.0));
        TValue result = wavg.Update(new TValue(DateTime.UtcNow, 30.0));

        Assert.Equal(140.0 / 6.0, result.Value, 10);
    }

    [Fact]
    public void Wavg_BatchTSeries_EqualsStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 6006);
        int n = 50;
        int period = 10;

        var series = new TSeries();
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            series.Add(new TValue(t0.AddMinutes(i), bar.Close));
        }

        var batchResult = Wavg.Batch(series, period);

        var streaming = new Wavg(period);
        TValue lastStream = default;
        for (int i = 0; i < n; i++)
        {
            lastStream = streaming.Update(series[i]);
        }

        Assert.Equal(lastStream.Value, batchResult[n - 1].Value, 6);
    }

    [Fact]
    public void Wavg_Period1_EqualsInput()
    {
        // With period=1, weight=1, denom=1 → result = input
        var wavg = new Wavg(1);
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 7007);
        for (int i = 0; i < 20; i++)
        {
            double price = rng.Next().Close;
            TValue result = wavg.Update(new TValue(DateTime.UtcNow, price));
            Assert.Equal(price, result.Value, 10);
        }
    }

    [Fact]
    public void Wavg_RecentValueHasHigherWeight()
    {
        // WAVG should be closer to recent values than SMA
        // Ascending series: WAVG > SMA
        var wavg = new Wavg(5);
        // Fill with ascending values
        for (int i = 1; i <= 5; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }

        // SMA = (10+20+30+40+50)/5 = 30
        // WAVG = (1*10+2*20+3*30+4*40+5*50)/(1+2+3+4+5) = (10+40+90+160+250)/15 = 550/15 ≈ 36.67
        Assert.True(wavg.Last.Value > 30.0); // WAVG > SMA for ascending
    }
}
