using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class HwcTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = new TSeries(capacity: count);
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(bars[i].Time, bars[i].Close, isNew: true);
        }
        return series;
    }

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidMultiplier_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(period: 20, multiplier: 0));
        Assert.Equal("multiplier", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeMultiplier_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(period: 20, multiplier: -1.0));
        Assert.Equal("multiplier", ex.ParamName);
    }

    [Fact]
    public void Constructor_DefaultParams()
    {
        var ind = new Hwc();
        Assert.Equal("Hwc(20,1.0)", ind.Name);
        Assert.Equal(20, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomParams()
    {
        var ind = new Hwc(period: 10, multiplier: 2.0);
        Assert.Equal("Hwc(10,2.0)", ind.Name);
        Assert.Equal(10, ind.WarmupPeriod);
    }

    // === A2) Alpha/Beta/Gamma constructor ===

    [Fact]
    public void Constructor_Alpha_InvalidLow_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(alpha: 0, beta: 0.1, gamma: 0.1));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_Alpha_InvalidHigh_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(alpha: 1.1, beta: 0.1, gamma: 0.1));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_Beta_InvalidNeg_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(alpha: 0.5, beta: -0.1, gamma: 0.1));
        Assert.Equal("beta", ex.ParamName);
    }

    [Fact]
    public void Constructor_Gamma_InvalidHigh_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hwc(alpha: 0.5, beta: 0.1, gamma: 1.1));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void Constructor_AlphaBetaGamma_ValidParams()
    {
        var ind = new Hwc(alpha: 0.1, beta: 0.05, gamma: 0.05, multiplier: 2.0);
        Assert.Contains("Hwc(", ind.Name, StringComparison.Ordinal);
        Assert.True(ind.WarmupPeriod >= 1);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Hwc(period: 5);
        var input = new TValue(DateTime.UtcNow, 100.0);
        TValue result = ind.Update(input);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Upper_Middle_Lower_Accessible()
    {
        var ind = new Hwc(period: 5);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Middle.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
    }

    [Fact]
    public void Upper_GreaterEqual_Middle_GreaterEqual_Lower()
    {
        var ind = new Hwc(period: 10, multiplier: 1.0);
        var series = GenerateSeries(50);
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i], isNew: true);
        }
        Assert.True(ind.Upper.Value >= ind.Middle.Value);
        Assert.True(ind.Middle.Value >= ind.Lower.Value);
    }

    [Fact]
    public void ConstantInput_BandsCollapse()
    {
        var ind = new Hwc(period: 5, multiplier: 1.0);
        for (int i = 0; i < 50; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        // With constant input, forecast error = 0, so upper = middle = lower
        Assert.Equal(ind.Middle.Value, ind.Upper.Value, precision: 8);
        Assert.Equal(ind.Middle.Value, ind.Lower.Value, precision: 8);
    }

    [Fact]
    public void ConstantInput_MiddleEqualsInput()
    {
        var ind = new Hwc(period: 5, multiplier: 1.0);
        for (int i = 0; i < 50; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        // HWMA of constant series should converge to the constant value
        Assert.Equal(100.0, ind.Middle.Value, precision: 4);
    }

    [Fact]
    public void Volatile_Data_Wider_Bands()
    {
        var indCalm = new Hwc(period: 10, multiplier: 1.0);
        var indVolatile = new Hwc(period: 10, multiplier: 1.0);

        for (int i = 0; i < 50; i++)
        {
            // Low volatility: small oscillation
            indCalm.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + Math.Sin(i * 0.1)));
            // High volatility: large oscillation
            indVolatile.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (Math.Sin(i * 0.1) * 20)));
        }

        double widthCalm = indCalm.Upper.Value - indCalm.Lower.Value;
        double widthVolatile = indVolatile.Upper.Value - indVolatile.Lower.Value;

        Assert.True(widthVolatile > widthCalm);
    }

    [Fact]
    public void Multiplier_Scales_Bands()
    {
        var ind1 = new Hwc(period: 10, multiplier: 1.0);
        var ind2 = new Hwc(period: 10, multiplier: 2.0);
        var series = GenerateSeries(50);

        for (int i = 0; i < series.Count; i++)
        {
            ind1.Update(series[i], isNew: true);
            ind2.Update(series[i], isNew: true);
        }

        double width1 = ind1.Upper.Value - ind1.Lower.Value;
        double width2 = ind2.Upper.Value - ind2.Lower.Value;

        // width2 should be ~2x width1
        Assert.Equal(2.0, width2 / width1, precision: 6);
    }

    // === C) State + bar correction ===

    [Fact]
    public void IsNew_True_Advances_State()
    {
        var ind = new Hwc(period: 5);
        var series = GenerateSeries(10);
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i], isNew: true);
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var ind = new Hwc(period: 5);
        var series = GenerateSeries(10);
        for (int i = 0; i < 9; i++)
        {
            ind.Update(series[i], isNew: true);
        }

        ind.Update(series[9], isNew: true);
        double midAfterNew = ind.Middle.Value;

        var corrected = new TValue(series[9].Time, 999.0);
        ind.Update(corrected, isNew: false);
        double midAfterCorrection = ind.Middle.Value;

        Assert.NotEqual(midAfterNew, midAfterCorrection, precision: 2);
    }

    [Fact]
    public void IsNew_False_Idempotent()
    {
        var ind = new Hwc(period: 5);
        var series = GenerateSeries(10);
        for (int i = 0; i < 9; i++)
        {
            ind.Update(series[i], isNew: true);
        }

        ind.Update(series[9], isNew: true);
        double baseline = ind.Middle.Value;

        ind.Update(series[9], isNew: false);
        Assert.Equal(baseline, ind.Middle.Value, precision: 10);
    }

    // === D) Reset ===

    [Fact]
    public void Reset_RestoresInitialState()
    {
        var ind = new Hwc(period: 5);
        var series = GenerateSeries(20);
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i], isNew: true);
        }
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void Reset_ThenUpdate_Identical()
    {
        var ind1 = new Hwc(period: 10, multiplier: 1.5);
        var ind2 = new Hwc(period: 10, multiplier: 1.5);
        var series = GenerateSeries(30);

        for (int i = 0; i < series.Count; i++)
        {
            ind1.Update(series[i], isNew: true);
        }

        ind1.Reset();
        for (int i = 0; i < series.Count; i++)
        {
            ind1.Update(series[i], isNew: true);
            ind2.Update(series[i], isNew: true);
        }

        Assert.Equal(ind2.Middle.Value, ind1.Middle.Value, precision: 10);
        Assert.Equal(ind2.Upper.Value, ind1.Upper.Value, precision: 10);
        Assert.Equal(ind2.Lower.Value, ind1.Lower.Value, precision: 10);
    }

    // === E) Series / Batch ===

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var ind = new Hwc(period: 10);
        var series = GenerateSeries(50);
        TSeries result = ind.Update(series);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Batch_TSeries_ReturnsThreeSeries()
    {
        var series = GenerateSeries(50);
        var (upper, middle, lower) = Hwc.Batch(series, period: 10, multiplier: 1.0);
        Assert.Equal(50, upper.Count);
        Assert.Equal(50, middle.Count);
        Assert.Equal(50, lower.Count);
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        var series = GenerateSeries(50);
        double[] source = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            source[i] = series[i].Value;
        }

        double[] upper = new double[series.Count];
        double[] middle = new double[series.Count];
        double[] lower = new double[series.Count];

        Hwc.Batch(source, upper, middle, lower, period: 10, multiplier: 1.5);

        // Compare with streaming
        var ind = new Hwc(period: 10, multiplier: 1.5);
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i], isNew: true);
        }

        Assert.Equal(ind.Middle.Value, middle[^1], precision: 10);
        Assert.Equal(ind.Upper.Value, upper[^1], precision: 10);
        Assert.Equal(ind.Lower.Value, lower[^1], precision: 10);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var series = GenerateSeries(50);
        var (results, indicator) = Hwc.Calculate(series, period: 10, multiplier: 1.0);
        Assert.Equal(50, results.Upper.Count);
        Assert.Equal(50, results.Middle.Count);
        Assert.Equal(50, results.Lower.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var ind = new Hwc(period: 10);
        double[] data = new double[50];
        for (int i = 0; i < 50; i++)
        {
            data[i] = 100.0 + i;
        }
        ind.Prime(data);
        Assert.True(ind.IsHot);
    }

    // === F) NaN handling ===

    [Fact]
    public void NaN_Input_ProducesNaN_WhenNotInitialized()
    {
        var ind = new Hwc(period: 5);
        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void NaN_Input_UsesLastValid_WhenInitialized()
    {
        var ind = new Hwc(period: 5);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        // Now send NaN — should use lastValidValue internally
        var result = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(10), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    // === G) Edge cases ===

    [Fact]
    public void SingleInput_ProducesFiniteOutput()
    {
        var ind = new Hwc(period: 5);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(100.0, result.Value, precision: 10);
    }

    [Fact]
    public void LargeDataset_ProducesFiniteOutput()
    {
        var ind = new Hwc();
        var series = GenerateSeries(10_000);
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i], isNew: true);
        }
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Middle.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
    }

    [Fact]
    public void Batch_EmptySeries_ReturnsEmpty()
    {
        var series = new TSeries();
        var (upper, middle, lower) = Hwc.Batch(series);
        Assert.Empty(upper);
        Assert.Empty(middle);
        Assert.Empty(lower);
    }

    [Fact]
    public void Batch_Span_LengthMismatch_Throws()
    {
        double[] source = new double[10];
        double[] upper = new double[5]; // mismatch!
        double[] middle = new double[10];
        double[] lower = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Hwc.Batch(source, upper, middle, lower));
    }

    [Fact]
    public void Update_TSeries_Null_Throws()
    {
        var ind = new Hwc();
        Assert.Throws<ArgumentNullException>(() => ind.Update((TSeries)null!));
    }
}
