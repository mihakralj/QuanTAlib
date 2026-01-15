namespace QuanTAlib;

public class BwmaTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var bwma = new Bwma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            bwma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(bwma.Last.Value));
    }

    [Fact]
    public void DifferentOrders_ProduceDifferentResults()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var bwma0 = new Bwma(10, 0);
        var bwma1 = new Bwma(10, 1);
        var bwma3 = new Bwma(10, 3);

        for (int i = 0; i < series.Count; i++)
        {
            bwma0.Update(series[i]);
            bwma1.Update(series[i]);
            bwma3.Update(series[i]);
        }

        // Different orders should produce different results
        // Note: order 1 and 2 both use power=1.5 (PineScript special cases)
        Assert.NotEqual(bwma0.Last.Value, bwma1.Last.Value, 1e-9);
        Assert.NotEqual(bwma1.Last.Value, bwma3.Last.Value, 1e-9);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var bwma = new Bwma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 99; i++)
        {
            bwma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        bwma.Update(new TValue(bars[99].Time, bars[99].Close), true);
        var val2 = bwma.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), false);

        var bwma2 = new Bwma(10);
        for (int i = 0; i < 99; i++)
        {
            bwma2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        var val3 = bwma2.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var bwma = new Bwma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            bwma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        bwma.Reset();
        Assert.Equal(0, bwma.Last.Value);
        Assert.False(bwma.IsHot);

        for (int i = 0; i < bars.Count; i++)
        {
            bwma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(bwma.Last.Value));
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var bwma = new Bwma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(bwma.Update(series[i]).Value);
        }

        var bwma2 = new Bwma(10);
        var seriesResults = bwma2.Update(series);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var bwma = new Bwma(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(bwma.Update(series[i]).Value);
        }

        var staticResults = Bwma.Batch(series, 10);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatchSpan_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var bwma = new Bwma(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(bwma.Update(series[i]).Value);
        }

        var spanResults = new double[series.Count];
        Bwma.Calculate(series.Values, spanResults, 10);

        for (int i = 0; i < spanResults.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatchSpan_WithOrder_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var bwma = new Bwma(10, 2);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(bwma.Update(series[i]).Value);
        }

        var spanResults = new double[series.Count];
        Bwma.Calculate(series.Values, spanResults, 10, 2);

        for (int i = 0; i < spanResults.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var bwma = new Bwma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var result = bwma.Update(series);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        var result2 = bwma.Update(series[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Bwma(0));
        Assert.Throws<ArgumentException>(() => new Bwma(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bwma(10, -1));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        // Create a custom publisher that fires events
        var publisher = new TestPublisher();
        var bwma = new Bwma(publisher, 3);  // Use small period

        // Feed enough values to get a stable result
        publisher.Publish(new TValue(DateTime.UtcNow, 100));
        publisher.Publish(new TValue(DateTime.UtcNow, 100));
        publisher.Publish(new TValue(DateTime.UtcNow, 100));
        var lastBeforeDispose = bwma.Last.Value;
        Assert.True(double.IsFinite(lastBeforeDispose));

        bwma.Dispose();

        publisher.Publish(new TValue(DateTime.UtcNow, 200));
        // After dispose, indicator should not update
        Assert.Equal(lastBeforeDispose, bwma.Last.Value);
    }

    [Fact]
    public void NaN_Handling_Works()
    {
        var bwma = new Bwma(3);

        // First valid values
        bwma.Update(new TValue(DateTime.UtcNow, 1.0));
        bwma.Update(new TValue(DateTime.UtcNow, 2.0));

        // Then NaN - should use last valid value
        bwma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(bwma.Last.Value));

        // Continue with valid values
        bwma.Update(new TValue(DateTime.UtcNow, 3.0));
        Assert.True(double.IsFinite(bwma.Last.Value));
    }

    [Fact]
    public void InitialNaN_HandledGracefully()
    {
        var bwma = new Bwma(3);

        // When first value is NaN and no valid value exists, the result depends on weights
        // Edge weights may be 0, causing NaN*0 to produce 0 rather than NaN
        var result = bwma.Update(new TValue(DateTime.UtcNow, double.NaN));
        // Just verify it doesn't crash and produces a finite value or NaN
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));

        // After valid values, indicator should work normally
        bwma.Update(new TValue(DateTime.UtcNow, 100.0));
        bwma.Update(new TValue(DateTime.UtcNow, 100.0));
        var finalResult = bwma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(finalResult.Value));
    }

    // Helper class for testing event-based subscription
    private sealed class TestPublisher : ITValuePublisher
    {
        public event TValuePublishedHandler? Pub;

        public void Publish(TValue value)
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = true });
        }
    }

    [Fact]
    public void Order0_IsParabolic()
    {
        // For order 0, weights are (1 - x²) which forms a parabola
        var bwma = new Bwma(5, 0);

        // Feed simple values
        for (int i = 1; i <= 5; i++)
        {
            bwma.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(double.IsFinite(bwma.Last.Value));
        Assert.True(bwma.IsHot);
    }

    [Fact]
    public void Period1_ReturnsInput()
    {
        var bwma = new Bwma(1);
        var val = bwma.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, val.Value, 1e-9);
    }

    [Fact]
    public void Warmup_Period3_Order0_MatchesReference()
    {
        var bwma = new Bwma(3, 0);
        var t = DateTime.UtcNow;

        Assert.Equal(1.0, bwma.Update(new TValue(t, 1.0)).Value, 1e-9);
        Assert.Equal(2.0, bwma.Update(new TValue(t, 2.0)).Value, 1e-9);
        Assert.Equal(2.0, bwma.Update(new TValue(t, 3.0)).Value, 1e-9);
    }

    [Fact]
    public void Period2_Order0_FallsBackToCurrentValue()
    {
        var bwma = new Bwma(2, 0);
        var t = DateTime.UtcNow;

        Assert.Equal(10.0, bwma.Update(new TValue(t, 10.0)).Value, 1e-9);
        Assert.Equal(20.0, bwma.Update(new TValue(t, 20.0)).Value, 1e-9);
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming_WithNaNAtReplayStart()
    {
        const int period = 5;
        var series = new TSeries();
        var start = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double v = i == 5 ? double.NaN : 100.0 + i;
            series.Add(new TValue(start.AddMinutes(i), v));
        }

        var bwmaStreaming = new Bwma(period);
        var streaming = new List<double>(series.Count);
        foreach (var item in series)
        {
            streaming.Add(bwmaStreaming.Update(item).Value);
        }

        var bwmaBatch = new Bwma(period);
        var batch = bwmaBatch.Update(series);

        Assert.Equal(streaming.Count, batch.Count);
        for (int i = 0; i < batch.Count; i++)
        {
            Assert.Equal(streaming[i], batch.Values[i], 1e-9);
        }
    }
}
