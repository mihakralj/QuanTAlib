
namespace QuanTAlib.Tests;

public class MacdTests
{
    [Fact]
    public void BasicCalculation()
    {
        var macd = new Macd(12, 26, 9);
        Assert.False(macd.IsHot);
    }

    [Fact]
    public void Constructor_ValidParameters_Works()
    {
        // Macd delegates to Ema which handles validation
        // Testing that valid parameters work correctly
        var macd = new Macd(12, 26, 9);
        Assert.NotNull(macd);
        Assert.Equal("Macd(12,26,9)", macd.Name);
        Assert.Equal(33, macd.WarmupPeriod); // max(12,26) + 9 - 2 = 33
    }

    [Fact]
    public void Constructor_CustomParameters_Works()
    {
        var macd = new Macd(5, 10, 3);
        Assert.NotNull(macd);
        Assert.Equal("Macd(5,10,3)", macd.Name);
        Assert.Equal(11, macd.WarmupPeriod); // max(5,10) + 3 - 2 = 11
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 49; i++)
        {
            macd.Update(series.Close[i], isNew: true);
        }

        var val1 = macd.Update(series.Close[49], isNew: true);
        var val2 = macd.Update(new TValue(DateTime.UtcNow, series.Close[49].Value + 1), isNew: true);

        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 49; i++)
        {
            macd.Update(series.Close[i]);
        }

        var val1 = macd.Update(series.Close[49], isNew: true);
        var val2 = macd.Update(new TValue(series.Close[49].Time, series.Close[49].Value + 5), isNew: false);

        Assert.Equal(val1.Time, val2.Time);
        Assert.NotEqual(val1.Value, val2.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 50; i++)
        {
            macd.Update(series.Close[i]);
        }

        var originalValue = macd.Last;

        for (int m = 0; m < 5; m++)
        {
            var modified = new TValue(series.Close[49].Time, series.Close[49].Value + m);
            macd.Update(modified, isNew: false);
        }

        var restored = macd.Update(series.Close[49], isNew: false);
        Assert.Equal(originalValue.Value, restored.Value, 9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < series.Count; i++)
        {
            macd.Update(series.Close[i]);
        }

        macd.Reset();

        Assert.Equal(0, macd.Last.Value);
        Assert.False(macd.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        Assert.False(macd.IsHot);

        for (int i = 0; i < series.Count; i++)
        {
            macd.Update(series.Close[i]);
            if (i >= 40)
            {
                break;
            }
        }

        Assert.True(macd.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 40; i++)
        {
            macd.Update(series.Close[i]);
        }

        var result = macd.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var macd = new Macd(12, 26, 9);
        var gbm = new GBM();
        var series = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 40; i++)
        {
            macd.Update(series.Close[i]);
        }

        var result = macd.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchMatchesStreaming()
    {
        var macd = new Macd(12, 26, 9);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        var batchResult = macd.Update(series);

        macd.Reset();
        var streamResults = new System.Collections.Generic.List<double>();
        foreach (var item in series)
        {
            macd.Update(item);
            streamResults.Add(macd.Last.Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResults[i], 8);
        }
    }

    [Fact]
    public void SpanMatchesBatch()
    {
        var macd = new Macd(12, 26, 9);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        var batchResult = macd.Update(series);

        var output = new double[series.Count];
        Macd.Batch(series.Values, output, 12, 26);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(seed: 123);
        var series = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchMacd = new Macd(12, 26, 9);
        var batchResult = batchMacd.Update(series.Close);
        double expected = batchResult.Last.Value;

        // 2. Span Mode
        var spanOutput = new double[series.Count];
        Macd.Batch(series.Close.Values, spanOutput, 12, 26);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamMacd = new Macd(12, 26, 9);
        for (int i = 0; i < series.Count; i++)
        {
            streamMacd.Update(series.Close[i]);
        }

        double streamResult = streamMacd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventMacd = new Macd(pubSource, 12, 26, 9);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series.Close[i]);
        }

        double eventResult = eventMacd.Last.Value;

        Assert.Equal(expected, spanResult, 9);
        Assert.Equal(expected, streamResult, 9);
        Assert.Equal(expected, eventResult, 9);
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSize = new double[3];

        Assert.Throws<ArgumentException>(() => Macd.Batch(source, wrongSize, 12, 26));
        Assert.Throws<ArgumentException>(() => Macd.Batch(source, output, 0, 26));
        Assert.Throws<ArgumentException>(() => Macd.Batch(source, output, 12, 0));
    }
}
