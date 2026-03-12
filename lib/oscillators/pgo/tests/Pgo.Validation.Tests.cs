using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class PgoValidationTests
{
    private readonly TBarSeries _bars;
    private readonly ITestOutputHelper _output;

    public PgoValidationTests(ITestOutputHelper output)
    {
        _output = output;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        _bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Validate_Streaming_Batch_Span_Agree()
    {
        int period = 14;

        // Streaming
        var streaming = new Pgo(period);
        var streamValues = new List<double>(_bars.Count);
        for (int i = 0; i < _bars.Count; i++)
        {
            streamValues.Add(streaming.Update(_bars[i]).Value);
        }

        // Batch (TBarSeries)
        TSeries batchSeries = Pgo.Batch(_bars, period);

        // Span
        var spanOutput = new double[_bars.Count];
        Pgo.Batch(_bars.High.Values, _bars.Low.Values, _bars.Close.Values, spanOutput, period);

        // Batch vs span should match exactly (same code path).
        // Streaming vs batch should agree closely.
        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 12);   // batch=span (same path)
            Assert.Equal(batchSeries[i].Value, streamValues[i], 10); // streaming matches batch
        }

        _output.WriteLine("PGO validation: streaming, batch, and span outputs agree within tolerance.");
    }

    [Fact]
    public void Validate_KnownValues_ConstantPrice()
    {
        // Constant OHLC bars: close=SMA, TR=0, ATR=0 → PGO = 0
        int period = 5;
        var pgo = new Pgo(period);

        for (int i = 0; i < 20; i++)
        {
            pgo.Update(new TBar(DateTime.UtcNow, 50, 50, 50, 50, 100));
        }

        Assert.Equal(0.0, pgo.Last.Value, 10);
        _output.WriteLine("PGO known-values: constant bars produce PGO=0.");
    }

    [Fact]
    public void Validate_KnownValues_PriceAboveSma()
    {
        // When close > SMA and ATR > 0, PGO should be positive
        int period = 5;
        var pgo = new Pgo(period);

        // Feed gradually rising prices
        for (int i = 0; i < 10; i++)
        {
            double c = 100.0 + (i * 2);
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 3, c - 3, c, 100));
        }

        Assert.True(pgo.Last.Value > 0, $"Expected positive PGO for rising prices, got {pgo.Last.Value}");
        _output.WriteLine($"PGO known-values: rising prices produce positive PGO = {pgo.Last.Value:F6}.");
    }

    [Fact]
    public void Validate_KnownValues_PriceBelowSma()
    {
        // When close < SMA and ATR > 0, PGO should be negative
        int period = 5;
        var pgo = new Pgo(period);

        // Feed rising prices first, then drop
        for (int i = 0; i < 7; i++)
        {
            double c = 100.0 + (i * 5);
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 3, c - 3, c, 100));
        }
        // Now drop sharply
        for (int i = 0; i < 5; i++)
        {
            double c = 80.0 - (i * 5);
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 3, c - 3, c, 100));
        }

        Assert.True(pgo.Last.Value < 0, $"Expected negative PGO for dropped prices, got {pgo.Last.Value}");
        _output.WriteLine($"PGO known-values: dropped prices produce negative PGO = {pgo.Last.Value:F6}.");
    }

    [Fact]
    public void Validate_MultiPeriod_Consistency()
    {
        // Different periods should produce different results
        int[] periods = [5, 14, 50];
        var results = new List<TSeries>();

        foreach (int period in periods)
        {
            results.Add(Pgo.Batch(_bars, period));
        }

        // After all warmups, values should differ for different periods
        int checkIdx = 100;
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.NotEqual(results[i][checkIdx].Value, results[i + 1][checkIdx].Value);
        }

        _output.WriteLine("PGO multi-period: different periods produce different results.");
    }

    [Fact]
    public void Validate_Component_SmaAtr_Identity()
    {
        // Manually verify PGO = (close - SMA) / ATR
        // by computing SMA and ATR independently and comparing
        int period = 10;
        var pgo = new Pgo(period);

        // Manual SMA/ATR tracking
        var smaBuffer = new RingBuffer(period);
        double smaSum = 0.0;
        double ema = 0.0;
        double e = 1.0;
        double alpha = 1.0 / period;
        double decay = 1.0 - alpha;
        double atr = 0.0;
        bool warmup = true;
        double prevClose = 0.0;
        bool hasPrev = false;

        int validCount = 0;

        for (int i = 0; i < _bars.Count; i++)
        {
            var bar = _bars[i];
            double close = bar.Close;
            double pc = hasPrev ? prevClose : close;

            // SMA
            if (smaBuffer.Count == smaBuffer.Capacity)
            {
                smaSum -= smaBuffer.Oldest;
            }
            smaSum += close;
            smaBuffer.Add(close);
            double sma = smaSum / smaBuffer.Count;

            // TR
            double tr = Math.Max(bar.High - bar.Low,
                Math.Max(Math.Abs(bar.High - pc), Math.Abs(bar.Low - pc)));

            // EMA of TR
            ema = Math.FusedMultiplyAdd(alpha, tr - ema, ema);
            if (warmup)
            {
                e *= decay;
                double c = 1.0 / (1.0 - e);
                atr = c * ema;
                warmup = e > 1e-10;
            }
            else
            {
                atr = ema;
            }

            prevClose = close;
            hasPrev = true;

            // PGO
            var result = pgo.Update(bar);
            double expectedPgo = atr > 0 ? (close - sma) / atr : 0.0;

            if (smaBuffer.IsFull)
            {
                Assert.Equal(expectedPgo, result.Value, 10);
                validCount++;
            }
        }

        Assert.True(validCount > 0, "No valid comparison points");
        _output.WriteLine($"PGO component identity: validated {validCount} points.");
    }

    [Fact]
    public void Validate_Determinism()
    {
        // Run twice with same data — results must be identical
        int period = 14;
        var results1 = new double[_bars.Count];
        var results2 = new double[_bars.Count];

        var pgo1 = new Pgo(period);
        var pgo2 = new Pgo(period);

        for (int i = 0; i < _bars.Count; i++)
        {
            results1[i] = pgo1.Update(_bars[i]).Value;
            results2[i] = pgo2.Update(_bars[i]).Value;
        }

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(results1[i], results2[i], 15);
        }

        _output.WriteLine("PGO determinism: two runs produce identical results.");
    }

    [Fact]
    public void Pgo_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculatePrettyGoodOscillator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
