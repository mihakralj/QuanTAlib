using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Jbands against JMA internal bands.
/// Since Jbands exposes JMA's internal envelope bands, we validate:
/// 1. Middle band matches standalone JMA exactly
/// 2. All four API modes produce consistent results
/// 3. Band behavior matches JMA specification
/// </summary>
public class JbandsValidationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Jbands_MiddleBand_MatchesJma_Period7()
    {
        ValidateMiddleBandMatchesJma(7, 0, 0.45, 42);
    }

    [Fact]
    public void Jbands_MiddleBand_MatchesJma_Period14()
    {
        ValidateMiddleBandMatchesJma(14, 0, 0.45, 123);
    }

    [Fact]
    public void Jbands_MiddleBand_MatchesJma_Period20()
    {
        ValidateMiddleBandMatchesJma(20, 0, 0.45, 456);
    }

    [Fact]
    public void Jbands_MiddleBand_MatchesJma_WithPhase()
    {
        ValidateMiddleBandMatchesJma(14, 50, 0.45, 789);
        ValidateMiddleBandMatchesJma(14, -50, 0.45, 321);
        ValidateMiddleBandMatchesJma(14, 100, 0.45, 654);
        ValidateMiddleBandMatchesJma(14, -100, 0.45, 987);
    }

    private static void ValidateMiddleBandMatchesJma(int period, int phase, double power, int seed)
    {
        var jbands = new Jbands(period, phase, power);
        var jma = new Jma(period, phase, power);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: seed);

        for (int i = 0; i < 500; i++)
        {
            double price = gbm.Next().Close;
            var tv = new TValue(DateTime.UtcNow, price);
            jbands.Update(tv, isNew: true);
            jma.Update(tv, isNew: true);

            Assert.Equal(jma.Last.Value, jbands.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Jbands_StreamingVsBatch_Match()
    {
        var jStream = new Jbands(14, 0, 0.45);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 300; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            jStream.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        var (midBatch, upBatch, loBatch) = Jbands.Batch(series, 14, 0, 0.45);

        // Compare last 100 values
        for (int i = series.Count - 100; i < series.Count; i++)
        {
            // Rebuild streaming to get value at index i
            var jCheck = new Jbands(14, 0, 0.45);
            for (int j = 0; j <= i; j++)
            {
                jCheck.Update(new TValue(new DateTime(series.Times[j], DateTimeKind.Utc), series.Values[j]), isNew: true);
            }

            Assert.Equal(jCheck.Last.Value, midBatch.Values[i], Tolerance);
            Assert.Equal(jCheck.Upper.Value, upBatch.Values[i], Tolerance);
            Assert.Equal(jCheck.Lower.Value, loBatch.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Jbands_StreamingVsSpan_Match()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 777);
        double[] source = new double[200];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        double[] middle = new double[200];
        double[] upper = new double[200];
        double[] lower = new double[200];

        Jbands.Calculate(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 14);

        var jStream = new Jbands(14);
        for (int i = 0; i < source.Length; i++)
        {
            jStream.Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);

            Assert.Equal(jStream.Last.Value, middle[i], Tolerance);
            Assert.Equal(jStream.Upper.Value, upper[i], Tolerance);
            Assert.Equal(jStream.Lower.Value, lower[i], Tolerance);
        }
    }

    [Fact]
    public void Jbands_AllFourModes_Consistent()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.15, seed: 555);
        var series = new TSeries();
        double[] rawValues = new double[150];

        for (int i = 0; i < 150; i++)
        {
            var bar = gbm.Next();
            series.Add(bar.Time, bar.Close);
            rawValues[i] = bar.Close;
        }

        // Mode 1: Streaming
        var jStream = new Jbands(14, 25, 0.45);
        for (int i = 0; i < rawValues.Length; i++)
        {
            jStream.Update(new TValue(DateTime.UtcNow, rawValues[i]), isNew: true);
        }

        // Mode 2: Batch (TSeries)
        var (midBatch, upBatch, loBatch) = Jbands.Batch(series, 14, 25, 0.45);

        // Mode 3: Span Calculate
        double[] middleSpan = new double[150];
        double[] upperSpan = new double[150];
        double[] lowerSpan = new double[150];
        Jbands.Calculate(rawValues.AsSpan(), middleSpan.AsSpan(), upperSpan.AsSpan(), lowerSpan.AsSpan(), 14, 25, 0.45);

        // Mode 4: Event-based
        var jEvent = new Jbands(14, 25, 0.45);
        double lastEventMid = 0, lastEventUp = 0, lastEventLo = 0;
        jEvent.Pub += (object? sender, in TValueEventArgs args) =>
        {
            lastEventMid = args.Value.Value;
        };
        for (int i = 0; i < rawValues.Length; i++)
        {
            jEvent.Update(new TValue(DateTime.UtcNow, rawValues[i]), isNew: true);
        }
        lastEventUp = jEvent.Upper.Value;
        lastEventLo = jEvent.Lower.Value;

        // All modes should match
        Assert.Equal(jStream.Last.Value, midBatch.Last.Value, Tolerance);
        Assert.Equal(jStream.Upper.Value, upBatch.Last.Value, Tolerance);
        Assert.Equal(jStream.Lower.Value, loBatch.Last.Value, Tolerance);

        Assert.Equal(jStream.Last.Value, middleSpan[^1], Tolerance);
        Assert.Equal(jStream.Upper.Value, upperSpan[^1], Tolerance);
        Assert.Equal(jStream.Lower.Value, lowerSpan[^1], Tolerance);

        Assert.Equal(jStream.Last.Value, lastEventMid, Tolerance);
        Assert.Equal(jStream.Upper.Value, lastEventUp, Tolerance);
        Assert.Equal(jStream.Lower.Value, lastEventLo, Tolerance);
    }

    [Fact]
    public void Jbands_BandBehavior_SnapAndDecay()
    {
        var j = new Jbands(14);

        // Start at baseline
        for (int i = 0; i < 50; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        }

        // Spike up - upper should snap instantly
        j.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        Assert.Equal(110.0, j.Upper.Value, Tolerance);
        Assert.True(j.Lower.Value < 110.0); // Lower should NOT snap up

        // Return to baseline - upper should decay gradually
        double prevUpper = j.Upper.Value;
        for (int i = 0; i < 30; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
            Assert.True(j.Upper.Value <= prevUpper); // Monotonically decreasing
            prevUpper = j.Upper.Value;
        }

        // Spike down - lower should snap instantly
        j.Update(new TValue(DateTime.UtcNow, 90.0), isNew: true);
        Assert.Equal(90.0, j.Lower.Value, Tolerance);
        Assert.True(j.Upper.Value > 90.0); // Upper should NOT snap down

        // Return to baseline - lower should decay gradually
        double prevLower = j.Lower.Value;
        for (int i = 0; i < 30; i++)
        {
            j.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
            Assert.True(j.Lower.Value >= prevLower); // Monotonically increasing
            prevLower = j.Lower.Value;
        }
    }

    [Fact]
    public void Jbands_Warmup_ConsistentAcrossModes()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 333);
        var series = new TSeries();
        double[] rawValues = new double[50];

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            series.Add(bar.Time, bar.Close);
            rawValues[i] = bar.Close;
        }

        // Streaming warmup
        var jStream = new Jbands(14);
        for (int i = 0; i < rawValues.Length; i++)
        {
            jStream.Update(new TValue(DateTime.UtcNow, rawValues[i]), isNew: true);
        }
        int warmupPeriod = jStream.WarmupPeriod;

        // Span mode warmup values
        double[] middle = new double[50];
        double[] upper = new double[50];
        double[] lower = new double[50];
        Jbands.Calculate(rawValues.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 14);

        // After warmup, values should be stable and match
        for (int i = warmupPeriod; i < rawValues.Length; i++)
        {
            var jCheck = new Jbands(14);
            for (int j = 0; j <= i; j++)
            {
                jCheck.Update(new TValue(DateTime.UtcNow, rawValues[j]), isNew: true);
            }

            Assert.Equal(jCheck.Last.Value, middle[i], Tolerance);
            Assert.Equal(jCheck.Upper.Value, upper[i], Tolerance);
            Assert.Equal(jCheck.Lower.Value, lower[i], Tolerance);
        }
    }
}
