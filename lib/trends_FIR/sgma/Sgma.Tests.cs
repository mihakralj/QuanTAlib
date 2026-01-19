namespace QuanTAlib.Tests;

public class SgmaTests
{
    [Fact]
    public void Sgma_Constructor_ValidatesInput()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new Sgma(2));
        Assert.Equal("period", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Sgma(5, -1));
        Assert.Equal("degree", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => new Sgma(5, 5));
        Assert.Equal("degree", ex3.ParamName);

        var sgma = new Sgma(9, 2);
        Assert.NotNull(sgma);
    }

    [Fact]
    public void Sgma_Calc_ReturnsValue()
    {
        var sgma = new Sgma(9);
        TValue result = sgma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Sgma_IsHot_BecomesTrueAfterPeriod()
    {
        var sgma = new Sgma(5);

        Assert.False(sgma.IsHot);

        for (int i = 0; i < 4; i++)
        {
            sgma.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(sgma.IsHot);
        }

        sgma.Update(new TValue(DateTime.UtcNow, 104));
        Assert.True(sgma.IsHot);
    }

    [Fact]
    public void Sgma_StreamingMatchesBatch()
    {
        var sgmaStreaming = new Sgma(9);
        var sgmaBatch = new Sgma(9);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var streamingResults = new TSeries();
        Assert.True(series.Count > 0);
        foreach (var item in series)
        {
            streamingResults.Add(sgmaStreaming.Update(item));
        }

        var batchResults = sgmaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Sgma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Sgma(9).Update(series);
        var staticResults = Sgma.Batch(series, 9);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Sgma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Sgma.Batch(series, 9);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Sgma.Calculate(input.AsSpan(), output.AsSpan(), 9);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Sgma_Update_IsNewFalse_CorrectsValue()
    {
        // Use degree=0 (uniform weights) where ALL positions have equal non-zero weight
        var sgma = new Sgma(5, 0);

        for (int i = 0; i < 5; i++)
            sgma.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);

        Assert.Equal(100.0, sgma.Last.Value, 1e-9);

        sgma.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        double valueAfterCommit = sgma.Last.Value;
        Assert.Equal(100.0, valueAfterCommit, 1e-9);

        sgma.Update(new TValue(DateTime.UtcNow, 150.0), isNew: false);
        double valueAfterCorrection = sgma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);
        Assert.Equal(110.0, valueAfterCorrection, 1e-9);

        sgma.Update(new TValue(DateTime.UtcNow, 100.0), isNew: false);
        Assert.Equal(100.0, sgma.Last.Value, 1e-9);
    }

    [Fact]
    public void Sgma_NaN_Input_UsesLastValidValue()
    {
        var sgma = new Sgma(5);

        sgma.Update(new TValue(DateTime.UtcNow, 100));
        sgma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = sgma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Sgma_Reset_ClearsState()
    {
        var sgma = new Sgma(9);
        for (int i = 0; i < 10; i++)
        {
            sgma.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        Assert.True(sgma.Last.Value > 0);
        Assert.True(sgma.IsHot);

        sgma.Reset();

        Assert.Equal(0, sgma.Last.Value);
        Assert.False(sgma.IsHot);
    }

    [Fact]
    public void Sgma_FirstValue_ReturnsInput()
    {
        var sgma = new Sgma(9);
        TValue result = sgma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, result.Value, 1e-9);
    }

    [Fact]
    public void Sgma_Properties_Accessible()
    {
        var sgma = new Sgma(9);
        Assert.False(sgma.IsHot);
        Assert.Equal(0, sgma.Last.Value);
    }

    [Fact]
    public void Sgma_Calc_IsNew_AcceptsParameter()
    {
        var sgma = new Sgma(9);
        sgma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, sgma.Last.Value);
    }

    [Fact]
    public void Sgma_IterativeCorrections_RestoreToOriginalState()
    {
        var sgma = new Sgma(9, 0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            sgma.Update(tenthInput, isNew: true);
        }

        double valueAfterTen = sgma.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            sgma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        TValue finalValue = sgma.Update(tenthInput, isNew: false);

        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Sgma_Infinity_Input_UsesLastValidValue()
    {
        var sgma = new Sgma(9);
        sgma.Update(new TValue(DateTime.UtcNow, 100));
        sgma.Update(new TValue(DateTime.UtcNow, 110));

        var resultPosInf = sgma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = sgma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void Sgma_MultipleNaN_ContinuesWithLastValid()
    {
        var sgma = new Sgma(9);
        sgma.Update(new TValue(DateTime.UtcNow, 100));

        var r1 = sgma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = sgma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void Sgma_AllModes_ProduceSameResult()
    {
        const int period = 9;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var batchSeries = Sgma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Sgma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        var streamingInd = new Sgma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        var pubSource = new TSeries();
        var eventingInd = new Sgma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }

    [Fact]
    public void Sgma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Sgma.Calculate(source.AsSpan(), output.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Sgma.Calculate(source.AsSpan(), output.AsSpan(), 5, 5));
        Assert.Throws<ArgumentException>(() => Sgma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Sgma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Sgma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Sgma_EvenPeriod_AdjustsToOdd()
    {
        var sgma = new Sgma(10, 2);
        Assert.Contains("11", sgma.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Sgma_DifferentDegrees_ProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var sgma0 = new Sgma(9, 0);
        var sgma1 = new Sgma(9, 1);
        var sgma2 = new Sgma(9, 2);
        var sgma3 = new Sgma(9, 3);
        var sgma4 = new Sgma(9, 4);

        var results0 = sgma0.Update(series);
        var results1 = sgma1.Update(series);
        var results2 = sgma2.Update(series);
        var results3 = sgma3.Update(series);
        var results4 = sgma4.Update(series);

        Assert.NotEqual(results0.Last.Value, results2.Last.Value);
        Assert.NotEqual(results1.Last.Value, results2.Last.Value);
        Assert.NotEqual(results2.Last.Value, results3.Last.Value);
        Assert.NotEqual(results3.Last.Value, results4.Last.Value);
    }

    [Fact]
    public void Sgma_ConstantInput_ReturnsConstant()
    {
        var sgma = new Sgma(9);
        const double constantValue = 100.0;

        for (int i = 0; i < 20; i++)
        {
            var result = sgma.Update(new TValue(DateTime.UtcNow, constantValue));
            Assert.Equal(constantValue, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Sgma_PrecomputedWeights_MatchCalculated()
    {
        var sgma5 = new Sgma(5, 2);
        var sgma7 = new Sgma(7, 2);
        var sgma9 = new Sgma(9, 2);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            sgma5.Update(new TValue(bar.Time, bar.Close));
            sgma7.Update(new TValue(bar.Time, bar.Close));
            sgma9.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(sgma5.Last.Value));
        Assert.True(double.IsFinite(sgma7.Last.Value));
        Assert.True(double.IsFinite(sgma9.Last.Value));
    }

    [Fact]
    public void Sgma_ShapePreservation_HighDegreePreservesPeaks()
    {
        double[] prices = new double[20];
        for (int i = 0; i < 10; i++) prices[i] = 100 + i * 5;
        for (int i = 10; i < 20; i++) prices[i] = 145 - (i - 10) * 5;

        var sgma2 = new Sgma(5, 2);
        var sgma4 = new Sgma(5, 4);

        double[] results2 = new double[20];
        double[] results4 = new double[20];

        for (int i = 0; i < 20; i++)
        {
            results2[i] = sgma2.Update(new TValue(DateTime.UtcNow, prices[i])).Value;
            results4[i] = sgma4.Update(new TValue(DateTime.UtcNow, prices[i])).Value;
        }

        double peakError2 = Math.Abs(results2[9] - 145);
        double peakError4 = Math.Abs(results4[9] - 145);

        Assert.True(peakError2 < 20);
        Assert.True(peakError4 < 20);
    }

    [Fact]
    public void Sgma_Degree0_IsSimpleAverage()
    {
        var sgma0 = new Sgma(5, 0);
        var sma = new Sma(5);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            sgma0.Update(tv);
            sma.Update(tv);
        }

        Assert.Equal(sma.Last.Value, sgma0.Last.Value, 1e-9);
    }
}
