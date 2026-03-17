using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validates Rocket RSI against manual step-by-step computation of the
/// Ehlers TASC May 2018 algorithm. No external library implements RocketRSI,
/// so validation is self-consistency + manual reference implementation.
/// </summary>
public sealed class RrsiValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestSmooth = 10;
    private const int TestRsi = 10;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region Manual Computation Cross-Validation

    /// <summary>
    /// Validates that our RocketRSI Batch exactly matches a manual
    /// step-by-step reference implementation of Ehlers' algorithm.
    /// </summary>
    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Manual_Computation()
    {
        double[] values = _testData.RawData.ToArray();
        int smoothLength = TestSmooth;
        int rsiLength = TestRsi;

        // Batch output
        double[] batchOutput = new double[values.Length];
        Rrsi.Batch(values.AsSpan(), batchOutput.AsSpan(), smoothLength, rsiLength);

        // Manual reference implementation — Ehlers TASC May 2018
        double[] manualOutput = ManualRocketRsi(values, smoothLength, rsiLength);

        // Compare after warmup
        int startIdx = smoothLength + rsiLength;
        int validCount = 0;
        for (int i = startIdx; i < values.Length; i++)
        {
            Assert.True(Math.Abs(manualOutput[i] - batchOutput[i]) < 1e-9,
                $"RocketRSI mismatch at i={i}: manual={manualOutput[i]:F9}, batch={batchOutput[i]:F9}");
            validCount++;
        }

        Assert.True(validCount > 100, $"Expected >100 valid comparisons, got {validCount}");
        _output.WriteLine($"RocketRSI manual validation: {validCount} points at 1e-9 tolerance.");
    }

    /// <summary>
    /// Tests with different parameter combinations.
    /// </summary>
    [Theory]
    [InlineData(5, 5)]
    [InlineData(8, 10)]
    [InlineData(10, 10)]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public void Validate_Manual_DifferentParams(int smoothLength, int rsiLength)
    {
        double[] values = _testData.RawData.ToArray();

        double[] batchOutput = new double[values.Length];
        Rrsi.Batch(values.AsSpan(), batchOutput.AsSpan(), smoothLength, rsiLength);

        double[] manualOutput = ManualRocketRsi(values, smoothLength, rsiLength);

        int startIdx = smoothLength + rsiLength;
        int validCount = 0;
        for (int i = startIdx; i < values.Length; i++)
        {
            Assert.True(Math.Abs(manualOutput[i] - batchOutput[i]) < 1e-9,
                $"RocketRSI mismatch at i={i}, smooth={smoothLength}, rsi={rsiLength}: " +
                $"manual={manualOutput[i]:F9}, batch={batchOutput[i]:F9}");
            validCount++;
        }

        Assert.True(validCount > 0, $"No valid comparison points for smooth={smoothLength}, rsi={rsiLength}");
        _output.WriteLine($"RocketRSI smooth={smoothLength}, rsi={rsiLength}: validated {validCount} points.");
    }

    /// <summary>
    /// Validates arctanh identity used in Fisher Transform step.
    /// </summary>
    [Fact]
    public void Validate_Arctanh_Identity()
    {
        double[] testValues = [-0.999, -0.9, -0.5, 0.0, 0.5, 0.9, 0.999];

        foreach (double v in testValues)
        {
            double formula = 0.5 * Math.Log((1.0 + v) / (1.0 - v));
            double builtin = Math.Atanh(v);

            Assert.True(Math.Abs(formula - builtin) < 1e-12,
                $"arctanh({v}): formula={formula}, builtin={builtin}");
        }

        _output.WriteLine("arctanh mathematical identity verified for RocketRSI.");
    }

    #endregion

    #region Consistency Validation

    /// <summary>
    /// Validates streaming matches batch across all data points.
    /// </summary>
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Batch_Span_Agree()
    {
        double[] tData = _testData.RawData.ToArray();

        // Batch TSeries
        TSeries batchSeries = Rrsi.Batch(_testData.Data, TestSmooth, TestRsi);

        // Batch Span
        double[] spanOutput = new double[tData.Length];
        Rrsi.Batch(tData.AsSpan(), spanOutput.AsSpan(), TestSmooth, TestRsi);

        // Batch and Span should be identical
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(batchSeries.Values[i], spanOutput[i], 12);
        }

        // Streaming
        var rrsi = new Rrsi(TestSmooth, TestRsi);
        double[] streamResults = new double[tData.Length];
        for (int i = 0; i < tData.Length; i++)
        {
            streamResults[i] = rrsi.Update(_testData.Data[i]).Value;
        }

        // Streaming vs Batch should match
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], 9);
        }

        _output.WriteLine("RocketRSI streaming/batch/span agreement verified.");
    }

    /// <summary>
    /// Validates event-based matches streaming.
    /// </summary>
    [Fact]
    [SkipLocalsInit]
    public void Validate_Event_Matches_Streaming()
    {
        // Streaming
        var streamRrsi = new Rrsi(TestSmooth, TestRsi);
        double[] streamResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            streamResults[i] = streamRrsi.Update(_testData.Data[i]).Value;
        }

        // Event-based
        var eventSource = new TSeries();
        var eventRrsi = new Rrsi(eventSource, TestSmooth, TestRsi);
        double[] eventResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            eventSource.Add(_testData.Data[i]);
            eventResults[i] = eventRrsi.Last.Value;
        }

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 12);
        }

        _output.WriteLine("RocketRSI event-based matches streaming.");
    }

    /// <summary>
    /// All batch outputs must be finite.
    /// </summary>
    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(50, 10)]
    public void Validate_All_Outputs_Finite(int smoothLength, int rsiLength)
    {
        double[] values = _testData.RawData.ToArray();
        double[] results = new double[values.Length];

        Rrsi.Batch(values.AsSpan(), results.AsSpan(), smoothLength, rsiLength);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.True(double.IsFinite(results[i]),
                $"RocketRSI not finite at i={i}, smooth={smoothLength}, rsi={rsiLength}: {results[i]}");
        }

        _output.WriteLine($"RocketRSI smooth={smoothLength}, rsi={rsiLength}: all {values.Length} outputs finite.");
    }

    #endregion

    #region Super Smoother Component Validation

    /// <summary>
    /// Validates that the Super Smoother coefficients satisfy the constraint c1 + c2 + c3 = 1
    /// (unity DC gain).
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Validate_SuperSmoother_Coefficients_UnitGain(int smoothLength)
    {
        double a1 = Math.Exp(-1.414 * Math.PI / smoothLength);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / smoothLength);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        double sum = c1 + c2 + c3;
        Assert.True(Math.Abs(sum - 1.0) < 1e-12,
            $"SuperSmoother DC gain should be 1.0, got {sum} for smoothLength={smoothLength}");

        _output.WriteLine($"SuperSmoother coefficients for L={smoothLength}: c1={c1:F6}, c2={c2:F6}, c3={c3:F6}, sum={sum:F12}");
    }

    #endregion

    #region Manual Reference Implementation

    /// <summary>
    /// Pure reference implementation of Ehlers Rocket RSI (TASC May 2018).
    /// This is a direct transcription of the EasyLanguage code for verification.
    /// </summary>
    private static double[] ManualRocketRsi(double[] source, int smoothLength, int rsiLength)
    {
        int len = source.Length;
        double[] output = new double[len];
        double[] mom = new double[len];
        double[] filt = new double[len];

        // Super Smoother coefficients
        double a1 = Math.Exp(-1.414 * Math.PI / smoothLength);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / smoothLength);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        // Pass 1: Momentum
        for (int i = 0; i < len; i++)
        {
            mom[i] = (i >= rsiLength - 1) ? source[i] - source[i - rsiLength + 1] : 0.0;
        }

        // Pass 2: Super Smoother Filter
        filt[0] = mom[0];
        if (len > 1)
        {
            filt[1] = (c1 * (mom[1] + mom[0]) * 0.5) + (c2 * filt[0]);
        }
        for (int i = 2; i < len; i++)
        {
            filt[i] = (c1 * (mom[i] + mom[i - 1]) * 0.5) + (c2 * filt[i - 1]) + (c3 * filt[i - 2]);
        }

        // Pass 3: Ehlers RSI + Fisher Transform
        for (int i = 0; i < len; i++)
        {
            double cu = 0.0;
            double cd = 0.0;
            int lookback = Math.Min(rsiLength, i);

            for (int j = 0; j < lookback; j++)
            {
                double diff = filt[i - j] - filt[i - j - 1];
                if (diff > 0.0)
                {
                    cu += diff;
                }
                else if (diff < 0.0)
                {
                    cd -= diff;
                }
            }

            double cuCd = cu + cd;
            double myRsi = (cuCd > 1e-10) ? (cu - cd) / cuCd : 0.0;

            // Clamp
            if (myRsi > 0.999) { myRsi = 0.999; }
            else if (myRsi < -0.999) { myRsi = -0.999; }

            output[i] = 0.5 * Math.Log((1.0 + myRsi) / (1.0 - myRsi));
        }

        return output;
    }

    #endregion
}
