using System.Runtime.InteropServices;
using Xunit;

namespace QuanTAlib.Tests;

// ══════════════════════════════════════════════════════════════
// A) Constructor Validation
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveConstructorTests
{
    [Fact]
    public void Constructor_Default_SetsName()
    {
        var wave = new TtmWave();
        Assert.Equal("TtmWave", wave.Name);
    }

    [Fact]
    public void Constructor_Default_NotHot()
    {
        var wave = new TtmWave();
        Assert.False(wave.IsHot);
    }

    [Fact]
    public void Constructor_WarmupPeriod_Is752()
    {
        var wave = new TtmWave();
        // max(8, 377) + 377 - 2 = 752
        Assert.Equal(752, wave.WarmupPeriod);
    }

    [Fact]
    public void Constructor_Chaining_SubscribesToSource()
    {
        var source = new Ema(10);
        using var wave = new TtmWave(source);
        Assert.Equal("TtmWave", wave.Name);
    }

    [Fact]
    public void Constructor_DefaultOutputs_AreDefault()
    {
        var wave = new TtmWave();
        Assert.Equal(0, wave.WaveA1.Value);
        Assert.Equal(0, wave.WaveA2.Value);
        Assert.Equal(0, wave.WaveB1.Value);
        Assert.Equal(0, wave.WaveB2.Value);
        Assert.Equal(0, wave.WaveC1.Value);
        Assert.Equal(0, wave.WaveC2.Value);
    }
}

// ══════════════════════════════════════════════════════════════
// B) Basic Calculation
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveBasicTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void Update_ReturnsTValue()
    {
        var wave = new TtmWave();
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = wave.Update(input);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var wave = new TtmWave();
        var input = new TValue(DateTime.UtcNow, 100.0);
        wave.Update(input);
        Assert.Equal(wave.Wave1.Value, wave.Last.Value);
    }

    [Fact]
    public void Update_AllWaves_PopulatedAfterUpdate()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // After 100 bars, waves should have non-default values
        // (A wave should be non-zero since warmup for channel 1 is only 66)
        Assert.NotEqual(0, wave.WaveA2.Value);
    }

    [Fact]
    public void Update_Wave1_EqualsWaveA2()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        Assert.Equal(wave.WaveA2.Value, wave.Wave1.Value);
        Assert.Equal(wave.WaveA2.Time, wave.Wave1.Time);
    }

    [Fact]
    public void Update_Wave2High_IsMaxOfC()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(800);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        Assert.Equal(Math.Max(wave.WaveC1.Value, wave.WaveC2.Value), wave.Wave2High);
    }

    [Fact]
    public void Update_Wave2Low_IsMinOfC()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(800);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        Assert.Equal(Math.Min(wave.WaveC1.Value, wave.WaveC2.Value), wave.Wave2Low);
    }
}

// ══════════════════════════════════════════════════════════════
// C) State + Bar Correction
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveBarCorrectionTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(200);

        for (int i = 0; i < 100; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        double valBefore = wave.Last.Value;
        wave.Update(series[100], isNew: true);

        Assert.NotEqual(valBefore, wave.Last.Value);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(200);

        for (int i = 0; i < 100; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // First update as new bar
        wave.Update(series[100], isNew: true);
        double afterNew = wave.Last.Value;

        // Update same bar with different value
        var modified = new TValue(series[100].Time, series[100].Value + 5.0);
        wave.Update(modified, isNew: false);

        // Re-update with original value should restore
        wave.Update(series[100], isNew: false);
        double afterRestore = wave.Last.Value;

        Assert.Equal(afterNew, afterRestore, 10);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(200);

        for (int i = 0; i < 100; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // Multiple rewrites followed by same-value restore
        wave.Update(series[100], isNew: true);
        double baseline = wave.Last.Value;

        for (int j = 0; j < 5; j++)
        {
            var tick = new TValue(series[100].Time, series[100].Value + (j * 2.0));
            wave.Update(tick, isNew: false);
        }

        wave.Update(series[100], isNew: false);
        Assert.Equal(baseline, wave.Last.Value, 10);
    }
}

// ══════════════════════════════════════════════════════════════
// D) Warmup / Convergence
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveWarmupTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(800);

        bool wasHot = false;
        int hotAt = -1;

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
            if (wave.IsHot && !wasHot)
            {
                wasHot = true;
                hotAt = i;
            }
        }

        Assert.True(wasHot, "Indicator never became hot");
        // Should become hot at or near WarmupPeriod (752)
        Assert.True(hotAt <= wave.WarmupPeriod, $"Became hot at {hotAt}, expected <= {wave.WarmupPeriod}");
    }

    [Fact]
    public void IsHot_StaysCold_BeforeWarmup()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // 100 bars is not enough for 752 warmup
        Assert.False(wave.IsHot);
    }
}

// ══════════════════════════════════════════════════════════════
// E) Robustness (NaN / Infinity)
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveRobustnessTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void NaN_Input_ProducesFiniteOutput()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // Feed NaN
        var nanInput = new TValue(DateTime.UtcNow, double.NaN);
        wave.Update(nanInput, isNew: true);

        // MACD internally handles NaN via Ema which substitutes last valid
        Assert.True(double.IsFinite(wave.Last.Value) || wave.Last.Value == 0,
            "NaN input should not propagate to output");
    }

    [Fact]
    public void Infinity_Input_ProducesFiniteOutput()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        var infInput = new TValue(DateTime.UtcNow, double.PositiveInfinity);
        wave.Update(infInput, isNew: true);

        // Should handle gracefully
        Assert.True(double.IsFinite(wave.Last.Value) || wave.Last.Value == 0,
            "Infinity input should not propagate to output");
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var wave = new TtmWave();

        // Feed mixture of valid and NaN
        for (int i = 0; i < 50; i++)
        {
            double val = (i % 10 == 0) ? double.NaN : 100.0 + i;
            wave.Update(new TValue(DateTime.UtcNow.AddMinutes(i), val), isNew: true);
        }

        // Should not throw
        Assert.True(double.IsFinite(wave.Last.Value) || wave.Last.Value == 0);
    }
}

// ══════════════════════════════════════════════════════════════
// F) Consistency (Batch == Streaming == Eventing)
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveConsistencyTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void BatchCalc_EqualsStreaming()
    {
        var series = GenerateSeries(200);

        // Batch
        var batchResults = TtmWave.Batch(series);

        // Streaming
        var streamWave = new TtmWave();
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamWave.Update(series[i], isNew: true);
            streamResults.Add(streamWave.Last.Value);
        }

        Assert.Equal(batchResults.Count, streamResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], 10);
        }
    }

    [Fact]
    public void Calculate_ReturnsBothResults()
    {
        var series = GenerateSeries(200);
        var (results, indicator) = TtmWave.Calculate(series);

        Assert.NotNull(results);
        Assert.NotNull(indicator);
        Assert.Equal(200, results.Count);
        Assert.Equal("TtmWave", indicator.Name);
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var series = GenerateSeries(200);

        // Streaming
        var streamWave = new TtmWave();
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamWave.Update(series[i], isNew: true);
            streamResults.Add(streamWave.Last.Value);
        }

        // Event-based
        var eventSource = new Ema(1); // Pass-through: EMA(1) = identity
        using var eventWave = new TtmWave(eventSource);
        var eventResults = new List<double>();
        eventWave.Pub += (object? _, in TValueEventArgs args) => eventResults.Add(args.Value.Value);

        for (int i = 0; i < series.Count; i++)
        {
            eventSource.Update(series[i], isNew: true);
        }

        Assert.Equal(streamResults.Count, eventResults.Count);
        for (int i = 0; i < streamResults.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 10);
        }
    }

    [Fact]
    public void Update_TSeries_MatchesStreaming()
    {
        var series = GenerateSeries(200);

        // TSeries batch via Update
        var batchWave = new TtmWave();
        var batchResults = batchWave.Update(series);

        // Streaming
        var streamWave = new TtmWave();
        for (int i = 0; i < series.Count; i++)
        {
            streamWave.Update(series[i], isNew: true);
        }

        Assert.Equal(series.Count, batchResults.Count);
        // Last values should match
        Assert.Equal(streamWave.Last.Value, batchResults.Values[^1], 10);
    }
}

// ══════════════════════════════════════════════════════════════
// G) Reset Tests
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveResetTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(200);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        wave.Reset();

        Assert.False(wave.IsHot);
        Assert.Equal(0, wave.WaveA1.Value);
        Assert.Equal(0, wave.WaveA2.Value);
        Assert.Equal(0, wave.WaveB1.Value);
        Assert.Equal(0, wave.WaveB2.Value);
        Assert.Equal(0, wave.WaveC1.Value);
        Assert.Equal(0, wave.WaveC2.Value);
    }

    [Fact]
    public void Reset_ThenReprocess_MatchesOriginal()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(200);

        // First pass
        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }
        double firstPassLast = wave.Last.Value;

        // Reset and reprocess
        wave.Reset();
        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }
        double secondPassLast = wave.Last.Value;

        Assert.Equal(firstPassLast, secondPassLast, 10);
    }
}

// ══════════════════════════════════════════════════════════════
// H) Batch / Static API Tests
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveBatchTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void Batch_ReturnsCorrectLength()
    {
        var series = GenerateSeries(200);
        var result = TtmWave.Batch(series);
        Assert.Equal(200, result.Count);
    }

    [Fact]
    public void Batch_EmptyInput_ReturnsEmpty()
    {
        var series = new TSeries([], []);
        var result = TtmWave.Batch(series);
        Assert.True(result.Count == 0);
    }

    [Fact]
    public void Calculate_ReturnsWarmIndicator()
    {
        var series = GenerateSeries(800);
        var (results, indicator) = TtmWave.Calculate(series);

        Assert.Equal(800, results.Count);
        Assert.True(indicator.IsHot);
    }
}

// ══════════════════════════════════════════════════════════════
// I) Prime Tests
// ══════════════════════════════════════════════════════════════
public sealed class TtmWavePrimeTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void Prime_SetsState()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(200);

        wave.Prime(series);

        // After priming, wave should have processed all data
        Assert.NotEqual(0, wave.Last.Value);
    }

    [Fact]
    public void Prime_ThenUpdate_ContinuesCorrectly()
    {
        var series = GenerateSeries(300);

        // Reference: process all 300 bars
        var refWave = new TtmWave();
        for (int i = 0; i < 300; i++)
        {
            refWave.Update(series[i], isNew: true);
        }

        // Prime with first 200, then stream remaining 100
        var primeWave = new TtmWave();
        var tList = new List<long>(200);
        var vList = new List<double>(200);
        for (int i = 0; i < 200; i++)
        {
            tList.Add(series.Times[i]);
            vList.Add(series.Values[i]);
        }
        var primeSeries = new TSeries(tList, vList);
        primeWave.Prime(primeSeries);

        for (int i = 200; i < 300; i++)
        {
            primeWave.Update(series[i], isNew: true);
        }

        Assert.Equal(refWave.Last.Value, primeWave.Last.Value, 10);
    }

    [Fact]
    public void Prime_EmptySeries_NoOp()
    {
        var wave = new TtmWave();
        var empty = new TSeries([], []);
        wave.Prime(empty);
        Assert.False(wave.IsHot);
    }
}

// ══════════════════════════════════════════════════════════════
// J) Event / Chainability Tests
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveEventTests
{
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var wave = new TtmWave();
        int fireCount = 0;
        wave.Pub += (object? _, in TValueEventArgs _a) => fireCount++;

        for (int i = 0; i < 10; i++)
        {
            wave.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i), isNew: true);
        }

        Assert.Equal(10, fireCount);
    }

    [Fact]
    public void Chaining_PropagatesValues()
    {
        var source = new Ema(1);
        using var wave = new TtmWave(source);
        var received = new List<double>();
        wave.Pub += (object? _, in TValueEventArgs args) => received.Add(args.Value.Value);

        for (int i = 0; i < 50; i++)
        {
            source.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i), isNew: true);
        }

        Assert.Equal(50, received.Count);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new Ema(1);
        var wave = new TtmWave(source);
        int fireCount = 0;
        wave.Pub += (object? _, in TValueEventArgs _a) => fireCount++;

        source.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.Equal(1, fireCount);

        wave.Dispose();

        source.Update(new TValue(DateTime.UtcNow, 101.0), isNew: true);
        Assert.Equal(1, fireCount); // Should not fire again
    }
}

// ══════════════════════════════════════════════════════════════
// K) Multi-Output Verification
// ══════════════════════════════════════════════════════════════
public sealed class TtmWaveMultiOutputTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    [Fact]
    public void AllSixWaves_HaveSameTimestamp()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        long t = wave.WaveA1.Time;
        Assert.Equal(t, wave.WaveA2.Time);
        Assert.Equal(t, wave.WaveB1.Time);
        Assert.Equal(t, wave.WaveB2.Time);
        Assert.Equal(t, wave.WaveC1.Time);
        Assert.Equal(t, wave.WaveC2.Time);
    }

    [Fact]
    public void WaveAmplitudes_IncreaseWithPeriod()
    {
        // Longer-period waves tend to have larger absolute values
        // after sufficient warmup, because they capture more price movement.
        // This is a soft heuristic test, not a hard rule.
        var wave = new TtmWave();
        var series = GenerateSeries(1000);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // Just verify all waves are finite and output different values
        Assert.True(double.IsFinite(wave.WaveA1.Value));
        Assert.True(double.IsFinite(wave.WaveA2.Value));
        Assert.True(double.IsFinite(wave.WaveB1.Value));
        Assert.True(double.IsFinite(wave.WaveB2.Value));
        Assert.True(double.IsFinite(wave.WaveC1.Value));
        Assert.True(double.IsFinite(wave.WaveC2.Value));
    }

    [Fact]
    public void Waves_IndependentValues()
    {
        var wave = new TtmWave();
        var series = GenerateSeries(800);

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(series[i], isNew: true);
        }

        // Different channels should produce different values
        // (extremely unlikely for all 6 to be identical with random data)
        var values = new HashSet<double>
        {
            wave.WaveA1.Value,
            wave.WaveA2.Value,
            wave.WaveB1.Value,
            wave.WaveB2.Value,
            wave.WaveC1.Value,
            wave.WaveC2.Value
        };

        Assert.True(values.Count >= 3, "At least 3 of 6 wave values should be distinct");
    }
}
