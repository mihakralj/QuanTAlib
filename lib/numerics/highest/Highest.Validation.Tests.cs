using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class HighestValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public HighestValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib Highest (batch TSeries)
            var highest = new Highest(period);
            var qResult = highest.Update(_testData.Data);

            // Calculate TA-Lib MAX
            var retCode = TALib.Functions.Max<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MaxLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("Highest Batch(TSeries) validated successfully against TA-Lib MAX");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Highest (streaming)
            var highest = new Highest(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(highest.Update(item).Value);
            }

            // Calculate TA-Lib MAX
            var retCode = TALib.Functions.Max<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MaxLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("Highest Streaming validated successfully against TA-Lib MAX");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] sourceData = _testData.RawData.ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Highest (Span API)
            double[] qOutput = new double[sourceData.Length];
            Highest.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib MAX
            var retCode = TALib.Functions.Max<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MaxLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("Highest Span validated successfully against TA-Lib MAX");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Highest (batch TSeries)
            var highest = new Highest(period);
            var qResult = highest.Update(_testData.Data);

            // Calculate Tulip max
            var maxIndicator = Tulip.Indicators.max;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            maxIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("Highest Batch(TSeries) validated successfully against Tulip max");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Highest (streaming)
            var highest = new Highest(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(highest.Update(item).Value);
            }

            // Calculate Tulip max
            var maxIndicator = Tulip.Indicators.max;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            maxIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback);
        }
        _output.WriteLine("Highest Streaming validated successfully against Tulip max");
    }

    [Fact]
    public void Validate_KnownValues()
    {
        // Test with simple known sequence
        double[] data = { 1, 5, 3, 8, 2, 9, 4, 7, 6, 10 };
        int period = 3;

        // Expected: first=1, second=max(1,5)=5, then sliding max of last 3
        // [1] -> 1
        // [1,5] -> 5
        // [1,5,3] -> 5
        // [5,3,8] -> 8
        // [3,8,2] -> 8
        // [8,2,9] -> 9
        // [2,9,4] -> 9
        // [9,4,7] -> 9
        // [4,7,6] -> 7
        // [7,6,10] -> 10
        double[] expected = { 1, 5, 5, 8, 8, 9, 9, 9, 7, 10 };

        var highest = new Highest(period);
        for (int i = 0; i < data.Length; i++)
        {
            var result = highest.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 10);
        }
        _output.WriteLine("Highest validated with known values");
    }
}
