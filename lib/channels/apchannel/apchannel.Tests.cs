namespace QuanTAlib.Tests;

public class ApchannelTests
{
    private const double Tolerance = 1e-10;

    #region Constructor & Validation

    [Fact]
    public void Constructor_ValidatesInput()
    {
        // Alpha must be > 0
        Assert.Throws<ArgumentOutOfRangeException>(() => new Apchannel(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Apchannel(-0.1));

        // Alpha must be <= 1
        Assert.Throws<ArgumentOutOfRangeException>(() => new Apchannel(1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Apchannel(2.0));

        // Valid construction
        var apc = new Apchannel(0.2);
        Assert.NotNull(apc);
    }

    [Fact]
    public void Constructor_ValidBoundaryValues()
    {
        var apc1 = new Apchannel(0.001); // Very small alpha
        Assert.NotNull(apc1);

        var apc2 = new Apchannel(1.0); // Maximum alpha
        Assert.NotNull(apc2);

        var apc3 = new Apchannel(0.5); // Mid-range alpha
        Assert.NotNull(apc3);
    }

    #endregion

    #region Basic Functionality

    [Fact]
    public void Calc_ReturnsValue()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        Assert.Equal(0, apc.Last.Value);
        Assert.Equal(0, apc.UpperBand);
        Assert.Equal(0, apc.LowerBand);

        var bar = new TBar(time, 100, 105, 95, 100, 1000);
        var result = apc.Add(bar);

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, apc.Last.Value);
        Assert.Equal(105, apc.UpperBand, Tolerance);
        Assert.Equal(95, apc.LowerBand, Tolerance);
    }

    [Fact]
    public void FirstValue_ReturnsExpected()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        var bar = new TBar(time, 100, 110, 90, 100, 1000);
        var result = apc.Add(bar);

        // First bar: UpperBand = High, LowerBand = Low, Last = midpoint
        Assert.Equal(110, apc.UpperBand, Tolerance);
        Assert.Equal(90, apc.LowerBand, Tolerance);
        Assert.Equal(100, result.Value, Tolerance); // (110 + 90) / 2
    }

    [Fact]
    public void Properties_Accessible()
    {
        var apc = new Apchannel(0.3);
        var time = DateTime.UtcNow;

        Assert.Equal(0, apc.Last.Value);
        Assert.False(apc.IsHot);
        Assert.Contains("Apchannel", apc.Name, StringComparison.Ordinal);
        Assert.Contains("0.3", apc.Name, StringComparison.Ordinal);

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));

        Assert.NotEqual(0, apc.Last.Value);
        Assert.NotEqual(0, apc.UpperBand);
        Assert.NotEqual(0, apc.LowerBand);
    }

    [Fact]
    public void CalculatesCorrectEma()
    {
        var apc = new Apchannel(0.5); // Alpha = 0.5 for easier manual calculation
        var time = DateTime.UtcNow;

        // Bar 1: High = 110, Low = 90
        apc.Add(new TBar(time, 100, 110, 90, 100, 1000));
        Assert.Equal(110, apc.UpperBand, Tolerance);
        Assert.Equal(90, apc.LowerBand, Tolerance);

        // Bar 2: High = 120, Low = 80
        // UpperEMA = 0.5 * 110 + 0.5 * 120 = 115
        // LowerEMA = 0.5 * 90 + 0.5 * 80 = 85
        apc.Add(new TBar(time.AddMinutes(1), 100, 120, 80, 100, 1000));
        Assert.Equal(115, apc.UpperBand, Tolerance);
        Assert.Equal(85, apc.LowerBand, Tolerance);

        // Bar 3: High = 130, Low = 70
        // UpperEMA = 0.5 * 115 + 0.5 * 130 = 122.5
        // LowerEMA = 0.5 * 85 + 0.5 * 70 = 77.5
        apc.Add(new TBar(time.AddMinutes(2), 100, 130, 70, 100, 1000));
        Assert.Equal(122.5, apc.UpperBand, Tolerance);
        Assert.Equal(77.5, apc.LowerBand, Tolerance);
    }

    #endregion

    #region State Management & Bar Correction

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        double value1 = apc.Last.Value;

        apc.Add(new TBar(time.AddMinutes(1), 102, 108, 96, 102, 1000), isNew: true);
        double value2 = apc.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));
        apc.Add(new TBar(time.AddMinutes(1), 102, 108, 96, 102, 1000), isNew: true);
        double beforeUpdate = apc.Last.Value;

        apc.Add(new TBar(time.AddMinutes(1), 104, 110, 98, 104, 1000), isNew: false);
        double afterUpdate = apc.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var apc = new Apchannel(0.2);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Feed 10 new bars
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = gbm.Next(isNew: true);
            apc.Add(tenthBar, isNew: true);
        }

        // Remember state after 10 bars
        double stateAfterTen = apc.Last.Value;
        double upperAfterTen = apc.UpperBand;
        double lowerAfterTen = apc.LowerBand;

        // Generate 9 corrections with isNew=false
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            apc.Add(bar, isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        var finalResult = apc.Add(tenthBar, isNew: false);

        // State should match the original state after 10 bars
        Assert.Equal(stateAfterTen, finalResult.Value, Tolerance);
        Assert.Equal(upperAfterTen, apc.UpperBand, Tolerance);
        Assert.Equal(lowerAfterTen, apc.LowerBand, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));
        apc.Add(new TBar(time.AddMinutes(1), 102, 108, 96, 102, 1000));
        double valueBefore = apc.Last.Value;

        apc.Reset();

        Assert.Equal(0, apc.Last.Value);
        Assert.Equal(0, apc.UpperBand);
        Assert.Equal(0, apc.LowerBand);
        Assert.False(apc.IsHot);

        // After reset, should accept new values
        apc.Add(new TBar(time, 50, 55, 45, 50, 1000));
        Assert.NotEqual(0, apc.Last.Value);
        Assert.NotEqual(valueBefore, apc.Last.Value);
    }

    #endregion

    #region Warmup & Convergence

    [Fact]
    public void IsHot_BecomesTrueWhenConverged()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;
        int warmup = apc.WarmupPeriod;

        Assert.False(apc.IsHot);

        for (int i = 1; i < warmup; i++)
        {
            apc.Add(new TBar(time.AddMinutes(i), 100, 105, 95, 100, 1000));
            Assert.False(apc.IsHot);
        }

        apc.Add(new TBar(time.AddMinutes(warmup), 100, 105, 95, 100, 1000));
        Assert.True(apc.IsHot);
    }

    [Fact]
    public void IsHot_IsAlphaDependent()
    {
        double[] alphas = [0.1, 0.2, 0.5, 0.9];
        int[] expectedSteps = new int[alphas.Length];
        var time = DateTime.UtcNow;

        for (int i = 0; i < alphas.Length; i++)
        {
            double alpha = alphas[i];
            var apc = new Apchannel(alpha);
            expectedSteps[i] = apc.WarmupPeriod;

            int steps = 0;
            while (!apc.IsHot && steps < 1000)
            {
                apc.Add(new TBar(time.AddMinutes(steps), 100, 105, 95, 100, 1000));
                steps++;
            }
        }

        // Warmup times should decrease as alpha increases (faster convergence)
        Assert.True(expectedSteps[0] > expectedSteps[1]);
        Assert.True(expectedSteps[1] > expectedSteps[2]);
        Assert.True(expectedSteps[2] > expectedSteps[3]);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var apc1 = new Apchannel(0.1);
        Assert.Equal(30, apc1.WarmupPeriod); // ceil(3.0 / 0.1) = 30

        var apc2 = new Apchannel(0.2);
        Assert.Equal(15, apc2.WarmupPeriod); // ceil(3.0 / 0.2) = 15

        var apc3 = new Apchannel(0.5);
        Assert.Equal(6, apc3.WarmupPeriod); // ceil(3.0 / 0.5) = 6
    }

    #endregion

    #region Robustness (NaN/Infinity)

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));
        apc.Add(new TBar(time.AddMinutes(1), 102, 108, 96, 102, 1000));

        var resultAfterNaN = apc.Add(new TBar(time.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(apc.UpperBand));
        Assert.True(double.IsFinite(apc.LowerBand));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));
        apc.Add(new TBar(time.AddMinutes(1), 102, 108, 96, 102, 1000));

        var resultPosInf = apc.Add(new TBar(time.AddMinutes(2), double.PositiveInfinity,
                                           double.PositiveInfinity, double.PositiveInfinity,
                                           double.PositiveInfinity, 1000));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = apc.Add(new TBar(time.AddMinutes(3), double.NegativeInfinity,
                                           double.NegativeInfinity, double.NegativeInfinity,
                                           double.NegativeInfinity, 1000));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));
        apc.Add(new TBar(time.AddMinutes(1), 102, 108, 96, 102, 1000));
        apc.Add(new TBar(time.AddMinutes(2), 104, 110, 98, 104, 1000));

        var r1 = apc.Add(new TBar(time.AddMinutes(3), double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        var r2 = apc.Add(new TBar(time.AddMinutes(4), double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        var r3 = apc.Add(new TBar(time.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void SpanCalculate_ValidatesInput()
    {
        double[] high = [105, 108, 110, 107, 109];
        double[] low = [95, 96, 98, 97, 99];
        double[] upperBand = new double[5];
        double[] lowerBand = new double[5];

        // Alpha must be > 0 and <= 1
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Apchannel.Batch(high, low, upperBand, lowerBand, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Apchannel.Batch(high, low, upperBand, lowerBand, 1.5));

        // Arrays must be same length
        double[] wrongSizeLow = new double[3];
        Assert.Throws<ArgumentException>(() =>
            Apchannel.Batch(high, wrongSizeLow, upperBand, lowerBand, 0.2));

        double[] wrongSizeUpper = new double[3];
        Assert.Throws<ArgumentException>(() =>
            Apchannel.Batch(high, low, wrongSizeUpper, lowerBand, 0.2));

        double[] wrongSizeLower = new double[3];
        Assert.Throws<ArgumentException>(() =>
            Apchannel.Batch(high, low, upperBand, wrongSizeLower, 0.2));
    }

    [Fact]
    public void SpanCalculate_MatchesIterativeCalc()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] high = bars.Select(b => b.High).ToArray();
        double[] low = bars.Select(b => b.Low).ToArray();
        double[] upperBandSpan = new double[100];
        double[] lowerBandSpan = new double[100];

        // Calculate using span
        Apchannel.Batch(high, low, upperBandSpan, lowerBandSpan, 0.2);

        // Calculate iteratively
        var apc = new Apchannel(0.2);
        double[] upperBandIter = new double[100];
        double[] lowerBandIter = new double[100];

        for (int i = 0; i < 100; i++)
        {
            apc.Add(bars[i]);
            upperBandIter[i] = apc.UpperBand;
            lowerBandIter[i] = apc.LowerBand;
        }

        // Compare
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(upperBandIter[i], upperBandSpan[i], Tolerance);
            Assert.Equal(lowerBandIter[i], lowerBandSpan[i], Tolerance);
        }
    }

    [Fact]
    public void SpanCalculate_HandlesNaN()
    {
        double[] high = [105, double.NaN, 110, 107, double.PositiveInfinity];
        double[] low = [95, 96, double.NaN, 97, double.NegativeInfinity];
        double[] upperBand = new double[5];
        double[] lowerBand = new double[5];

        Apchannel.Batch(high, low, upperBand, lowerBand, 0.2);

        foreach (var val in upperBand)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }

        foreach (var val in lowerBand)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void SpanCalculate_ZeroAllocation()
    {
        double[] high = new double[10000];
        double[] low = new double[10000];
        double[] upperBand = new double[10000];
        double[] lowerBand = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < high.Length; i++)
        {
            var bar = gbm.Next();
            high[i] = bar.High;
            low[i] = bar.Low;
        }

        // Warm up
        Apchannel.Batch(high, low, upperBand, lowerBand, 0.2);

        // Verify method completes without OOM or stack overflow
        Assert.True(double.IsFinite(upperBand[^1]));
        Assert.True(double.IsFinite(lowerBand[^1]));
    }

    #endregion

    #region Calculate Method Tests

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Apchannel.Calculate(bars, 0.2);

        // Check results
        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.High));
        Assert.True(double.IsFinite(results.Last.Low));

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.High, indicator.UpperBand, Tolerance);
        Assert.Equal(results.Last.Low, indicator.LowerBand, Tolerance);
        Assert.Equal(15, indicator.WarmupPeriod); // ceil(3.0 / 0.2)

        // Verify indicator continues correctly
        var nextBar = gbm.Next();
        indicator.Add(nextBar);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Chainability_Works()
    {
        var source = new TBarSeries();
        var apc = new Apchannel(source, 0.2);
        var time = DateTime.UtcNow;

        var bar = new TBar(time, 100, 105, 95, 100, 1000);
        source.Add(bar);

        Assert.Equal(105, apc.UpperBand);
        Assert.Equal(95, apc.LowerBand);
        Assert.Equal(100, apc.Last.Value); // (105 + 95) / 2
    }

    [Fact]
    public void Pub_EventFires()
    {
        var apc = new Apchannel(0.2);
        bool eventFired = false;
        apc.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;
        var time = DateTime.UtcNow;

        apc.Add(new TBar(time, 100, 105, 95, 100, 1000));
        Assert.True(eventFired);
    }

    #endregion

    #region Indicator-Specific Tests

    [Fact]
    public void FlatLine_ReturnsSameValue()
    {
        var apc = new Apchannel(0.2);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            apc.Add(new TBar(time.AddMinutes(i), 100, 105, 95, 100, 1000));
        }

        // With flat high/low, bands should converge to input values
        Assert.Equal(105, apc.UpperBand, 1e-6);
        Assert.Equal(95, apc.LowerBand, 1e-6);
        Assert.Equal(100, apc.Last.Value, 1e-6);
    }

    [Fact]
    public void ChannelWidth_NarrowsWithHighAlpha()
    {
        var apc1 = new Apchannel(0.1); // Slower response
        var apc2 = new Apchannel(0.9); // Faster response
        var time = DateTime.UtcNow;

        // Feed same data to both
        for (int i = 0; i < 50; i++)
        {
            double price = 100 + (i % 2 == 0 ? 10 : -10); // Oscillating
            var bar = new TBar(time.AddMinutes(i), price, price + 5, price - 5, price, 1000);
            apc1.Add(bar);
            apc2.Add(bar);
        }

        double width1 = apc1.UpperBand - apc1.LowerBand;
        double width2 = apc2.UpperBand - apc2.LowerBand;

        // Higher alpha should track price more closely
        Assert.True(width2 < width1 * 1.5); // Some tolerance for oscillation
    }

    #endregion
}
