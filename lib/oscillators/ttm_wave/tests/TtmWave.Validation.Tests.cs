using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// TTM Wave validation tests.
/// No external libraries (Skender/TA-Lib/Tulip/Ooples) implement TTM Wave,
/// so validation is self-consistency: streaming vs batch, prime vs cold,
/// deterministic reproducibility, and multi-wave coherence checks.
/// </summary>
public sealed class TtmWaveValidationTests
{
    private readonly ITestOutputHelper _output;

    public TtmWaveValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        // Extract close prices into TSeries for TtmWave (which operates on single values)
        var t = new List<long>(count);
        var v = new List<double>(count);
        for (int i = 0; i < bars.Count; i++)
        {
            t.Add(bars[i].Time);
            v.Add(bars[i].Close); // Close price
        }
        return new TSeries(t, v);
    }

    // --- A) Streaming vs Batch agreement ---

    [Fact]
    public void Streaming_Matches_Batch()
    {
        var series = GenerateSeries(1000);

        var wave = new TtmWave();
        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }

        var batch = TtmWave.Batch(series);

        Assert.Equal(wave.Last.Value, batch[^1].Value, 1e-10);
        _output.WriteLine($"Streaming last={wave.Last.Value:F10}, Batch last={batch[^1].Value:F10}");
    }

    [Fact]
    public void Streaming_Matches_Batch_AllValues()
    {
        var series = GenerateSeries(1000);
        int warmup = 752;

        var wave = new TtmWave();
        var streamValues = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
            streamValues[i] = wave.Last.Value;
        }

        var batch = TtmWave.Batch(series);

        int mismatches = 0;
        for (int i = warmup; i < series.Count; i++)
        {
            double diff = Math.Abs(streamValues[i] - batch[i].Value);
            if (diff > 1e-8)
            {
                mismatches++;
                if (mismatches <= 5)
                {
                    _output.WriteLine($"Mismatch at i={i}: stream={streamValues[i]:F10}, batch={batch[i].Value:F10}, diff={diff:E3}");
                }
            }
        }

        Assert.Equal(0, mismatches);
    }

    // --- B) Primed vs Cold start agreement ---

    [Fact]
    public void Primed_Matches_Cold_Start()
    {
        var series = GenerateSeries(1000);
        int splitAt = 800;

        // Cold: process all at once
        var cold = new TtmWave();
        for (int i = 0; i < series.Count; i++)
        {
            cold.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }

        // Primed: prime with first chunk, then stream remainder
        var primed = new TtmWave();
        var primeSeries = GenerateSubSeries(series, splitAt);
        primed.Prime(primeSeries);

        for (int i = splitAt; i < series.Count; i++)
        {
            primed.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }

        double diff = Math.Abs(cold.Last.Value - primed.Last.Value);
        _output.WriteLine($"Cold={cold.Last.Value:F10}, Primed={primed.Last.Value:F10}, diff={diff:E3}");
        Assert.True(diff < 1e-8, $"Primed vs cold diff={diff:E3} exceeds tolerance");
    }

    // --- C) Deterministic reproducibility ---

    [Fact]
    public void Same_Input_Produces_Same_Output()
    {
        var series1 = GenerateSeries(1000, seed: 99);
        var series2 = GenerateSeries(1000, seed: 99);

        var batch1 = TtmWave.Batch(series1);
        var batch2 = TtmWave.Batch(series2);

        for (int i = 0; i < batch1.Count; i++)
        {
            Assert.Equal(batch1[i].Value, batch2[i].Value, 1e-15);
        }
    }

    [Fact]
    public void Different_Seed_Produces_Different_Output()
    {
        var series1 = GenerateSeries(1000, seed: 42);
        var series2 = GenerateSeries(1000, seed: 99);

        var batch1 = TtmWave.Batch(series1);
        var batch2 = TtmWave.Batch(series2);

        bool anyDifferent = false;
        for (int i = 800; i < batch1.Count; i++)
        {
            if (Math.Abs(batch1[i].Value - batch2[i].Value) > 1e-6)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "Different seeds should produce different outputs");
    }

    // --- D) Multi-wave coherence ---

    [Fact]
    public void All_Six_Waves_Produce_Finite_Values()
    {
        var series = GenerateSeries(1000);
        var wave = new TtmWave();

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }

        Assert.True(double.IsFinite(wave.WaveA1.Value), "WaveA1 not finite");
        Assert.True(double.IsFinite(wave.WaveA2.Value), "WaveA2 not finite");
        Assert.True(double.IsFinite(wave.WaveB1.Value), "WaveB1 not finite");
        Assert.True(double.IsFinite(wave.WaveB2.Value), "WaveB2 not finite");
        Assert.True(double.IsFinite(wave.WaveC1.Value), "WaveC1 not finite");
        Assert.True(double.IsFinite(wave.WaveC2.Value), "WaveC2 not finite");

        _output.WriteLine($"A1={wave.WaveA1.Value:F6}, A2={wave.WaveA2.Value:F6}");
        _output.WriteLine($"B1={wave.WaveB1.Value:F6}, B2={wave.WaveB2.Value:F6}");
        _output.WriteLine($"C1={wave.WaveC1.Value:F6}, C2={wave.WaveC2.Value:F6}");
    }

    [Fact]
    public void Wave_Magnitudes_Follow_Expected_Ordering()
    {
        // Longer-period MACD channels should generally have larger absolute histograms
        // (wider slow EMA separation from fast). Not guaranteed per-bar, but on average.
        var series = GenerateSeries(2000);
        var wave = new TtmWave();

        double sumAbsA = 0, sumAbsB = 0, sumAbsC = 0;
        int hotBars = 0;

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
            if (wave.IsHot)
            {
                sumAbsA += Math.Abs(wave.WaveA1.Value) + Math.Abs(wave.WaveA2.Value);
                sumAbsB += Math.Abs(wave.WaveB1.Value) + Math.Abs(wave.WaveB2.Value);
                sumAbsC += Math.Abs(wave.WaveC1.Value) + Math.Abs(wave.WaveC2.Value);
                hotBars++;
            }
        }

        double avgA = sumAbsA / (2 * hotBars);
        double avgB = sumAbsB / (2 * hotBars);
        double avgC = sumAbsC / (2 * hotBars);

        _output.WriteLine($"Avg |A|={avgA:F6}, |B|={avgB:F6}, |C|={avgC:F6}, hotBars={hotBars}");

        // Longer periods tend to produce larger histogram deviations on trending GBM data
        Assert.True(avgC > avgA * 0.5, $"Wave C avg ({avgC:F6}) should not be drastically smaller than A ({avgA:F6})");
    }

    [Fact]
    public void TOS_Compatibility_Properties_Are_Consistent()
    {
        var series = GenerateSeries(1000);
        var wave = new TtmWave();

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }

        // Wave1 == WaveA2 (per TOS mapping)
        Assert.Equal(wave.WaveA2.Value, wave.Wave1.Value, 1e-15);

        // Wave2High = max(C1, C2)
        Assert.Equal(Math.Max(wave.WaveC1.Value, wave.WaveC2.Value), wave.Wave2High, 1e-15);

        // Wave2Low = min(C1, C2)
        Assert.Equal(Math.Min(wave.WaveC1.Value, wave.WaveC2.Value), wave.Wave2Low, 1e-15);

        // Last == Wave1
        Assert.Equal(wave.Wave1.Value, wave.Last.Value, 1e-15);
    }

    // --- E) Calculate returns warm indicator ---

    [Fact]
    public void Calculate_Returns_Warm_Indicator()
    {
        var series = GenerateSeries(1000);
        var (results, indicator) = TtmWave.Calculate(series);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(results[^1].Value, indicator.Last.Value, 1e-10);
    }

    // --- F) Reset produces clean slate ---

    [Fact]
    public void Reset_Then_Replay_Matches_Fresh()
    {
        var series = GenerateSeries(1000);

        var wave = new TtmWave();
        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }
        double firstRun = wave.Last.Value;

        wave.Reset();
        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }
        double secondRun = wave.Last.Value;

        Assert.Equal(firstRun, secondRun, 1e-15);
    }

    // --- G) Large dataset stability ---

    [Fact]
    public void Large_Dataset_No_Overflow()
    {
        var series = GenerateSeries(5000);
        var wave = new TtmWave();

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
        }

        Assert.True(wave.IsHot);
        Assert.True(double.IsFinite(wave.Last.Value), "Last value should be finite after 5000 bars");
        Assert.True(double.IsFinite(wave.WaveC1.Value), "WaveC1 should be finite after 5000 bars");
        Assert.True(double.IsFinite(wave.WaveC2.Value), "WaveC2 should be finite after 5000 bars");
    }

    // --- H) Warm-up period validation ---

    [Fact]
    public void WarmupPeriod_Is_752()
    {
        var wave = new TtmWave();
        Assert.Equal(752, wave.WarmupPeriod);
    }

    [Fact]
    public void IsHot_False_Before_Warmup_True_After()
    {
        var series = GenerateSeries(1000);
        var wave = new TtmWave();

        bool wasHot = false;
        int firstHotBar = -1;

        for (int i = 0; i < series.Count; i++)
        {
            wave.Update(new TValue(new DateTime(series.Times[i], DateTimeKind.Utc), series.Values[i]));
            if (wave.IsHot && !wasHot)
            {
                firstHotBar = i;
                wasHot = true;
            }
        }

        Assert.True(wasHot, "Should become hot before 1000 bars");
        _output.WriteLine($"First hot bar index: {firstHotBar}");

        // IsHot should engage roughly around the warmup period
        Assert.True(firstHotBar > 0, "Should not be hot immediately");
        Assert.True(firstHotBar <= wave.WarmupPeriod, $"First hot bar {firstHotBar} should be <= WarmupPeriod {wave.WarmupPeriod}");
    }

    // --- helper ---

    private static TSeries GenerateSubSeries(TSeries source, int count)
    {
        var t = new List<long>(count);
        var v = new List<double>(count);
        for (int i = 0; i < count && i < source.Count; i++)
        {
            t.Add(source.Times[i]);
            v.Add(source.Values[i]);
        }
        return new TSeries(t, v);
    }
}
