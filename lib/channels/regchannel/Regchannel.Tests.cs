using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class RegchannelTests
{
    private const int TestPeriod = 20;
    private const double TestMultiplier = 2.0;

    [Fact]
    public void Constructor_ValidParameters_CreatesIndicator()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);

        Assert.Equal($"Regchannel({TestPeriod},{TestMultiplier:F1})", ind.Name);
        Assert.Equal(TestPeriod, ind.WarmupPeriod);
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Regchannel(1));
    }

    [Fact]
    public void Constructor_ZeroMultiplier_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Regchannel(10, 0));
    }

    [Fact]
    public void Constructor_NegativeMultiplier_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Regchannel(10, -1));
    }

    [Fact]
    public void InitialState_AllDefaultValues()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);

        Assert.Equal(default, ind.Last);
        Assert.Equal(default, ind.Upper);
        Assert.Equal(default, ind.Lower);
        Assert.Equal(0, ind.Slope);
        Assert.Equal(0, ind.StdDev);
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void FirstValue_AllBandsEqualInput()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);
        var now = DateTime.UtcNow;

        ind.Update(new TValue(now, 100.0));

        Assert.Equal(100.0, ind.Last.Value, 1e-10);
        Assert.Equal(100.0, ind.Upper.Value, 1e-10);
        Assert.Equal(100.0, ind.Lower.Value, 1e-10);
    }

    [Fact]
    public void LinearData_ZeroStdDev()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        // Feed perfect linear data: y = 100 + i
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // With perfect linear fit, stddev should be ~0
        Assert.True(ind.StdDev < 1e-9, $"StdDev should be ~0 for linear data, got {ind.StdDev}");
        Assert.Equal(ind.Last.Value, ind.Upper.Value, 1e-9);
        Assert.Equal(ind.Last.Value, ind.Lower.Value, 1e-9);
    }

    [Fact]
    public void LinearData_CorrectSlope()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        // Feed perfect linear data: y = 100 + 2*i (slope = 2)
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + 2 * i));
        }

        // Slope should be 2
        Assert.Equal(2.0, ind.Slope, 1e-9);
    }

    [Fact]
    public void BandWidth_IncreasesWithVolatility()
    {
        var ind1 = new Regchannel(10, 2.0);
        var ind2 = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        // Low volatility: close to linear
        for (int i = 0; i < 20; i++)
        {
            ind1.Update(new TValue(now.AddMinutes(i), 100 + i + 0.1 * Math.Sin(i)));
        }

        // High volatility: large deviations from linear
        for (int i = 0; i < 20; i++)
        {
            ind2.Update(new TValue(now.AddMinutes(i), 100 + i + 5 * Math.Sin(i)));
        }

        double width1 = ind1.Upper.Value - ind1.Lower.Value;
        double width2 = ind2.Upper.Value - ind2.Lower.Value;

        Assert.True(width2 > width1, $"High volatility width ({width2}) should be > low volatility width ({width1})");
    }

    [Fact]
    public void BandsSymmetric_AroundMiddle()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i + Math.Sin(i) * 3));
        }

        double upperDist = ind.Upper.Value - ind.Last.Value;
        double lowerDist = ind.Last.Value - ind.Lower.Value;

        Assert.Equal(upperDist, lowerDist, 1e-10);
    }

    [Fact]
    public void MultiplierAffectsBandWidth()
    {
        var ind1 = new Regchannel(10, 1.0);
        var ind2 = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double val = 100 + i + Math.Sin(i) * 3;
            ind1.Update(new TValue(now.AddMinutes(i), val));
            ind2.Update(new TValue(now.AddMinutes(i), val));
        }

        double width1 = ind1.Upper.Value - ind1.Lower.Value;
        double width2 = ind2.Upper.Value - ind2.Lower.Value;

        Assert.Equal(width2, width1 * 2, 1e-9);
    }

    [Fact]
    public void IsNew_False_RollsBackState()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        // Add some initial data
        for (int i = 0; i < 15; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // Add new bar
        ind.Update(new TValue(now.AddMinutes(15), 200), isNew: true);
        var lastAfterNew = ind.Last.Value;

        // Update same bar with different value (isNew=false)
        ind.Update(new TValue(now.AddMinutes(15), 116), isNew: false);

        // Should be different from the 200 update
        Assert.NotEqual(lastAfterNew, ind.Last.Value);
    }

    [Fact]
    public void IsNew_False_IterativeCorrections()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // Multiple corrections to same bar
        ind.Update(new TValue(now.AddMinutes(15), 150), isNew: true);
        var first = ind.Last.Value;

        ind.Update(new TValue(now.AddMinutes(15), 160), isNew: false);
        var second = ind.Last.Value;

        ind.Update(new TValue(now.AddMinutes(15), 155), isNew: false);
        var third = ind.Last.Value;

        // All should be different (different inputs)
        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // Update with NaN
        ind.Update(new TValue(now.AddMinutes(10), double.NaN));

        // Should still produce finite result
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        ind.Update(new TValue(now.AddMinutes(10), double.PositiveInfinity));

        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        Assert.True(ind.IsHot);

        ind.Reset();

        Assert.False(ind.IsHot);
        Assert.Equal(default, ind.Last);
        Assert.Equal(default, ind.Upper);
        Assert.Equal(default, ind.Lower);
        Assert.Equal(0, ind.Slope);
        Assert.Equal(0, ind.StdDev);
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var ind = new Regchannel(10, 2.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 9; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
            Assert.False(ind.IsHot, $"Should not be hot at bar {i + 1}");
        }

        ind.Update(new TValue(now.AddMinutes(9), 109));
        Assert.True(ind.IsHot, "Should be hot after 10 bars");
    }

    [Fact]
    public void BatchVsStreaming_Match()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streamMiddle = new List<double>(source.Count);
        var streamUpper = new List<double>(source.Count);
        var streamLower = new List<double>(source.Count);

        foreach (var item in source)
        {
            ind.Update(item);
            streamMiddle.Add(ind.Last.Value);
            streamUpper.Add(ind.Upper.Value);
            streamLower.Add(ind.Lower.Value);
        }

        // Batch
        var (batchMiddle, batchUpper, batchLower) = Regchannel.Batch(source, TestPeriod, TestMultiplier);

        // Compare last 80 values (after warmup)
        for (int i = 20; i < source.Count; i++)
        {
            Assert.Equal(streamMiddle[i], batchMiddle[i].Value, 1e-9);
            Assert.Equal(streamUpper[i], batchUpper[i].Value, 1e-9);
            Assert.Equal(streamLower[i], batchLower[i].Value, 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streamMiddle = new List<double>(source.Count);
        var streamUpper = new List<double>(source.Count);
        var streamLower = new List<double>(source.Count);

        foreach (var item in source)
        {
            ind.Update(item);
            streamMiddle.Add(ind.Last.Value);
            streamUpper.Add(ind.Upper.Value);
            streamLower.Add(ind.Lower.Value);
        }

        // Span batch
        var middle = new double[source.Count];
        var upper = new double[source.Count];
        var lower = new double[source.Count];

        Regchannel.Batch(source.Values, middle, upper, lower, TestPeriod, TestMultiplier);

        // Compare last 80 values
        for (int i = 20; i < source.Count; i++)
        {
            Assert.Equal(streamMiddle[i], middle[i], 1e-9);
            Assert.Equal(streamUpper[i], upper[i], 1e-9);
            Assert.Equal(streamLower[i], lower[i], 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_ValidatesOutputLength()
    {
        var source = new double[100];
        var middle = new double[50]; // Too short
        var upper = new double[100];
        var lower = new double[100];

        Assert.Throws<ArgumentException>(() =>
            Regchannel.Batch(source, middle, upper, lower, 10));
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        var source = new double[100];
        var middle = new double[100];
        var upper = new double[100];
        var lower = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Regchannel.Batch(source, middle, upper, lower, 1));
    }

    [Fact]
    public void SpanBatch_ValidatesMultiplier()
    {
        var source = new double[100];
        var middle = new double[100];
        var upper = new double[100];
        var lower = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Regchannel.Batch(source, middle, upper, lower, 10, 0));
    }

    [Fact]
    public void Event_FiresOnUpdate()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);
        var now = DateTime.UtcNow;
        int eventCount = 0;

        ind.Pub += (object? sender, in TValueEventArgs e) => eventCount++;

        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        Assert.Equal(30, eventCount);
    }

    [Fact]
    public void LongSeries_StableResults()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10000; i++)
        {
            double val = 100 + Math.Sin(i * 0.01) * 10 + i * 0.001;
            ind.Update(new TValue(now.AddMinutes(i), val));
        }

        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
        Assert.True(double.IsFinite(ind.Slope));
        Assert.True(double.IsFinite(ind.StdDev));
        Assert.True(ind.Upper.Value >= ind.Last.Value);
        Assert.True(ind.Lower.Value <= ind.Last.Value);
    }

    [Fact]
    public void Prime_SetsCorrectState()
    {
        var ind = new Regchannel(TestPeriod, TestMultiplier);
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        ind.Prime(source);

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var (results, indicator) = Regchannel.Calculate(source, TestPeriod, TestMultiplier);

        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
        Assert.Equal(source.Count, results.Middle.Count);
        Assert.Equal(source.Count, results.Upper.Count);
        Assert.Equal(source.Count, results.Lower.Count);
    }
}
