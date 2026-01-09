
namespace QuanTAlib;

public class DwmaTests
{
    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Dwma(0));
        Assert.Throws<ArgumentException>(() => new Dwma(-1));
    }

    [Fact]
    public void Update_ValidInput_CalculatesCorrectly()
    {
        // DWMA(3) of [1, 2, 3, 4, 5]
        // WMA(3) of [1, 2, 3, 4, 5]
        // 1: 1
        // 2: (1*1 + 2*2) / 3 = 5/3 = 1.666...
        // 3: (1*1 + 2*2 + 3*3) / 6 = 14/6 = 2.333...
        // 4: (1*2 + 2*3 + 3*4) / 6 = 20/6 = 3.333...
        // 5: (1*3 + 2*4 + 3*5) / 6 = 26/6 = 4.333...

        // WMA(3) results: [1, 1.666, 2.333, 3.333, 4.333]

        // DWMA(3) = WMA(3) of [1, 1.666, 2.333, 3.333, 4.333]
        // 1: 1
        // 2: (1*1 + 2*1.666) / 3 = 4.333/3 = 1.444...
        // 3: (1*1 + 2*1.666 + 3*2.333) / 6 = (1 + 3.333 + 7) / 6 = 11.333/6 = 1.888...

        var dwma = new Dwma(3);

        var v1 = dwma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        var v2 = dwma.Update(new TValue(DateTime.UtcNow, 2)).Value;
        var v3 = dwma.Update(new TValue(DateTime.UtcNow, 3)).Value;

        Assert.Equal(1.0, v1, 6);
        Assert.Equal(1.444444, v2, 5);
        Assert.Equal(1.888888, v3, 5);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsValue()
    {
        var dwma = new Dwma(3);

        dwma.Update(new TValue(DateTime.UtcNow, 1));
        dwma.Update(new TValue(DateTime.UtcNow, 2));

        // Update with 3, then correct to 4
        var v3 = dwma.Update(new TValue(DateTime.UtcNow, 3), isNew: true).Value;
        var v3_corrected = dwma.Update(new TValue(DateTime.UtcNow, 4), isNew: false).Value;

        // Manual calc for sequence [1, 2, 4]
        // WMA(3):
        // 1: 1
        // 2: 1.666
        // 4: (1*1 + 2*2 + 3*4) / 6 = 17/6 = 2.8333

        // DWMA(3) of [1, 1.666, 2.8333]
        // 3: (1*1 + 2*1.666 + 3*2.8333) / 6 = (1 + 3.333 + 8.5) / 6 = 12.833/6 = 2.1388

        Assert.Equal(1.888888, v3, 5); // From previous test
        Assert.Equal(2.138888, v3_corrected, 5);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var dwma = new Dwma(3);
        dwma.Update(new TValue(DateTime.UtcNow, 1));
        dwma.Update(new TValue(DateTime.UtcNow, 2));

        dwma.Reset();

        Assert.False(dwma.IsHot);
        var v1 = dwma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        Assert.Equal(1.0, v1);
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        int period = 10;
        int count = 100;
        var source = new TSeries();
        var dwma = new Dwma(period);

        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i));
            dwma.Update(source.Last);
        }

        var staticResult = Dwma.Batch(source, period);

        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(dwma.Last.Value, staticResult.Last.Value, 8);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var dwma = new Dwma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            dwma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = dwma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            dwma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = dwma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var dwma = new Dwma(5);
        dwma.Update(new TValue(DateTime.UtcNow, 100));
        dwma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = dwma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentOutOfRangeException>(() => Dwma.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Dwma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Dwma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Dwma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Dwma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Dwma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Dwma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
