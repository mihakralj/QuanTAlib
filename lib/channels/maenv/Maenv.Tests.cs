using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class MaenvTests
{
    [Fact]
    public void Maenv_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Maenv(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Maenv(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Maenv(10, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Maenv(10, -1.0));

        var m = new Maenv(10, 2.0);
        Assert.Equal(10, m.WarmupPeriod);
        Assert.Contains("Maenv", m.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Maenv_InitialState_Defaults()
    {
        var m = new Maenv(5);

        Assert.Equal(0, m.Last.Value);
        Assert.Equal(0, m.Upper.Value);
        Assert.Equal(0, m.Lower.Value);
        Assert.False(m.IsHot);
    }

    [Fact]
    public void Maenv_FirstValue_AllBandsCorrect()
    {
        var m = new Maenv(10, 1.0, MaenvType.EMA);

        var result = m.Update(new TValue(DateTime.UtcNow, 100));

        // First value: MA = input, bands at ±1%
        Assert.Equal(100.0, result.Value, 1e-10);
        Assert.Equal(101.0, m.Upper.Value, 1e-10);
        Assert.Equal(99.0, m.Lower.Value, 1e-10);
    }

    [Fact]
    public void Maenv_BandWidth_ProportionalToPercentage()
    {
        var m1 = new Maenv(10, 1.0);
        var m2 = new Maenv(10, 2.0);
        var m3 = new Maenv(10, 5.0);

        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            m1.Update(tv);
            m2.Update(tv);
            m3.Update(tv);
        }

        double width1 = m1.Upper.Value - m1.Lower.Value;
        double width2 = m2.Upper.Value - m2.Lower.Value;
        double width3 = m3.Upper.Value - m3.Lower.Value;

        // Width should scale with percentage (width = 2 * middle * pct / 100)
        Assert.Equal(width2, width1 * 2, 1e-9);
        Assert.Equal(width3, width1 * 5, 1e-9);
    }

    [Fact]
    public void Maenv_BandOrder_Correct()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var m = new Maenv(10, 2.0, maType);
            var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

            for (int i = 0; i < 50; i++)
            {
                var bar = gbm.Next(isNew: true);
                m.Update(new TValue(bar.Time, bar.Close));

                // Upper > Middle > Lower (for positive prices)
                Assert.True(m.Upper.Value > m.Last.Value, $"{maType}: Upper > Middle at bar {i}");
                Assert.True(m.Lower.Value < m.Last.Value, $"{maType}: Lower < Middle at bar {i}");
            }
        }
    }

    [Fact]
    public void Maenv_BandSymmetry_PercentageBased()
    {
        var m = new Maenv(10, 3.0, MaenvType.EMA);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            m.Update(new TValue(bar.Time, bar.Close));

            // Bands should be symmetric around middle
            double upperDist = m.Upper.Value - m.Last.Value;
            double lowerDist = m.Last.Value - m.Lower.Value;
            Assert.Equal(upperDist, lowerDist, 1e-10);

            // Distance should be exactly percentage of middle
            double expectedDist = m.Last.Value * 3.0 / 100.0;
            Assert.Equal(expectedDist, upperDist, 1e-10);
        }
    }

    [Fact]
    public void Maenv_SMA_CorrectCalculation()
    {
        var m = new Maenv(3, 1.0, MaenvType.SMA);

        m.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, m.Last.Value, 1e-10); // SMA(100) = 100

        m.Update(new TValue(DateTime.UtcNow, 110));
        Assert.Equal(105.0, m.Last.Value, 1e-10); // SMA(100,110) = 105

        m.Update(new TValue(DateTime.UtcNow, 120));
        Assert.Equal(110.0, m.Last.Value, 1e-10); // SMA(100,110,120) = 110

        m.Update(new TValue(DateTime.UtcNow, 130));
        Assert.Equal(120.0, m.Last.Value, 1e-10); // SMA(110,120,130) = 120
    }

    [Fact]
    public void Maenv_EMA_WarmupCompensation()
    {
        var m = new Maenv(20, 1.0, MaenvType.EMA);

        // Feed constant values
        for (int i = 0; i < 100; i++)
        {
            m.Update(new TValue(DateTime.UtcNow, 100));
        }

        // EMA should converge to 100 due to warmup compensation
        Assert.InRange(m.Last.Value, 99.9, 100.1);
    }

    [Fact]
    public void Maenv_WMA_WeightedCorrectly()
    {
        var m = new Maenv(3, 1.0, MaenvType.WMA);

        m.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, m.Last.Value, 1e-10);

        // Second value: weights (3,2) for values (110,100)
        // norm = 3*3 + 2*3 = 15, but for partial fill: w=(3-0)*3=9 for newest, w=(3-1)*3=6 for older
        // Actual: first bar w=9, second bar: newest w=9, oldest w=6; sum=110*9+100*6=990+600=1590; norm=15
        // WMA = 1590/15 = 106
        m.Update(new TValue(DateTime.UtcNow, 110));
        double expected2 = (110 * 9 + 100 * 6) / 15.0;
        Assert.Equal(expected2, m.Last.Value, 1e-10);
    }

    [Fact]
    public void Maenv_IsHot_TurnsTrueAfterWarmup()
    {
        var m = new Maenv(5, 1.0);

        for (int i = 0; i < 4; i++)
        {
            m.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(m.IsHot);
        }

        m.Update(new TValue(DateTime.UtcNow, 200));
        Assert.True(m.IsHot);
    }

    [Fact]
    public void Maenv_IsNewFalse_RebuildsState()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var m = new Maenv(10, 2.0, maType);
            var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

            TValue remembered = default;
            for (int i = 0; i < 30; i++)
            {
                var bar = gbm.Next(isNew: true);
                remembered = new TValue(bar.Time, bar.Close);
                m.Update(remembered, isNew: true);
            }

            double mid = m.Last.Value;
            double up = m.Upper.Value;
            double lo = m.Lower.Value;

            // Apply corrections
            for (int i = 0; i < 5; i++)
            {
                var bar = gbm.Next(isNew: false);
                m.Update(new TValue(bar.Time, bar.Close), isNew: false);
            }

            // Restore with remembered value
            m.Update(remembered, isNew: false);

            Assert.Equal(mid, m.Last.Value, 1e-6);
            Assert.Equal(up, m.Upper.Value, 1e-6);
            Assert.Equal(lo, m.Lower.Value, 1e-6);
        }
    }

    [Fact]
    public void Maenv_NaN_UsesLastValid()
    {
        var m = new Maenv(10, 2.0);

        m.Update(new TValue(DateTime.UtcNow, 100));
        m.Update(new TValue(DateTime.UtcNow, 105));

        var result = m.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(m.Upper.Value));
        Assert.True(double.IsFinite(m.Lower.Value));

        var result2 = m.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Maenv_Reset_Clears()
    {
        var m = new Maenv(10, 2.0);
        m.Update(new TValue(DateTime.UtcNow, 100));
        m.Update(new TValue(DateTime.UtcNow, 110));
        m.Update(new TValue(DateTime.UtcNow, 120));

        m.Reset();

        Assert.Equal(0, m.Last.Value);
        Assert.Equal(0, m.Upper.Value);
        Assert.Equal(0, m.Lower.Value);
        Assert.False(m.IsHot);

        m.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, m.Last.Value);
    }

    [Fact]
    public void Maenv_BatchVsStreaming_Match()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var mStream = new Maenv(20, 1.5, maType);
            var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
            var series = new TSeries();

            for (int i = 0; i < 200; i++)
            {
                var bar = gbm.Next(isNew: true);
                series.Add(new TValue(bar.Time, bar.Close));
                mStream.Update(series.Last, isNew: true);
            }

            double expectedMid = mStream.Last.Value;
            double expectedUp = mStream.Upper.Value;
            double expectedLo = mStream.Lower.Value;

            var (midBatch, upBatch, loBatch) = Maenv.Batch(series, 20, 1.5, maType);

            Assert.Equal(expectedMid, midBatch.Last.Value, 1e-9);
            Assert.Equal(expectedUp, upBatch.Last.Value, 1e-9);
            Assert.Equal(expectedLo, loBatch.Last.Value, 1e-9);
        }
    }

    [Fact]
    public void Maenv_SpanBatch_Validates()
    {
        double[] source = [100, 105, 110];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentOutOfRangeException>(() => Maenv.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Maenv.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Maenv.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 10, 0.0));
        Assert.Throws<ArgumentException>(() => Maenv.Batch(source.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Maenv_SpanBatch_ComputesCorrectly()
    {
        double[] source = [100, 105, 110, 107, 115];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Maenv.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, 2.0, MaenvType.EMA);

        // First value: MA = 100, bands at ±2%
        Assert.Equal(100.0, middle[0], 1e-10);
        Assert.Equal(102.0, upper[0], 1e-10);
        Assert.Equal(98.0, lower[0], 1e-10);

        // All bars: upper > middle > lower
        for (int i = 0; i < 5; i++)
        {
            Assert.True(upper[i] > middle[i], $"Upper > Middle at {i}");
            Assert.True(lower[i] < middle[i], $"Lower < Middle at {i}");
        }
    }

    [Fact]
    public void Maenv_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TSeries();
        series.Add(new TValue(DateTime.UtcNow, 100));
        series.Add(new TValue(DateTime.UtcNow, 105));
        series.Add(new TValue(DateTime.UtcNow, 102));

        var ((mid, up, lo), ind) = Maenv.Calculate(series, 2);

        Assert.True(double.IsFinite(mid.Last.Value));
        Assert.True(double.IsFinite(up.Last.Value));
        Assert.True(double.IsFinite(lo.Last.Value));

        // Continue streaming
        ind.Update(new TValue(DateTime.UtcNow, 108));
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Maenv_Event_Publishes()
    {
        var src = new TSeries();
        var m = new Maenv(src, 2);
        bool fired = false;
        m.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(fired);
    }

    [Fact]
    public void Maenv_AllMaTypes_ProduceFiniteResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var m = new Maenv(20, 2.5, maType);

            for (int i = 0; i < 200; i++)
            {
                var bar = gbm.Next(isNew: true);
                m.Update(new TValue(bar.Time, bar.Close));

                Assert.True(double.IsFinite(m.Last.Value), $"{maType} Middle finite at {i}");
                Assert.True(double.IsFinite(m.Upper.Value), $"{maType} Upper finite at {i}");
                Assert.True(double.IsFinite(m.Lower.Value), $"{maType} Lower finite at {i}");
            }
        }
    }

    [Fact]
    public void Maenv_LongSeriesStability()
    {
        var m = new Maenv(20, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.001, sigma: 0.02, seed: 123);

        for (int i = 0; i < 10000; i++)
        {
            var bar = gbm.Next(isNew: true);
            m.Update(new TValue(bar.Time, bar.Close));

            Assert.True(double.IsFinite(m.Last.Value), $"Middle finite at {i}");
            Assert.True(double.IsFinite(m.Upper.Value), $"Upper finite at {i}");
            Assert.True(double.IsFinite(m.Lower.Value), $"Lower finite at {i}");
            Assert.True(m.Upper.Value > m.Last.Value, $"Upper > Middle at {i}");
            Assert.True(m.Lower.Value < m.Last.Value, $"Lower < Middle at {i}");
        }
    }
}
