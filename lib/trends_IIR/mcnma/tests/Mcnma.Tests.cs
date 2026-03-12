namespace QuanTAlib.Tests;

public class McnmaTests
{
    [Fact]
    public void Mcnma_Matches_ManualCalculation()
    {
        // Manual 6-EMA with first-value seeding (matches Pine exactly)
        const int period = 10;
        double alpha = 2.0 / (period + 1);
        double decay = 1.0 - alpha;
        var mcnma = new Mcnma(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        double e1 = 0, e2 = 0, e3 = 0, e4 = 0, e5 = 0, e6 = 0;
        bool init = false;

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var mVal = mcnma.Update(tVal);

            double val = tVal.Value;
            if (!init)
            {
                e1 = e2 = e3 = e4 = e5 = e6 = val;
                init = true;
                Assert.Equal(val, mVal.Value, 1e-9);
                continue;
            }

            e1 = Math.FusedMultiplyAdd(e1, decay, alpha * val);
            e2 = Math.FusedMultiplyAdd(e2, decay, alpha * e1);
            e3 = Math.FusedMultiplyAdd(e3, decay, alpha * e2);
            double tema1 = (3.0 * e1) - (3.0 * e2) + e3;

            e4 = Math.FusedMultiplyAdd(e4, decay, alpha * tema1);
            e5 = Math.FusedMultiplyAdd(e5, decay, alpha * e4);
            e6 = Math.FusedMultiplyAdd(e6, decay, alpha * e5);
            double tema2 = (3.0 * e4) - (3.0 * e5) + e6;

            double expected = (2.0 * tema1) - tema2;
            Assert.Equal(expected, mVal.Value, 1e-9);
        }
    }

    [Fact]
    public void StaticCalculate_Matches_ObjectUpdate()
    {
        const int period = 10;
        var source = new TSeries();

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var mcnmaSeries = Mcnma.Batch(source, period);
        var mcnmaObj = new Mcnma(period);

        for (int i = 0; i < source.Count; i++)
        {
            var val = mcnmaObj.Update(source[i]);
            Assert.Equal(val.Value, mcnmaSeries[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ZeroAllocCalculate_Matches_ObjectUpdate()
    {
        const int period = 10;
        const int count = 100;
        var source = new double[count];
        var output = new double[count];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < count; i++)
        {
            source[i] = gbm.Next().Close;
        }

        Mcnma.Batch(source, output, period);
        var mcnmaObj = new Mcnma(period);

        for (int i = 0; i < count; i++)
        {
            var val = mcnmaObj.Update(new TValue(DateTime.UtcNow, source[i]));
            Assert.Equal(val.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Alpha_Constructor_Matches_Period_Constructor()
    {
        const int period = 10;
        double alpha = 2.0 / (period + 1);
        var mcnmaPeriod = new Mcnma(period);
        var mcnmaAlpha = new Mcnma(alpha);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var pVal = mcnmaPeriod.Update(tVal);
            var aVal = mcnmaAlpha.Update(tVal);

            Assert.Equal(pVal.Value, aVal.Value, 1e-9);
        }
    }

    [Fact]
    public void Alpha_Constructor_Sets_WarmupPeriod()
    {
        const int period = 10;
        double alpha = 2.0 / (period + 1);
        var mcnma = new Mcnma(alpha);
        Assert.Equal(period, mcnma.WarmupPeriod);
    }

    [Fact]
    public void StaticCalculate_Alpha_Matches_ObjectUpdate()
    {
        const double alpha = 0.15;
        var source = new TSeries();

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var mcnmaSeries = Mcnma.Batch(source, alpha);
        var mcnmaObj = new Mcnma(alpha);

        for (int i = 0; i < source.Count; i++)
        {
            var val = mcnmaObj.Update(source[i]);
            Assert.Equal(val.Value, mcnmaSeries[i].Value, 1e-9);
        }
    }

    [Fact]
    public void ZeroAllocCalculate_Alpha_Matches_ObjectUpdate()
    {
        const double alpha = 0.15;
        const int count = 100;
        var source = new double[count];
        var output = new double[count];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        for (int i = 0; i < count; i++)
        {
            source[i] = gbm.Next().Close;
        }

        Mcnma.Batch(source, output, alpha);
        var mcnmaObj = new Mcnma(alpha);

        for (int i = 0; i < count; i++)
        {
            var val = mcnmaObj.Update(new TValue(DateTime.UtcNow, source[i]));
            Assert.Equal(val.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Mcnma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mcnma(0));
        Assert.Throws<ArgumentException>(() => new Mcnma(-1));
        Assert.Throws<ArgumentException>(() => new Mcnma(0.0));
        Assert.Throws<ArgumentException>(() => new Mcnma(1.1));
    }

    [Fact]
    public void Mcnma_Calc_IsNew_AcceptsParameter()
    {
        var mcnma = new Mcnma(10);
        mcnma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, mcnma.Last.Value);
    }

    [Fact]
    public void Mcnma_Reset_ClearsState()
    {
        var mcnma = new Mcnma(10);
        mcnma.Update(new TValue(DateTime.UtcNow, 100));
        mcnma.Update(new TValue(DateTime.UtcNow, 110));

        mcnma.Reset();

        Assert.Equal(0, mcnma.Last.Value);
        Assert.False(mcnma.IsHot);
    }

    [Fact]
    public void Mcnma_IterativeCorrections_RestoreToOriginalState()
    {
        var mcnma = new Mcnma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            mcnma.Update(tenthInput, isNew: true);
        }

        double valueAfterTen = mcnma.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            mcnma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        TValue finalValue = mcnma.Update(tenthInput, isNew: false);

        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Mcnma_NaN_Input_UsesLastValidValue()
    {
        var mcnma = new Mcnma(10);
        mcnma.Update(new TValue(DateTime.UtcNow, 100));
        mcnma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = mcnma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Mcnma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Mcnma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Mcnma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Mcnma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Mcnma.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Mcnma_AllModes_ProduceSameResult()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Mcnma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Mcnma.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Mcnma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Mcnma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void StaticCalculate_HandlesInitialNaN_Correctly()
    {
        double[] source = { double.NaN, double.NaN, 10.0, 11.0, 12.0 };
        double[] output = new double[source.Length];

        Mcnma.Batch(source, output, 3);

        Assert.True(double.IsNaN(output[0]), $"Output[0] should be NaN, but was {output[0]}");
        Assert.True(double.IsNaN(output[1]), $"Output[1] should be NaN, but was {output[1]}");

        Assert.Equal(10.0, output[2], 1e-9);
    }
}
