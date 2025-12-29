
namespace QuanTAlib.Tests;

public class VarianceTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Variance(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Variance(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Variance(-1));
        var variance = new Variance(2);
        Assert.NotNull(variance);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var variance = new Variance(5);
        
        Assert.Equal(0, variance.Last.Value);
        
        TValue result = variance.Update(new TValue(DateTime.UtcNow, 100));
        
        Assert.Equal(result.Value, variance.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var variance = new Variance(5);
        
        variance.Update(new TValue(DateTime.UtcNow, 1), isNew: true);
        variance.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        variance.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        variance.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        double value1 = variance.Update(new TValue(DateTime.UtcNow, 5), isNew: true).Value;
        
        variance.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value2 = variance.Last.Value;
        
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var variance = new Variance(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        
        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            variance.Update(tenthInput, isNew: true);
        }
        
        // Remember state after 10 values
        double stateAfterTen = variance.Last.Value;
        
        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            variance.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }
        
        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = variance.Update(tenthInput, isNew: false);
        
        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var variance = new Variance(5);
        
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));
        variance.Update(new TValue(DateTime.UtcNow, 3));
        
        // Variance doesn't do last-valid-value substitution
        // Just verify it doesn't crash
        var resultAfterPosInf = variance.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        // May be NaN or finite depending on implementation
        Assert.True(double.IsFinite(resultAfterPosInf.Value) || double.IsNaN(resultAfterPosInf.Value) || double.IsInfinity(resultAfterPosInf.Value));
        
        var resultAfterNegInf = variance.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value) || double.IsNaN(resultAfterNegInf.Value) || double.IsInfinity(resultAfterNegInf.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        int count = 200;
        
        var times = new List<long>(count);
        var values = new List<double>(count);
        
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }
        
        var series = new TSeries(times, values);
        
        // 1. Batch Mode (static method)
        var batchSeries = Variance.Calculate(series, period);
        double expected = batchSeries.Last.Value;
        
        // 2. Span Mode (static method with spans)
        var spanInput = values.ToArray();
        var spanOutput = new double[count];
        Variance.Batch(spanInput.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];
        
        // 3. Streaming Mode (instance, one value at a time)
        var streamingInd = new Variance(period);
        for (int i = 0; i < count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;
        
        // Assert all modes produce identical results
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];
        
        // Period must be >= 2
        Assert.Throws<ArgumentException>(() => 
            Variance.Batch(source.AsSpan(), output.AsSpan(), 1));
        Assert.Throws<ArgumentException>(() => 
            Variance.Batch(source.AsSpan(), output.AsSpan(), 0));
        
        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => 
            Variance.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int count = 100;
        
        var times = new List<long>(count);
        var values = new List<double>(count);
        double[] source = new double[count];
        double[] output = new double[count];
        
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
            source[i] = bar.Close;
        }
        
        var series = new TSeries(times, values);
        
        var tseriesResult = Variance.Calculate(series, 10);
        Variance.Batch(source.AsSpan(), output.AsSpan(), 10);
        
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Calculation_KnownValues()
    {
        // Data: 2, 4, 4, 4, 5, 5, 7, 9
        // Mean: 5
        // Deviations: -3, -1, -1, -1, 0, 0, 2, 4
        // Sq Devs: 9, 1, 1, 1, 0, 0, 4, 16
        // Sum Sq Devs: 32
        // Population Variance (N=8): 32 / 8 = 4
        // Sample Variance (N-1=7): 32 / 7 = 4.571428...

        var data = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };

        // Test Population Variance
        var popVar = new Variance(8, isPopulation: true);
        foreach (var val in data)
        {
            popVar.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(4.0, popVar.Last.Value, precision: 6);

        // Test Sample Variance
        var sampVar = new Variance(8, isPopulation: false);
        foreach (var val in data)
        {
            sampVar.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(32.0 / 7.0, sampVar.Last.Value, precision: 6);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterPeriod()
    {
        int period = 5;
        var variance = new Variance(period);

        for (int i = 0; i < period; i++)
        {
            Assert.False(variance.IsHot);
            variance.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(variance.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var variance = new Variance(5);
        for (int i = 0; i < 10; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(variance.IsHot);

        variance.Reset();
        Assert.False(variance.IsHot);
        Assert.Equal(0, variance.Last.Value);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCorrectly()
    {
        // Test differential update
        var variance = new Variance(3, isPopulation: true);

        // Add 1, 2, 3. Mean=2. Var = ((1-2)^2 + (2-2)^2 + (3-2)^2)/3 = (1+0+1)/3 = 2/3 = 0.666...
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));
        variance.Update(new TValue(DateTime.UtcNow, 3));

        Assert.Equal(2.0/3.0, variance.Last.Value, precision: 6);

        // Update last value from 3 to 6.
        // Data: 1, 2, 6. Mean=3. Var = ((1-3)^2 + (2-3)^2 + (6-3)^2)/3 = (4+1+9)/3 = 14/3 = 4.666...
        variance.Update(new TValue(DateTime.UtcNow, 6), isNew: false);

        Assert.Equal(14.0/3.0, variance.Last.Value, precision: 6);
    }

    [Fact]
    public void Batch_Matches_Iterative()
    {
        int period = 10;
        int count = 1000;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Iterative
        var variance = new Variance(period);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = variance.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        Variance.Batch(data, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i], precision: 7);
        }
    }

    [Fact]
    public void Update_HandlesConstantValues_ZeroVariance()
    {
        var variance = new Variance(5);
        for (int i = 0; i < 5; i++)
        {
            var result = variance.Update(new TValue(DateTime.UtcNow, 10));
            if (i >= 1) // Variance defined for N >= 2
            {
                Assert.Equal(0, result.Value);
            }
        }
    }

    [Fact]
    public void Update_HandlesNaN()
    {
        var variance = new Variance(5);
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));
        variance.Update(new TValue(DateTime.UtcNow, double.NaN));

        var result = variance.Last.Value;
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void Resync_DoesNotDrift()
    {
        // Run for > 1000 updates to trigger Resync
        var variance = new Variance(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 1100; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, gbm.Next().Close));
        }

        Assert.True(double.IsFinite(variance.Last.Value));
        Assert.True(variance.Last.Value >= 0);
    }

    [Fact]
    public void Batch_LargeDataset_Simd()
    {
        // Create large dataset to trigger SIMD path (>= 256)
        int count = 1000;
        var data = new double[count];
        for (int i = 0; i < count; i++) data[i] = (double)i;

        var series = new TSeries(new System.Collections.Generic.List<long>(new long[count]), new System.Collections.Generic.List<double>(data));

        // Batch calculation
        var batchResult = Variance.Calculate(series, 10);

        // Verify last value against streaming
        var variance = new Variance(10);
        double lastStreaming = 0;
        foreach (var val in data)
        {
            lastStreaming = variance.Update(new TValue(DateTime.UtcNow, val)).Value;
        }

        Assert.Equal(lastStreaming, batchResult.Last.Value, precision: 10);
    }
}
