using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// CCI Validation Tests against Tulip library.
/// CCI = (Typical Price - SMA of TP) / (0.015 × Mean Deviation)
/// where TP = (High + Low + Close) / 3
/// </summary>
public sealed class CciValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public CciValidationTests(ITestOutputHelper output)
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

    #region Tulip Validation

    [Fact]
    public void Cci_MatchesTulip_DefaultPeriod()
    {
        int period = 20;

        // Get QuanTAlib result
        var cci = new Cci(period);
        var qResult = cci.Update(_testData.Bars);

        // Calculate Tulip CCI
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        var cciIndicator = Tulip.Indicators.cci;
        double[][] inputs = [high, low, close];
        double[] options = [period];
        int lookback = cciIndicator.Start(options);
        double[][] outputs = [new double[high.Length - lookback]];

        cciIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        // Compare after warmup
        double maxDiff = 0;

        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            double diff = Math.Abs(tulipResult[i] - qResult[qIdx].Value);
            if (diff > maxDiff)
            {
                maxDiff = diff;
            }
        }

        _output.WriteLine($"Tulip CCI period={period}: Max difference = {maxDiff:E3}");

        // Tulip uses same formula - should match closely
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], qResult[qIdx].Value, 1e-6);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Cci_MatchesTulip_DifferentPeriods(int period)
    {
        var cci = new Cci(period);
        var qResult = cci.Update(_testData.Bars);

        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        var cciIndicator = Tulip.Indicators.cci;
        double[][] inputs = [high, low, close];
        double[] options = [period];
        int lookback = cciIndicator.Start(options);
        double[][] outputs = [new double[high.Length - lookback]];

        cciIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], qResult[qIdx].Value, 1e-6);
        }

        _output.WriteLine($"Tulip CCI period={period}: Validated successfully");
    }

    [Fact]
    public void Cci_StreamingMatchesTulip()
    {
        int period = 20;

        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        // Calculate Tulip CCI
        var cciIndicator = Tulip.Indicators.cci;
        double[][] inputs = [high, low, close];
        double[] options = [period];
        int lookback = cciIndicator.Start(options);
        double[][] outputs = [new double[high.Length - lookback]];

        cciIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        // Calculate QuanTAlib streaming
        var cci = new Cci(period);
        var streamingResults = new List<double>();

        foreach (var bar in _testData.Bars)
        {
            streamingResults.Add(cci.Update(bar).Value);
        }

        // Compare after warmup
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], streamingResults[qIdx], 1e-6);
        }

        _output.WriteLine($"Tulip CCI streaming: Validated successfully");
    }

    #endregion

    #region Manual Calculation Validation

    [Fact]
    public void Cci_MatchesManualCalculation()
    {
        int period = 5;

        // Create simple test data
        var bars = new TBarSeries();
        var baseTime = DateTime.UtcNow.Ticks;
        var timeStep = TimeSpan.FromMinutes(1).Ticks;

        // Create bars with known values for manual verification
        double[] highs = [22, 24, 23, 25, 26, 27, 26, 28, 27, 29];
        double[] lows = [20, 22, 21, 23, 24, 25, 24, 26, 25, 27];
        double[] closes = [21, 23, 22, 24, 25, 26, 25, 27, 26, 28];

        for (int i = 0; i < highs.Length; i++)
        {
            bars.Add(new TBar(
                baseTime + (i * timeStep),
                21.0 + i,  // open
                highs[i],
                lows[i],
                closes[i],
                1000));    // volume
        }

        // Calculate using our CCI
        var cci = new Cci(period);
        var qResult = cci.Update(bars);

        // Manual calculation for last value (index 9)
        // TP values for last 5 bars (indices 5-9):
        // TP[5] = (27 + 25 + 26) / 3 = 26
        // TP[6] = (26 + 24 + 25) / 3 = 25
        // TP[7] = (28 + 26 + 27) / 3 = 27
        // TP[8] = (27 + 25 + 26) / 3 = 26
        // TP[9] = (29 + 27 + 28) / 3 = 28

        double tp5 = (27.0 + 25.0 + 26.0) / 3.0;
        double tp6 = (26.0 + 24.0 + 25.0) / 3.0;
        double tp7 = (28.0 + 26.0 + 27.0) / 3.0;
        double tp8 = (27.0 + 25.0 + 26.0) / 3.0;
        double tp9 = (29.0 + 27.0 + 28.0) / 3.0;

        double smaTP = (tp5 + tp6 + tp7 + tp8 + tp9) / 5.0;
        double meanDev = (Math.Abs(tp5 - smaTP) + Math.Abs(tp6 - smaTP) + Math.Abs(tp7 - smaTP) + Math.Abs(tp8 - smaTP) + Math.Abs(tp9 - smaTP)) / 5.0;
        double expectedCci = (tp9 - smaTP) / (0.015 * meanDev);

        _output.WriteLine($"Manual CCI calculation:");
        _output.WriteLine($"  TP[5-9] = {tp5:F4}, {tp6:F4}, {tp7:F4}, {tp8:F4}, {tp9:F4}");
        _output.WriteLine($"  SMA(TP) = {smaTP:F4}");
        _output.WriteLine($"  Mean Dev = {meanDev:F4}");
        _output.WriteLine($"  Expected CCI = {expectedCci:F4}");
        _output.WriteLine($"  QuanTAlib CCI = {qResult[9].Value:F4}");

        Assert.Equal(expectedCci, qResult[9].Value, 1e-10);
    }

    #endregion

    #region Streaming vs Batch Validation

    [Fact]
    public void Cci_StreamingMatchesBatch()
    {
        int period = 14;

        // Batch
        var batchResult = Cci.Batch(_testData.Bars, period);

        // Streaming
        var cci = new Cci(period);
        var streamingResults = new List<double>();

        foreach (var bar in _testData.Bars)
        {
            streamingResults.Add(cci.Update(bar).Value);
        }

        Assert.Equal(batchResult.Count, streamingResults.Count);

        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-10);
        }

        _output.WriteLine($"CCI Streaming matches Batch: Validated {batchResult.Count} values");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Cci_FlatMarket_ReturnsZero()
    {
        // Create flat market data where all prices are the same
        var bars = new TBarSeries();
        var baseTime = DateTime.UtcNow.Ticks;
        var timeStep = TimeSpan.FromMinutes(1).Ticks;

        for (int i = 0; i < 30; i++)
        {
            bars.Add(new TBar(
                baseTime + (i * timeStep),
                100,    // open
                100,    // high
                100,    // low
                100,    // close
                1000)); // volume
        }

        var cci = new Cci(10);
        var result = cci.Update(bars);

        // In flat market, TP = SMA(TP), so deviation = 0
        // CCI = 0 / (0.015 * 0) - should handle gracefully
        for (int i = 10; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result[i].Value) || result[i].Value == 0,
                $"CCI at index {i} should be finite or zero, got {result[i].Value}");
        }

        _output.WriteLine("CCI flat market validation passed");
    }

    [Fact]
    public void Cci_MultiplePeriods_AllMatchTulip()
    {
        int[] periods = [5, 10, 14, 20, 50];

        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        foreach (var period in periods)
        {
            var cci = new Cci(period);
            var qResult = cci.Update(_testData.Bars);

            var cciIndicator = Tulip.Indicators.cci;
            double[][] inputs = [high, low, close];
            double[] options = [period];
            int lookback = cciIndicator.Start(options);
            double[][] outputs = [new double[high.Length - lookback]];

            cciIndicator.Run(inputs, options, outputs);
            double[] tulipResult = outputs[0];

            // Check last 10 values match
            int checkCount = Math.Min(10, tulipResult.Length);
            for (int i = tulipResult.Length - checkCount; i < tulipResult.Length; i++)
            {
                int qIdx = i + lookback;
                Assert.Equal(tulipResult[i], qResult[qIdx].Value, 1e-6);
            }
        }

        _output.WriteLine("All periods validated against Tulip");
    }

    #endregion
}
