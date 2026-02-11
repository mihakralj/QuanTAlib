using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class LowestValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public LowestValidationTests(ITestOutputHelper output)
    {
        _output = output;
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

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Lowest (batch TSeries)
            var lowest = new Lowest(period);
            var qResult = lowest.Update(_testData.Data);

            // Calculate TA-Lib MIN
            var retCode = TALib.Functions.Min<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MinLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("Lowest Batch(TSeries) validated successfully against TA-Lib MIN");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Lowest (streaming)
            var lowest = new Lowest(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(lowest.Update(item).Value);
            }

            // Calculate TA-Lib MIN
            var retCode = TALib.Functions.Min<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MinLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("Lowest Streaming validated successfully against TA-Lib MIN");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] sourceData = _testData.RawData.ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Lowest (Span API)
            double[] qOutput = new double[sourceData.Length];
            Lowest.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib MIN
            var retCode = TALib.Functions.Min<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MinLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("Lowest Span validated successfully against TA-Lib MIN");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Lowest (batch TSeries)
            var lowest = new Lowest(period);
            var qResult = lowest.Update(_testData.Data);

            // Calculate Tulip min
            var minIndicator = Tulip.Indicators.min;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            minIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("Lowest Batch(TSeries) validated successfully against Tulip min");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Lowest (streaming)
            var lowest = new Lowest(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(lowest.Update(item).Value);
            }

            // Calculate Tulip min
            var minIndicator = Tulip.Indicators.min;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            minIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback);
        }
        _output.WriteLine("Lowest Streaming validated successfully against Tulip min");
    }

    [Fact]
    public void Validate_KnownValues()
    {
        // Test with simple known sequence
        double[] data = { 10, 5, 8, 2, 9, 1, 7, 4, 6, 3 };
        int period = 3;

        // Expected: first=10, second=min(10,5)=5, then sliding min of last 3
        // [10] -> 10
        // [10,5] -> 5
        // [10,5,8] -> 5
        // [5,8,2] -> 2
        // [8,2,9] -> 2
        // [2,9,1] -> 1
        // [9,1,7] -> 1
        // [1,7,4] -> 1
        // [7,4,6] -> 4
        // [4,6,3] -> 3
        double[] expected = { 10, 5, 5, 2, 2, 1, 1, 1, 4, 3 };

        var lowest = new Lowest(period);
        for (int i = 0; i < data.Length; i++)
        {
            var result = lowest.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 10);
        }
        _output.WriteLine("Lowest validated with known values");
    }
}
