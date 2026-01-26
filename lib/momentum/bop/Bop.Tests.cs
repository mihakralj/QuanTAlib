
namespace QuanTAlib.Tests;

public class BopTests
{
    [Fact]
    public void BasicCalculation()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
        // Open=10, High=20, Low=5, Close=15
        // Range = 20 - 5 = 15
        // Diff = 15 - 10 = 5
        // BOP = 5 / 15 = 0.3333...

        var result = bop.Update(bar);
        Assert.Equal(1.0 / 3.0, result.Value, 6);
    }

    [Fact]
    public void HighEqualsLow()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        // Range = 0
        // BOP should be 0

        var result = bop.Update(bar);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void BuyersDominate()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 10, 20, 100);
        // Open=10, High=20, Low=10, Close=20
        // Range = 10
        // Diff = 10
        // BOP = 1

        var result = bop.Update(bar);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void SellersDominate()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 20, 20, 10, 10, 100);
        // Open=20, High=20, Low=10, Close=10
        // Range = 10
        // Diff = -10
        // BOP = -1

        var result = bop.Update(bar);
        Assert.Equal(-1, result.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        // BOP is stateless - each bar is calculated independently
        // isNew parameter is accepted but doesn't affect stateless calculation
        var bop = new Bop();
        // bar1: BOP = (15-10)/(20-5) = 5/15 = 0.333
        var bar1 = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
        // bar2: BOP = (25-15)/(30-10) = 10/20 = 0.5
        var bar2 = new TBar(DateTime.UtcNow, 15, 30, 10, 25, 100);

        bop.Update(bar1, isNew: true);
        var val1 = bop.Last.Value;

        bop.Update(bar2, isNew: true);
        var val2 = bop.Last.Value;

        // Different bars produce different BOP values
        Assert.NotEqual(val1, val2);
        Assert.Equal(1.0 / 3.0, val1, 6); // bar1 BOP
        Assert.Equal(0.5, val2, 6);       // bar2 BOP
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        // BOP is stateless - each bar is calculated independently
        // isNew=false still calculates the new value
        var bop = new Bop();
        var bar1 = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 10, 25, 5, 20, 100);

        var val1 = bop.Update(bar1, isNew: true);
        var val2 = bop.Update(bar2, isNew: false);

        // Different bars produce different values (BOP has no state to preserve)
        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);

        var originalValue = bop.Update(bar, isNew: true);

        for (int i = 0; i < 5; i++)
        {
            var modified = new TBar(bar.Time, bar.Open, bar.High + i, bar.Low, bar.Close, bar.Volume);
            bop.Update(modified, isNew: false);
        }

        var restored = bop.Update(bar, isNew: false);
        Assert.Equal(originalValue.Value, restored.Value, 9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);

        bop.Update(bar);
        bop.Reset();

        Assert.Equal(0, bop.Last.Value);
    }

    [Fact]
    public void IsHot_AlwaysTrueForBop()
    {
        // BOP has no warmup - IsHot is always true (static property)
        Assert.True(Bop.IsHot);

        var bop = new Bop();
        var bar = new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100);
        bop.Update(bar);

        Assert.True(Bop.IsHot);
    }

    [Fact]
    public void NaN_Input_ProducesNaN()
    {
        // BOP is stateless and doesn't track last valid value
        // NaN input propagates through the calculation
        var bop = new Bop();
        var barNaN = new TBar(DateTime.UtcNow, double.NaN, 20, 5, 15, 100);

        var result = bop.Update(barNaN);

        // BOP = (Close - Open) / (High - Low) = (15 - NaN) / (20 - 5) = NaN
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Infinity_Input_ProducesInfinity()
    {
        // BOP is stateless and doesn't track last valid value
        // Infinity input propagates through the calculation
        var bop = new Bop();
        var barInf = new TBar(DateTime.UtcNow, double.PositiveInfinity, 20, 5, 15, 100);

        var result = bop.Update(barInf);

        // BOP = (Close - Open) / (High - Low) = (15 - Infinity) / (20 - 5) = -Infinity
        Assert.True(double.IsInfinity(result.Value));
    }

    [Fact]
    public void BatchMatchesStreaming()
    {
        var bop = new Bop();
        var bars = new TBarSeries();
        bars.Add(new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 15, 25, 10, 20, 100));

        var batchResult = Bop.Update(bars);

        bop.Reset();
        var streamResult1 = bop.Update(bars[0]);
        var streamResult2 = bop.Update(bars[1]);

        Assert.Equal(batchResult[0].Value, streamResult1.Value);
        Assert.Equal(batchResult[1].Value, streamResult2.Value);
    }

    [Fact]
    public void SpanMatchesBatch()
    {
        var bars = new TBarSeries();
        bars.Add(new TBar(DateTime.UtcNow, 10, 20, 5, 15, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 15, 25, 10, 20, 100));

        var batchResult = Bop.Batch(bars);

        var output = new double[bars.Count];
        Bop.Calculate(bars.Open.Values, bars.High.Values, bars.Low.Values, bars.Close.Values, output);

        Assert.Equal(batchResult[0].Value, output[0]);
        Assert.Equal(batchResult[1].Value, output[1]);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchResult = Bop.Batch(bars);
        double expected = batchResult.Last.Value;

        // 2. Span Mode
        var spanOutput = new double[bars.Count];
        Bop.Calculate(bars.Open.Values, bars.High.Values, bars.Low.Values, bars.Close.Values, spanOutput);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamBop = new Bop();
        for (int i = 0; i < bars.Count; i++)
        {
            streamBop.Update(bars[i]);
        }

        double streamResult = streamBop.Last.Value;

        Assert.Equal(expected, spanResult, 9);
        Assert.Equal(expected, streamResult, 9);
    }

    [Fact]
    public void SpanBatch_ProcessesMinimumLength()
    {
        // BOP.Calculate processes the minimum length of all arrays
        // It doesn't throw when output is smaller - it just processes fewer elements
        double[] open = [1, 2, 3];
        double[] high = [2, 3, 4];
        double[] low = [0, 1, 2];
        double[] close = [1.5, 2.5, 3.5];
        double[] smallOutput = new double[2];

        // This should process 2 elements (minimum of all array lengths)
        Bop.Calculate(open, high, low, close, smallOutput);

        // Verify values are calculated for the first 2 elements
        Assert.Equal(0.25, smallOutput[0], 6); // (1.5 - 1) / (2 - 0) = 0.5/2 = 0.25
        Assert.Equal(0.25, smallOutput[1], 6); // (2.5 - 2) / (3 - 1) = 0.5/2 = 0.25
    }
}
