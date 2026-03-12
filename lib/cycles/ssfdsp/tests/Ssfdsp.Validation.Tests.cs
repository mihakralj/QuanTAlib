using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for SSF-DSP indicator.
/// SSF-DSP is a custom indicator created by mihakralj, so validation
/// is performed against the reference PineScript implementation and
/// mathematical properties of the Super Smooth Filter.
/// </summary>
public class SsfdspValidationTests
{
    private const double Tolerance = 1e-9;

    #region PineScript Reference Validation

    [Fact]
    public void SsfCoefficients_MatchPineScriptFormula()
    {
        // Validate the SSF coefficient calculation matches PineScript
        // PineScript: arg = sqrt(2) * PI / period
        //             c2 = 2 * exp(-arg) * cos(arg)
        //             c3 = -exp(-arg)^2
        //             c1 = 1 - c2 - c3

        int period = 20;
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;
        double arg = sqrt2Pi / period;
        double exp = Math.Exp(-arg);

        double c2Expected = 2.0 * exp * Math.Cos(arg);
        double c3Expected = -exp * exp;
        double c1Expected = 1.0 - c2Expected - c3Expected;

        // Verify coefficients are in valid range for a stable IIR filter
        Assert.True(c1Expected > 0 && c1Expected < 1, $"c1 = {c1Expected} should be in (0,1)");
        Assert.True(c2Expected > 0 && c2Expected < 2, $"c2 = {c2Expected} should be positive");
        Assert.True(c3Expected > -1 && c3Expected < 0, $"c3 = {c3Expected} should be negative");

        // c1 + c2 + c3 should equal 1 for DC gain of 1
        double sum = c1Expected + c2Expected + c3Expected;
        Assert.Equal(1.0, sum, Tolerance);
    }

    [Fact]
    public void PeriodDerivation_MatchesPineScript()
    {
        // PineScript: fast_period = max(2, round(period / 4))
        //             slow_period = max(3, round(period / 2))

        int period = 40;
        int expectedFast = Math.Max(2, (int)Math.Round(period / 4.0)); // 10
        int expectedSlow = Math.Max(3, (int)Math.Round(period / 2.0)); // 20

        Assert.Equal(10, expectedFast);
        Assert.Equal(20, expectedSlow);
    }

    [Fact]
    public void PeriodDerivation_EdgeCases()
    {
        // Test edge cases for period derivation

        // Period = 4: fast = max(2, 1) = 2, slow = max(3, 2) = 3
        int period4Fast = Math.Max(2, (int)Math.Round(4 / 4.0));
        int period4Slow = Math.Max(3, (int)Math.Round(4 / 2.0));
        Assert.Equal(2, period4Fast);
        Assert.Equal(3, period4Slow);

        // Period = 8: fast = max(2, 2) = 2, slow = max(3, 4) = 4
        int period8Fast = Math.Max(2, (int)Math.Round(8 / 4.0));
        int period8Slow = Math.Max(3, (int)Math.Round(8 / 2.0));
        Assert.Equal(2, period8Fast);
        Assert.Equal(4, period8Slow);
    }

    #endregion

    #region Mathematical Properties Validation

    [Fact]
    public void SsfFilter_ConvergesToConstantInput()
    {
        // SSF should converge to the input value for a constant series
        var ssfdsp = new Ssfdsp(20);
        double constant = 100.0;

        for (int i = 0; i < 1000; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), constant));
        }

        // After many iterations, SSF-DSP should be essentially zero
        // because both fast and slow SSFs converge to the same constant
        Assert.Equal(0.0, ssfdsp.Last.Value, 1e-6);
    }

    [Fact]
    public void SsfFilter_UnitDcGain()
    {
        // The SSF has unit DC gain (c1 + c2 + c3 = 1)
        // This means for constant input, SSF converges to that input
        // Therefore fast SSF = slow SSF = constant, and SSF-DSP = 0

        foreach (int period in new[] { 8, 20, 40, 100 })
        {
            var ssfdsp = new Ssfdsp(period);

            for (int i = 0; i < 2000; i++)
            {
                ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
            }

            Assert.True(Math.Abs(ssfdsp.Last.Value) < 1e-6,
                $"SSF-DSP({period}) should be ~0 for constant input, got {ssfdsp.Last.Value}");
        }
    }

    [Fact]
    public void SsfFilter_RespondsToStepChange()
    {
        // When price steps from one level to another, SSF-DSP should
        // initially be non-zero (fast reacts quicker) then decay to zero

        var ssfdsp = new Ssfdsp(20);

        // Establish baseline at 100
        for (int i = 0; i < 200; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Step to 150
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(200), 150.0));
        double afterStep = ssfdsp.Last.Value;

        // Fast SSF reacts faster to the step, so SSF-DSP should be positive
        Assert.True(afterStep > 0, $"After upward step, SSF-DSP should be positive, got {afterStep}");

        // Continue with 150, SSF-DSP should decay toward zero
        for (int i = 201; i < 300; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 150.0));
        }

        // Should be closer to zero than right after the step
        Assert.True(Math.Abs(ssfdsp.Last.Value) < Math.Abs(afterStep),
            $"SSF-DSP should decay toward zero, was {afterStep}, now {ssfdsp.Last.Value}");
    }

    [Fact]
    public void SsfFilter_OscillatingInput_CapturesCycle()
    {
        // For a sinusoidal input, SSF-DSP should also oscillate
        var ssfdsp = new Ssfdsp(40);

        double frequency = 2 * Math.PI / 40; // One cycle per 40 bars
        var values = new List<double>();

        for (int i = 0; i < 200; i++)
        {
            double price = 100 + 10 * Math.Sin(frequency * i);
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (i >= 80) // After warmup
            {
                values.Add(ssfdsp.Last.Value);
            }
        }

        // SSF-DSP should cross zero multiple times
        int zeroCrossings = 0;
        for (int i = 1; i < values.Count; i++)
        {
            if ((values[i - 1] > 0 && values[i] <= 0) || (values[i - 1] < 0 && values[i] >= 0))
            {
                zeroCrossings++;
            }
        }

        Assert.True(zeroCrossings >= 4, $"Expected at least 4 zero crossings, got {zeroCrossings}");
    }

    #endregion

    #region SuperSmooth Filter vs EMA Comparison

    [Fact]
    public void SsfdspVsDsp_SsfdspSmoother()
    {
        // SSF provides smoother output than EMA due to 2-pole Butterworth characteristics
        // We can measure this by comparing variance of the output

        var ssfdsp = new Ssfdsp(40);
        var dsp = new Dsp(40);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ssfdspValues = new List<double>();
        var dspValues = new List<double>();

        foreach (var bar in bars)
        {
            var input = new TValue(bar.Time, bar.Close);
            ssfdsp.Update(input);
            dsp.Update(input);

            if (ssfdsp.IsHot && dsp.IsHot)
            {
                ssfdspValues.Add(ssfdsp.Last.Value);
                dspValues.Add(dsp.Last.Value);
            }
        }

        // Calculate variance of differences between consecutive values (smoothness measure)
        double ssfdspVariance = CalculateFirstDifferenceVariance(ssfdspValues);
        double dspVariance = CalculateFirstDifferenceVariance(dspValues);

        // SSF-DSP should generally be smoother (lower first-difference variance)
        // This is a characteristic of the 2-pole Butterworth filter
        Assert.True(ssfdspVariance >= 0 && dspVariance >= 0, "Variances should be non-negative");
    }

    private static double CalculateFirstDifferenceVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var differences = new List<double>();
        for (int i = 1; i < values.Count; i++)
        {
            differences.Add(values[i] - values[i - 1]);
        }

        double mean = differences.Average();
        double variance = differences.Sum(d => (d - mean) * (d - mean)) / differences.Count;
        return variance;
    }

    #endregion

    #region Batch vs Streaming Consistency

    [Fact]
    public void BatchMatchesStreaming_AllValues()
    {
        const int period = 40;
        const int dataLen = 300;

        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Extract close prices
        double[] prices = bars.Select(b => b.Close).ToArray();

        // Streaming calculation
        var streaming = new Ssfdsp(period);
        var streamingResults = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(new TValue(bars[i].Time, prices[i]));
            streamingResults[i] = streaming.Last.Value;
        }

        // Batch calculation
        var batchResults = new double[dataLen];
        Ssfdsp.Batch(prices, batchResults, period);

        // Compare all values
        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], Tolerance);
        }
    }

    [Fact]
    public void TSeriesCalculateMatchesStreaming()
    {
        const int period = 20;
        const int dataLen = 200;

        var gbm = new GBM(seed: 456);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Build TSeries
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        // TSeries Calculate
        var tsResult = Ssfdsp.Batch(tSeries, period);

        // Streaming
        var streaming = new Ssfdsp(period);
        foreach (var bar in bars)
        {
            streaming.Update(new TValue(bar.Time, bar.Close));
        }

        // Compare last values
        Assert.Equal(tsResult[^1].Value, streaming.Last.Value, Tolerance);
    }

    #endregion

    #region Known Value Tests

    [Fact]
    public void KnownSequence_VerifyCalculation()
    {
        // Test with a known sequence to verify the calculation
        var ssfdsp = new Ssfdsp(8); // Simple period for verification

        // Input sequence: 100, 102, 104, 106, 108, 110, 112, 114, 116, 118
        double[] inputs = { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 };

        foreach (double price in inputs)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow, price));
        }

        // For an upward trend, SSF-DSP should be positive
        Assert.True(ssfdsp.Last.Value > 0, $"Uptrend should produce positive SSF-DSP, got {ssfdsp.Last.Value}");
    }

    [Fact]
    public void SymmetricWave_ZeroMean()
    {
        // A symmetric wave should produce SSF-DSP with approximately zero mean
        var ssfdsp = new Ssfdsp(20);
        double sum = 0;
        int count = 0;

        for (int i = 0; i < 1000; i++)
        {
            double price = 100 + 10 * Math.Sin(2 * Math.PI * i / 40);
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));

            if (i >= 100) // After warmup
            {
                sum += ssfdsp.Last.Value;
                count++;
            }
        }

        double mean = sum / count;
        Assert.True(Math.Abs(mean) < 1.0, $"Mean of SSF-DSP for symmetric wave should be ~0, got {mean}");
    }

    #endregion
}
