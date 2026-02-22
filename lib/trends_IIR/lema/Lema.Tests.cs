namespace QuanTAlib.Tests;

public class LemaTests
{
    [Fact]
    public void Lema_Matches_ManualCalculation()
    {
        // Arrange
        const int period = 10;
        var lema = new Lema(period);
        var ema1 = new Ema(period);
        var ema2 = new Ema(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var lVal = lema.Update(tVal);

            var e1Val = ema1.Update(tVal);
            double error = tVal.Value - e1Val.Value;
            var e2Val = ema2.Update(new TValue(tVal.Time, error));
            double expected = e1Val.Value + e2Val.Value;

            Assert.Equal(expected, lVal.Value, 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Matches_ObjectUpdate()
    {
        // Arrange
        const int period = 10;
        var source = new TSeries();

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Act
        var lemaSeries = Lema.Batch(source, period);
        var lemaObj = new Lema(period);

        // Assert
        for (int i = 0; i < source.Count; i++)
        {
            var val = lemaObj.Update(source[i]);
            Assert.Equal(val.Value, lemaSeries[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ZeroAllocCalculate_Matches_ObjectUpdate()
    {
        // Arrange
        const int period = 10;
        const int count = 100;
        var source = new double[count];
        var output = new double[count];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < count; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Act
        Lema.Batch(source, output, period);
        var lemaObj = new Lema(period);

        // Assert
        for (int i = 0; i < count; i++)
        {
            var val = lemaObj.Update(new TValue(DateTime.UtcNow, source[i]));
            Assert.Equal(val.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Alpha_Constructor_Matches_Period_Constructor()
    {
        // Arrange
        const int period = 10;
        double alpha = 2.0 / (period + 1);
        var lemaPeriod = new Lema(period);
        var lemaAlpha = new Lema(alpha);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var pVal = lemaPeriod.Update(tVal);
            var aVal = lemaAlpha.Update(tVal);

            Assert.Equal(pVal.Value, aVal.Value, 1e-9);
        }
    }

    [Fact]
    public void Alpha_Constructor_Sets_WarmupPeriod()
    {
        const int period = 10;
        double alpha = 2.0 / (period + 1);
        var lema = new Lema(alpha);
        Assert.Equal(period, lema.WarmupPeriod);
    }

    [Fact]
    public void StaticCalculate_Alpha_Matches_ObjectUpdate()
    {
        // Arrange
        const double alpha = 0.15;
        var source = new TSeries();

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Act
        var lemaSeries = Lema.Batch(source, alpha);
        var lemaObj = new Lema(alpha);

        // Assert
        for (int i = 0; i < source.Count; i++)
        {
            var val = lemaObj.Update(source[i]);
            Assert.Equal(val.Value, lemaSeries[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ZeroAllocCalculate_Alpha_Matches_ObjectUpdate()
    {
        // Arrange
        const double alpha = 0.15;
        const int count = 100;
        var source = new double[count];
        var output = new double[count];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < count; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Act
        Lema.Batch(source, output, alpha);
        var lemaObj = new Lema(alpha);

        // Assert
        for (int i = 0; i < count; i++)
        {
            var val = lemaObj.Update(new TValue(DateTime.UtcNow, source[i]));
            Assert.Equal(val.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Lema_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Lema(0));
        Assert.Throws<ArgumentException>(() => new Lema(-1));
        Assert.Throws<ArgumentException>(() => new Lema(0.0));
        Assert.Throws<ArgumentException>(() => new Lema(1.1));
    }

    [Fact]
    public void Lema_Calc_IsNew_AcceptsParameter()
    {
        var lema = new Lema(10);
        lema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, lema.Last.Value);
    }

    [Fact]
    public void Lema_Reset_ClearsState()
    {
        var lema = new Lema(10);
        lema.Update(new TValue(DateTime.UtcNow, 100));
        lema.Update(new TValue(DateTime.UtcNow, 110));

        lema.Reset();

        Assert.Equal(0, lema.Last.Value);
        Assert.False(lema.IsHot);
    }

    [Fact]
    public void Lema_IterativeCorrections_RestoreToOriginalState()
    {
        var lema = new Lema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            lema.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = lema.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            lema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = lema.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Lema_NaN_Input_UsesLastValidValue()
    {
        var lema = new Lema(10);
        lema.Update(new TValue(DateTime.UtcNow, 100));
        lema.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = lema.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Lema_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Lema.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Lema.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Lema_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Lema.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Lema_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Lema.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Lema.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Lema(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Lema(pubSource, period);
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

    [Fact]
    public void StaticCalculate_HandlesInitialNaN_Correctly()
    {
        double[] source = { double.NaN, double.NaN, 10.0, 11.0, 12.0 };
        double[] output = new double[source.Length];

        Lema.Batch(source, output, 3);

        // We expect the first two outputs to be NaN because the input was NaN
        Assert.True(double.IsNaN(output[0]), $"Output[0] should be NaN, but was {output[0]}");
        Assert.True(double.IsNaN(output[1]), $"Output[1] should be NaN, but was {output[1]}");

        // The first valid value is 10.0.
        Assert.Equal(10.0, output[2], 1e-9);
    }
}
