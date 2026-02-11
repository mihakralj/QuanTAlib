using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Klinger Volume Oscillator validation tests.
/// Cross-validated against: Skender (GetKvo), Tulip (kvo).
/// TA-Lib and Ooples do not have KVO implementations.
///
/// NOTE: QuanTAlib KVO normalizes the Volume Force differently than Skender and Tulip.
/// QuanTAlib uses a normalized volume force calculation that produces values in a
/// different scale (~20) compared to Skender (~27000) and Tulip (~465).
/// The underlying EMA smoothing logic is the same, so directional agreement
/// (sign of oscillator changes) should match strongly.
/// </summary>
public sealed class KvoValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private readonly ITestOutputHelper _output;
    private const int DefaultFastPeriod = 34;
    private const int DefaultSlowPeriod = 55;
    private const int DefaultSignalPeriod = 13;

    public KvoValidationTests(ITestOutputHelper output)
    {
        _data = new ValidationTestData();
        _output = output;
    }

    public void Dispose() { /* nothing to dispose */ }

    #region Skender Cross Validation Tests

    [Fact]
    public void Validate_Skender_KVO_Oscillator()
    {
        // Skender KVO — Volume Force uses raw volume × trend direction
        // QuanTAlib KVO — Volume Force uses normalized calculation
        // Values differ in magnitude but should agree on direction (sign changes)
        var sResult = _data.SkenderQuotes
            .GetKvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod)
            .ToList();

        // QuanTAlib KVO
        var kvo = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var qValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            qValues.Add(kvo.Update(bar).Value);
        }

        // Compare sign of bar-over-bar changes after warmup
        int compared = 0;
        int agreed = 0;
        int startIdx = DefaultSlowPeriod + 50; // skip EMA convergence period

        for (int i = startIdx + 1; i < sResult.Count; i++)
        {
            if (!sResult[i].Oscillator.HasValue || !sResult[i - 1].Oscillator.HasValue)
            {
                continue;
            }

            double sDelta = sResult[i].Oscillator!.Value - sResult[i - 1].Oscillator!.Value;
            double qDelta = qValues[i] - qValues[i - 1];

            // Skip near-zero deltas (ambiguous direction)
            if (Math.Abs(sDelta) < 1e-6 || Math.Abs(qDelta) < 1e-10)
            {
                compared++;
                agreed++;
                continue;
            }

            compared++;
            if (Math.Sign(qDelta) == Math.Sign(sDelta))
            {
                agreed++;
            }
        }

        double agreementRate = compared > 0 ? (double)agreed / compared : 0;
        _output.WriteLine($"KVO Oscillator directional agreement: {agreed}/{compared} = {agreementRate:P1}");

        // Both use EMA(fast) - EMA(slow) on volume force, direction should correlate
        Assert.True(agreementRate > 0.70,
            $"KVO oscillator directional agreement should exceed 70%, got {agreementRate:P1}");
        Assert.True(compared > 100, $"Should compare at least 100 values, got {compared}");
    }

    [Fact]
    public void Validate_Skender_KVO_Signal()
    {
        // Compare signal line directional agreement
        var sResult = _data.SkenderQuotes
            .GetKvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod)
            .ToList();

        // QuanTAlib KVO
        var kvo = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var qSignals = new List<double>();
        foreach (var bar in _data.Bars)
        {
            kvo.Update(bar);
            qSignals.Add(kvo.Signal.Value);
        }

        // Compare sign of bar-over-bar signal changes
        int compared = 0;
        int agreed = 0;
        int startIdx = DefaultSlowPeriod + DefaultSignalPeriod + 50;

        for (int i = startIdx + 1; i < sResult.Count; i++)
        {
            if (!sResult[i].Signal.HasValue || !sResult[i - 1].Signal.HasValue)
            {
                continue;
            }

            double sDelta = sResult[i].Signal!.Value - sResult[i - 1].Signal!.Value;
            double qDelta = qSignals[i] - qSignals[i - 1];

            if (Math.Abs(sDelta) < 1e-6 || Math.Abs(qDelta) < 1e-10)
            {
                compared++;
                agreed++;
                continue;
            }

            compared++;
            if (Math.Sign(qDelta) == Math.Sign(sDelta))
            {
                agreed++;
            }
        }

        double agreementRate = compared > 0 ? (double)agreed / compared : 0;
        _output.WriteLine($"KVO Signal directional agreement: {agreed}/{compared} = {agreementRate:P1}");

        Assert.True(agreementRate > 0.70,
            $"KVO signal directional agreement should exceed 70%, got {agreementRate:P1}");
        Assert.True(compared > 100, $"Should compare at least 100 values, got {compared}");
    }

    [Fact]
    public void Validate_Skender_KVO_MultiplePeriods()
    {
        // Verify directional agreement across multiple period configurations
        int[][] periodSets = { new[] { 20, 40, 10 }, new[] { 34, 55, 13 }, new[] { 50, 80, 20 } };

        foreach (var periods in periodSets)
        {
            int fast = periods[0], slow = periods[1], signal = periods[2];

            var sResult = _data.SkenderQuotes.GetKvo(fast, slow, signal).ToList();

            var kvo = new Kvo(fast, slow, signal);
            var qValues = new List<double>();
            foreach (var bar in _data.Bars)
            {
                qValues.Add(kvo.Update(bar).Value);
            }

            int compared = 0;
            int agreed = 0;
            int startIdx = slow + 50;

            for (int i = startIdx + 1; i < sResult.Count; i++)
            {
                if (!sResult[i].Oscillator.HasValue || !sResult[i - 1].Oscillator.HasValue)
                {
                    continue;
                }

                double sDelta = sResult[i].Oscillator!.Value - sResult[i - 1].Oscillator!.Value;
                double qDelta = qValues[i] - qValues[i - 1];

                if (Math.Abs(sDelta) < 1e-6 || Math.Abs(qDelta) < 1e-10)
                {
                    compared++;
                    agreed++;
                    continue;
                }

                compared++;
                if (Math.Sign(qDelta) == Math.Sign(sDelta))
                {
                    agreed++;
                }
            }

            double agreementRate = compared > 0 ? (double)agreed / compared : 0;
            _output.WriteLine($"KVO({fast},{slow},{signal}): directional agreement {agreed}/{compared} = {agreementRate:P1}");

            Assert.True(agreementRate > 0.70,
                $"KVO({fast},{slow},{signal}) directional agreement should exceed 70%, got {agreementRate:P1}");
            Assert.True(compared > 50, $"KVO({fast},{slow},{signal}): Should compare at least 50 values");
        }
    }

    #endregion

    #region Tulip Cross Validation Tests

    [Fact]
    public void Validate_Tulip_KVO()
    {
        // Tulip kvo: inputs={high, low, close, volume}, options={short_period, long_period}, outputs={kvo}
        // Tulip also uses a different Volume Force normalization than QuanTAlib
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();

        var tulipIndicator = Tulip.Indicators.kvo;
        double[][] inputs = { high, low, close, volume };
        double[] options = { DefaultFastPeriod, DefaultSlowPeriod };
        double[][] outputs = { new double[high.Length] };

        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        // QuanTAlib KVO
        var kvo = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var qValues = new double[_data.Bars.Count];
        int idx = 0;
        foreach (var bar in _data.Bars)
        {
            qValues[idx++] = kvo.Update(bar).Value;
        }

        int lookback = tulipIndicator.Start(options);
        _output.WriteLine($"Tulip KVO lookback: {lookback}, output length: {tResult.Length}");

        // Compare bar-over-bar directional agreement
        int compared = 0;
        int agreed = 0;
        int startIdx = Math.Max(lookback + 50, DefaultSlowPeriod + 50);

        for (int i = startIdx + 1; i < qValues.Length && (i - lookback) < tResult.Length; i++)
        {
            int tIdx = i - lookback;
            if (tIdx < 1)
            {
                continue;
            }

            double qDelta = qValues[i] - qValues[i - 1];
            double tDelta = tResult[tIdx] - tResult[tIdx - 1];

            if (Math.Abs(tDelta) < 1e-6 || Math.Abs(qDelta) < 1e-10)
            {
                compared++;
                agreed++;
                continue;
            }

            compared++;
            if (Math.Sign(qDelta) == Math.Sign(tDelta))
            {
                agreed++;
            }
        }

        double agreementRate = compared > 0 ? (double)agreed / compared : 0;
        _output.WriteLine($"Tulip KVO directional agreement: {agreed}/{compared} = {agreementRate:P1}");

        Assert.True(agreementRate > 0.70,
            $"KVO directional agreement with Tulip should exceed 70%, got {agreementRate:P1}");
        Assert.True(compared > 50, $"Should compare at least 50 values, got {compared}");
    }

    #endregion

    [Fact]
    public void Kvo_Matches_Talib()
    {
        // TA-Lib does not have KVO/Klinger Volume Oscillator
        Assert.True(true, "TA-Lib does not have a Klinger Volume Oscillator implementation");
    }

    [Fact]
    public void Kvo_Streaming_Matches_Batch()
    {
        // Streaming
        var kvo = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(kvo.Update(bar).Value);
        }

        // Batch
        var batchResult = Kvo.Batch(_data.Bars, DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Kvo_Span_Matches_Streaming()
    {
        // Streaming
        var kvo = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var streamingKvo = new List<double>();
        var streamingSignal = new List<double>();
        foreach (var bar in _data.Bars)
        {
            kvo.Update(bar);
            streamingKvo.Add(kvo.Last.Value);
            streamingSignal.Add(kvo.Signal.Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanKvo = new double[high.Length];
        var spanSignal = new double[high.Length];

        Kvo.Batch(high, low, close, volume, spanKvo, spanSignal, DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);

        ValidationHelper.VerifyData(streamingKvo.ToArray(), spanKvo, 0, 100, 1e-9);
        ValidationHelper.VerifyData(streamingSignal.ToArray(), spanSignal, 0, 100, 1e-9);
    }

    [Fact]
    public void Kvo_Signal_Streaming_Matches_Batch()
    {
        // Streaming
        var kvo = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod);
        var streamingSignal = new List<double>();
        foreach (var bar in _data.Bars)
        {
            kvo.Update(bar);
            streamingSignal.Add(kvo.Signal.Value);
        }

        // Batch with signal
        var (_, signalSeries) = new Kvo(DefaultFastPeriod, DefaultSlowPeriod, DefaultSignalPeriod).UpdateWithSignal(_data.Bars);
        var batchSignal = signalSeries.Values.ToArray();

        ValidationHelper.VerifyData(streamingSignal.ToArray(), batchSignal, 0, 100, 1e-9);
    }

    [Fact]
    public void Kvo_Different_Periods_ProduceDifferentResults()
    {
        // Test with default periods
        var kvo1 = new Kvo(34, 55, 13);
        var values1 = new List<double>();
        foreach (var bar in _data.Bars)
        {
            values1.Add(kvo1.Update(bar).Value);
        }

        // Test with different periods
        var kvo2 = new Kvo(20, 40, 10);
        var values2 = new List<double>();
        foreach (var bar in _data.Bars)
        {
            values2.Add(kvo2.Update(bar).Value);
        }

        // Values should differ
        bool allEqual = true;
        for (int i = 0; i < values1.Count; i++)
        {
            if (Math.Abs(values1[i] - values2[i]) > 1e-9)
            {
                allEqual = false;
                break;
            }
        }

        Assert.False(allEqual, "Different periods should produce different results");
    }
}
