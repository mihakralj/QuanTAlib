namespace QuanTAlib.Tests;

public class BesselTests
{
    [Fact]
    public void Bessel_Constructor_Length_ValidatesInput()
    {
        var ex0 = Assert.Throws<ArgumentException>(() => new Bessel(0));
        Assert.Equal("length", ex0.ParamName);

        var exNeg = Assert.Throws<ArgumentException>(() => new Bessel(-1));
        Assert.Equal("length", exNeg.ParamName);

        var ex1 = Assert.Throws<ArgumentException>(() => new Bessel(1));
        Assert.Equal("length", ex1.ParamName);

        var bessel = new Bessel(2);
        Assert.NotNull(bessel);

        var bessel14 = new Bessel(14);
        Assert.NotNull(bessel14);
    }

    [Fact]
    public void Bessel_SpanCalculate_ValidatesLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        var exLength = Assert.Throws<ArgumentException>(() =>
            Bessel.Batch(source.AsSpan(), output.AsSpan(), 1));
        Assert.Equal("length", exLength.ParamName);

        var exLengthZero = Assert.Throws<ArgumentException>(() =>
            Bessel.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("length", exLengthZero.ParamName);
    }

    [Fact]
    public void Bessel_SpanCalculate_ValidatesBufferLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSizeOutput = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Bessel.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 14));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Bessel_Calc_ReturnsValue()
    {
        var bessel = new Bessel(14);

        Assert.Equal(0, bessel.Last.Value);

        TValue result = bessel.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, bessel.Last.Value);
    }

    [Fact]
    public void Bessel_Calc_IsNew_AcceptsParameter()
    {
        var bessel = new Bessel(14);

        bessel.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = bessel.Last.Value;

        bessel.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = bessel.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Bessel_Calc_IsNew_False_UpdatesValue()
    {
        var bessel = new Bessel(14);

        bessel.Update(new TValue(DateTime.UtcNow, 100));
        bessel.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = bessel.Last.Value;

        bessel.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = bessel.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Bessel_Reset_ClearsState()
    {
        var bessel = new Bessel(14);

        bessel.Update(new TValue(DateTime.UtcNow, 100));
        bessel.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = bessel.Last.Value;

        bessel.Reset();

        Assert.Equal(0, bessel.Last.Value);

        // After reset, should accept new values
        bessel.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, bessel.Last.Value);
        Assert.NotEqual(valueBefore, bessel.Last.Value);
    }

    [Fact]
    public void Bessel_Properties_Accessible()
    {
        var bessel = new Bessel(14);

        Assert.Equal(0, bessel.Last.Value);
        Assert.False(bessel.IsHot);

        bessel.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, bessel.Last.Value);
    }

    [Fact]
    public void Bessel_IsHot_BecomesTrueAfterWarmup()
    {
        const int length = 14;
        var bessel = new Bessel(length);

        // Initially IsHot should be false
        Assert.False(bessel.IsHot);

        int steps = 0;
        while (!bessel.IsHot && steps < 1000)
        {
            bessel.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(bessel.IsHot);
        Assert.True(steps > 0);
        Assert.Equal(length, steps); // WarmupPeriod is length
    }

    [Fact]
    public void Bessel_IterativeCorrections_RestoreToOriginalState()
    {
        var bessel = new Bessel(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 14 new values
        TValue lastInput = default;
        for (int i = 0; i < 14; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            bessel.Update(lastInput, isNew: true);
        }

        double valueAfterWarmup = bessel.Last.Value;

        // Generate corrections with isNew=false (different values)
        for (int i = 0; i < 13; i++)
        {
            var bar = gbm.Next(isNew: false);
            bessel.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered last input again with isNew=false
        TValue finalValue = bessel.Update(lastInput, isNew: false);

        Assert.Equal(valueAfterWarmup, finalValue.Value, 1e-10);
    }

    [Fact]
    public void Bessel_BatchCalc_MatchesIterativeCalc()
    {
        var besselIterative = new Bessel(14);
        var besselBatch = new Bessel(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        Assert.True(series.Count > 0);

        var iterativeResults = new TSeries();
        foreach (var item in series)
        {
            iterativeResults.Add(besselIterative.Update(item));
        }

        var batchResults = besselBatch.Update(series);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Bessel_NaN_Input_UsesLastValidValue()
    {
        var bessel = new Bessel(14);

        bessel.Update(new TValue(DateTime.UtcNow, 100));
        bessel.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = bessel.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Bessel_Infinity_Input_UsesLastValidValue()
    {
        var bessel = new Bessel(14);

        bessel.Update(new TValue(DateTime.UtcNow, 100));
        bessel.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = bessel.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = bessel.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Bessel_SpanBatch_MatchesTSeriesBatch()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        var tseriesResult = Bessel.Calculate(series, 14).Results;

        Bessel.Batch(source.AsSpan(), output.AsSpan(), 14);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Bessel_AllModes_ProduceSameResult()
    {
        int length = 14;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Bessel.Calculate(series, length).Results;
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Bessel.Batch(spanInput, spanOutput, length);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Bessel(length);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Bessel(pubSource, length);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
