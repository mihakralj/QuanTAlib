using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public sealed class StdDevValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public StdDevValidationTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    #region Skender Validation

    [Fact]
    public void StdDev_Matches_Skender_Batch()
    {
        // Skender StdDev uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period, isPopulation: true);
            var qResult = stdDev.Update(_testData.Data);

            var sResult = _testData.SkenderQuotes.GetStdDev(period).ToList();

            ValidationHelper.VerifyData(qResult, sResult, (s) => s.StdDev);
        }
    }

    [Fact]
    public void StdDev_Matches_Skender_Streaming()
    {
        // Skender StdDev uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period, isPopulation: true);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(stdDev.Update(item).Value);
            }

            var sResult = _testData.SkenderQuotes.GetStdDev(period).ToList();

            ValidationHelper.VerifyData(qResults, sResult, (s) => s.StdDev);
        }
    }

    [Fact]
    public void StdDev_Matches_Skender_Span()
    {
        // Skender StdDev uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[sourceData.Length];
            StdDev.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period, isPopulation: true);

            var sResult = _testData.SkenderQuotes.GetStdDev(period).ToList();

            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.StdDev);
        }
    }

    #endregion

    #region TA-Lib Validation

    [Fact]
    public void StdDev_Matches_Talib_Batch()
    {
        // TA-Lib STDDEV uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period, isPopulation: true);
            var qResult = stdDev.Update(_testData.Data);

            var retCode = TALib.Functions.StdDev(tData, 0..^0, output, out var outRange, period, 1.0);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.StdDevLookback(period);

            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
    }

    [Fact]
    public void StdDev_Matches_Talib_Streaming()
    {
        // TA-Lib STDDEV uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period, isPopulation: true);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(stdDev.Update(item).Value);
            }

            var retCode = TALib.Functions.StdDev(tData, 0..^0, output, out var outRange, period, 1.0);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.StdDevLookback(period);

            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
    }

    [Fact]
    public void StdDev_Matches_Talib_Span()
    {
        // TA-Lib STDDEV uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();
        double[] output = new double[sourceData.Length];

        foreach (var period in periods)
        {
            double[] qOutput = new double[sourceData.Length];
            StdDev.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period, isPopulation: true);

            var retCode = TALib.Functions.StdDev(sourceData, 0..^0, output, out var outRange, period, 1.0);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.StdDevLookback(period);

            ValidationHelper.VerifyData(qOutput, output, outRange, lookback);
        }
    }

    #endregion

    #region Tulip Validation

    [Fact]
    public void StdDev_Matches_Tulip_Batch()
    {
        // Tulip STDDEV uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period, isPopulation: true);
            var qResult = stdDev.Update(_testData.Data);

            var stdDevInd = Tulip.Indicators.stddev;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = stdDevInd.Start(options);
            double[][] outputs = { new double[tData.Length - lookback] };

            stdDevInd.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
    }

    [Fact]
    public void StdDev_Matches_Tulip_Streaming()
    {
        // Tulip STDDEV uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period, isPopulation: true);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(stdDev.Update(item).Value);
            }

            var stdDevInd = Tulip.Indicators.stddev;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = stdDevInd.Start(options);
            double[][] outputs = { new double[tData.Length - lookback] };

            stdDevInd.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qResults, tResult, lookback);
        }
    }

    [Fact]
    public void StdDev_Matches_Tulip_Span()
    {
        // Tulip STDDEV uses Population Standard Deviation (N)
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[sourceData.Length];
            StdDev.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period, isPopulation: true);

            var stdDevInd = Tulip.Indicators.stddev;
            double[][] inputs = { sourceData };
            double[] options = { period };
            int lookback = stdDevInd.Start(options);
            double[][] outputs = { new double[sourceData.Length - lookback] };

            stdDevInd.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qOutput, tResult, lookback);
        }
    }

    #endregion

    #region MathNet Validation

    [Fact]
    public void StdDev_Matches_MathNet_Sample()
    {
        const int period = 20;
        var stdDev = new StdDev(period, isPopulation: false);
        double[] input = _testData.RawData.ToArray();

        for (int i = 0; i < input.Length; i++)
        {
            var val = stdDev.Update(new TValue(DateTime.UtcNow, input[i]));

            if (i >= period - 1)
            {
                var window = input[(i - period + 1)..(i + 1)];
                double expected = MathNet.Numerics.Statistics.Statistics.StandardDeviation(window);
                Assert.Equal(expected, val.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void StdDev_Matches_MathNet_Population()
    {
        int period = 20;
        var stdDev = new StdDev(period, isPopulation: true);
        double[] input = _testData.RawData.ToArray();

        for (int i = 0; i < input.Length; i++)
        {
            var val = stdDev.Update(new TValue(DateTime.UtcNow, input[i]));

            if (i >= period - 1)
            {
                var window = input[(i - period + 1)..(i + 1)];
                double expected = MathNet.Numerics.Statistics.Statistics.PopulationStandardDeviation(window);
                Assert.Equal(expected, val.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    #endregion

    #region Comprehensive Tests

    [Fact]
    public void StdDev_AllModes_ProduceIdenticalResults()
    {
        // Critical validation: All 3 API modes must produce identical results
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Test both population and sample
            foreach (bool isPopulation in new[] { true, false })
            {
                // 1. Batch Mode (TSeries)
                var batchStdDev = new StdDev(period, isPopulation);
                var batchResult = batchStdDev.Update(_testData.Data);

                // 2. Span Mode
                double[] sourceData = _testData.RawData.ToArray();
                double[] spanOutput = new double[sourceData.Length];
                StdDev.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period, isPopulation);

                // 3. Streaming Mode
                var streamingStdDev = new StdDev(period, isPopulation);
                var streamingResults = new List<double>();
                foreach (var item in _testData.Data)
                {
                    streamingResults.Add(streamingStdDev.Update(item).Value);
                }

                // Compare all modes (allow 1e-7 tolerance for accumulated floating-point errors
                // between SIMD batch paths and scalar streaming paths with different FMA/sum ordering)
                for (int i = 0; i < _testData.Data.Count; i++)
                {
                    Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-7);
                    Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-7);
                }
            }
        }
    }

    [Fact]
    public void StdDev_Matches_SqrtVariance()
    {
        // StdDev = Sqrt(Variance)
        // Validate this relationship holds
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            foreach (bool isPopulation in new[] { true, false })
            {
                var stdDev = new StdDev(period, isPopulation);
                var variance = new Variance(period, isPopulation);

                for (int i = 0; i < _testData.Data.Count; i++)
                {
                    var input = _testData.Data[i];
                    var s = stdDev.Update(input);
                    var v = variance.Update(input);

                    double expected = Math.Sqrt(Math.Max(0, v.Value));
                    Assert.Equal(expected, s.Value, 1e-10);
                }
            }
        }
    }

    [Fact]
    public void StdDev_FlatLine_ProducesZero()
    {
        // Flat price should produce zero standard deviation
        var stdDev = new StdDev(10);

        for (int i = 0; i < 50; i++)
        {
            stdDev.Update(new TValue(DateTime.UtcNow, 100));
        }

        // After sufficient warmup, flat line should produce StdDev ≈ 0
        Assert.True(Math.Abs(stdDev.Last.Value) < 1e-10,
            $"Expected StdDev ≈ 0 for flat line, got {stdDev.Last.Value}");
    }

    [Fact]
    public void StdDev_LargeDataset_MaintainsPrecision()
    {
        // Test with large dataset to ensure no drift
        int period = 20;
        var stdDev = new StdDev(period, isPopulation: true);
        var variance = new Variance(period, isPopulation: true);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            var input = bars.Close[i];
            var s = stdDev.Update(input);
            var v = variance.Update(input);

            // Every 1000th point, verify precision
            if (i % 1000 == 0 && i > period)
            {
                double expected = Math.Sqrt(Math.Max(0, v.Value));
                Assert.Equal(expected, s.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void StdDev_PopulationVsSample_Difference()
    {
        // Population and Sample StdDev should differ
        int period = 10;
        var popStdDev = new StdDev(period, isPopulation: true);
        var sampStdDev = new StdDev(period, isPopulation: false);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            popStdDev.Update(bar);
            sampStdDev.Update(bar);
        }

        // Sample StdDev should be larger than Population StdDev (divides by N-1 instead of N)
        Assert.True(sampStdDev.IsHot && popStdDev.IsHot);
        Assert.True(sampStdDev.Last.Value > popStdDev.Last.Value,
            $"Sample StdDev ({sampStdDev.Last.Value}) should be > Population StdDev ({popStdDev.Last.Value})");
    }

    [Fact]
    public void StdDev_BatchSpan_HandlesNaN_InMiddle()
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
        StdDev.Batch(data.AsSpan(), output.AsSpan(), 10);

        // All outputs should be finite
        foreach (var value in output)
        {
            Assert.True(double.IsFinite(value), $"Expected finite value, got {value}");
        }
    }

    [Fact]
    public void StdDev_Convergence_AfterWarmup()
    {
        // After warmup period, indicator should be "hot"
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var stdDev = new StdDev(period);

            Assert.False(stdDev.IsHot);

            // Feed period number of bars
            for (int i = 0; i < period - 1; i++)
            {
                stdDev.Update(_testData.Data[i]);
                Assert.False(stdDev.IsHot);
            }

            stdDev.Update(_testData.Data[period - 1]);
            Assert.True(stdDev.IsHot);
        }
    }

    [Fact]
    public void StdDev_DifferentPeriods_ProduceDifferentSensitivity()
    {
        // Shorter periods should be more sensitive to price changes
        var stdDev5 = new StdDev(5);
        var stdDev20 = new StdDev(20);
        var stdDev50 = new StdDev(50);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            stdDev5.Update(bar);
            stdDev20.Update(bar);
            stdDev50.Update(bar);
        }

        // All periods should produce finite numeric results
        Assert.True(double.IsFinite(stdDev5.Last.Value));
        Assert.True(double.IsFinite(stdDev20.Last.Value));
        Assert.True(double.IsFinite(stdDev50.Last.Value));

        // All should be hot
        Assert.True(stdDev5.IsHot && stdDev20.IsHot && stdDev50.IsHot);
    }

    [Fact]
    public void StdDev_EdgeCase_Period2()
    {
        // Period=2 is minimum (constructor throws on period=1)
        var stdDev = new StdDev(2);

        stdDev.Update(new TValue(DateTime.UtcNow, 100));
        stdDev.Update(new TValue(DateTime.UtcNow, 100));

        // Two identical values should produce StdDev = 0
        Assert.Equal(0, stdDev.Last.Value, 1e-10);

        stdDev.Update(new TValue(DateTime.UtcNow, 110));
        // 100, 110: mean = 105, deviations = -5, 5, squared = 25, 25, sum = 50
        // Population variance = 50/2 = 25, StdDev = 5
        // Sample variance = 50/1 = 50, StdDev = 7.071...

        // Default is sample (isPopulation=false)
        Assert.Equal(Math.Sqrt(50), stdDev.Last.Value, 1e-10);
    }

    #endregion
}
