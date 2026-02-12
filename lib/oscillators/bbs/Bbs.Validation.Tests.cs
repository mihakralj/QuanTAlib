using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class BbsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public BbsValidationTests(ITestOutputHelper output)
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
    public void Validate_Streaming_Batch_Span_Agree()
    {
        int bbPeriod = 20;
        double bbMult = 2.0;
        int kcPeriod = 20;
        double kcMult = 1.5;

        // Streaming
        var streaming = new Bbs(bbPeriod, bbMult, kcPeriod, kcMult);
        var streamValues = new List<double>(_testData.Bars.Count);
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            streamValues.Add(streaming.Update(_testData.Bars[i]).Value);
        }

        // Batch (TBarSeries)
        TSeries batchSeries = Bbs.Batch(_testData.Bars, bbPeriod, bbMult, kcPeriod, kcMult);

        // Span
        double[] spanOutput = new double[_testData.Bars.Count];
        Bbs.Batch(_testData.Bars.HighValues, _testData.Bars.LowValues, _testData.Bars.CloseValues,
                  spanOutput.AsSpan(), bbPeriod, bbMult);

        // Compare last 200 samples for stability
        int start = Math.Max(0, spanOutput.Length - 200);
        for (int i = start; i < spanOutput.Length; i++)
        {
            Assert.Equal(batchSeries[i].Value, streamValues[i], 7);
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 7);
        }

        _output.WriteLine("BBS validation: streaming, batch, and span outputs agree.");
    }

    [Fact]
    public void Validate_SpanWithSqueeze_MatchesStreaming()
    {
        int bbPeriod = 20;
        double bbMult = 2.0;
        int kcPeriod = 20;
        double kcMult = 1.5;

        // Streaming - collect squeeze states
        var streaming = new Bbs(bbPeriod, bbMult, kcPeriod, kcMult);
        var streamBandwidths = new List<double>(_testData.Bars.Count);
        var streamSqueezes = new List<bool>(_testData.Bars.Count);
        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            streaming.Update(_testData.Bars[i]);
            streamBandwidths.Add(streaming.Last.Value);
            streamSqueezes.Add(streaming.SqueezeOn);
        }

        // Span with squeeze
        int len = _testData.Bars.Count;
        double[] spanBw = new double[len];
        bool[] spanSq = new bool[len];
        Bbs.Batch(_testData.Bars.HighValues, _testData.Bars.LowValues, _testData.Bars.CloseValues,
                  spanBw.AsSpan(), spanSq.AsSpan(), bbPeriod, bbMult, kcPeriod, kcMult);

        // Compare last 200 samples
        int start = Math.Max(0, len - 200);
        for (int i = start; i < len; i++)
        {
            Assert.Equal(streamBandwidths[i], spanBw[i], 7);
            Assert.Equal(streamSqueezes[i], spanSq[i]);
        }

        _output.WriteLine("BBS validation: squeeze span matches streaming.");
    }

    [Fact]
    public void Validate_Bandwidth_MatchesBbw()
    {
        // BBS bandwidth should match BBW (Bollinger Band Width) when using same BB parameters.
        // BBS bandwidth = ((upper - lower) / middle) * 100
        // BBW = ((upper - lower) / middle) * 100 (same formula)
        int[] periods = { 5, 10, 20, 50 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            // BBS (uses close for BB, needs OHLC for KC)
            var bbs = new Bbs(bbPeriod: period, bbMult: multiplier, kcPeriod: period, kcMult: 1.5);
            var bbsValues = new List<double>(_testData.Bars.Count);
            for (int i = 0; i < _testData.Bars.Count; i++)
            {
                bbs.Update(_testData.Bars[i]);
                bbsValues.Add(bbs.Last.Value);
            }

            // Skender Bollinger Bands Width
            var skenderBb = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

            // Compare bandwidth values where both are valid
            int start = period + 10; // skip warmup
            int compared = 0;
            for (int i = start; i < Math.Min(bbsValues.Count, skenderBb.Count); i++)
            {
                var sk = skenderBb[i];
                if (sk.Width is not null and not double.NaN)
                {
                    // BBS bandwidth = width * 100 (as percentage)
                    // Skender Width = (Upper - Lower) / Middle
                    double expected = sk.Width.Value * 100.0;
                    Assert.Equal(expected, bbsValues[i], 4);
                    compared++;
                }
            }

            Assert.True(compared > 0, $"No valid comparisons for period {period}");
        }

        _output.WriteLine("BBS bandwidth validated against Skender BB Width.");
    }

    [Fact]
    public void Validate_AllOutputsFinite()
    {
        var bbs = new Bbs(bbPeriod: 20, bbMult: 2.0, kcPeriod: 20, kcMult: 1.5);

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var result = bbs.Update(_testData.Bars[i]);
            Assert.True(double.IsFinite(result.Value), $"Non-finite output at bar {i}: {result.Value}");
        }

        _output.WriteLine("BBS validation: all outputs are finite.");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Bbs.Calculate(_testData.Bars);

        Assert.Equal(_testData.Bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));

        _output.WriteLine("BBS validation: Calculate returns hot indicator.");
    }

    [Fact]
    public void Validate_LargeDataset_Stability()
    {
        var (results, _) = Bbs.Calculate(_testData.Bars, bbPeriod: 50, bbMult: 2.0, kcPeriod: 50, kcMult: 1.5);

        // Check last 100 values are finite and non-negative
        int start = Math.Max(0, results.Count - 100);
        for (int i = start; i < results.Count; i++)
        {
            Assert.True(double.IsFinite(results[i].Value));
            Assert.True(results[i].Value >= 0, $"Bandwidth should be non-negative at {i}: {results[i].Value}");
        }

        _output.WriteLine("BBS validation: large dataset stability verified.");
    }
}
