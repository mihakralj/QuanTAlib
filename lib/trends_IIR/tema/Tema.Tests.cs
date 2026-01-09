
namespace QuanTAlib.Tests;

public class TemaTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var tema = new Tema(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            tema.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(tema.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var tema = new Tema(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            tema.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Update with 100th point (isNew=true)
        tema.Update(new TValue(bars[99].Time, bars[99].Close), true);

        // Update with modified 100th point (isNew=false)
        var val2 = tema.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), false);

        // Create new instance and feed up to modified
        var tema2 = new Tema(10);
        for (int i = 0; i < 99; i++)
        {
            tema2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        var val3 = tema2.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var tema = new Tema(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            tema.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        tema.Reset();
        Assert.Equal(0, tema.Last.Value);
        Assert.False(tema.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            tema.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(tema.Last.Value));
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var tema = new Tema(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(tema.Update(series[i]).Value);
        }

        var tema2 = new Tema(10);
        var seriesResults = tema2.Update(series);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void BatchCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var tema = new Tema(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(tema.Update(series[i]).Value);
        }

        var batchResults = Tema.Batch(series, 10);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void BatchCalculateSpan_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var tema = new Tema(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(tema.Update(series[i]).Value);
        }

        var spanResults = new double[series.Count];
        Tema.Batch(series.Values, spanResults, 10);

        for (int i = 0; i < spanResults.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var tema = new Tema(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Test TSeries chain
        var result = tema.Update(series);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TValue chain
        var result2 = tema.Update(series[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Tema(0));
        Assert.Throws<ArgumentException>(() => new Tema(-1));
        Assert.Throws<ArgumentException>(() => new Tema(0.0));
        Assert.Throws<ArgumentException>(() => new Tema(1.0));
    }
}
