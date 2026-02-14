// IMPULSE Validation Tests - Elder Impulse System
// Self-consistency validation (no external library implements Elder Impulse directly)

using Xunit;

namespace QuanTAlib.Tests;

public class ImpulseValidationTests
{
    private const int DataCount = 200;

    private static (TSeries Series, GBM Gbm) CreateTestData()
    {
        var gbm = new GBM();
        var time = DateTime.UtcNow;
        var times = new List<long>(DataCount);
        var values = new List<double>(DataCount);
        for (int i = 0; i < DataCount; i++)
        {
            times.Add(time.AddMinutes(i).Ticks);
            values.Add(gbm.Next().Close);
        }
        return (new TSeries(times, values), gbm);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Streaming vs Batch consistency
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void StreamingMatchesBatch()
    {
        var (series, _) = CreateTestData();

        // Batch
        var batchResults = Impulse.Batch(series);

        // Streaming
        var streaming = new Impulse();
        var streamValues = new double[DataCount];
        for (int i = 0; i < DataCount; i++)
        {
            streaming.Update(series[i], isNew: true);
            streamValues[i] = streaming.Last.Value;
        }

        for (int i = 0; i < DataCount; i++)
        {
            Assert.Equal(batchResults.Values[i], streamValues[i], 10);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Component identity: EMA output matches standalone EMA
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void EmaOutput_MatchesStandaloneEma()
    {
        var (series, _) = CreateTestData();

        var impulse = new Impulse();
        var ema = new Ema(13);

        for (int i = 0; i < DataCount; i++)
        {
            var impulseResult = impulse.Update(series[i], isNew: true);
            var emaResult = ema.Update(series[i], isNew: true);

            Assert.Equal(emaResult.Value, impulseResult.Value, 10);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Signal correctness: manual EMA + MACD verification
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Signal_MatchesManualEmaAndMacdComparison()
    {
        var (series, _) = CreateTestData();

        var impulse = new Impulse();
        var ema = new Ema(13);
        var macd = new Macd(12, 26, 9);

        double prevEma = 0;
        double prevHist = 0;
        bool hasPrev = false;

        for (int i = 0; i < DataCount; i++)
        {
            impulse.Update(series[i], isNew: true);
            ema.Update(series[i], isNew: true);
            macd.Update(series[i], isNew: true);

            double curEma = ema.Last.Value;
            double curHist = macd.Histogram.Value;

            if (hasPrev && impulse.IsHot)
            {
                bool emaRising = curEma > prevEma;
                bool emaFalling = curEma < prevEma;
                bool histRising = curHist > prevHist;
                bool histFalling = curHist < prevHist;

                int expectedSignal;
                if (emaRising && histRising)
                {
                    expectedSignal = 1;
                }
                else if (emaFalling && histFalling)
                {
                    expectedSignal = -1;
                }
                else
                {
                    expectedSignal = 0;
                }

                Assert.Equal(expectedSignal, impulse.Signal);
            }

            prevEma = curEma;
            prevHist = curHist;
            hasPrev = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Determinism: same input produces same output
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Determinism_SameInputSameOutput()
    {
        var time = DateTime.UtcNow;
        var values = new double[100];
        var rng = new GBM(seed: 42);
        for (int i = 0; i < 100; i++)
        {
            values[i] = rng.Next().Close;
        }

        var impulse1 = new Impulse();
        var impulse2 = new Impulse();
        for (int i = 0; i < 100; i++)
        {
            var tv = new TValue(time.AddMinutes(i).Ticks, values[i]);
            impulse1.Update(tv, isNew: true);
            impulse2.Update(tv, isNew: true);

            Assert.Equal(impulse1.Last.Value, impulse2.Last.Value, 12);
            Assert.Equal(impulse1.Signal, impulse2.Signal);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Directional correctness
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SteadyUptrend_ProducesBullishSignals()
    {
        var impulse = new Impulse();
        var time = DateTime.UtcNow;

        // Exponential uptrend must produce at least one bullish signal
        bool seenBullish = false;
        for (int i = 0; i < 100; i++)
        {
            impulse.Update(new TValue(time.AddMinutes(i).Ticks, 100.0 * Math.Exp(0.02 * i)), isNew: true);
            if (impulse.Signal == 1) { seenBullish = true; }
        }

        Assert.True(seenBullish, "Exponential uptrend should produce at least one bullish signal");
    }

    [Fact]
    public void SteadyDowntrend_ProducesBearishSignals()
    {
        var impulse = new Impulse();
        var time = DateTime.UtcNow;

        // Exponential downtrend must produce at least one bearish signal
        bool seenBearish = false;
        for (int i = 0; i < 100; i++)
        {
            impulse.Update(new TValue(time.AddMinutes(i).Ticks, 200.0 * Math.Exp(-0.02 * i)), isNew: true);
            if (impulse.Signal == -1) { seenBearish = true; }
        }

        Assert.True(seenBearish, "Exponential downtrend should produce at least one bearish signal");
    }
}
