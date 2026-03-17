using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class DmhValidationTests
{
    private readonly ITestOutputHelper _output;

    public DmhValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Validate_Consistency_UpdateVsSeries()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmh = new Dmh(14);
        var streamResult = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            streamResult.Add(dmh.Update(bars[i]));
        }

        var dmh2 = new Dmh(14);
        var seriesResult = dmh2.Update(bars);

        Assert.Equal(streamResult.Count, seriesResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResult[i].Value, seriesResult[i].Value, ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("DMH Update vs Series validated successfully");
    }

    [Fact]
    public void Validate_SpanBatch_Matches_Streaming()
    {
        var gbm = new GBM(seed: 99);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmh = new Dmh(14);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamResults[i] = dmh.Update(bars[i]).Value;
        }

        var destination = new double[bars.Count];
        Dmh.Batch(bars.High.Values, bars.Low.Values, 14, destination);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], destination[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("DMH Span batch vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_Trend_Direction()
    {
        // Synthetic uptrend
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        double price = 100;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(time, price, price + 2, price - 1, price + 1, 1000);
            time = time.AddMinutes(1);
            price += 1.0;
        }

        var dmh = new Dmh(14);
        var result = dmh.Update(bars);

        for (int i = 80; i < 100; i++)
        {
            Assert.True(result[i].Value > 0, $"DMH should be positive in uptrend at index {i}, got {result[i].Value}");
        }

        // Synthetic downtrend
        bars = new TBarSeries();
        time = DateTime.UtcNow;
        price = 200;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(time, price, price + 1, price - 2, price - 1, 1000);
            time = time.AddMinutes(1);
            price -= 1.0;
        }

        dmh = new Dmh(14);
        result = dmh.Update(bars);

        for (int i = 80; i < 100; i++)
        {
            Assert.True(result[i].Value < 0, $"DMH should be negative in downtrend at index {i}, got {result[i].Value}");
        }

        _output.WriteLine("DMH trend direction validated successfully");
    }

    [Fact]
    public void Validate_ConstantPrice_ZeroOutput()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            bars.Add(time, 100, 100, 100, 100, 1000);
            time = time.AddMinutes(1);
        }

        var dmh = new Dmh(14);
        var result = dmh.Update(bars);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(0.0, result[i].Value, 1e-12);
        }
        _output.WriteLine("DMH constant price → zero validated successfully");
    }

    [Fact]
    public void Validate_DifferentPeriods()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (int period in new[] { 5, 10, 14, 20, 50 })
        {
            var dmh = new Dmh(period);
            for (int i = 0; i < bars.Count; i++)
            {
                var val = dmh.Update(bars[i]);
                Assert.True(double.IsFinite(val.Value), $"DMH period={period}, bar={i}: non-finite value {val.Value}");
            }
        }
        _output.WriteLine("DMH different periods validated successfully");
    }

    [Fact]
    public void Validate_BarCorrection_Consistency()
    {
        var gbm = new GBM(seed: 77);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmh = new Dmh(14);
        for (int i = 0; i < 100; i++)
        {
            dmh.Update(bars[i]);
        }

        var valueBeforeCorrection = dmh.Last.Value;

        // Apply multiple corrections
        for (int c = 0; c < 10; c++)
        {
            var modified = new TBar(bars[99].Time, bars[99].Open + c, bars[99].High + c, bars[99].Low - c, bars[99].Close + c, bars[99].Volume);
            dmh.Update(modified, isNew: false);
        }

        // Restore original bar
        var restored = dmh.Update(bars[99], isNew: false);
        Assert.Equal(valueBeforeCorrection, restored.Value, 1e-9);
        _output.WriteLine("DMH bar correction consistency validated successfully");
    }

    [Fact]
    public void Validate_Subset_Stability()
    {
        var gbm = new GBM(seed: 55);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmhFull = new Dmh(14);
        for (int i = 0; i < 500; i++)
        {
            dmhFull.Update(bars[i]);
        }

        var dmhSubset = new Dmh(14);
        for (int i = 0; i < 300; i++)
        {
            dmhSubset.Update(bars[i]);
        }

        // Values at bar 299 should match
        var dmhRef = new Dmh(14);
        double val299 = 0;
        for (int i = 0; i < 300; i++)
        {
            val299 = dmhRef.Update(bars[i]).Value;
        }

        Assert.Equal(val299, dmhSubset.Last.Value, 1e-9);
        _output.WriteLine("DMH subset stability validated successfully");
    }
}
