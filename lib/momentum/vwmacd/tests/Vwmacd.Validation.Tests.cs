using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for VWMACD (Volume-Weighted MACD) against its own component primitives.
/// No external library implements VWMACD directly, so this validates the composition:
/// VWMACD = VWMA(fast) - VWMA(slow), Signal = EMA(VWMACD, signalPeriod).
/// </summary>
public sealed class VwmacdValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
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
    public void Vwmacd_MatchesVwmaComposition_Batch()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;
        const int signalPeriod = 9;

        var (vwmacd, signal, histogram) = Vwmacd.Batch(_testData.Bars, fastPeriod, slowPeriod, signalPeriod);

        // Recompute from primitives: VWMA(fast) - VWMA(slow), then EMA on the difference.
        var vwmaFast = Vwma.Batch(_testData.Bars, fastPeriod);
        var vwmaSlow = Vwma.Batch(_testData.Bars, slowPeriod);

        var expectedLine = new List<double>(_testData.Bars.Count);
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            expectedLine.Add(vwmaFast[i].Value - vwmaSlow[i].Value);
        }

        var ema = new Ema(signalPeriod);
        var expectedSignal = new List<double>(_testData.Bars.Count);
        var expectedHist = new List<double>(_testData.Bars.Count);
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            double s = ema.Update(new TValue(_testData.Bars[i].Time, expectedLine[i])).Value;
            expectedSignal.Add(s);
            expectedHist.Add(expectedLine[i] - s);
        }

        int count = _testData.Bars.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(expectedLine[i], vwmacd[i].Value, precision: 8);
            Assert.Equal(expectedSignal[i], signal[i].Value, precision: 8);
            Assert.Equal(expectedHist[i], histogram[i].Value, precision: 8);
        }

        _output.WriteLine("VWMACD composition validated: matches VWMA(fast)-VWMA(slow) with EMA signal");
    }

    [Fact]
    public void Vwmacd_StreamingMatchesBatch()
    {
        const int fastPeriod = 12;
        const int slowPeriod = 26;
        const int signalPeriod = 9;

        var indicator = new Vwmacd(fastPeriod, slowPeriod, signalPeriod);
        var streamLine = new List<double>(_testData.Bars.Count);
        var streamSignal = new List<double>(_testData.Bars.Count);
        var streamHist = new List<double>(_testData.Bars.Count);
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            indicator.Update(_testData.Bars[i], isNew: true);
            streamLine.Add(indicator.Last.Value);
            streamSignal.Add(indicator.Signal.Value);
            streamHist.Add(indicator.Histogram.Value);
        }

        var (batchLine, batchSignal, batchHist) = Vwmacd.Batch(_testData.Bars, fastPeriod, slowPeriod, signalPeriod);

        int count = _testData.Bars.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchLine[i].Value, streamLine[i], precision: 10);
            Assert.Equal(batchSignal[i].Value, streamSignal[i], precision: 10);
            Assert.Equal(batchHist[i].Value, streamHist[i], precision: 10);
        }

        _output.WriteLine("VWMACD streaming vs batch consistency validated");
    }
}
