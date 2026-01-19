using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class MidpointValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MidpointValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib Midpoint (batch TSeries)
            var midpoint = new Midpoint(period);
            var qResult = midpoint.Update(_testData.Data);

            // Calculate TA-Lib MIDPOINT
            var retCode = TALib.Functions.MidPoint<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MidPointLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("Midpoint Batch(TSeries) validated successfully against TA-Lib MIDPOINT");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Midpoint (streaming)
            var midpoint = new Midpoint(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(midpoint.Update(item).Value);
            }

            // Calculate TA-Lib MIDPOINT
            var retCode = TALib.Functions.MidPoint<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MidPointLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("Midpoint Streaming validated successfully against TA-Lib MIDPOINT");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        double[] sourceData = _testData.RawData.ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Midpoint (Span API)
            double[] qOutput = new double[sourceData.Length];
            Midpoint.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib MIDPOINT
            var retCode = TALib.Functions.MidPoint<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.MidPointLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("Midpoint Span validated successfully against TA-Lib MIDPOINT");
    }

    [Fact]
    public void Validate_KnownValues()
    {
        // Test with simple known sequence
        double[] data = { 1, 5, 3, 8, 2, 9, 4, 7, 6, 10 };
        int period = 3;

        // For each window:
        // [1] -> (1+1)/2 = 1
        // [1,5] -> (5+1)/2 = 3
        // [1,5,3] -> (5+1)/2 = 3
        // [5,3,8] -> (8+3)/2 = 5.5
        // [3,8,2] -> (8+2)/2 = 5
        // [8,2,9] -> (9+2)/2 = 5.5
        // [2,9,4] -> (9+2)/2 = 5.5
        // [9,4,7] -> (9+4)/2 = 6.5
        // [4,7,6] -> (7+4)/2 = 5.5
        // [7,6,10] -> (10+6)/2 = 8
        double[] expected = { 1, 3, 3, 5.5, 5, 5.5, 5.5, 6.5, 5.5, 8 };

        var midpoint = new Midpoint(period);
        for (int i = 0; i < data.Length; i++)
        {
            var result = midpoint.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 10);
        }
        _output.WriteLine("Midpoint validated with known values");
    }

    [Fact]
    public void Validate_ConsistencyWithHighestLowest()
    {
        // Verify that Midpoint = (Highest + Lowest) / 2
        int period = 14;
        var midpoint = new Midpoint(period);
        var highest = new Highest(period);
        var lowest = new Lowest(period);

        foreach (var item in _testData.Data)
        {
            var midResult = midpoint.Update(item);
            var highResult = highest.Update(item);
            var lowResult = lowest.Update(item);

            double expected = (highResult.Value + lowResult.Value) * 0.5;
            Assert.Equal(expected, midResult.Value, precision: 10);
        }
        _output.WriteLine("Midpoint consistency validated: equals (Highest + Lowest) / 2");
    }

    [Fact]
    public void Validate_ConstantInput()
    {
        // For constant input, midpoint should equal that constant
        double constant = 42.5;
        int period = 10;
        var midpoint = new Midpoint(period);

        for (int i = 0; i < 50; i++)
        {
            var result = midpoint.Update(new TValue(DateTime.UtcNow, constant));
            Assert.Equal(constant, result.Value, precision: 10);
        }
        _output.WriteLine("Midpoint validated with constant input");
    }
}
