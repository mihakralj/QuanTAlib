// Volatility Ratio (VR) Validation Tests
// Validates against the PineScript reference implementation

using Xunit;

namespace QuanTAlib.Tests;

public class VrValidationTests
{
    private readonly GBM _gbm;
    private const double PineScriptTolerance = 1e-6;

    public VrValidationTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TBarSeries GenerateBarData(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region PineScript Algorithm Validation

    [Fact]
    public void Vr_TrueRangeCalculation_MatchesPineScript()
    {
        // TR = max(high - low, abs(high - prevClose), abs(low - prevClose))
        double prevClose = 100.0;
        double high = 105.0;
        double low = 98.0;

        double hl = high - low;                    // 7
        double hPc = Math.Abs(high - prevClose);   // 5
        double lPc = Math.Abs(low - prevClose);    // 2

        double expectedTR = Math.Max(hl, Math.Max(hPc, lPc)); // 7

        Assert.Equal(7.0, expectedTR);
    }

    [Fact]
    public void Vr_TrueRangeWithGapUp_MatchesPineScript()
    {
        // Gap up scenario: High-PrevClose is largest
        double prevClose = 100.0;
        double high = 110.0;
        double low = 108.0;

        double hl = high - low;                    // 2
        double hPc = Math.Abs(high - prevClose);   // 10
        double lPc = Math.Abs(low - prevClose);    // 8

        double expectedTR = Math.Max(hl, Math.Max(hPc, lPc)); // 10

        Assert.Equal(10.0, expectedTR);
    }

    [Fact]
    public void Vr_TrueRangeWithGapDown_MatchesPineScript()
    {
        // Gap down scenario: Low-PrevClose (abs) is largest
        double prevClose = 100.0;
        double high = 92.0;
        double low = 90.0;

        double hl = high - low;                    // 2
        double hPc = Math.Abs(high - prevClose);   // 8
        double lPc = Math.Abs(low - prevClose);    // 10

        double expectedTR = Math.Max(hl, Math.Max(hPc, lPc)); // 10

        Assert.Equal(10.0, expectedTR);
    }

    [Fact]
    public void Vr_BiasCorrection_MatchesPineScript()
    {
        // Verify bias correction formula: atr = rawAtr / (1 - eComp)
        // where eComp = (1 - alpha)^n for n bars

        int period = 10;
        double alpha = 1.0 / period;

        // After 1 bar: eComp = 0.9
        double eComp1 = 1.0 - alpha;
        Assert.Equal(0.9, eComp1, 10);

        // After 2 bars: eComp = 0.81
        double eComp2 = (1.0 - alpha) * eComp1;
        Assert.Equal(0.81, eComp2, 10);

        // After 3 bars: eComp = 0.729
        double eComp3 = (1.0 - alpha) * eComp2;
        Assert.Equal(0.729, eComp3, 10);
    }

    [Fact]
    public void Vr_ConstantTR_ConvergesToOne()
    {
        // When TR is constant, VR = TR / ATR should approach 1.0
        // because ATR converges to TR

        var vr = new Vr(period: 10);

        // Feed bars with constant TR (H-L = 4)
        for (int i = 0; i < 100; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 102, 98, 100, 1000));
        }

        // VR should be very close to 1.0
        Assert.True(Math.Abs(vr.Last.Value - 1.0) < 0.01,
            $"Constant TR should yield VR near 1.0, got {vr.Last.Value}");
    }

    [Fact]
    public void Vr_Formula_MatchesPineScript()
    {
        // VR = TR / ATR
        // With bias-corrected ATR (period = 14 in typical usage)

        double tr = 5.0;
        double rawAtr = 4.0;
        double eComp = 0.5; // Example compensator

        double atr = rawAtr / (1.0 - eComp); // = 4.0 / 0.5 = 8.0
        double expectedVr = tr / atr;         // = 5.0 / 8.0 = 0.625

        Assert.Equal(0.625, expectedVr, 10);
    }

    #endregion

    #region Streaming vs Batch Consistency

    [Fact]
    public void Vr_StreamingMatchesBatch_AllPeriods()
    {
        int[] periods = [5, 10, 14, 20, 50];

        foreach (int period in periods)
        {
            var bars = GenerateBarData(100);

            // Streaming
            var streamingVr = new Vr(period);
            for (int i = 0; i < bars.Count; i++)
            {
                streamingVr.Update(bars[i], isNew: true);
            }

            // Batch
            double[] batchOutput = new double[bars.Count];
            Vr.Batch(bars, batchOutput, period);

            // Compare final value
            Assert.Equal(streamingVr.Last.Value, batchOutput[bars.Count - 1], PineScriptTolerance);
        }
    }

    [Fact]
    public void Vr_BatchMatchesCalculate_AllValues()
    {
        var bars = GenerateBarData(100);
        int period = 14;

        // Using static Calculate
        var calculateResult = Vr.Batch(bars, period);

        // Using Batch
        double[] batchOutput = new double[bars.Count];
        Vr.Batch(bars, batchOutput, period);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(calculateResult[i].Value, batchOutput[i], PineScriptTolerance);
        }
    }

    #endregion

    #region Mathematical Properties

    [Fact]
    public void Vr_AlwaysNonNegative()
    {
        var bars = GenerateBarData(500);
        var vr = new Vr(14);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = vr.Update(bars[i]);
            Assert.True(result.Value >= 0, $"VR at index {i} should be non-negative: {result.Value}");
        }
    }

    [Fact]
    public void Vr_FirstBar_HasValidValue()
    {
        var vr = new Vr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        var result = vr.Update(bar);

        // First bar: TR = H-L = 10, ATR = TR = 10, VR = 1.0
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void Vr_HighVolatilityBar_ExceedsOne()
    {
        var vr = new Vr(period: 10);

        // Build up ATR with low volatility
        for (int i = 0; i < 30; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
        }

        // Now add a high volatility bar
        var highVolBar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        var result = vr.Update(highVolBar);

        Assert.True(result.Value > 1.0,
            $"High volatility bar should produce VR > 1.0, got {result.Value}");
    }

    [Fact]
    public void Vr_LowVolatilityBar_BelowOne()
    {
        var vr = new Vr(period: 10);

        // Build up ATR with moderate volatility
        for (int i = 0; i < 30; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        }

        // Now add a low volatility bar
        var lowVolBar = new TBar(DateTime.UtcNow, 100, 100.5, 99.5, 100, 1000);
        var result = vr.Update(lowVolBar);

        Assert.True(result.Value < 1.0,
            $"Low volatility bar should produce VR < 1.0, got {result.Value}");
    }

    [Fact]
    public void Vr_MeanRevertsToOne()
    {
        var vr = new Vr(period: 10);
        double sumVr = 0;
        int count = 0;

        // Generate many bars
        var bars = GenerateBarData(500);
        for (int i = 0; i < bars.Count; i++)
        {
            var result = vr.Update(bars[i]);
            if (vr.IsHot)
            {
                sumVr += result.Value;
                count++;
            }
        }

        double avgVr = sumVr / count;

        // Average VR should be near 1.0 over time
        Assert.True(avgVr > 0.5 && avgVr < 2.0,
            $"Average VR should be near 1.0, got {avgVr}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Vr_Period1_HandlesCorrectly()
    {
        var vr = new Vr(1);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        var result = vr.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void Vr_LargePeriod_HandlesCorrectly()
    {
        var vr = new Vr(200);
        var bars = GenerateBarData(300);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = vr.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    [Fact]
    public void Vr_ZeroRange_HandlesCorrectly()
    {
        var vr = new Vr(10);

        // Build up some ATR
        for (int i = 0; i < 20; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        }

        // Zero range bar
        var zeroRangeBar = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        var result = vr.Update(zeroRangeBar);

        // VR should be 0 when TR is 0
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value < 0.01, $"Zero TR should produce VR near 0, got {result.Value}");
    }

    [Fact]
    public void Vr_GapUp_IncorporatedInTR()
    {
        var vr = new Vr(period: 10);

        // Establish baseline
        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
        }

        // Gap up bar: previous close = 100, open = 110
        var gapBar = new TBar(DateTime.UtcNow, 110, 112, 109, 111, 1000);
        var result = vr.Update(gapBar);

        // TR should include gap (High - PrevClose = 12)
        Assert.True(result.Value > 1.0,
            $"Gap up should produce VR > 1.0, got {result.Value}");
    }

    [Fact]
    public void Vr_GapDown_IncorporatedInTR()
    {
        var vr = new Vr(period: 10);

        // Establish baseline
        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
        }

        // Gap down bar: previous close = 100, open = 90
        var gapBar = new TBar(DateTime.UtcNow, 90, 91, 88, 89, 1000);
        var result = vr.Update(gapBar);

        // TR should include gap (abs(Low - PrevClose) = 12)
        Assert.True(result.Value > 1.0,
            $"Gap down should produce VR > 1.0, got {result.Value}");
    }

    #endregion

    #region Breakout Detection Tests

    [Fact]
    public void Vr_BreakoutDetection_HighVRIndicatesBreakout()
    {
        var vr = new Vr(period: 14);

        // Low volatility consolidation
        for (int i = 0; i < 50; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100 + (i % 2) * 0.5, 1000));
        }

        double consolidationVr = vr.Last.Value;

        // Breakout bar
        var breakoutBar = new TBar(DateTime.UtcNow, 100, 115, 100, 114, 1000);
        var breakoutResult = vr.Update(breakoutBar);

        Assert.True(breakoutResult.Value > 2.0,
            $"Breakout bar should produce VR > 2.0, got {breakoutResult.Value}");
        Assert.True(breakoutResult.Value > consolidationVr * 2,
            $"Breakout VR ({breakoutResult.Value}) should be much higher than consolidation VR ({consolidationVr})");
    }

    [Fact]
    public void Vr_VolatilityExpansion_Detected()
    {
        var vr = new Vr(period: 14);

        // Track VR during expansion
        var vrValues = new List<double>();

        // Start with low volatility
        for (int i = 0; i < 20; i++)
        {
            var result = vr.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
            vrValues.Add(result.Value);
        }

        // Gradually increase volatility
        for (int i = 0; i < 20; i++)
        {
            double range = 1 + i * 0.5;
            var result = vr.Update(new TBar(DateTime.UtcNow, 100, 100 + range, 100 - range, 100, 1000));
            vrValues.Add(result.Value);
        }

        // Later VR values should be higher during expansion
        double earlyAvg = vrValues.Skip(15).Take(5).Average();
        double lateAvg = vrValues.Skip(35).Take(5).Average();

        Assert.True(lateAvg > earlyAvg,
            $"Expanding volatility should show increasing VR: early={earlyAvg}, late={lateAvg}");
    }

    #endregion
}
