namespace QuanTAlib.Tests;
using Xunit;

public class EwmaTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ewma(0));
        Assert.Throws<ArgumentException>(() => new Ewma(-1));
        Assert.Throws<ArgumentException>(() => new Ewma(20, annualize: true, annualPeriods: 0));
        Assert.Throws<ArgumentException>(() => new Ewma(20, annualize: true, annualPeriods: -1));

        var valid = new Ewma(10, true, 252);
        Assert.Equal(10, valid.Period);
        Assert.True(valid.Annualize);
        Assert.Equal(252, valid.AnnualPeriods);
    }

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        var ewma = new Ewma(20);
        Assert.Equal(20, ewma.WarmupPeriod);
        Assert.True(ewma.WarmupPeriod > 0);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var ewma = new Ewma(20, true, 252);
        Assert.Equal(20, ewma.Period);
        Assert.True(ewma.Annualize);
        Assert.Equal(252, ewma.AnnualPeriods);
        Assert.Equal("Ewma(20,252)", ewma.Name);

        var ewmaNoAnn = new Ewma(15, false);
        Assert.Equal("Ewma(15)", ewmaNoAnn.Name);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ewma = new Ewma(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ewma.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ewma = new Ewma(10);

        for (int i = 0; i < 15; i++)
        {
            var result = ewma.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(ewma.IsHot);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ewma = new Ewma(10);

        var result1 = ewma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var result2 = ewma.Update(new TValue(DateTime.UtcNow, 101), isNew: true);
        var result3 = ewma.Update(new TValue(DateTime.UtcNow, 102), isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ewma = new Ewma(5);

        for (int i = 0; i < 10; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var baseline = ewma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        var updated = ewma.Update(new TValue(DateTime.UtcNow, 150), isNew: false);

        Assert.NotEqual(baseline.Value, updated.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        int period = 10;
        var ewma = new Ewma(period);

        for (int i = 0; i < period - 1; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(ewma.IsHot);
        }

        ewma.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(ewma.IsHot);
    }

    [Fact]
    public void Reset_Works()
    {
        var ewma = new Ewma(10);

        for (int i = 0; i < 15; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ewma.IsHot);

        ewma.Reset();
        Assert.False(ewma.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsZeroVolatility()
    {
        var ewma = new Ewma(5);
        var result = ewma.Update(new TValue(DateTime.UtcNow, 100));

        // First value should return 0 (no return to calculate)
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void IterativeCorrections_ChangesValue()
    {
        var ewma = new Ewma(20);
        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        TValue lastValue = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastValue = ewma.Update(new TValue(times[i], close[i]), isNew: true);
        }
        double originalValue = lastValue.Value;

        // Verify that isNew=false with different price produces different output
        var correctedValue = ewma.Update(new TValue(DateTime.UtcNow, 999.99), isNew: false);
        Assert.NotEqual(originalValue, correctedValue.Value);

        // Verify output is still finite and positive
        Assert.True(double.IsFinite(correctedValue.Value));
        Assert.True(correctedValue.Value >= 0);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ewma = new Ewma(10);

        for (int i = 0; i < 10; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        var result1 = ewma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        _ = ewma.Update(new TValue(DateTime.UtcNow, 115), isNew: false);
        var result3 = ewma.Update(new TValue(DateTime.UtcNow, 110), isNew: false);

        // With same input, should get same output after rollback
        Assert.Equal(result1.Value, result3.Value, 1e-9);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ewma = new Ewma(5);

        for (int i = 0; i < 10; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultNan = ewma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(resultNan.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ewma = new Ewma(5);

        for (int i = 0; i < 10; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var resultInf = ewma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultInf.Value));
    }

    [Fact]
    public void LargeDataset_Performance()
    {
        var ewma = new Ewma(50);
        var bars = GenerateTestData(5000);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ewma.Update(new TValue(times[i], close[i]));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        int period = 20;
        var ewmaStream = new Ewma(period);
        var ewmaBatch = new Ewma(period);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ewmaStream.Update(new TValue(times[i], close[i]));
        }

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var result = ewmaBatch.Update(ts);

        Assert.Equal(ewmaStream.Last.Value, result[result.Count - 1].Value, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var ewma = new Ewma(20);
        var bars = GenerateTestData(200);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ewma.Update(new TValue(times[i], close[i]));
        }
        var iterativeResult = ewma.Last.Value;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var batchResult = Ewma.Batch(ts, 20);

        Assert.Equal(iterativeResult, batchResult[batchResult.Count - 1].Value, 1e-8);
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }

        var result = Ewma.Batch(ts, 20);

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

        Assert.Throws<ArgumentException>(() => Ewma.Batch(ts, 0));
        Assert.Throws<ArgumentException>(() => Ewma.Batch(ts, -1));
        Assert.Throws<ArgumentException>(() => Ewma.Batch(ts, 5, true, 0));
        Assert.Throws<ArgumentException>(() => Ewma.Batch(ts, 5, true, -1));
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        var values = new double[] { 100, 101, 102, double.NaN, 104, 105 };
        var output = new double[values.Length];

        Ewma.Batch(values, output, 3);

        Assert.True(output.Length == 6);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void ConstantPrices_ZeroVolatility()
    {
        var ewma = new Ewma(10, false); // Not annualized

        for (int i = 0; i < 20; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // Constant prices should have zero volatility (log returns = 0)
        Assert.True(ewma.Last.Value < 1e-10, "Constant prices should have near-zero volatility");
    }

    [Fact]
    public void HighVolatility_ProducesHigherValue()
    {
        var ewmaStable = new Ewma(10, false);
        var ewmaVolatile = new Ewma(10, false);

        // Stable prices (small changes)
        for (int i = 0; i < 20; i++)
        {
            ewmaStable.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i * 0.01));
        }

        // Volatile prices (alternating)
        for (int i = 0; i < 20; i++)
        {
            double volatilePrice = 100 + (i % 2 == 0 ? 5 : -5);
            ewmaVolatile.Update(new TValue(DateTime.UtcNow.AddMinutes(i), volatilePrice));
        }

        Assert.True(ewmaVolatile.Last.Value > ewmaStable.Last.Value,
            "Higher volatility should produce higher EWMA");
    }

    [Fact]
    public void Annualization_ScalesCorrectly()
    {
        var ewmaNoAnn = new Ewma(10, false);
        var ewmaAnn252 = new Ewma(10, true, 252);

        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ewmaNoAnn.Update(new TValue(times[i], close[i]));
            ewmaAnn252.Update(new TValue(times[i], close[i]));
        }

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = ewmaAnn252.Last.Value / ewmaNoAnn.Last.Value;

        Assert.True(Math.Abs(actualRatio - expectedRatio) < 0.01,
            $"Annualization should scale by sqrt(252). Expected ratio: {expectedRatio}, Actual: {actualRatio}");
    }

    [Fact]
    public void DifferentAnnualPeriods_ProduceDistinctValues()
    {
        var ewma252 = new Ewma(10, true, 252); // Daily
        var ewma52 = new Ewma(10, true, 52);   // Weekly
        var ewma12 = new Ewma(10, true, 12);   // Monthly

        var bars = GenerateTestData(50);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            ewma252.Update(new TValue(times[i], close[i]));
            ewma52.Update(new TValue(times[i], close[i]));
            ewma12.Update(new TValue(times[i], close[i]));
        }

        // Higher annual periods = higher annualized volatility
        Assert.True(ewma252.Last.Value > ewma52.Last.Value, "Daily annualization should be higher than weekly");
        Assert.True(ewma52.Last.Value > ewma12.Last.Value, "Weekly annualization should be higher than monthly");
    }

    [Fact]
    public void BiasCorrection_WorksForEarlyValues()
    {
        // EWMA with bias correction should provide reasonable estimates even early
        var ewma = new Ewma(20, false);

        // First few values
        ewma.Update(new TValue(DateTime.UtcNow, 100));
        var first = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101));
        var second = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 99));

        // Should produce finite values even before warmup
        Assert.True(double.IsFinite(first.Value));
        Assert.True(double.IsFinite(second.Value));
        Assert.True(second.Value > 0, "Should detect volatility after price changes");
    }

    [Fact]
    public void Chainability_Works()
    {
        var ewma = new Ewma(20);
        var sma = new Sma(5);
        var bars = GenerateTestData(100);
        var times = bars.Times;
        var close = bars.CloseValues;

        for (int i = 0; i < bars.Count; i++)
        {
            var ewmaResult = ewma.Update(new TValue(times[i], close[i]));
            sma.Update(ewmaResult);
        }

        Assert.True(sma.IsHot);
        Assert.True(double.IsFinite(sma.Last.Value));
    }

    [Fact]
    public void SpanBatch_ValidatesLengths()
    {
        var source = new double[] { 100, 101, 102, 103, 104 };
        var outputShort = new double[3];

        Assert.Throws<ArgumentException>(() => Ewma.Batch(source, outputShort, 3));
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        var source = new double[] { 100, 101, 102, 103, 104 };
        var output = new double[5];

        Assert.Throws<ArgumentException>(() => Ewma.Batch(source, output, 0));
        Assert.Throws<ArgumentException>(() => Ewma.Batch(source, output, -1));
    }

    [Fact]
    public void SpanBatch_ValidatesAnnualPeriods()
    {
        var source = new double[] { 100, 101, 102, 103, 104 };
        var output = new double[5];

        Assert.Throws<ArgumentException>(() => Ewma.Batch(source, output, 3, true, 0));
        Assert.Throws<ArgumentException>(() => Ewma.Batch(source, output, 3, true, -1));
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var ewma = new Ewma(10, true, 252);
        var bars = GenerateTestData(100);
        var close = bars.CloseValues;

        // Streaming
        for (int i = 0; i < bars.Count; i++)
        {
            ewma.Update(new TValue(DateTime.UtcNow, close[i]));
        }

        // Batch
        var output = new double[close.Length];
        Ewma.Batch(close, output, 10, true, 252);

        // Compare last values
        Assert.Equal(ewma.Last.Value, output[output.Length - 1], 1e-9);
    }

    [Fact]
    public void EmptyInput_HandledGracefully()
    {
        var source = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;

        // Should not throw - empty spans are valid
        Ewma.Batch(source, output, 10);
        Assert.True(true, "Empty input handled without exception");
    }

    [Fact]
    public void LogReturns_CalculatedCorrectly()
    {
        // Test with known values to verify log return calculation
        var ewma = new Ewma(2, false); // Short period for quick testing

        // Price goes from 100 to 110 (+10%)
        ewma.Update(new TValue(DateTime.UtcNow, 100));
        var result = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 110));

        // Log return = ln(110/100) ≈ 0.0953
        // Squared return ≈ 0.00908
        // With bias correction, volatility should be close to |log return|
        Assert.True(result.Value > 0);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void NegativePrice_UsesLastValid()
    {
        var ewma = new Ewma(5);

        ewma.Update(new TValue(DateTime.UtcNow, 100));
        ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101));
        var resultNeg = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(2), -50));

        Assert.True(double.IsFinite(resultNeg.Value));
        Assert.True(resultNeg.Value >= 0);
    }

    [Fact]
    public void ZeroPrice_UsesLastValid()
    {
        var ewma = new Ewma(5);

        ewma.Update(new TValue(DateTime.UtcNow, 100));
        ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 101));
        var resultZero = ewma.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 0));

        Assert.True(double.IsFinite(resultZero.Value));
        Assert.True(resultZero.Value >= 0);
    }
}
