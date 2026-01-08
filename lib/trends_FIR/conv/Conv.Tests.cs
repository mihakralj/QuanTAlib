
namespace QuanTAlib;

public class ConvTests
{
    [Fact]
    public void Constructor_EmptyKernel_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Conv(Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => new Conv(null!));
    }

    [Fact]
    public void BasicCalculation_MatchesExpected()
    {
        // Kernel: [0.5, 1.0]
        // Data: [1, 2, 3, 4]
        // 1: 1*1.0 = 1.0 (partial)
        // 2: 1*0.5 + 2*1.0 = 2.5
        // 3: 2*0.5 + 3*1.0 = 4.0
        // 4: 3*0.5 + 4*1.0 = 5.5

        double[] kernel = [0.5, 1.0];
        var conv = new Conv(kernel);

        var result1 = conv.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(1.0, result1.Value);

        var result2 = conv.Update(new TValue(DateTime.UtcNow, 2));
        Assert.Equal(2.5, result2.Value);

        var result3 = conv.Update(new TValue(DateTime.UtcNow, 3));
        Assert.Equal(4.0, result3.Value);

        var result4 = conv.Update(new TValue(DateTime.UtcNow, 4));
        Assert.Equal(5.5, result4.Value);
    }

    [Fact]
    public void BarCorrection_UpdatesCorrectly()
    {
        double[] kernel = [0.5, 1.0];
        var conv = new Conv(kernel);

        // 1
        conv.Update(new TValue(DateTime.UtcNow, 1));

        // 2 (isNew=true) -> 2.5
        var res1 = conv.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        Assert.Equal(2.5, res1.Value);

        // Update 2 to 3 (isNew=false)
        // Buffer was [1, 2]. Now [1, 3].
        // 1*0.5 + 3*1.0 = 3.5
        var res2 = conv.Update(new TValue(DateTime.UtcNow, 3), isNew: false);
        Assert.Equal(3.5, res2.Value);

        // New bar 4 (isNew=true)
        // Buffer was [1, 3]. New bar 4. Buffer becomes [3, 4].
        // 3*0.5 + 4*1.0 = 1.5 + 4 = 5.5
        var res3 = conv.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        Assert.Equal(5.5, res3.Value);
    }

    [Fact]
    public void NanHandling_UsesLastValid()
    {
        double[] kernel = [1.0, 1.0]; // Sum of last 2
        var conv = new Conv(kernel);

        // 1 -> 1
        conv.Update(new TValue(DateTime.UtcNow, 1));

        // NaN -> treated as 1. Buffer: [1, 1]. Result: 2.
        var res = conv.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(2.0, res.Value);

        // 2 -> Buffer: [1, 2]. Result: 3.
        res = conv.Update(new TValue(DateTime.UtcNow, 2));
        Assert.Equal(3.0, res.Value);
    }

    [Fact]
    public void StaticCalculate_MatchesObjectApi()
    {
        double[] kernel = [0.5, 1.0];
        var source = new TSeries();
        source.Add(new TValue(DateTime.UtcNow, 1));
        source.Add(new TValue(DateTime.UtcNow, 2));
        source.Add(new TValue(DateTime.UtcNow, 3));
        source.Add(new TValue(DateTime.UtcNow, 4));

        var result = Conv.Batch(source, kernel);

        Assert.Equal(1.0, result.Values[0]);
        Assert.Equal(2.5, result.Values[1]);
        Assert.Equal(4.0, result.Values[2]);
        Assert.Equal(5.5, result.Values[3]);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        double[] kernel = [1.0, 1.0];
        var conv = new Conv(kernel);

        conv.Update(new TValue(DateTime.UtcNow, 1));
        conv.Update(new TValue(DateTime.UtcNow, 2));
        Assert.True(conv.IsHot);

        conv.Reset();
        Assert.False(conv.IsHot);
        Assert.Equal(0, conv.Last.Value);

        // Should behave as new
        var res = conv.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(1.0, res.Value);
    }

    [Fact]
    public void LeadingNaN_RemainsNaN()
    {
        double[] kernel = [1.0];
        var conv = new Conv(kernel);
        var res = conv.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(res.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        double[] kernel = [0.5, 1.0];
        var conv = new Conv(kernel);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            conv.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = conv.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            conv.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = conv.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        double[] kernel = [0.1, 0.2, 0.3, 0.4];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Conv.Batch(series, kernel);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Conv.Batch(spanInput, spanOutput, kernel);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Conv(kernel);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Conv(pubSource, kernel);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }

    [Fact]
    public void SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];
        double[] kernel = [0.5, 0.5];

        Assert.Throws<ArgumentException>(() => Conv.Batch(source.AsSpan(), output.AsSpan(), Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => Conv.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), kernel));
    }

    [Fact]
    public void SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];
        double[] kernel = [0.5, 0.5];

        Conv.Batch(source.AsSpan(), output.AsSpan(), kernel);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }
}
