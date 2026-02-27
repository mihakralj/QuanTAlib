using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Ease of Movement validation tests.
/// Tulip has emv (Ease of Movement Value) but outputs raw unsmoothed values
/// without volume scaling, while QuanTAlib applies SMA(period) smoothing with
/// configurable volumeScale (default 10000). Direct comparison not possible.
/// Skender, TA-Lib, and Ooples do not have EOM implementations.
/// </summary>
public sealed class EomValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private readonly ITestOutputHelper _output;
    private const int DefaultPeriod = 14;

    public EomValidationTests(ITestOutputHelper output)
    {
        _data = new ValidationTestData();
        _output = output;
    }

    public void Dispose() { /* nothing to dispose */ }

    [Fact]
    public void Eom_Matches_Tulip_Directional_Agreement()
    {
        // Tulip emv: inputs={high, low, volume}, options={}, outputs={emv}
        // Tulip computes raw EMV without SMA smoothing or volumeScale division.
        // QuanTAlib EOM = SMA(raw_eom / volumeScale, period).
        // We can only verify directional agreement (sign correlation) after warmup.
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();

        var tulipIndicator = Tulip.Indicators.emv;
        double[][] inputs = { high, low, volume };
        double[] options = Array.Empty<double>();
        double[][] outputs = { new double[high.Length] };

        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];
        int lookback = tulipIndicator.Start(options);

        // QuanTAlib EOM with period=1 (no smoothing) for directional comparison
        var eom = new Eom(1);
        var qValues = new double[_data.Bars.Count];
        int idx = 0;
        foreach (var bar in _data.Bars)
        {
            qValues[idx++] = eom.Update(bar).Value;
        }

        _output.WriteLine($"Tulip EMV lookback: {lookback}, output length: {tResult.Length}");

        // Verify directional agreement (both positive or both negative) in most bars
        int agreementCount = 0;
        int totalCompared = 0;
        int startIdx = lookback + 5;
        for (int i = startIdx; i < qValues.Length && (i - lookback) < tResult.Length; i++)
        {
            double qValue = qValues[i];
            double tValue = tResult[i - lookback];

            // Skip near-zero values where sign is meaningless
            if (Math.Abs(qValue) < 1e-10 || Math.Abs(tValue) < 1e-10)
            {
                continue;
            }

            totalCompared++;
            if (Math.Sign(qValue) == Math.Sign(tValue))
            {
                agreementCount++;
            }
        }

        double agreementRate = totalCompared > 0 ? (double)agreementCount / totalCompared : 0;
        _output.WriteLine($"Tulip EMV directional agreement: {agreementCount}/{totalCompared} ({agreementRate:P1})");

        // With period=1, directional agreement should be high (>80%)
        Assert.True(agreementRate > 0.80,
            $"EOM directional agreement with Tulip EMV should be >80%, got {agreementRate:P1}");
    }

    [Fact]
    public void Eom_Matches_Skender()
    {
        // Skender does not have Ease of Movement implementation
        Assert.True(true, "Skender does not have an Ease of Movement implementation");
    }

    [Fact]
    public void Eom_Matches_Talib()
    {
        // TA-Lib does not have EOM/Ease of Movement
        Assert.True(true, "TA-Lib does not have an Ease of Movement implementation");
    }

    [Fact]
    public void Eom_Streaming_Matches_Batch()
    {
        // Streaming
        var eom = new Eom(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(eom.Update(bar).Value);
        }

        // Batch
        var batchResult = Eom.Batch(_data.Bars, DefaultPeriod);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Eom_Span_Matches_Streaming()
    {
        // Streaming
        var eom = new Eom(DefaultPeriod);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(eom.Update(bar).Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Eom.Batch(high, low, volume, spanValues, DefaultPeriod);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-9);
    }
}
