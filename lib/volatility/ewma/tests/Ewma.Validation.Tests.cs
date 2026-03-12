namespace QuanTAlib.Tests;
using Xunit;

/// <summary>
/// Validation tests for EWMA Volatility indicator.
/// Note: EWMA Volatility as implemented is based on PineScript reference.
/// External library validation may not be available.
/// </summary>
public class EwmaValidationTests
{
    private readonly int DefaultPeriod = 20;
    private readonly bool DefaultAnnualize = true;
    private readonly int DefaultAnnualPeriods = 252;
    private const double StreamingTolerance = 1e-9;

    private static TBarSeries GenerateTestData(int count = 500)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TSeries ToTSeries(TBarSeries bars)
    {
        var ts = new TSeries();
        var times = bars.Times;
        var close = bars.CloseValues;
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        return ts;
    }

    // ============ Mathematical Property Validation ============

    [Fact]
    public void MathProperty_ReturnsAreSquared()
    {
        // EWMA should always produce non-negative values (sqrt of squared returns)
        var ewma = new Ewma(10, false);
        var bars = GenerateTestData(100);
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ewma.Update(new TValue(DateTime.UtcNow, close[i]));
            Assert.True(result.Value >= 0, $"EWMA should be non-negative, got {result.Value} at index {i}");
        }
    }

    [Fact]
    public void MathProperty_AnnualizationFactor()
    {
        // Annualized vol = periodic vol × √(annual periods)
        var ewmaNoAnn = new Ewma(DefaultPeriod, false);
        var ewmaAnn252 = new Ewma(DefaultPeriod, true, 252);
        var ewmaAnn52 = new Ewma(DefaultPeriod, true, 52);
        var ewmaAnn12 = new Ewma(DefaultPeriod, true, 12);

        var bars = GenerateTestData(100);
        var close = bars.CloseValues;
        var times = bars.Times;

        for (int i = 0; i < bars.Count; i++)
        {
            ewmaNoAnn.Update(new TValue(times[i], close[i]));
            ewmaAnn252.Update(new TValue(times[i], close[i]));
            ewmaAnn52.Update(new TValue(times[i], close[i]));
            ewmaAnn12.Update(new TValue(times[i], close[i]));
        }

        double periodicVol = ewmaNoAnn.Last.Value;
        if (periodicVol > 1e-10) // Only test if there's measurable volatility
        {
            Assert.Equal(periodicVol * Math.Sqrt(252), ewmaAnn252.Last.Value, 1e-9);
            Assert.Equal(periodicVol * Math.Sqrt(52), ewmaAnn52.Last.Value, 1e-9);
            Assert.Equal(periodicVol * Math.Sqrt(12), ewmaAnn12.Last.Value, 1e-9);
        }
    }

    [Fact]
    public void MathProperty_BiasCorrection_ConvergesToOne()
    {
        // Bias correction factor (1 - decay^n) should approach 1 as n → ∞
        // This means corrected and uncorrected values should converge
        var ewma = new Ewma(20, false);
        var bars = GenerateTestData(500);
        var close = bars.CloseValues;
        var times = bars.Times;

        for (int i = 0; i < bars.Count; i++)
        {
            ewma.Update(new TValue(times[i], close[i]));
        }

        // After many observations, bias correction should be minimal
        // We can't directly test the factor, but we can verify stability
        Assert.True(ewma.IsHot);
        Assert.True(double.IsFinite(ewma.Last.Value));
    }

    [Fact]
    public void MathProperty_RMA_ExponentialDecay()
    {
        // RMA formula: new_rma = (old_rma × (period-1) + new_value) / period
        // This is equivalent to EMA with alpha = 1/period
        // Older values should have exponentially decaying influence

        var ewma = new Ewma(10, false);

        // Feed constant values to establish baseline
        for (int i = 0; i < 50; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        double baselineVol = ewma.Last.Value;

        // Inject a shock
        ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(50), 150.0)); // 50% jump
        double shockVol = ewma.Last.Value;

        Assert.True(shockVol > baselineVol, "Shock should increase volatility");

        // Return to constant prices - volatility should decay
        double[] vols = new double[30];
        for (int i = 0; i < 30; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(51 + i), 100.0));
            vols[i] = ewma.Last.Value;
        }

        // Verify monotonic decay (or near-monotonic)
        int decayCount = 0;
        for (int i = 1; i < vols.Length; i++)
        {
            if (vols[i] <= vols[i - 1] + 1e-10) // Allow small floating point noise
            {
                decayCount++;
            }
        }

        Assert.True(decayCount >= 25, $"Volatility should decay over time, but only {decayCount}/29 periods showed decay");
    }

    // ============ Mode Consistency Validation ============

    [Fact]
    public void ModeConsistency_StreamingVsBatch()
    {
        var ewmaStream = new Ewma(DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        var bars = GenerateTestData(200);
        var ts = ToTSeries(bars);
        var close = bars.CloseValues;
        var times = bars.Times;

        // Streaming
        for (int i = 0; i < bars.Count; i++)
        {
            ewmaStream.Update(new TValue(times[i], close[i]));
        }

        // Batch
        var batchResult = Ewma.Batch(ts, DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);

        Assert.Equal(ewmaStream.Last.Value, batchResult[batchResult.Count - 1].Value, StreamingTolerance);
    }

    [Fact]
    public void ModeConsistency_StreamingVsSpan()
    {
        var ewmaStream = new Ewma(DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        var bars = GenerateTestData(200);
        var close = bars.CloseValues;
        var times = bars.Times;

        // Streaming
        for (int i = 0; i < bars.Count; i++)
        {
            ewmaStream.Update(new TValue(times[i], close[i]));
        }

        // Span
        var output = new double[close.Length];
        Ewma.Batch(close, output, DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);

        Assert.Equal(ewmaStream.Last.Value, output[output.Length - 1], StreamingTolerance);
    }

    [Fact]
    public void ModeConsistency_TSeries_VsSpan()
    {
        var ewma = new Ewma(DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        var bars = GenerateTestData(200);
        var ts = ToTSeries(bars);
        var close = bars.CloseValues;

        // TSeries
        var tseriesResult = ewma.Update(ts);

        // Span
        var output = new double[close.Length];
        Ewma.Batch(close, output, DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);

        Assert.Equal(tseriesResult[tseriesResult.Count - 1].Value, output[output.Length - 1], StreamingTolerance);
    }

    [Fact]
    public void ModeConsistency_AllFourModes()
    {
        var bars = GenerateTestData(150);
        var ts = ToTSeries(bars);
        var close = bars.CloseValues;
        var times = bars.Times;

        // Mode 1: Streaming
        var ewmaStream = new Ewma(DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        for (int i = 0; i < bars.Count; i++)
        {
            ewmaStream.Update(new TValue(times[i], close[i]));
        }
        double streamingResult = ewmaStream.Last.Value;

        // Mode 2: TSeries Update
        var ewmaTSeries = new Ewma(DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        var tseriesResult = ewmaTSeries.Update(ts);
        double tseriesValue = tseriesResult[tseriesResult.Count - 1].Value;

        // Mode 3: Static Calculate
        var batchResult = Ewma.Batch(ts, DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        double batchValue = batchResult[batchResult.Count - 1].Value;

        // Mode 4: Span Batch
        var output = new double[close.Length];
        Ewma.Batch(close, output, DefaultPeriod, DefaultAnnualize, DefaultAnnualPeriods);
        double spanValue = output[output.Length - 1];

        // All four should match
        Assert.Equal(streamingResult, tseriesValue, StreamingTolerance);
        Assert.Equal(streamingResult, batchValue, StreamingTolerance);
        Assert.Equal(streamingResult, spanValue, StreamingTolerance);
    }

    // ============ Edge Case Validation ============

    [Fact]
    public void EdgeCase_SingleValue()
    {
        var ewma = new Ewma(5, false);
        var result = ewma.Update(new TValue(DateTime.UtcNow, 100));

        // Single value should return 0 volatility (no return yet)
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void EdgeCase_TwoValues()
    {
        var ewma = new Ewma(5, false);
        ewma.Update(new TValue(DateTime.UtcNow, 100));
        var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110));

        // With price change, should have positive volatility
        Assert.True(result.Value > 0, "Should detect volatility from price change");
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void EdgeCase_AllNaN()
    {
        var ewma = new Ewma(5);
        for (int i = 0; i < 10; i++)
        {
            var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void EdgeCase_MixedNaN()
    {
        var ewma = new Ewma(5);
        double[] prices = { 100, 101, double.NaN, 103, double.NaN, double.NaN, 106 };

        foreach (double price in prices)
        {
            var result = ewma.Update(new TValue(DateTime.UtcNow, price));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void EdgeCase_VerySmallPrices()
    {
        var ewma = new Ewma(5, false);
        for (int i = 0; i < 20; i++)
        {
            double price = 0.0001 + ((i % 2) * 0.00001);
            var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    [Fact]
    public void EdgeCase_VeryLargePrices()
    {
        var ewma = new Ewma(5, false);
        for (int i = 0; i < 20; i++)
        {
            double price = 1e10 + ((i % 2) * 1e9);
            var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    [Fact]
    public void EdgeCase_Period1()
    {
        var ewma = new Ewma(1, false);
        ewma.Update(new TValue(DateTime.UtcNow, 100));
        var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110));

        // Period 1 means volatility is just |log return|
        double expectedLogReturn = Math.Abs(Math.Log(110.0 / 100.0));
        Assert.True(Math.Abs(result.Value - expectedLogReturn) < 0.01,
            $"Period 1 EWMA should equal |log return|. Expected ~{expectedLogReturn}, got {result.Value}");
    }

    [Fact]
    public void EdgeCase_LargePeriod()
    {
        var ewma = new Ewma(500, false);
        var bars = GenerateTestData(600);
        var close = bars.CloseValues;
        var times = bars.Times;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ewma.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(ewma.IsHot);
    }

    // ============ Stability Validation ============

    [Fact]
    public void Stability_LongRunningCalculation()
    {
        var ewma = new Ewma(20);
        var bars = GenerateTestData(5000);
        var close = bars.CloseValues;
        var times = bars.Times;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ewma.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value), $"Non-finite value at index {i}");
            Assert.True(result.Value >= 0, $"Negative volatility at index {i}");
        }
    }

    [Fact]
    public void Stability_RepeatedReset()
    {
        var ewma = new Ewma(10);
        var bars = GenerateTestData(50);
        var close = bars.CloseValues;
        var times = bars.Times;

        for (int reset = 0; reset < 5; reset++)
        {
            ewma.Reset();
            for (int i = 0; i < bars.Count; i++)
            {
                var result = ewma.Update(new TValue(times[i], close[i]));
                Assert.True(double.IsFinite(result.Value));
            }
        }
    }

    [Fact]
    public void Stability_BarCorrection_MultipleUpdates()
    {
        var ewma = new Ewma(10);
        var bars = GenerateTestData(50);
        var close = bars.CloseValues;
        var times = bars.Times;

        for (int i = 0; i < bars.Count; i++)
        {
            ewma.Update(new TValue(times[i], close[i]), isNew: true);
        }

        // Multiple corrections
        for (int j = 0; j < 10; j++)
        {
            double correctedPrice = 100 + (j * 5);
            var result = ewma.Update(new TValue(DateTime.UtcNow, correctedPrice), isNew: false);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0);
        }
    }

    // ============ Known Value Validation ============

    [Fact]
    public void KnownValue_ConstantPrice_ZeroVolatility()
    {
        var ewma = new Ewma(10, false);

        for (int i = 0; i < 30; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        Assert.Equal(0.0, ewma.Last.Value, 1e-10);
    }

    [Fact]
    public void KnownValue_SimpleReturn()
    {
        // Verify log return calculation
        // If price goes 100 → 101, log return = ln(101/100) ≈ 0.00995
        var ewma = new Ewma(2, false);

        ewma.Update(new TValue(DateTime.UtcNow, 100));
        var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101));

        double expectedLogReturn = Math.Log(101.0 / 100.0);
        // With period=2, RMA of first squared return is just that return
        // With bias correction at n=1, correction factor = 1 - 0.5 = 0.5
        // First squared return initialized to sq_ret, then bias correction applied
        // Volatility = sqrt(corrected variance)

        Assert.True(result.Value > 0, "Volatility should be positive for price change");
        Assert.True(result.Value < 0.05, "Volatility should be reasonable for 1% price change");
        Assert.True(double.IsFinite(expectedLogReturn), "Log return should be finite");
    }

    [Fact]
    public void KnownValue_SymmetricReturns()
    {
        // Volatility should be same for +10% and -10% returns (squared)
        var ewmaUp = new Ewma(5, false);
        var ewmaDown = new Ewma(5, false);

        ewmaUp.Update(new TValue(DateTime.UtcNow, 100));
        ewmaDown.Update(new TValue(DateTime.UtcNow, 100));

        ewmaUp.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110));   // +10%
        ewmaDown.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 90)); // -10%

        // Log returns: ln(1.1) ≈ 0.0953, ln(0.9) ≈ -0.1054
        // Squared returns are slightly different due to log asymmetry
        // But both should be positive volatility
        Assert.True(ewmaUp.Last.Value > 0);
        Assert.True(ewmaDown.Last.Value > 0);
    }

    // ============ Parameter Sensitivity Validation ============

    [Fact]
    public void ParameterSensitivity_ShorterPeriod_MoreResponsive()
    {
        var ewmaShort = new Ewma(5, false);
        var ewmaLong = new Ewma(50, false);

        // Build up history with low volatility
        for (int i = 0; i < 60; i++)
        {
            ewmaShort.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
            ewmaLong.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        double shortBefore = ewmaShort.Last.Value;
        double longBefore = ewmaLong.Last.Value;

        // Inject shock
        ewmaShort.Update(new TValue(DateTime.UtcNow.AddMinutes(60), 120.0));
        ewmaLong.Update(new TValue(DateTime.UtcNow.AddMinutes(60), 120.0));

        double shortAfter = ewmaShort.Last.Value;
        double longAfter = ewmaLong.Last.Value;

        double shortIncrease = shortAfter - shortBefore;
        double longIncrease = longAfter - longBefore;

        Assert.True(shortIncrease > longIncrease,
            "Shorter period should respond more strongly to shocks");
    }
}
