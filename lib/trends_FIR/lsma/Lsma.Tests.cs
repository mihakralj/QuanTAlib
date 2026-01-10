
namespace QuanTAlib.Tests;

public class LsmaTests
{
    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Lsma(0));
        Assert.Throws<ArgumentException>(() => new Lsma(-1));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var lsma = new Lsma(14, 0);
        Assert.Equal("Lsma(14)", lsma.Name);
        Assert.False(lsma.IsHot);
    }

    [Fact]
    public void Update_SingleValue_ReturnsSameValue()
    {
        var lsma = new Lsma(14);
        var result = lsma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Update_LinearTrend_ReturnsExactValue()
    {
        // For a perfect linear trend y = x, LSMA should return x
        const int period = 10;
        var lsma = new Lsma(period);

        for (int i = 0; i < period * 2; i++)
        {
            var result = lsma.Update(new TValue(DateTime.UtcNow, i));
            if (i >= period) // After warmup
            {
                Assert.Equal(i, result.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Update_ConstantValue_ReturnsSameValue()
    {
        const int period = 10;
        var lsma = new Lsma(period);
        const double value = 123.45;

        for (int i = 0; i < period * 2; i++)
        {
            var result = lsma.Update(new TValue(DateTime.UtcNow, value));
            Assert.Equal(value, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Update_WithOffset_ProjectsCorrectly()
    {
        // y = 2x + 1
        // At x=10, y=21. Slope=2, Intercept=1
        // LSMA(offset=1) should project to x=11 -> y=23

        const int period = 5;
        const int offset = 1;
        var lsma = new Lsma(period, offset);

        for (int i = 0; i < 20; i++)
        {
            double y = 2 * i + 1;
            var result = lsma.Update(new TValue(DateTime.UtcNow, y));

            if (i >= period)
            {
                double expected = 2 * (i + offset) + 1;
                Assert.Equal(expected, result.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Update_BarCorrection_UpdatesCorrectly()
    {
        var lsma = new Lsma(5);

        // Fill buffer
        for (int i = 0; i < 5; i++)
        {
            lsma.Update(new TValue(DateTime.UtcNow, i));
        }

        // New bar
        var result1 = lsma.Update(new TValue(DateTime.UtcNow, 10));

        // Update same bar with different value
        var result2 = lsma.Update(new TValue(DateTime.UtcNow, 20), isNew: false);

        Assert.NotEqual(result1.Value, result2.Value);

        // Verify internal state by adding next bar
        // If state was corrupted, this would fail
        var result3 = lsma.Update(new TValue(DateTime.UtcNow, 30));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Update_NaN_HandlesGracefully()
    {
        var lsma = new Lsma(5);

        lsma.Update(new TValue(DateTime.UtcNow, 1));
        lsma.Update(new TValue(DateTime.UtcNow, 2));
        var result = lsma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Input sequence becomes: 1, 2, 2 (NaN replaced by last valid 2)
        // Regression on (2,1), (1,2), (0,2)
        // Result should be 2.166666667
        Assert.Equal(2.1666666666666665, result.Value, 1e-9);
    }

    [Fact]
    public void Calculate_StaticMethod_MatchesObjectInstance()
    {
        const int period = 10;
        const int count = 100;
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        var lsma = new Lsma(period);
        var series1 = lsma.Update(source);
        var series2 = Lsma.Batch(source, period);

        Assert.Equal(series1.Count, series2.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(series1[i].Value, series2[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_MatchesSeries()
    {
        const int period = 10;
        const int count = 100;
        var values = new double[count];
        var output = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            values[i] = bar.Close;
        }

        Lsma.Calculate(values, output, period);

        var lsma = new Lsma(period);
        for (int i = 0; i < count; i++)
        {
            var result = lsma.Update(new TValue(DateTime.UtcNow, values[i]));
            Assert.Equal(result.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var lsma = new Lsma(5);
        for (int i = 0; i < 10; i++)
        {
            lsma.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(lsma.IsHot);

        lsma.Reset();

        Assert.False(lsma.IsHot);
        Assert.Equal(0, lsma.Last.Value);

        // Should behave like new instance
        var result = lsma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var lsma = new Lsma(period);

        for (int i = 0; i < period; i++)
        {
            Assert.False(lsma.IsHot);
            lsma.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(lsma.IsHot);
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var lsma = new Lsma(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, lsma.Last.Value);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var lsma = new Lsma(source, 5);

        // Verify subscription works
        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, lsma.Last.Value);

        // Dispose and verify unsubscription
        lsma.Dispose();

        // Add more data - lsma should NOT update
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(100, lsma.Last.Value); // Should remain at previous value
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var source = new TSeries();
        var lsma = new Lsma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));

        // Multiple Dispose calls should not throw
        // Suppressing S3966: Multiple Dispose calls are intentional to test idempotency
#pragma warning disable S3966
        lsma.Dispose();
        lsma.Dispose();
        lsma.Dispose();
#pragma warning restore S3966

        // Verify still unsubscribed
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(100, lsma.Last.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Dispose_IsThreadSafe()
    {
        var source = new TSeries();
        var lsma = new Lsma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));

        // Dispose from multiple threads simultaneously
        var tasks = new System.Threading.Tasks.Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() => lsma.Dispose());
        }

        await System.Threading.Tasks.Task.WhenAll(tasks);

        // Verify unsubscribed
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(100, lsma.Last.Value);
    }

    [Fact]
    public void Dispose_WithoutSource_DoesNotThrow()
    {
        // Lsma created without source parameter
        var lsma = new Lsma(5);

        // Should not throw even though there's no source to unsubscribe from
        // Suppressing S3966: Multiple Dispose calls are intentional to test idempotency
#pragma warning disable S3966
        lsma.Dispose();
        lsma.Dispose(); // Idempotent
#pragma warning restore S3966

        // Verify state remains valid
        Assert.False(lsma.IsHot);
    }

    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Lsma(null!, 5));
    }
}
