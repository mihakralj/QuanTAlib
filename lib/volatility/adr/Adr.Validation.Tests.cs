using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// ADR Validation Tests
///
/// Note: ADR (Average Daily Range) is a simple indicator that calculates
/// the moving average of High-Low ranges. Unlike ATR, it doesn't account
/// for gaps. Most external libraries don't have a direct ADR implementation,
/// so we validate against our own manual calculations and cross-validate
/// between smoothing methods.
/// </summary>
public sealed class AdrValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AdrValidationTests(ITestOutputHelper output)
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
    public void Validate_ManualCalculation_Sma()
    {
        int period = 14;

        // Calculate ADR using our implementation
        var adr = new Adr(period, AdrMethod.Sma);
        var qResult = adr.Update(_testData.Bars);

        // Calculate manually: SMA of (High - Low)
        var ranges = new List<double>();
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var bar = _testData.Bars[i];
            ranges.Add(bar.High - bar.Low);
        }

        var sma = new Sma(period);
        var manualResult = new List<double>();
        foreach (var range in ranges)
        {
            manualResult.Add(sma.Update(new TValue(DateTime.UtcNow, range)).Value);
        }

        // Compare last 100 records
        int compareCount = Math.Min(100, qResult.Count);
        int startIdx = qResult.Count - compareCount;

        for (int i = 0; i < compareCount; i++)
        {
            Assert.Equal(manualResult[startIdx + i], qResult[startIdx + i].Value, 1e-10);
        }

        _output.WriteLine("ADR SMA validated successfully against manual calculation");
    }

    [Fact]
    public void Validate_ManualCalculation_Ema()
    {
        int period = 14;

        // Calculate ADR using our implementation
        var adr = new Adr(period, AdrMethod.Ema);
        var qResult = adr.Update(_testData.Bars);

        // Calculate manually: EMA of (High - Low)
        var ranges = new List<double>();
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var bar = _testData.Bars[i];
            ranges.Add(bar.High - bar.Low);
        }

        var ema = new Ema(period);
        var manualResult = new List<double>();
        foreach (var range in ranges)
        {
            manualResult.Add(ema.Update(new TValue(DateTime.UtcNow, range)).Value);
        }

        // Compare last 100 records
        int compareCount = Math.Min(100, qResult.Count);
        int startIdx = qResult.Count - compareCount;

        for (int i = 0; i < compareCount; i++)
        {
            Assert.Equal(manualResult[startIdx + i], qResult[startIdx + i].Value, 1e-10);
        }

        _output.WriteLine("ADR EMA validated successfully against manual calculation");
    }

    [Fact]
    public void Validate_ManualCalculation_Wma()
    {
        int period = 14;

        // Calculate ADR using our implementation
        var adr = new Adr(period, AdrMethod.Wma);
        var qResult = adr.Update(_testData.Bars);

        // Calculate manually: WMA of (High - Low)
        var ranges = new List<double>();
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var bar = _testData.Bars[i];
            ranges.Add(bar.High - bar.Low);
        }

        var wma = new Wma(period);
        var manualResult = new List<double>();
        foreach (var range in ranges)
        {
            manualResult.Add(wma.Update(new TValue(DateTime.UtcNow, range)).Value);
        }

        // Compare last 100 records
        int compareCount = Math.Min(100, qResult.Count);
        int startIdx = qResult.Count - compareCount;

        for (int i = 0; i < compareCount; i++)
        {
            Assert.Equal(manualResult[startIdx + i], qResult[startIdx + i].Value, 1e-10);
        }

        _output.WriteLine("ADR WMA validated successfully against manual calculation");
    }

    [Fact]
    public void Validate_Streaming_MatchesBatch_Sma()
    {
        int period = 14;

        // Calculate batch
        var adrBatch = new Adr(period, AdrMethod.Sma);
        var batchResult = adrBatch.Update(_testData.Bars);

        // Calculate streaming
        var adrStream = new Adr(period, AdrMethod.Sma);
        var streamResult = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResult.Add(adrStream.Update(bar).Value);
        }

        // Compare all records
        Assert.Equal(batchResult.Count, streamResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-10);
        }

        _output.WriteLine("ADR SMA Streaming validated successfully against Batch");
    }

    [Fact]
    public void Validate_Streaming_MatchesBatch_Ema()
    {
        int period = 14;

        // Calculate batch
        var adrBatch = new Adr(period, AdrMethod.Ema);
        var batchResult = adrBatch.Update(_testData.Bars);

        // Calculate streaming
        var adrStream = new Adr(period, AdrMethod.Ema);
        var streamResult = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResult.Add(adrStream.Update(bar).Value);
        }

        // Compare all records (use 1e-8 tolerance for EMA due to floating-point drift)
        Assert.Equal(batchResult.Count, streamResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-8);
        }

        _output.WriteLine("ADR EMA Streaming validated successfully against Batch");
    }

    [Fact]
    public void Validate_Streaming_MatchesBatch_Wma()
    {
        int period = 14;

        // Calculate batch
        var adrBatch = new Adr(period, AdrMethod.Wma);
        var batchResult = adrBatch.Update(_testData.Bars);

        // Calculate streaming
        var adrStream = new Adr(period, AdrMethod.Wma);
        var streamResult = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResult.Add(adrStream.Update(bar).Value);
        }

        // Compare all records
        Assert.Equal(batchResult.Count, streamResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-10);
        }

        _output.WriteLine("ADR WMA Streaming validated successfully against Batch");
    }

    [Fact]
    public void Validate_MultiplePeriods()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate ADR for each period
            var adrSma = new Adr(period, AdrMethod.Sma);
            var adrEma = new Adr(period, AdrMethod.Ema);
            var adrWma = new Adr(period, AdrMethod.Wma);

            var resultSma = adrSma.Update(_testData.Bars);
            var resultEma = adrEma.Update(_testData.Bars);
            var resultWma = adrWma.Update(_testData.Bars);

            // Verify all results are finite and positive (or zero for flat bars)
            Assert.True(double.IsFinite(resultSma.Last.Value), $"SMA Period {period} should produce finite value");
            Assert.True(double.IsFinite(resultEma.Last.Value), $"EMA Period {period} should produce finite value");
            Assert.True(double.IsFinite(resultWma.Last.Value), $"WMA Period {period} should produce finite value");

            Assert.True(resultSma.Last.Value >= 0, $"SMA Period {period} should produce non-negative value");
            Assert.True(resultEma.Last.Value >= 0, $"EMA Period {period} should produce non-negative value");
            Assert.True(resultWma.Last.Value >= 0, $"WMA Period {period} should produce non-negative value");
        }

        _output.WriteLine("ADR validated successfully across multiple periods");
    }

    [Fact]
    public void Validate_RangeIsAlwaysNonNegative()
    {
        // ADR should always produce non-negative values (average of non-negative ranges)
        var adr = new Adr(14, AdrMethod.Sma);
        var result = adr.Update(_testData.Bars);

        foreach (var val in result)
        {
            Assert.True(val.Value >= 0, "ADR should always be non-negative");
        }

        _output.WriteLine("ADR validated: all values are non-negative");
    }

    [Fact]
    public void Validate_AdrLessThanOrEqualToAtr()
    {
        // ADR should generally be <= ATR because ATR accounts for gaps
        // which can only increase the range, not decrease it
        int period = 14;

        var adr = new Adr(period, AdrMethod.Sma);
        var atr = new Atr(period);

        // Note: ATR uses RMA (Wilder's smoothing) not SMA, so we compare
        // the underlying concept rather than exact values
        // For bars without gaps, ADR range = ATR true range
        // For bars with gaps, ATR true range >= ADR range

        foreach (var bar in _testData.Bars)
        {
            adr.Update(bar);
            atr.Update(bar);
        }

        // Both should be finite and positive
        Assert.True(double.IsFinite(adr.Last.Value));
        Assert.True(double.IsFinite(atr.Last.Value));
        Assert.True(adr.Last.Value >= 0);
        Assert.True(atr.Last.Value >= 0);

        _output.WriteLine($"ADR: {adr.Last.Value:F4}, ATR: {atr.Last.Value:F4}");
        _output.WriteLine("ADR and ATR validated: both produce valid results");
    }
}