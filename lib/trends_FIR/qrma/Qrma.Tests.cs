namespace QuanTAlib.Tests;

public class QrmaTests
{
    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Qrma(0));
        Assert.Throws<ArgumentException>(() => new Qrma(-1));
        Assert.Throws<ArgumentException>(() => new Qrma(2)); // Minimum is 3
    }

    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var qrma = new Qrma(14);
        Assert.Equal("Qrma(14)", qrma.Name);
        Assert.False(qrma.IsHot);
    }

    [Fact]
    public void Update_SingleValue_ReturnsSameValue()
    {
        var qrma = new Qrma(14);
        var result = qrma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Update_LinearTrend_ReturnsExactValue()
    {
        // For a perfect linear trend y = x, quadratic regression should also return x
        // (higher-order coefficient c becomes zero)
        const int period = 10;
        var qrma = new Qrma(period);

        for (int i = 0; i < period * 2; i++)
        {
            var result = qrma.Update(new TValue(DateTime.UtcNow, i));
            if (i >= period) // After warmup
            {
                Assert.Equal(i, result.Value, 1e-6);
            }
        }
    }

    [Fact]
    public void Update_QuadraticTrend_ReturnsExactValue()
    {
        // For y = x², quadratic regression should fit exactly
        const int period = 10;
        var qrma = new Qrma(period);

        for (int i = 0; i < period * 2; i++)
        {
            double y = (double)i * i;
            var result = qrma.Update(new TValue(DateTime.UtcNow, y));
            if (i >= period)
            {
                Assert.Equal(y, result.Value, 1e-4);
            }
        }
    }

    [Fact]
    public void Update_ConstantValue_ReturnsSameValue()
    {
        const int period = 10;
        var qrma = new Qrma(period);
        const double value = 123.45;

        for (int i = 0; i < period * 2; i++)
        {
            var result = qrma.Update(new TValue(DateTime.UtcNow, value));
            Assert.Equal(value, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Update_BarCorrection_UpdatesCorrectly()
    {
        var qrma = new Qrma(5);

        // Fill buffer
        for (int i = 0; i < 5; i++)
        {
            qrma.Update(new TValue(DateTime.UtcNow, i));
        }

        // New bar
        var result1 = qrma.Update(new TValue(DateTime.UtcNow, 10));

        // Update same bar with different value
        var result2 = qrma.Update(new TValue(DateTime.UtcNow, 20), isNew: false);

        Assert.NotEqual(result1.Value, result2.Value);

        // Verify internal state by adding next bar
        var result3 = qrma.Update(new TValue(DateTime.UtcNow, 30));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var qrma = new Qrma(5);

        // Build up state
        for (int i = 0; i < 10; i++)
        {
            qrma.Update(new TValue(DateTime.UtcNow, i * 10.0));
        }

        // New bar
        var resultNew = qrma.Update(new TValue(DateTime.UtcNow, 100));

        // Multiple corrections on the same bar
        qrma.Update(new TValue(DateTime.UtcNow, 105), isNew: false);
        qrma.Update(new TValue(DateTime.UtcNow, 110), isNew: false);
        var resultFinal = qrma.Update(new TValue(DateTime.UtcNow, 100), isNew: false);

        // Correcting back to original value should give same result
        Assert.Equal(resultNew.Value, resultFinal.Value, 1e-9);
    }

    [Fact]
    public void Update_NaN_HandlesGracefully()
    {
        var qrma = new Qrma(5);

        for (int i = 1; i <= 5; i++)
        {
            qrma.Update(new TValue(DateTime.UtcNow, i));
        }

        // NaN should be replaced with last valid value
        var result = qrma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_HandlesGracefully()
    {
        var qrma = new Qrma(5);

        for (int i = 1; i <= 5; i++)
        {
            qrma.Update(new TValue(DateTime.UtcNow, i));
        }

        var result = qrma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var qrma = new Qrma(5);
        qrma.Update(new TValue(DateTime.UtcNow, 10));

        // Several NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = qrma.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
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

        var qrma = new Qrma(period);
        var series1 = qrma.Update(source);
        var series2 = Qrma.Batch(source, period);

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

        Qrma.Batch(values, output, period);

        var qrma = new Qrma(period);
        for (int i = 0; i < count; i++)
        {
            var result = qrma.Update(new TValue(DateTime.UtcNow, values[i]));
            Assert.Equal(result.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Span_InvalidLength_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[5]; // Mismatched length

        var ex = Assert.Throws<ArgumentException>(() => Qrma.Batch(source, output, 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Span_InvalidPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Qrma.Batch(source, output, 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Span_LargeData_DoesNotStackOverflow()
    {
        const int period = 20;
        const int count = 5000;
        var values = new double[count];
        var output = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < count; i++)
        {
            values[i] = gbm.Next().Close;
        }

        // Should not throw
        Qrma.Batch(values, output, period);

        // All post-warmup values should be finite
        for (int i = period; i < count; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} is not finite");
        }
    }

    [Fact]
    public void Span_NaN_HandledCorrectly()
    {
        const int period = 5;
        var source = new double[] { 1, 2, 3, double.NaN, 5, 6, 7, 8, 9, 10 };
        var output = new double[source.Length];

        Qrma.Batch(source, output, period);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} is not finite");
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var qrma = new Qrma(5);
        for (int i = 0; i < 10; i++)
        {
            qrma.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(qrma.IsHot);

        qrma.Reset();

        Assert.False(qrma.IsHot);
        Assert.Equal(0, qrma.Last.Value);

        var result = qrma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var qrma = new Qrma(period);

        for (int i = 0; i < period; i++)
        {
            Assert.False(qrma.IsHot);
            qrma.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(qrma.IsHot);
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var qrma = new Qrma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, qrma.Last.Value);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var qrma = new Qrma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, qrma.Last.Value);

        qrma.Dispose();

        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(100, qrma.Last.Value); // Should remain at previous value
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var source = new TSeries();
        var qrma = new Qrma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));

#pragma warning disable S3966
        qrma.Dispose();
        qrma.Dispose();
#pragma warning restore S3966

        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(100, qrma.Last.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Dispose_IsThreadSafe()
    {
        var source = new TSeries();
        var qrma = new Qrma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));

        var tasks = new System.Threading.Tasks.Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() => qrma.Dispose());
        }

        await System.Threading.Tasks.Task.WhenAll(tasks);

        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(100, qrma.Last.Value);
    }

    [Fact]
    public void Dispose_WithoutSource_DoesNotThrow()
    {
        var qrma = new Qrma(5);

#pragma warning disable S3966
        qrma.Dispose();
        qrma.Dispose();
#pragma warning restore S3966

        Assert.False(qrma.IsHot);
    }

    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Qrma(null!, 5));
    }

    [Fact]
    public void MinPeriod_Three_Works()
    {
        // QRMA minimum period is 3 (3 unknowns for quadratic)
        var qrma = new Qrma(3);

        qrma.Update(new TValue(DateTime.UtcNow, 1));
        qrma.Update(new TValue(DateTime.UtcNow, 4));
        var result = qrma.Update(new TValue(DateTime.UtcNow, 9));

        // y = x² with x={0,1,2} → at x=2 → 4
        // But input values are {1,4,9} which is y=(x+1)² → endpoint at x=2: a+2b+4c
        Assert.True(double.IsFinite(result.Value));
        Assert.True(qrma.IsHot);
    }

    [Fact]
    public void AllModes_ProduceConsistentResults()
    {
        const int period = 10;
        const int count = 50;
        var gbm = new GBM(startPrice: 100, seed: 42);
        var source = new TSeries();
        var values = new double[count];

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
            values[i] = bar.Close;
        }

        // Mode 1: Streaming
        var streaming = new Qrma(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = streaming.Update(source[i]).Value;
        }

        // Mode 2: Batch TSeries
        var batchResults = Qrma.Batch(source, period);

        // Mode 3: Span
        var spanOutput = new double[count];
        Qrma.Batch(values, spanOutput, period);

        // Mode 4: Event-based
        var eventSource = new TSeries();
        var eventQrma = new Qrma(eventSource, period);
        var eventResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventQrma.Last.Value;
        }

        // All four modes should match
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, 1e-9);
            Assert.Equal(streamingResults[i], spanOutput[i], 1e-9);
            Assert.Equal(streamingResults[i], eventResults[i], 1e-9);
        }
    }
}
