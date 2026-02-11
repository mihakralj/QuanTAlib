namespace QuanTAlib.Tests;
using Xunit;

public class CviTests
{
    private const double Tolerance = 1e-10;

    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Cvi(0, 10));
        Assert.Throws<ArgumentException>(() => new Cvi(-1, 10));
        Assert.Throws<ArgumentException>(() => new Cvi(10, 0));
        Assert.Throws<ArgumentException>(() => new Cvi(10, -1));

        var valid = new Cvi(10, 10);
        Assert.Equal(10, valid.RocLength);
        Assert.Equal(10, valid.SmoothLength);
    }

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        var cvi = new Cvi(10, 10);
        Assert.Equal(20, cvi.WarmupPeriod); // smoothLength + rocLength
        Assert.True(cvi.WarmupPeriod > 0);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var cvi = new Cvi(14, 10);
        Assert.Equal(14, cvi.RocLength);
        Assert.Equal(10, cvi.SmoothLength);
        Assert.Equal("Cvi(14,10)", cvi.Name);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var cvi = new Cvi(5, 5);
        var bars = GenerateTestData(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = cvi.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var cvi = new Cvi(10, 10);
        var bars = GenerateTestData(30);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = cvi.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(cvi.IsHot);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var cvi = new Cvi(10, 10);
        var bars = GenerateTestData(5);

        var result1 = cvi.Update(bars[0], isNew: true);
        var result2 = cvi.Update(bars[1], isNew: true);
        var result3 = cvi.Update(bars[2], isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var cvi = new Cvi(5, 5);
        var bars = GenerateTestData(20);

        for (int i = 0; i < 15; i++)
        {
            cvi.Update(bars[i], isNew: true);
        }

        var baseline = cvi.Update(bars[15], isNew: true);

        // Create a bar with very different High-Low range
        var modifiedBar = new TBar(
            bars[15].Time,
            bars[15].Open,
            bars[15].High + 10, // Increase high
            bars[15].Low - 10,  // Decrease low
            bars[15].Close,
            bars[15].Volume
        );
        var updated = cvi.Update(modifiedBar, isNew: false);

        Assert.NotEqual(baseline.Value, updated.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        int rocLength = 10;
        int smoothLength = 10;
        var cvi = new Cvi(rocLength, smoothLength);
        var bars = GenerateTestData(30);

        int warmup = smoothLength + rocLength; // 20

        for (int i = 0; i < warmup - 1; i++)
        {
            cvi.Update(bars[i]);
            Assert.False(cvi.IsHot);
        }

        cvi.Update(bars[warmup - 1]);
        Assert.True(cvi.IsHot);
    }

    [Fact]
    public void Reset_Works()
    {
        var cvi = new Cvi(10, 10);
        var bars = GenerateTestData(30);

        for (int i = 0; i < bars.Count; i++)
        {
            cvi.Update(bars[i]);
        }
        Assert.True(cvi.IsHot);

        cvi.Reset();
        Assert.False(cvi.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsZero()
    {
        var cvi = new Cvi(5, 5);
        var bars = GenerateTestData(1);
        var result = cvi.Update(bars[0]);

        // First value has no ROC data yet, should be 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var cvi = new Cvi(10, 10);
        var bars = GenerateTestData(50);

        TValue lastValue = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastValue = cvi.Update(bars[i], isNew: true);
        }
        double originalValue = lastValue.Value;

        // Apply a correction with very different range
        var modifiedBar = new TBar(
            bars[bars.Count - 1].Time,
            100, 200, 50, 150, 1000
        );
        var correctedValue = cvi.Update(modifiedBar, isNew: false);
        Assert.NotEqual(originalValue, correctedValue.Value);

        // Restore original value
        var restoredValue = cvi.Update(bars[bars.Count - 1], isNew: false);
        Assert.Equal(originalValue, restoredValue.Value, 1e-9);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var cvi = new Cvi(10, 10);
        var bars = GenerateTestData(25);

        for (int i = 0; i < 20; i++)
        {
            cvi.Update(bars[i], isNew: true);
        }

        var result1 = cvi.Update(bars[20], isNew: true);
        _ = cvi.Update(bars[21], isNew: false);
        var result3 = cvi.Update(bars[20], isNew: false);

        Assert.Equal(result1.Value, result3.Value, Tolerance);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var cvi = new Cvi(5, 5);
        var bars = GenerateTestData(20);

        for (int i = 0; i < 15; i++)
        {
            cvi.Update(bars[i]);
        }

        // Update with NaN value (treated as pre-calculated range)
        var resultNan = cvi.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(resultNan.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var cvi = new Cvi(5, 5);
        var bars = GenerateTestData(20);

        for (int i = 0; i < 15; i++)
        {
            cvi.Update(bars[i]);
        }

        var resultInf = cvi.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultInf.Value));
    }

    [Fact]
    public void NegativeRange_UsesLastValidValue()
    {
        var cvi = new Cvi(5, 5);
        var bars = GenerateTestData(20);

        for (int i = 0; i < 15; i++)
        {
            cvi.Update(bars[i]);
        }

        // Negative range is invalid for High-Low
        var resultNeg = cvi.Update(new TValue(DateTime.UtcNow, -5.0));
        Assert.True(double.IsFinite(resultNeg.Value));
    }

    [Fact]
    public void LargeDataset_Performance()
    {
        var cvi = new Cvi(14, 10);
        var bars = GenerateTestData(5000);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = cvi.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void TBarSeries_Update_Works()
    {
        int rocLength = 10;
        int smoothLength = 10;
        var cvi = new Cvi(rocLength, smoothLength);
        var bars = GenerateTestData(100);

        var result = cvi.Update(bars);

        Assert.Equal(bars.Count, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        int rocLength = 10;
        int smoothLength = 10;
        var cviStream = new Cvi(rocLength, smoothLength);
        var cviBatch = new Cvi(rocLength, smoothLength);
        var bars = GenerateTestData(100);

        // Streaming mode
        for (int i = 0; i < bars.Count; i++)
        {
            cviStream.Update(bars[i]);
        }

        // Batch mode with TBarSeries
        var result = cviBatch.Update(bars);

        Assert.Equal(cviStream.Last.Value, result[result.Count - 1].Value, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var cvi = new Cvi(10, 10);
        var bars = GenerateTestData(200);

        // Streaming
        for (int i = 0; i < bars.Count; i++)
        {
            cvi.Update(bars[i]);
        }
        var iterativeResult = cvi.Last.Value;

        // Batch via static method
        var batchResult = Cvi.Batch(bars, 10, 10);

        Assert.Equal(iterativeResult, batchResult[batchResult.Count - 1].Value, 1e-8);
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var bars = GenerateTestData(100);

        var result = Cvi.Batch(bars, 14, 10);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[result.Count - 1].Value));
    }

    [Fact]
    public void StaticBatch_ValidatesInput()
    {
        var ts = new TSeries();
        for (int i = 0; i < 10; i++)
        {
            ts.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Throws<ArgumentException>(() => Cvi.Batch(ts, 0, 10));
        Assert.Throws<ArgumentException>(() => Cvi.Batch(ts, -1, 10));
        Assert.Throws<ArgumentException>(() => Cvi.Batch(ts, 10, 0));
        Assert.Throws<ArgumentException>(() => Cvi.Batch(ts, 10, -1));
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        var values = new double[] { 1.0, 1.2, 1.1, double.NaN, 1.3, 1.4 };
        var output = new double[values.Length];

        Cvi.Batch(values, output, 2, 2);

        Assert.True(output.Length == 6);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void ConstantRange_ZeroVolatility()
    {
        var cvi = new Cvi(10, 10);

        // Feed constant high-low range
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 105.0, 95.0, 102.0, 1000.0 // Constant 10-point range
            );
            cvi.Update(bar);
        }

        // Constant range should result in zero or near-zero CVI (no rate of change)
        Assert.True(Math.Abs(cvi.Last.Value) < 1.0, "Constant range should have near-zero CVI");
    }

    [Fact]
    public void ExpandingVolatility_PositiveValue()
    {
        var cvi = new Cvi(5, 5);

        // Start with small range, expand over time
        for (int i = 0; i < 20; i++)
        {
            double range = 5 + i * 0.5; // Expanding range
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.0 + range / 2, 100.0 - range / 2, 100.0, 1000.0
            );
            cvi.Update(bar);
        }

        // Expanding volatility should produce positive CVI
        Assert.True(cvi.Last.Value > 0, "Expanding volatility should produce positive CVI");
    }

    [Fact]
    public void ContractingVolatility_NegativeValue()
    {
        var cvi = new Cvi(5, 5);

        // Start with large range, contract over time
        for (int i = 0; i < 20; i++)
        {
            double range = 20 - i * 0.5; // Contracting range
            if (range < 1)
            {
                range = 1;
            }
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.0 + range / 2, 100.0 - range / 2, 100.0, 1000.0
            );
            cvi.Update(bar);
        }

        // Contracting volatility should produce negative CVI
        Assert.True(cvi.Last.Value < 0, "Contracting volatility should produce negative CVI");
    }

    [Fact]
    public void DifferentParameters_ProduceDistinctValues()
    {
        var bars = GenerateTestData(50);

        var cvi1 = new Cvi(10, 10);
        var cvi2 = new Cvi(14, 10);
        var cvi3 = new Cvi(10, 14);

        for (int i = 0; i < bars.Count; i++)
        {
            cvi1.Update(bars[i]);
            cvi2.Update(bars[i]);
            cvi3.Update(bars[i]);
        }

        Assert.True(double.IsFinite(cvi1.Last.Value));
        Assert.True(double.IsFinite(cvi2.Last.Value));
        Assert.True(double.IsFinite(cvi3.Last.Value));
        // Different parameters should produce different values
        Assert.NotEqual(cvi1.Last.Value, cvi2.Last.Value);
    }

    [Fact]
    public void TValueUpdate_TreatsValueAsRange()
    {
        var cvi = new Cvi(5, 5);

        // Feed pre-calculated range values via TValue
        for (int i = 0; i < 20; i++)
        {
            var result = cvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 5.0 + i * 0.1));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(cvi.IsHot);
    }

    [Fact]
    public void Chainability_Works()
    {
        var cvi = new Cvi(10, 10);
        var sma = new Sma(5);
        var bars = GenerateTestData(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var cviResult = cvi.Update(bars[i]);
            sma.Update(cviResult);
        }

        Assert.True(sma.IsHot);
        Assert.True(double.IsFinite(sma.Last.Value));
    }

    [Fact]
    public void SpanBatch_MatchesOutputLength()
    {
        var values = new double[] { 1.0, 1.2, 1.1, 1.3, 1.4, 1.2, 1.5, 1.3, 1.6, 1.4 };
        var output = new double[values.Length];

        Cvi.Batch(values, output, 3, 3);

        Assert.Equal(values.Length, output.Length);
    }

    [Fact]
    public void SpanBatch_ValidatesArguments()
    {
        var source = new double[] { 1.0, 1.2, 1.1 };
        var outputShort = new double[2];
        var outputCorrect = new double[3];

        Assert.Throws<ArgumentException>(() => Cvi.Batch(source, outputShort, 2, 2));
        Assert.Throws<ArgumentException>(() => Cvi.Batch(source, outputCorrect, 0, 2));
        Assert.Throws<ArgumentException>(() => Cvi.Batch(source, outputCorrect, 2, 0));
    }
}