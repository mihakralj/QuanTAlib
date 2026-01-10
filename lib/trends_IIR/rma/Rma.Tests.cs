namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class RmaTests
{
    [Fact]
    public void Rma_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Rma(0));
        Assert.Throws<ArgumentException>(() => new Rma(-1));

        var rma = new Rma(10);
        Assert.NotNull(rma);
    }

    [Fact]
    public void Rma_Calc_ReturnsValue()
    {
        var rma = new Rma(10);

        Assert.Equal(0, rma.Last.Value);

        TValue result = rma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, rma.Last.Value);
    }

    [Fact]
    public void Rma_Calc_IsNew_AcceptsParameter()
    {
        var rma = new Rma(10);

        rma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = rma.Last.Value;

        rma.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = rma.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Rma_Calc_IsNew_False_UpdatesValue()
    {
        var rma = new Rma(10);

        rma.Update(new TValue(DateTime.UtcNow, 100));
        rma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = rma.Last.Value;

        rma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = rma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Rma_Reset_ClearsState()
    {
        var rma = new Rma(10);

        rma.Update(new TValue(DateTime.UtcNow, 100));
        rma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = rma.Last.Value;

        rma.Reset();

        Assert.Equal(0, rma.Last.Value);

        // After reset, should accept new values
        rma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, rma.Last.Value);
        Assert.NotEqual(valueBefore, rma.Last.Value);
    }

    [Fact]
    public void Rma_IsHot_BecomesTrueAt95PercentCoverage()
    {
        var rma = new Rma(10);

        // Initially IsHot should be false
        Assert.False(rma.IsHot);

        // IsHot triggers at 95% coverage (E <= 0.05)
        // E = (1 - alpha)^N where alpha = 1 / period
        // For period 10: alpha = 0.1, (1-alpha) = 0.9
        // N = ln(0.05) / ln(0.9) ≈ 28.4, so ~29 bars

        int steps = 0;
        while (!rma.IsHot && steps < 1000)
        {
            rma.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(rma.IsHot);
        Assert.True(steps > 0);
        // For period 10, should become hot around 29 bars
        Assert.InRange(steps, 28, 30);
    }

    [Fact]
    public void Rma_EquivalentToEmaWithAlpha()
    {
        const int period = 10;
        double alpha = 1.0 / period;

        var rma = new Rma(period);
        var ema = new Ema(alpha);

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var rmaVal = rma.Update(new TValue(bar.Time, bar.Close));
            var emaVal = ema.Update(new TValue(bar.Time, bar.Close));

            Assert.Equal(emaVal.Value, rmaVal.Value, 1e-10);
        }
    }

    [Fact]
    public void Rma_BatchCalc_MatchesIterativeCalc()
    {
        var rmaIterative = new Rma(10);
        var rmaBatch = new Rma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Generate data
        var series = new TSeries();
        var inputList = new List<TValue>();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            inputList.Add(new TValue(bar.Time, bar.Close));
        }

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var item in inputList)
        {
            iterativeResults.Add(rmaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = rmaBatch.Update(series);

        // Compare
        Assert.Equal(series.Count, iterativeResults.Count);
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < inputList.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Rma_SpanCalc_MatchesTSeriesCalc()
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

        // Calculate with TSeries API
        var tseriesResult = Rma.Batch(series, 10);

        // Calculate with Span API
        Rma.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Rma_NaN_Input_UsesLastValidValue()
    {
        var rma = new Rma(10);

        // Feed some valid values
        rma.Update(new TValue(DateTime.UtcNow, 100));
        rma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = rma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var rma = new Rma(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, rma.Last.Value, 1e-9);
    }
}
