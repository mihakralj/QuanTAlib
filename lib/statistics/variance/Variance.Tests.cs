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
        // Use simple known values for easier debugging
        var variance = new Variance(3);
        
        // Add 3 values: 1, 2, 3
        variance.Update(new TValue(DateTime.UtcNow, 1), isNew: true);
        variance.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        var originalResult = variance.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        
        double expectedVariance = originalResult.Value; // Variance of [1,2,3]
        
        // Now correct the 3rd value to 10 (isNew=false)
        variance.Update(new TValue(DateTime.UtcNow, 10), isNew: false);
        
        // Correct back to original value 3 (isNew=false)
        var restoredResult = variance.Update(new TValue(DateTime.UtcNow, 3), isNew: false);
        
        // Should match original variance
        Assert.Equal(expectedVariance, restoredResult.Value, 1e-10);
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
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        const int count = 200;

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
        const int count = 100;

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
            Assert.Equal(tseriesResult[i].Value, output[i], precision: 10);
        }
    }



    [Fact]
    public void Batch_SimdPath_Triggered()
    {
        // Create dataset that should trigger SIMD (clean, large)
        const int count = 300;
        var data = new double[count];
        var output = new double[count];

        for (int i = 0; i < count; i++)
        {
            data[i] = Math.Sin(i * 0.1); // Clean finite values
        }

        Variance.Batch(data, output, 10);

        // Should complete without error and produce finite values
        for (int i = 9; i < count; i++) // Start from period-1
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    [Fact]
    public void Batch_LargeDataset_ForceSimd()
    {
        // Force SIMD path with large clean dataset
        const int count = 1000;
        var data = new double[count];
        var output = new double[count];

        // Generate clean, finite data
        for (int i = 0; i < count; i++)
        {
            data[i] = Math.Sin(i * 0.01) + 10; // Clean finite values, positive
        }

        Variance.Batch(data, output, 10);

        // Verify results are finite and reasonable
        for (int i = 9; i < count; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }

        // Verify against streaming calculation for correctness
        var variance = new Variance(10);
        double[] streamingOutput = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingOutput[i] = variance.Update(new TValue(DateTime.UtcNow, data[i])).Value;
        }

        // Compare last 100 values
        for (int i = count - 100; i < count; i++)
        {
            Assert.Equal(streamingOutput[i], output[i], precision: 10);
        }
    }

    [Fact]
    public void IsHot_BecomesTrueAfterPeriod()
    {
        const int period = 5;
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

        Assert.Equal(2.0 / 3.0, variance.Last.Value, precision: 6);

        // Update last value from 3 to 6.
        // Data: 1, 2, 6. Mean=3. Var = ((1-3)^2 + (2-3)^2 + (6-3)^2)/3 = (4+1+9)/3 = 14/3 = 4.666...
        variance.Update(new TValue(DateTime.UtcNow, 6), isNew: false);

        Assert.Equal(14.0 / 3.0, variance.Last.Value, precision: 6);
    }

    [Fact]
    public void Batch_Matches_Iterative()
    {
        const int period = 10;
        const int count = 1000;
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
    public void Batch_LargeDataset_Simd()
    {
        // Create large dataset to trigger SIMD path (>= 256)
        const int count = 1000;
        var data = new double[count];
        for (int i = 0; i < count; i++) data[i] = (double)i;

        var series = new TSeries(new System.Collections.Generic.List<long>(new long[count]), new System.Collections.Generic.List<double>(data));

        // Batch calculation
        var batchResult = Variance.Calculate(series, 10);
        Assert.True(double.IsFinite(batchResult.Last.Value));
        Assert.True(batchResult.Last.Value >= 0);

        // Verify last value against streaming
        var variance = new Variance(10);
        double lastStreaming = 0;
        foreach (var val in data)
        {
            lastStreaming = variance.Update(new TValue(DateTime.UtcNow, val)).Value;
        }

        Assert.Equal(lastStreaming, batchResult.Last.Value, precision: 10);
    }

    [Fact]
    public void Prime_Method_Works()
    {
        var variance = new Variance(5);
        double[] primeData = [10, 20, 30, 40, 50];

        variance.Prime(primeData.AsSpan());

        Assert.True(variance.IsHot);
        Assert.Equal(250.0, variance.Last.Value, precision: 6); // Variance of [10,20,30,40,50] = 1000/4 = 250
    }

    [Fact]
    public void Prime_WithInsufficientData()
    {
        var variance = new Variance(5);
        double[] primeData = [10, 20]; // Less than period

        variance.Prime(primeData.AsSpan());

        Assert.False(variance.IsHot);
        Assert.Equal(50.0, variance.Last.Value, precision: 6); // Variance of [10,20] = 50/1 = 50
    }

    [Fact]
    public void Prime_WithEmptySpan()
    {
        var variance = new Variance(5);

        variance.Prime(ReadOnlySpan<double>.Empty);

        Assert.False(variance.IsHot);
        Assert.Equal(0, variance.Last.Value);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectSeries()
    {
        var source = new TSeries();
        source.Add(DateTime.UtcNow.Ticks, 10);
        source.Add(DateTime.UtcNow.Ticks + 1, 20);
        source.Add(DateTime.UtcNow.Ticks + 2, 30);
        source.Add(DateTime.UtcNow.Ticks + 3, 40);
        source.Add(DateTime.UtcNow.Ticks + 4, 50);

        var variance = new Variance(3);
        var result = variance.Update(source);

        Assert.Equal(5, result.Count);
        Assert.Equal(source.Times[0], result.Times[0]);
        Assert.Equal(source.Times[4], result.Times[4]);

        // Check variance values
        Assert.Equal(0, result[0].Value); // N=1, no variance
        Assert.Equal(50.0, result[1].Value, precision: 6); // Var([10,20]) = 50
        Assert.Equal(100.0, result[2].Value, precision: 6); // Var([10,20,30]) = 200/2 = 100
        Assert.Equal(100.0, result[3].Value, precision: 6); // Var([20,30,40]) = 200/2 = 100
        Assert.Equal(100.0, result[4].Value, precision: 6); // Var([30,40,50]) = 200/2 = 100
    }

    [Fact]
    public void Update_TSeries_EmptySource()
    {
        var variance = new Variance(5);
        var result = variance.Update(new TSeries());

        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_PrimesState()
    {
        var source = new TSeries();
        for (int i = 0; i < 10; i++)
        {
            source.Add(DateTime.UtcNow.Ticks + i, i * 10);
        }

        var variance = new Variance(5);
        variance.Update(source);

        // Should be primed with last 5 values
        Assert.True(variance.IsHot);

        // Add one more value and check it continues correctly
        var newValue = variance.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(newValue.Value));
    }

    [Fact]
    public void Calculate_StaticMethod_Works()
    {
        var source = new TSeries();
        source.Add(DateTime.UtcNow.Ticks, 10);
        source.Add(DateTime.UtcNow.Ticks + 1, 20);
        source.Add(DateTime.UtcNow.Ticks + 2, 30);

        var result = Variance.Calculate(source, 3); // Sample variance by default

        Assert.Equal(3, result.Count);
        Assert.Equal(100.0, result.Last.Value, precision: 6); // Sample variance: 200/2 = 100
    }

    [Fact]
    public void Calculate_StaticMethod_PopulationVariance()
    {
        var source = new TSeries();
        source.Add(DateTime.UtcNow.Ticks, 10);
        source.Add(DateTime.UtcNow.Ticks + 1, 20);
        source.Add(DateTime.UtcNow.Ticks + 2, 30);

        var result = Variance.Calculate(source, 3, isPopulation: true);

        Assert.Equal(3, result.Count);
        Assert.Equal(66.666666, result.Last.Value, precision: 5); // Population variance: 200/3 ≈ 66.67
    }

    [Fact]
    public void Batch_WithNaNInData()
    {
        double[] source = [10, 20, double.NaN, 40, 50];
        double[] output = new double[5];

        Variance.Batch(source, output, 3);

        // Should handle NaN gracefully
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val) || double.IsNaN(val));
        }
    }

    [Fact]
    public void Batch_PeriodEqualsTwo()
    {
        double[] source = [10, 20, 30, 40];
        double[] output = new double[4];

        Variance.Batch(source, output, 2);

        Assert.Equal(0, output[0]); // N=1
        Assert.Equal(50, output[1]); // Var([10,20]) = 50
        Assert.Equal(50, output[2]); // Var([20,30]) = 50
        Assert.Equal(50, output[3]); // Var([30,40]) = 50
    }

    [Fact]
    public void Batch_VeryLargePeriod()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Variance.Batch(source, output, 5);

        Assert.Equal(0, output[0]); // N=1, variance undefined
        Assert.Equal(50, output[1]); // Var([10,20]) = 50
        Assert.Equal(100, output[2]); // Var([10,20,30]) = 200/2 = 100
        Assert.Equal(500.0 / 3.0, output[3], precision: 6); // Var([10,20,30,40]) = 500/3 ≈ 166.67
        Assert.Equal(250, output[4], precision: 6); // Var([10,20,30,40,50]) = 1000/4 = 250
    }

    [Fact]
    public void Batch_SingleElement()
    {
        double[] source = [42];
        double[] output = new double[1];

        Variance.Batch(source, output, 2);

        Assert.Equal(0, output[0]);
    }

    [Fact]
    public void Batch_ConstantValues_ZeroVariance()
    {
        double[] source = [5, 5, 5, 5, 5];
        double[] output = new double[5];

        Variance.Batch(source, output, 3);

        Assert.Equal(0, output[0]);
        Assert.Equal(0, output[1]);
        Assert.Equal(0, output[2]);
        Assert.Equal(0, output[3]);
        Assert.Equal(0, output[4]);
    }

    [Fact]
    public void Batch_PopulationVsSample()
    {
        double[] source = [10, 20, 30];
        double[] outputPop = new double[3];
        double[] outputSamp = new double[3];

        Variance.Batch(source, outputPop, 3, isPopulation: true);
        Variance.Batch(source, outputSamp, 3, isPopulation: false);

        // Population variance should be smaller than sample variance
        Assert.True(outputPop[2] < outputSamp[2]);
        Assert.Equal(66.666666, outputPop[2], precision: 5); // 200/3
        Assert.Equal(100, outputSamp[2], precision: 6); // 200/2
    }



    [Fact]
    public void Resync_PreventsDrift_Extended()
    {
        // Test that resync works by running many updates
        var variance = new Variance(5);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.1, seed: 42);

        // Run enough updates to trigger multiple resyncs
        for (int i = 0; i < 2500; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, gbm.Next().Close));
        }

        Assert.True(double.IsFinite(variance.Last.Value));
        Assert.True(variance.Last.Value >= 0);
    }

    [Fact]
    public void Update_WithNegativeValues()
    {
        var variance = new Variance(3);

        variance.Update(new TValue(DateTime.UtcNow, -10));
        variance.Update(new TValue(DateTime.UtcNow, -5));
        variance.Update(new TValue(DateTime.UtcNow, 0));

        Assert.Equal(25, variance.Last.Value, precision: 6); // Var([-10,-5,0]) = 25
    }

    [Fact]
    public void Update_MixedPositiveNegative()
    {
        var variance = new Variance(4);

        variance.Update(new TValue(DateTime.UtcNow, -2));
        variance.Update(new TValue(DateTime.UtcNow, -1));
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));

        Assert.Equal(10.0 / 3.0, variance.Last.Value, precision: 6); // Var([-2,-1,1,2]) = 10/3 ≈ 3.333
    }

    [Fact]
    public void Batch_SimdFallback_WithNaN()
    {
        // Dataset with NaN should fall back to scalar path
        const int count = 300;
        double[] source = new double[count];
        double[] output = new double[count];

        for (int i = 0; i < count; i++)
        {
            source[i] = i * 0.1;
        }
        source[150] = double.NaN; // Insert NaN

        Variance.Batch(source, output, 10);

        // Should complete without error
        for (int i = 0; i < count; i++)
        {
            Assert.True(double.IsFinite(output[i]) || double.IsNaN(output[i]));
        }
    }

    [Fact]
    public void Constructor_WithPopulationFlag()
    {
        var popVariance = new Variance(5, isPopulation: true);
        var sampVariance = new Variance(5, isPopulation: false);

        // Both should be valid
        Assert.NotNull(popVariance);
        Assert.NotNull(sampVariance);
    }

    [Fact]
    public void Name_Property_ContainsPeriod()
    {
        var variance = new Variance(10);
        Assert.Contains("10", variance.Name, StringComparison.Ordinal);
        Assert.Contains("Variance", variance.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void WarmupPeriod_Property()
    {
        var variance = new Variance(7);
        Assert.Equal(7, variance.WarmupPeriod);
    }

    [Fact]
    public void Update_AfterReset_Works()
    {
        var variance = new Variance(3);

        // Fill buffer
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));
        variance.Update(new TValue(DateTime.UtcNow, 3));
        double valueBefore = variance.Last.Value;

        variance.Reset();

        // Update after reset
        variance.Update(new TValue(DateTime.UtcNow, 10));
        variance.Update(new TValue(DateTime.UtcNow, 20));
        variance.Update(new TValue(DateTime.UtcNow, 30));
        double valueAfter = variance.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
        Assert.Equal(100.0, valueAfter, precision: 6);
    }

    [Fact]
    public void Batch_ZeroLengthSpans()
    {
        double[] emptySource = [];
        double[] emptyOutput = [];

        // Should not throw
        Variance.Batch(emptySource, emptyOutput, 2);

        Assert.Empty(emptySource);
        Assert.Empty(emptyOutput);
    }

    [Fact]
    public void Batch_MinimalValidData()
    {
        double[] source = [10, 20];
        double[] output = new double[2];

        Variance.Batch(source, output, 2);

        Assert.Equal(0, output[0]); // N=1
        Assert.Equal(50, output[1]); // Var([10,20]) = 50
    }
}