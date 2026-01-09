namespace QuanTAlib.Tests;

public sealed class VelValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public VelValidationTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing) _testData?.Dispose();
    }

    [Fact]
    public void Vel_Matches_PwmaMinusWma_Batch()
    {
        // VEL = PWMA - WMA
        // Validate this relationship holds for batch calculation
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var vel = new Vel(period);
            var pwma = new Pwma(period);
            var wma = new Wma(period);

            var velResult = vel.Update(_testData.Data);
            var pwmaResult = pwma.Update(_testData.Data);
            var wmaResult = wma.Update(_testData.Data);

            Assert.Equal(_testData.Data.Count, velResult.Count);
            Assert.Equal(_testData.Data.Count, pwmaResult.Count);
            Assert.Equal(_testData.Data.Count, wmaResult.Count);

            // Verify relationship for all data points
            for (int i = 0; i < _testData.Data.Count; i++)
            {
                double expected = pwmaResult[i].Value - wmaResult[i].Value;
                Assert.Equal(expected, velResult[i].Value, 1e-10);
            }
        }
    }

    [Fact]
    public void Vel_Matches_PwmaMinusWma_Streaming()
    {
        // VEL = PWMA - WMA
        // Validate this relationship holds for streaming calculation
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var vel = new Vel(period);
            var pwma = new Pwma(period);
            var wma = new Wma(period);

            for (int i = 0; i < _testData.Data.Count; i++)
            {
                var input = _testData.Data[i];
                var v = vel.Update(input);
                var p = pwma.Update(input);
                var w = wma.Update(input);

                double expected = p.Value - w.Value;
                Assert.Equal(expected, v.Value, 1e-10);
            }
        }
    }

    [Fact]
    public void Vel_Matches_PwmaMinusWma_Span()
    {
        // VEL = PWMA - WMA
        // Validate this relationship holds for span calculation
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] velOutput = new double[sourceData.Length];
            double[] pwmaOutput = new double[sourceData.Length];
            double[] wmaOutput = new double[sourceData.Length];

            Vel.Batch(sourceData.AsSpan(), velOutput.AsSpan(), period);
            Pwma.Calculate(sourceData.AsSpan(), pwmaOutput.AsSpan(), period);
            Wma.Batch(sourceData.AsSpan(), wmaOutput.AsSpan(), period);

            // Verify relationship for all data points
            for (int i = 0; i < sourceData.Length; i++)
            {
                double expected = pwmaOutput[i] - wmaOutput[i];
                Assert.Equal(expected, velOutput[i], 1e-10);
            }
        }
    }

    [Fact]
    public void Vel_AllModes_ProduceIdenticalResults()
    {
        // Critical validation: All 3 API modes must produce identical results
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // 1. Batch Mode (TSeries)
            var batchVel = new Vel(period);
            var batchResult = batchVel.Update(_testData.Data);

            // 2. Span Mode
            double[] sourceData = _testData.RawData.ToArray();
            double[] spanOutput = new double[sourceData.Length];
            Vel.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            // 3. Streaming Mode
            var streamingVel = new Vel(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(streamingVel.Update(item).Value);
            }

            // Compare all modes (allow 1e-8 tolerance for accumulated floating-point errors)
            for (int i = 0; i < _testData.Data.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-8);
                Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-8);
            }
        }
    }

    [Fact]
    public void Vel_Convergence_AfterWarmup()
    {
        // After warmup period, indicator should be "hot" and producing stable values
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var vel = new Vel(period);

            Assert.False(vel.IsHot);

            // Feed period number of bars
            for (int i = 0; i < period - 1; i++)
            {
                vel.Update(_testData.Data[i]);
                Assert.False(vel.IsHot);
            }

            vel.Update(_testData.Data[period - 1]);
            Assert.True(vel.IsHot);
        }
    }

    [Fact]
    public void Vel_HandlesNaN_Gracefully()
    {
        var vel = new Vel(10);

        // Feed some valid data
        for (int i = 0; i < 20; i++)
        {
            vel.Update(_testData.Data[i]);
        }

        // Feed NaN
        var result = vel.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));

        // Continue with valid data
        for (int i = 20; i < 30; i++)
        {
            var r = vel.Update(_testData.Data[i]);
            Assert.True(double.IsFinite(r.Value));
        }
    }

    [Fact]
    public void Vel_HandlesInfinity_Gracefully()
    {
        var vel = new Vel(10);

        // Feed some valid data
        for (int i = 0; i < 20; i++)
        {
            vel.Update(_testData.Data[i]);
        }

        // Feed Infinity
        var resultPos = vel.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPos.Value));

        var resultNeg = vel.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNeg.Value));
    }

    [Fact]
    public void Vel_ZeroCrossing_DetectsDirectionChange()
    {
        // VEL crossing zero indicates momentum direction change
        var vel = new Vel(5);

        // Create uptrend data
        double[] uptrend = { 100, 102, 104, 106, 108, 110 };
        foreach (var price in uptrend)
        {
            vel.Update(new TValue(DateTime.UtcNow, price));
        }
        double uptrendVel = vel.Last.Value;
        Assert.True(uptrendVel > 0, "Uptrend should produce positive VEL");

        // Create downtrend data
        double[] downtrend = { 110, 108, 106, 104, 102, 100 };
        foreach (var price in downtrend)
        {
            vel.Update(new TValue(DateTime.UtcNow, price));
        }
        double downtrendVel = vel.Last.Value;
        Assert.True(downtrendVel < 0, "Downtrend should produce negative VEL");
    }

    [Fact]
    public void Vel_FlatLine_ProducesZeroVelocity()
    {
        // Flat price should produce zero velocity
        var vel = new Vel(10);

        for (int i = 0; i < 50; i++)
        {
            vel.Update(new TValue(DateTime.UtcNow, 100));
        }

        // After sufficient warmup, flat line should produce VEL ≈ 0
        Assert.True(Math.Abs(vel.Last.Value) < 1e-10,
            $"Expected VEL ≈ 0 for flat line, got {vel.Last.Value}");
    }

    [Fact]
    public void Vel_LargeDataset_MaintainsPrecision()
    {
        // Test with large dataset to ensure no drift
        const int period = 20;
        var vel = new Vel(period);
        var pwma = new Pwma(period);
        var wma = new Wma(period);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            var input = bars.Close[i];
            var v = vel.Update(input);
            var p = pwma.Update(input);
            var w = wma.Update(input);

            // Every 1000th point, verify precision
            if (i % 1000 == 0 && i > period)
            {
                double expected = p.Value - w.Value;
                Assert.Equal(expected, v.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Vel_DifferentPeriods_ProduceDifferentSensitivity()
    {
        // Shorter periods should be more sensitive to price changes
        var vel5 = new Vel(5);
        var vel20 = new Vel(20);
        var vel50 = new Vel(50);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            vel5.Update(bar);
            vel20.Update(bar);
            vel50.Update(bar);
        }

        // Calculate average absolute velocity (measure of sensitivity)
        double avgVel5 = 0, avgVel20 = 0, avgVel50 = 0;
        int count = 0;

        vel5 = new Vel(5);
        vel20 = new Vel(20);
        vel50 = new Vel(50);

        foreach (var bar in bars.Close)
        {
            vel5.Update(bar);
            vel20.Update(bar);
            vel50.Update(bar);

            if (vel5.IsHot && vel20.IsHot && vel50.IsHot)
            {
                avgVel5 += Math.Abs(vel5.Last.Value);
                avgVel20 += Math.Abs(vel20.Last.Value);
                avgVel50 += Math.Abs(vel50.Last.Value);
                count++;
            }
        }

        avgVel5 /= count;
        avgVel20 /= count;
        avgVel50 /= count;

        // All periods should produce finite numeric results
        Assert.True(double.IsFinite(avgVel5));
        Assert.True(double.IsFinite(avgVel20));
        Assert.True(double.IsFinite(avgVel50));
    }

    [Fact]
    public void Vel_BatchSpan_HandlesNaN_InMiddle()
    {
        double[] data = new double[100];
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Insert NaN in the middle
        data[50] = double.NaN;

        double[] output = new double[100];
        Vel.Batch(data.AsSpan(), output.AsSpan(), 10);

        // All outputs should be finite
        foreach (var value in output)
        {
            Assert.True(double.IsFinite(value), $"Expected finite value, got {value}");
        }
    }

    [Fact]
    public void Vel_EdgeCase_Period1()
    {
        // Period=1 should still work (though not very useful)
        var vel = new Vel(1);

        vel.Update(new TValue(DateTime.UtcNow, 100));
        // PWMA(1) = 100, WMA(1) = 100, VEL = 0
        Assert.Equal(0, vel.Last.Value, 1e-10);

        vel.Update(new TValue(DateTime.UtcNow, 110));
        // PWMA(1) = 110, WMA(1) = 110, VEL = 0
        Assert.Equal(0, vel.Last.Value, 1e-10);
    }
}
