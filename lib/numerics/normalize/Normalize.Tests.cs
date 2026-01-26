using Xunit;

namespace QuanTAlib.Tests;

public class NormalizeTests
{
    private readonly GBM _gbm = new(100, 0.05, 0.2, seed: 42);

    [Fact]
    public void Normalize_Constructor_ValidPeriod_SetsProperties()
    {
        var norm = new Normalize(20);

        Assert.Equal("Normalize(20)", norm.Name);
        Assert.Equal(20, norm.WarmupPeriod);
        Assert.False(norm.IsHot);
    }

    [Fact]
    public void Normalize_Constructor_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Normalize(0));
        Assert.Throws<ArgumentException>(() => new Normalize(-1));
    }

    [Fact]
    public void Normalize_Update_BasicCalculation()
    {
        var norm = new Normalize(5);

        // Feed values: 10, 20, 30, 40, 50
        // After 5 values: min=10, max=50, range=40
        // Current value 50: (50-10)/40 = 1.0
        norm.Update(new TValue(DateTime.UtcNow, 10));
        norm.Update(new TValue(DateTime.UtcNow, 20));
        norm.Update(new TValue(DateTime.UtcNow, 30));
        norm.Update(new TValue(DateTime.UtcNow, 40));
        var result = norm.Update(new TValue(DateTime.UtcNow, 50));

        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void Normalize_Update_MinValueReturnsZero()
    {
        var norm = new Normalize(5);

        norm.Update(new TValue(DateTime.UtcNow, 50));
        norm.Update(new TValue(DateTime.UtcNow, 40));
        norm.Update(new TValue(DateTime.UtcNow, 30));
        norm.Update(new TValue(DateTime.UtcNow, 20));
        var result = norm.Update(new TValue(DateTime.UtcNow, 10));

        // min=10, max=50, value=10: (10-10)/40 = 0.0
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Normalize_Update_MidValueReturnsFifty()
    {
        var norm = new Normalize(5);

        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 25));
        norm.Update(new TValue(DateTime.UtcNow, 75));
        var result = norm.Update(new TValue(DateTime.UtcNow, 50));

        // min=0, max=100, value=50: (50-0)/100 = 0.5
        Assert.Equal(0.5, result.Value, 1e-10);
    }

    [Fact]
    public void Normalize_Update_FlatRange_ReturnsHalf()
    {
        var norm = new Normalize(5);

        // All same values
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        var result = norm.Update(new TValue(DateTime.UtcNow, 100));

        // Flat range returns 0.5
        Assert.Equal(0.5, result.Value, 1e-10);
    }

    [Fact]
    public void Normalize_Update_IsNew_False_RollsBack()
    {
        var norm = new Normalize(5);

        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 50));

        var result1 = norm.Update(new TValue(DateTime.UtcNow, 25), isNew: true);
        var result2 = norm.Update(new TValue(DateTime.UtcNow, 75), isNew: false);

        // Both should use the same buffer state before the update
        // The last isNew=false should overwrite the isNew=true result
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Normalize_Update_NaN_UsesLastValid()
    {
        var norm = new Normalize(5);

        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        var valid = norm.Update(new TValue(DateTime.UtcNow, 50));

        var nanResult = norm.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.Equal(valid.Value, nanResult.Value, 1e-10);
    }

    [Fact]
    public void Normalize_Update_Infinity_UsesLastValid()
    {
        var norm = new Normalize(5);

        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        var valid = norm.Update(new TValue(DateTime.UtcNow, 50));

        var infResult = norm.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.Equal(valid.Value, infResult.Value, 1e-10);
    }

    [Fact]
    public void Normalize_IsHot_BecomesTrue_AfterWarmup()
    {
        var norm = new Normalize(5);

        for (int i = 0; i < 4; i++)
        {
            norm.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(norm.IsHot);
        }

        norm.Update(new TValue(DateTime.UtcNow, 40));
        Assert.True(norm.IsHot);
    }

    [Fact]
    public void Normalize_Reset_ClearsState()
    {
        var norm = new Normalize(5);

        for (int i = 0; i < 10; i++)
        {
            norm.Update(new TValue(DateTime.UtcNow, i * 10));
        }

        Assert.True(norm.IsHot);

        norm.Reset();

        Assert.False(norm.IsHot);
    }

    [Fact]
    public void Normalize_OutputAlwaysInRange()
    {
        var norm = new Normalize(20);
        var series = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in series)
        {
            var result = norm.Update(new TValue(bar.Time, bar.Close));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0,
                $"Normalize output {result.Value} should be in [0, 1]");
        }
    }

    [Fact]
    public void Normalize_Chaining_WorksCorrectly()
    {
        var source = new TSeries();
        var norm = new Normalize(source, 10);

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), i * 5));
        }

        Assert.True(norm.IsHot);
        // Last value is 95 (19*5), min in last 10 is 50 (10*5), max is 95
        // (95 - 50) / (95 - 50) = 1.0
        Assert.Equal(1.0, norm.Last.Value, 1e-10);
    }

    [Fact]
    public void Normalize_StaticCalculate_TSeries_MatchesStreaming()
    {
        var series = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tseries = new TSeries();
        foreach (var bar in series)
        {
            tseries.Add(new TValue(bar.Time, bar.Close), true);
        }

        // Static calculation
        var staticResult = Normalize.Calculate(tseries, 14);

        // Streaming calculation
        var streamNorm = new Normalize(14);
        var streamResult = new TSeries();
        foreach (var bar in series)
        {
            streamResult.Add(streamNorm.Update(new TValue(bar.Time, bar.Close)), true);
        }

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(staticResult[i].Value, streamResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Normalize_StaticCalculate_Span_MatchesStreaming()
    {
        var series = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] values = series.Select(b => b.Close).ToArray();
        double[] output = new double[values.Length];

        // Span calculation
        Normalize.Calculate(values, output, 14);

        // Streaming calculation
        var norm = new Normalize(14);
        for (int i = 0; i < values.Length; i++)
        {
            var result = norm.Update(new TValue(DateTime.UtcNow, values[i]));
            Assert.Equal(output[i], result.Value, 1e-10);
        }
    }

    [Fact]
    public void Normalize_StaticCalculate_Span_ValidatesParameters()
    {
        double[] source = { 1, 2, 3, 4, 5 };
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Normalize.Calculate(Array.Empty<double>(), output));
        Assert.Throws<ArgumentException>(() => Normalize.Calculate(source, new double[3]));
        Assert.Throws<ArgumentException>(() => Normalize.Calculate(source, output, 0));
    }

    [Fact]
    public void Normalize_RollingWindow_DropsOldValues()
    {
        var norm = new Normalize(3);

        // Feed: 0, 100, 50 -> range [0, 100]
        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 50));

        // Feed: 60, now window is [100, 50, 60] -> range [50, 100]
        // 60 in range [50, 100]: (60-50)/50 = 0.2
        var result = norm.Update(new TValue(DateTime.UtcNow, 60));
        Assert.Equal(0.2, result.Value, 1e-10);
    }
}
