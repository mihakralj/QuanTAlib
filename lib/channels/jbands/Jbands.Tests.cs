using Xunit;

namespace QuanTAlib.Tests;

public class JbandsTests
{
    [Fact]
    public void Jbands_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Jbands(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Jbands(-5));
        Assert.Throws<ArgumentException>(() => new Jbands(14, 0, double.NaN));

        var j = new Jbands(14);
        Assert.Contains("Jbands", j.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(j.WarmupPeriod > 0);
    }

    [Fact]
    public void Jbands_InitialState_Defaults()
    {
        var j = new Jbands(14);

        Assert.Equal(0, j.Last.Value);
        Assert.Equal(0, j.Upper.Value);
        Assert.Equal(0, j.Lower.Value);
        Assert.False(j.IsHot);
    }

    [Fact]
    public void Jbands_FirstBar_AllBandsEqual()
    {
        var j = new Jbands(14);
        j.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.Equal(100.0, j.Last.Value, 1e-10);
        Assert.Equal(100.0, j.Upper.Value, 1e-10);
        Assert.Equal(100.0, j.Lower.Value, 1e-10);
    }

    [Fact]
    public void Jbands_UpperBand_SnapToNewHigh()
    {
        var j = new Jbands(14);
        j.Update(new TValue(DateTime.UtcNow, 100.0));
        j.Update(new TValue(DateTime.UtcNow, 105.0));

        // Upper should snap to new high
        Assert.Equal(105.0, j.Upper.Value, 1e-10);
    }

    [Fact]
    public void Jbands_LowerBand_SnapToNewLow()
    {
        var j = new Jbands(14);
        j.Update(new TValue(DateTime.UtcNow, 100.0));
        j.Update(new TValue(DateTime.UtcNow, 95.0));

        // Lower should snap to new low
        Assert.Equal(95.0, j.Lower.Value, 1e-10);
    }

    [Fact]
    public void Jbands_BandsDecay_TowardPrice()
    {
        var j = new Jbands(14);

        // Create a spike then return to baseline
        j.Update(new TValue(DateTime.UtcNow, 100.0));
        j.Update(new TValue(DateTime.UtcNow, 120.0)); // Upper snaps to 120
        double upperAfterSpike = j.Upper.Value;

        // Feed lower prices - upper band should decay
        for (int i = 0; i < 20; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        // Upper band should have decayed toward price
        Assert.True(j.Upper.Value < upperAfterSpike);
        Assert.True(j.Upper.Value > 100.0); // But not below price yet
    }

    [Fact]
    public void Jbands_IsHot_TurnsTrueAfterWarmup()
    {
        var j = new Jbands(7);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.05, seed: 42);

        int warmup = j.WarmupPeriod;
        for (int i = 0; i < warmup - 1; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, gbm.Next().Close));
            Assert.False(j.IsHot);
        }

        j.Update(new TValue(DateTime.UtcNow, gbm.Next().Close));
        Assert.True(j.IsHot);
    }

    [Fact]
    public void Jbands_IsNewFalse_RestoresState()
    {
        var j = new Jbands(14);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TValue remembered = default;
        for (int i = 0; i < 50; i++)
        {
            remembered = new TValue(DateTime.UtcNow, gbm.Next().Close);
            j.Update(remembered, isNew: true);
        }

        double mid = j.Last.Value;
        double up = j.Upper.Value;
        double lo = j.Lower.Value;

        // Multiple corrections
        for (int i = 0; i < 5; i++)
        {
            var corrected = new TValue(DateTime.UtcNow, gbm.Next().Close);
            j.Update(corrected, isNew: false);
        }

        // Restore with original value
        j.Update(remembered, isNew: false);

        Assert.Equal(mid, j.Last.Value, 1e-10);
        Assert.Equal(up, j.Upper.Value, 1e-10);
        Assert.Equal(lo, j.Lower.Value, 1e-10);
    }

    [Fact]
    public void Jbands_NaN_UsesLastValid()
    {
        var j = new Jbands(14);

        j.Update(new TValue(DateTime.UtcNow, 100.0));
        j.Update(new TValue(DateTime.UtcNow, 105.0));

        var result = j.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(j.Upper.Value));
        Assert.True(double.IsFinite(j.Lower.Value));

        var result2 = j.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Jbands_Reset_Clears()
    {
        var j = new Jbands(14);
        for (int i = 0; i < 50; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        j.Reset();

        Assert.Equal(0, j.Last.Value);
        Assert.Equal(0, j.Upper.Value);
        Assert.Equal(0, j.Lower.Value);
        Assert.False(j.IsHot);

        j.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, j.Last.Value);
    }

    [Fact]
    public void Jbands_BatchVsStreaming_Match()
    {
        var jStream = new Jbands(14, 0, 0.45);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            jStream.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        double expectedMid = jStream.Last.Value;
        double expectedUp = jStream.Upper.Value;
        double expectedLo = jStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Jbands.Batch(series, 14, 0, 0.45);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Jbands_SpanCalculate_ValidatesArgs()
    {
        double[] source = [100, 105, 110];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];
        double[] shortOut = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Jbands.Batch(source.AsSpan(), shortOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 14));
        Assert.Throws<ArgumentException>(() =>
            Jbands.Batch(source.AsSpan(), middle.AsSpan(), shortOut.AsSpan(), lower.AsSpan(), 14));
        Assert.Throws<ArgumentException>(() =>
            Jbands.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), shortOut.AsSpan(), 14));
    }

    [Fact]
    public void Jbands_SpanCalculate_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 123);
        double[] source = new double[100];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        double[] middle = new double[100];
        double[] upper = new double[100];
        double[] lower = new double[100];

        Jbands.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 14);

        var jStream = new Jbands(14);
        for (int i = 0; i < source.Length; i++)
        {
            jStream.Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }

        Assert.Equal(jStream.Last.Value, middle[^1], 1e-10);
        Assert.Equal(jStream.Upper.Value, upper[^1], 1e-10);
        Assert.Equal(jStream.Lower.Value, lower[^1], 1e-10);
    }

    [Fact]
    public void Jbands_Event_Publishes()
    {
        var j = new Jbands(14);
        bool fired = false;
        j.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        j.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(fired);
    }

    [Fact]
    public void Jbands_Chaining_Works()
    {
        var src = new TSeries();
        var j = new Jbands(14);
        var downstream = new Sma(j, 5);

        for (int i = 0; i < 100; i++)
        {
            var val = new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i * 0.5);
            src.Add(val.Time, val.Value);
            j.Update(val, isNew: true);
        }

        Assert.True(downstream.IsHot);
        Assert.True(double.IsFinite(downstream.Last.Value));
    }

    [Fact]
    public void Jbands_MiddleBand_MatchesJma()
    {
        // Verify that middle band matches standalone JMA
        var jbands = new Jbands(14, 0, 0.45);
        var jma = new Jma(14, 0, 0.45);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 999);

        for (int i = 0; i < 200; i++)
        {
            double price = gbm.Next().Close;
            var tv = new TValue(DateTime.UtcNow, price);
            jbands.Update(tv, isNew: true);
            jma.Update(tv, isNew: true);
        }

        Assert.Equal(jma.Last.Value, jbands.Last.Value, 1e-10);
    }

    [Fact]
    public void Jbands_Phase_AffectsBehavior()
    {
        var jNeutral = new Jbands(14, 0);
        var jPositive = new Jbands(14, 50);
        var jNegative = new Jbands(14, -50);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            double price = gbm.Next().Close;
            var tv = new TValue(DateTime.UtcNow, price);
            jNeutral.Update(tv, isNew: true);
            jPositive.Update(tv, isNew: true);
            jNegative.Update(tv, isNew: true);
        }

        // Different phase settings should produce different JMA values
        Assert.NotEqual(jNeutral.Last.Value, jPositive.Last.Value, 1e-6);
        Assert.NotEqual(jNeutral.Last.Value, jNegative.Last.Value, 1e-6);
    }

    [Fact]
    public void Jbands_UpperAlwaysAboveLower()
    {
        var j = new Jbands(14);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.2, seed: 77);

        for (int i = 0; i < 500; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, gbm.Next().Close), isNew: true);
            Assert.True(j.Upper.Value >= j.Lower.Value);
        }
    }
}
